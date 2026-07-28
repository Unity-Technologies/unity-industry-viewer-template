using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using UnityEngine.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace Unity.Industry.Viewer.VR
{
    public class XRToolPanel : ToolPanelUIController
    {
        private HashSet<MonoBehaviour> m_MonoBehavioursToDisable;
        private Renderer m_GrabberRenderer;
        private Collider m_GrabberCollider;
        [SerializeField]
        private float m_SpawnDistance = 10f;
        [SerializeField]
        private float m_ResizeMaxScale = 2.5f;
        private BoxCollider m_InteractionCollider;
        private Vector2 m_OriginalWorldSize;
        public UIDocument UIDocument => m_UIDocument;

        // VR resize-drag state. The world-space panel is a fixed quad, so once the controller ray
        // leaves the UI collider (dragging off the edge) UITK stops sending pointer events. So we
        // start the resize from the handle's PointerDown, then drive the width from the controller
        // ray each frame (intersecting the panel plane) via VRInteractionController, ending on
        // trigger release. This works regardless of where the ray points.
        private bool m_Resizing;
        private bool m_ArmedForResize;
        private bool m_ResizeSubscribed;
        private int m_ResizeInteractorId = -1;
        private int m_HeldInteractorId = -1;
        private bool m_HasHeld;
        private Ray m_HeldRay;
        private float m_ResizeStartWidth;
        private Vector3 m_ResizeStartHit;
        private float m_WorldToWidthScale = 1f;

        protected override void InitializeUI()
        {
            if(m_UIDocument == null) return;

            m_OriginalWorldSize = m_UIDocument.worldSpaceSize;
            m_GrabberRenderer = m_UIDocument.transform.parent.GetComponentInParent<Renderer>();
            m_GrabberCollider = m_UIDocument.transform.parent.GetComponent<Collider>();
            m_MonoBehavioursToDisable ??= new HashSet<MonoBehaviour>();
            m_MonoBehavioursToDisable = m_UIDocument.transform.parent.GetComponents<MonoBehaviour>().Where(x => x != this).ToHashSet();
            GrabEnable(false);
            m_ToolPanelRoot = m_UIDocument.rootVisualElement.Q<VisualElement>(k_ToolPanelName);
            m_ToolPanelRoot.style.display = DisplayStyle.None;

            m_CloseToolPanelButton = m_ToolPanelRoot.Q<IconButton>(k_ToolCloseButtonName);
            m_CloseToolPanelButton.clickable.clicked += OnCloseToolPanelButtonClicked;
            m_ToolPanelTitle = m_ToolPanelRoot.Q<Text>(k_ToolTitleName);
            m_ContentPanel = m_ToolPanelRoot.Q<VisualElement>(k_ToolContentName);
            m_ResizeHandle = m_ToolPanelRoot.Q<VisualElement>("ResizeHandle");
            if (m_ResizeHandle != null)
            {
                m_ResizeHandle.RegisterCallback<PointerDownEvent>(OnHandlePointerDownVR);
                m_ResizeHandle.style.display = DisplayStyle.None;
            }
            StartCoroutine(WaitForBoxCollider());
            return;

            IEnumerator WaitForBoxCollider()
            {
                do
                {
                    m_InteractionCollider = m_UIDocument.GetComponent<BoxCollider>();
                    yield return null;
                } while (m_InteractionCollider == null);
                m_InteractionCollider.enabled = false;
            }
        }

        private void GrabEnable(bool value)
        {
            foreach (var behaviour in m_MonoBehavioursToDisable)
            {
                behaviour.enabled = value;
            }

            if (m_GrabberRenderer != null)
            {
                m_GrabberRenderer.enabled = value;
            }

            if (m_GrabberCollider != null)
            {
                m_GrabberCollider.enabled = value;
            }
        }

        protected override void OnOpenToolPanel(LocalizedString title, VisualElement content, bool resizable)
        {
            IsOpened = true;
            GrabEnable(true);
            AddContentToPanel(title, content);
            if (m_ResizeHandle != null)
            {
                m_ResizeHandle.style.display = resizable ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (resizable) SubscribeResizeInput(); else UnsubscribeResizeInput();
            m_InteractionCollider = m_UIDocument.GetComponent<BoxCollider>();
            m_InteractionCollider.enabled = true;

            Transform cam = Camera.main.transform;

            // Get the camera's forward direction and flatten it on the horizontal plane
            Vector3 forward = cam.forward;
            forward.y = 0;
            forward.Normalize();

            // Calculate the new position in front of the camera
            Vector3 newPosition = cam.position + forward * m_SpawnDistance;

            // Set the height to be relative to the camera's height plus an offset
            newPosition.y = cam.position.y;

            // Apply the new position
            transform.position = newPosition;

            // Make the UI panel face the camera
            transform.LookAt(cam.position);
            // Depending on your panel's geometry, you may need to reverse the forward vector
            transform.forward = -transform.forward;
        }

        // Grow the fixed-size world-space panel surface instead of the panel element: setting the
        // element width here would just overflow and clip the fixed quad. Growing worldSpaceSize
        // resizes the rendered surface (and its auto-updated collider); the ToolPanel fills it via flex-grow.
        protected override void ApplyResizeWidth(float width)
        {
            if (m_UIDocument == null) return;
            m_UIDocument.worldSpaceSize = new Vector2(width, m_OriginalWorldSize.y);
        }

        #region VR resize-drag

        private void SubscribeResizeInput()
        {
            if (m_ResizeSubscribed || VRInteractionController.Instance == null) return;
            VRInteractionController.SubscribePressActivate(this, OnVRPress);
            VRInteractionController.SubscribeControllerMoved(this, OnVRControllerMoved);
            m_ResizeSubscribed = true;
        }

        private void UnsubscribeResizeInput()
        {
            if (!m_ResizeSubscribed) return;
            VRInteractionController.UnsubscribePressActivate(this);
            VRInteractionController.UnsubscribeControllerMoved(this);
            m_ResizeSubscribed = false;
            m_Resizing = false;
            m_ArmedForResize = false;
        }

        // The handle press tells us the user grabbed the handle. Correlate it with the controller
        // that is currently holding the trigger (or arm for the press that is about to arrive).
        private void OnHandlePointerDownVR(PointerDownEvent evt)
        {
            if (m_HasHeld) StartResize(m_HeldInteractorId, m_HeldRay);
            else m_ArmedForResize = true;
            evt.StopPropagation();
        }

        private void OnVRPress(Ray ray, bool pressed, int interactorId)
        {
            if (pressed)
            {
                m_HasHeld = true;
                m_HeldInteractorId = interactorId;
                m_HeldRay = ray;
                if (m_ArmedForResize && !m_Resizing)
                {
                    m_ArmedForResize = false;
                    StartResize(interactorId, ray);
                }
            }
            else
            {
                if (interactorId == m_HeldInteractorId) m_HasHeld = false;
                if (m_Resizing && interactorId == m_ResizeInteractorId) StopResize();
            }
        }

        private void StartResize(int interactorId, Ray ray)
        {
            if (m_UIDocument == null) return;
            m_Resizing = true;
            m_ArmedForResize = false;
            m_ResizeInteractorId = interactorId;
            m_ResizeStartWidth = m_UIDocument.worldSpaceSize.x;
            // Convert a world-space drag distance into panel width units using the current
            // rendered world width of the panel collider.
            float colliderWidth = m_InteractionCollider != null ? m_InteractionCollider.bounds.size.x : 0f;
            m_WorldToWidthScale = colliderWidth > 0.0001f ? m_UIDocument.worldSpaceSize.x / colliderWidth : 1f;
            TryGetPlaneHit(ray, out m_ResizeStartHit);
        }

        private void StopResize()
        {
            m_Resizing = false;
            m_ResizeInteractorId = -1;
        }

        private void OnVRControllerMoved(Ray ray, int interactorId)
        {
            if (!m_Resizing || interactorId != m_ResizeInteractorId) return;
            if (!TryGetPlaneHit(ray, out var hit)) return;
            // Signed drag distance along the panel's horizontal axis. Handle sits on the left edge,
            // so dragging left (negative panel-right) should widen -> subtract.
            float worldDelta = Vector3.Dot(hit - m_ResizeStartHit, transform.right);
            float target = Mathf.Clamp(m_ResizeStartWidth - worldDelta * m_WorldToWidthScale,
                m_OriginalWorldSize.x, m_OriginalWorldSize.x * m_ResizeMaxScale);
            ApplyResizeWidth(target);
        }

        // Intersect a world ray with the panel's plane (works even when the ray is off the panel).
        private bool TryGetPlaneHit(Ray ray, out Vector3 hit)
        {
            hit = Vector3.zero;
            Vector3 n = transform.forward;
            float denom = Vector3.Dot(ray.direction, n);
            if (Mathf.Abs(denom) < 1e-6f) return false;
            float t = Vector3.Dot(transform.position - ray.origin, n) / denom;
            if (t <= 0f) return false;
            hit = ray.origin + ray.direction * t;
            return true;
        }

        #endregion

        protected override void ContentReset()
        {
            IsOpened = false;
            m_ToolPanelContent?.RemoveFromHierarchy();
            m_ToolPanelContent = null;
            UnsubscribeResizeInput();
            if (m_ResizeHandle != null)
            {
                m_ResizeHandle.style.display = DisplayStyle.None;
            }
            if (m_UIDocument != null)
            {
                m_UIDocument.worldSpaceSize = m_OriginalWorldSize;
            }
            if (m_InteractionCollider != null)
            {
                m_InteractionCollider.enabled = false;
            }
            GrabEnable(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeResizeInput();
            m_ResizeHandle?.UnregisterCallback<PointerDownEvent>(OnHandlePointerDownVR);
        }
    }
}
