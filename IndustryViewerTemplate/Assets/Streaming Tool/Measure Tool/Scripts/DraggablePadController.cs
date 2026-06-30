using System;
using System.Collections.Generic;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.VR;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Streaming.Measurement
{
    public class DraggablePadController : MonoBehaviour
    {
        private const string k_DraggablePad = "draggable_button";
        private const string k_HoverClass = "hover";
        
        public Action<Vector3, Ray?> NewCursorPosition;
        
        [SerializeField]
        private UIDocument m_DraggableDocument;
        
        private Vector3 m_dragStartPosition;
        
        private float m_DraggablePadDistance;
        
        private const float m_Tolerance = 0.1f;
        
        public bool IsDragging { get; private set; }

        private VisualElement m_DraggablePad;
        
#if VR_MODE
        private HashSet<int> m_ActiveInstanceIds = new HashSet<int>();
        private int? selectedInstanceId;
#endif

        private void Awake()
        {
            m_DraggablePad = m_DraggableDocument.rootVisualElement.Q(k_DraggablePad);
        }

        private void Start()
        {
#if !VR_MODE
            m_DraggablePad?.RegisterCallback<PointerDownEvent>(OnPointerDownDraggablePad);
            m_DraggablePad?.RegisterCallback<PointerUpEvent>(OnPointerUpDraggablePad);
#endif
            m_DraggablePad?.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            m_DraggablePad?.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            m_DraggablePad?.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            m_DraggablePad?.DisplayOff();
        }

        private void OnDisable()
        {
            NavigationController.PauseCameraControl?.Invoke(false);
        }

        private void OnDestroy()
        {
#if !VR_MODE
            m_DraggablePad?.UnregisterCallback<PointerDownEvent>(OnPointerDownDraggablePad);
            m_DraggablePad?.UnregisterCallback<PointerUpEvent>(OnPointerUpDraggablePad);
#endif
            m_DraggablePad?.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            m_DraggablePad?.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            m_DraggablePad?.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            IsDragging = false;
            if (!IsDragging)
            {
                if (m_DraggablePad.ClassListContains(k_HoverClass))
                {
                    m_DraggablePad?.RemoveFromClassList(k_HoverClass);
                }
            }
#if !VR_MODE
            SubscribePointerMove(false);
            NavigationController.PauseCameraControl?.Invoke(false);
#else
            m_ActiveInstanceIds.Remove(evt.pointerId);
            if (m_ActiveInstanceIds.Count == 0)
            {
                VRInteractionController.UnsubscribePressActivate(this);
            }
#endif
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (!IsDragging)
            {
                if (m_DraggablePad.ClassListContains(k_HoverClass))
                {
                    m_DraggablePad?.RemoveFromClassList(k_HoverClass);
                }
            }
#if VR_MODE
            m_ActiveInstanceIds.Remove(evt.pointerId);
            if (m_ActiveInstanceIds.Count == 0)
            {
                VRInteractionController.UnsubscribePressActivate(this);
            }
#endif
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (!m_DraggablePad.ClassListContains(k_HoverClass))
            {
                m_DraggablePad?.AddToClassList(k_HoverClass);
            }
#if VR_MODE
            m_ActiveInstanceIds.Add(evt.pointerId);
            if (m_ActiveInstanceIds.Count == 1)
            {
                VRInteractionController.SubscribePressActivate(this, OnTriggerPress);
            }
#endif
        }

        
        
#if VR_MODE
        private void OnTriggerPress(Ray ray, bool press, int instanceId)
        {
            if (press)
            {
                if(selectedInstanceId.HasValue) return;
                if (!Physics.Raycast(ray, out var hitInfo, 1000f, 1 << gameObject.layer)) return;
                if (hitInfo.transform != transform) return;
                selectedInstanceId = instanceId;
                m_dragStartPosition = transform.position;
                m_DraggablePadDistance = Vector3.Distance(ray.origin, transform.position);
                VRInteractionController.SubscribeControllerMoved(this, OnControllerMoved);
            }
            else
            {
                if(selectedInstanceId.HasValue && selectedInstanceId.Value == instanceId)
                {
                    selectedInstanceId = null;
                    IsDragging = false;
                    VRInteractionController.UnsubscribeControllerMoved(this);
                    if (m_DraggablePad.ClassListContains(k_HoverClass))
                    {
                        m_DraggablePad?.RemoveFromClassList(k_HoverClass);
                    }
                }
            }
        }

        private void OnControllerMoved(Ray ray, int instanceId)
        {
            if(!selectedInstanceId.HasValue || selectedInstanceId.Value != instanceId) return;
            Vector3 newPos = ray.origin + ray.direction * m_DraggablePadDistance;
            var delta = newPos - m_dragStartPosition;
            if (delta.magnitude >= m_Tolerance && !IsDragging)
            {
                IsDragging = true;
            }
            
            if (!IsDragging) return;
            transform.position = newPos;
            Vector3 newCursorPos = new Vector3(newPos.x, newPos.y + MeasurementToolUIController.DraggablePadOffset, newPos.z);
            Ray newRay = new Ray(new Vector3(ray.origin.x, ray.origin.y + MeasurementToolUIController.DraggablePadOffset, ray.origin.z), ray.direction);
            NewCursorPosition?.Invoke(newCursorPos, newRay);
        }
#endif

        public void SetCursorController(CursorController cursorController)
        {
            if (cursorController == null)
            {
                m_DraggablePad?.DisplayOff();
                return;
            }
            m_DraggablePad?.DisplayOn();
            Vector3 newCursorPos = new Vector3(cursorController.transform.position.x, cursorController.transform.position.y - MeasurementToolUIController.DraggablePadOffset, cursorController.transform.position.z);
            transform.position = newCursorPos;
        }

#if !VR_MODE
        private void SubscribePointerMove(bool subscribe)
        {
            if (subscribe)
            {

                InteractionController.SubscribePointerMove(this, OnDragPointerMove);
            }
            else
            {
                InteractionController.UnsubscribePointerMove(this);

            }
        }
        
        private void OnPointerUpDraggablePad(PointerUpEvent evt)
        {
            IsDragging = false;
            SubscribePointerMove(false);
            NavigationController.PauseCameraControl?.Invoke(false);
        }
        
        private void OnPointerDownDraggablePad(PointerDownEvent evt)
        {
            if (Camera.main == null) return;
            m_dragStartPosition = evt.position;
            m_DraggablePadDistance = Vector3.Distance(Camera.main.transform.position, transform.position);
            NavigationController.PauseCameraControl?.Invoke(true);
            SubscribePointerMove(true);
        }
        
        private void OnDragPointerMove(Vector3 newPos)
        {
            var delta = newPos - m_dragStartPosition;
            if (delta.magnitude >= m_Tolerance && !IsDragging){
                IsDragging = true;
            }
            if (!IsDragging) return;
            var mainCamera = Camera.main;
            if (mainCamera == null) return;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(newPos.x, newPos.y, m_DraggablePadDistance));
            transform.position = worldPos;
            Vector3 newCursorPos = new Vector3(worldPos.x, worldPos.y + MeasurementToolUIController.DraggablePadOffset, worldPos.z);
            NewCursorPosition?.Invoke(newCursorPos, null);
        }
#endif
    }
}
