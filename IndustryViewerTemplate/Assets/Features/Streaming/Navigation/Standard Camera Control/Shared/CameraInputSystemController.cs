using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Industry.Viewer.Streaming;
using Unity.Cloud.Common;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Mathematics;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine.InputSystem.Processors;
#endif
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared
{
    /// <summary>
    /// Shared functionality for the navigation camera controllers
    /// </summary>
    public abstract class CameraInputSystemController : MonoBehaviour
    {
        [SerializeField]
        protected InputActionProperty m_MoveActionProperty;

        [SerializeField]
        protected InputActionProperty m_RotateActionProperty;

        [SerializeField]
        protected StandardCamera m_Camera;

        protected InputAction m_MoveAction;
        protected InputAction m_RotateAction;

        protected Vector3 m_MovementVector = Vector3.zero;
        protected Vector3 m_LastMovingAction = Vector3.zero;
        protected Vector2 m_RotateVector = Vector2.zero;

        protected DoubleBounds? m_MainBounds;

        protected bool m_Initialized;

        protected bool m_UpdateRotation;

        public float MoveSensitivity => m_MoveSensitivity;
        public float RotateSensitivity => m_RotateSensitivity;

        protected float m_MoveSensitivity = 1f;
        protected float m_RotateSensitivity = 1f;

        protected Vector2 m_PreviousTouchPosition;
        protected bool m_IsTouching;
        protected Touch m_Touch;

        protected StreamingModelController m_StreamingModelController;

        public virtual void UpdateMovementVector(Vector3 value) { }
        public virtual void UpdateRotateVector(Vector3 value) { }
        
        private WaitForEndOfFrame m_WaitForEndOfFrame = new WaitForEndOfFrame();

        protected void OnEnable()
        {
            m_LastMovingAction = Vector3.zero;
            m_RotateVector = Vector2.zero;

            m_MoveAction?.Enable();
            m_RotateAction?.Enable();

            m_UpdateRotation = false;

            EnhancedTouchSupport.Enable();

            NavigationController.PauseCameraControl += PauseCameraControl;
            InteractionController.SubscribeDoubleTap(this, OnDoubleTapActionInvoked);

            m_StreamingModelController ??= FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);

            StartCoroutine(WaitForCamera());
            return;

            IEnumerator WaitForCamera()
            {
                while (m_StreamingModelController.ActiveCamera == null)
                {
                    yield return null;
                }
                Ray ray = m_StreamingModelController.ActiveCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                RaycastStreamingModel(ray, false);
            }
        }

        protected virtual void Start()
        {
            if (m_Initialized) return;

            m_Initialized = true;
            m_MoveAction = m_MoveActionProperty.reference != null ? m_MoveActionProperty.reference.action : m_MoveActionProperty.action;
            m_MoveAction.performed += OnMove;
            m_MoveAction.canceled += OnMoveCanceled;
            m_MoveAction.Enable();

            m_RotateAction = m_RotateActionProperty.reference != null ? m_RotateActionProperty.reference.action : m_RotateActionProperty.action;
            m_RotateAction.performed += OnRotate;
            m_RotateAction.canceled += OnRotateCanceled;
            m_RotateAction.Enable();

            //Apply the scale processor to the move action on WebGL as the input is too sensitive
#if UNITY_WEBGL && !UNITY_EDITOR
            m_RotateAction.ApplyParameterOverride((ScaleVector2Processor p) => p.x, 0.3f);
            m_RotateAction.ApplyParameterOverride((ScaleVector2Processor p) => p.y, 0.3f);
#endif

        }
        
        protected void OnDoubleTapActionInvoked(Vector3 position)
        {
            if (m_StreamingModelController.ActiveCamera == null || TransformController.Instance == null)
            {
                return;
            }
            if (IsPointerOverUI()) return;
            Ray ray = m_StreamingModelController.ActiveCamera.ScreenPointToRay(position);
            RaycastStreamingModel(ray, true);
        }

        protected void RaycastStreamingModel(Ray ray, bool walkTo)
        {
            if (m_StreamingModelController == null)
            {
                return;
            }
            _ = Raycast();
            return;

            async Task Raycast()
            {
                try
                {
                    var raycastResult = await m_StreamingModelController.Stage.RaycastAsync((DoubleRay)ray, m_StreamingModelController.ActiveCamera.farClipPlane);
                    if (raycastResult.InstanceId == InstanceId.None)
                    {
                        return;
                    }
                    // Calculate min and max points
                    Vector3 maxPoint = Vector3.Max(m_StreamingModelController.ActiveCamera.transform.position, raycastResult.Point.ToVector3());
                    Vector3 minPoint = 2 * m_StreamingModelController.ActiveCamera.transform.position - maxPoint;

                    // Define the bounds
                    DoubleBounds bounds = new DoubleBounds(raycastResult.Point, new double3(maxPoint - minPoint));
                    if (walkTo)
                    {
                        GoTo(bounds);
                    }
                    else
                    {
                        SetLookAt(bounds);
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        }

        protected void Update()
        {
            if (m_MainBounds is not null && m_Camera != null)
            {
                var newBounds = StreamingUtils.ReturnBounds(m_MainBounds.Value);
                m_Camera.Utility.SetClipPlane(newBounds);
            }

            CheckTouches();
        }

        protected void CheckTouches()
        {
            if (m_Camera is null || Touch.activeTouches.Count == 0 || Touch.activeTouches.Count > 2 || !m_MoveAction.enabled || !m_RotateAction.enabled) return;
            foreach (var touch in Touch.activeTouches)
            {
                if (EventSystem.current.IsPointerOverGameObject(touch.touchId)) continue;
                if (touch.phase != TouchPhase.Began) continue;
                m_Touch = touch;
                m_IsTouching = true;
                m_PreviousTouchPosition = touch.screenPosition;
            }

            if (m_IsTouching)
            {
                if (m_Touch.phase == TouchPhase.Moved)
                {
                    var delta = m_Touch.screenPosition - m_PreviousTouchPosition;
                    m_PreviousTouchPosition = m_Touch.screenPosition;
                    m_RotateVector.x = -delta.y;
                    m_RotateVector.y = delta.x;
                    m_RotateVector *= m_RotateSensitivity * 0.1f;
                    m_Camera.Rotate(m_RotateVector);
                }
                else if (m_Touch.phase == TouchPhase.Ended || m_Touch.phase == TouchPhase.Canceled)
                {
                    m_IsTouching = false;
                    m_RotateVector = Vector2.zero;
                    m_Camera.Rotate(m_RotateVector);
                }
            }
        }

        protected void OnDestroy()
        {
            InteractionController.UnsubscribeDoubleTap(this);

            if (m_MoveAction != null)
            {
                m_MoveAction.performed -= OnMove;
                //m_MoveAction.canceled -= OnMoveCanceled;
            }

            if (m_RotateAction != null)
            {
                m_RotateAction.performed -= OnRotate;
                m_RotateAction.canceled -= OnRotateCanceled;
            }
        }

        protected void OnDisable()
        {
            NavigationController.PauseCameraControl -= PauseCameraControl;
            InteractionController.UnsubscribeDoubleTap(this);
            EnhancedTouchSupport.Disable();

            m_MoveAction?.Disable();
            m_RotateAction?.Disable();
            m_UpdateRotation = false;
        }

        protected void PauseCameraControl(bool shouldPause)
        {
            m_LastMovingAction = Vector3.zero;
            m_RotateVector = Vector2.zero;

            if (shouldPause)
            {
                m_MoveAction?.Disable();
                m_RotateAction?.Disable();
            }
            else
            {
                m_MoveAction?.Enable();
                m_RotateAction?.Enable();
            }
        }

        protected static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
        }

        protected virtual void OnRotateCanceled(InputAction.CallbackContext inputAction)
        {
            m_RotateVector = Vector2.zero;
            m_Camera.Rotate(m_RotateVector);
        }

        protected virtual void OnRotate(InputAction.CallbackContext inputAction)
        {
            if (!inputAction.performed) return;
            StartCoroutine(IsHoverUI());
            return;

            IEnumerator IsHoverUI()
            {
#if UNITY_WEBGL
                yield return null;
#else
                yield return m_WaitForEndOfFrame;
#endif
                if (IsPointerOverUI()) yield break;
                var value = inputAction.ReadValue<Vector2>();
                m_RotateVector.x = value.y;
                m_RotateVector.y = value.x;
                m_RotateVector *= m_RotateSensitivity;
                m_Camera.Rotate(m_RotateVector);
            }
        }

        protected virtual void OnMove(InputAction.CallbackContext inputAction)
        {
            if (!inputAction.performed) return;

            m_MovementVector = Vector3.zero;

            if (inputAction.valueType == typeof(Vector3))
            {
                var value = inputAction.ReadValue<Vector3>();
                m_MovementVector = new Vector3(value.x, value.z, value.y);
            }
            else if (inputAction.valueType == typeof(Vector2))
            {
                var value = inputAction.ReadValue<Vector2>();
                m_MovementVector = new Vector3(value.x, 0, value.y);
            }
            m_MovementVector *= m_MoveSensitivity;
            if (m_MovementVector == m_LastMovingAction) return;
            m_LastMovingAction = m_MovementVector;
            m_Camera.MoveInLocalDirection(m_MovementVector);
        }

        protected virtual void OnMoveCanceled(InputAction.CallbackContext inputAction)
        {
            m_MovementVector = Vector3.zero;
            m_LastMovingAction = Vector3.zero;
            m_Camera.MoveInLocalDirection(m_MovementVector);
        }

        public void SetView(DoubleBounds bounds)
        {
            if (m_Camera == null) return;
            m_MainBounds = bounds;
            if (!gameObject.activeSelf) return;
            m_Camera.Utility.SetView(bounds);
            SetLookAt(bounds);
        }

        public void HomeView()
        {
            if (m_Camera == null) return;
            //SetSpeedSettings(m_MainBounds.Value);
            m_Camera.Utility.SetView(m_MainBounds.Value);
            SetLookAt(m_MainBounds.Value);
        }

        public virtual void SetSpeedSettings(DoubleBounds bounds)
        {
            m_Camera.SetCameraSpeedSettings(bounds);
        }

        protected virtual void SetLookAt(DoubleBounds bounds, bool zoom = false)
        {
            var t = m_Camera?.Transform;
            //m_Camera?.SetCameraSpeedSettings(bounds);
            var newPosition = t.position;
            if (zoom)
            {
                Vector3 direction = (t.position - ((Bounds)bounds).center).normalized; // Step 1: Direction
                newPosition = ((Bounds)bounds).center + direction * 5f; // Step 2: Move along direction
            }
            
            m_Camera?.ResetTracking(newPosition, ((Bounds)bounds).center);
        }

        public virtual void GoTo(DoubleBounds bounds)
        {
            if (m_Camera == null) return;
            var t = m_Camera.Transform;
            Vector3 direction = (t.position - ((Bounds)bounds).center).normalized; // Step 1: Direction
            Vector3 result = ((Bounds)bounds).center + direction * 5f; // Step 2: Move along direction

            m_Camera.ResetTracking(result, ((Bounds)bounds).center);
        }

        public void UpdateView(DoubleBounds bounds)
        {
            m_MainBounds = bounds;
        }

        public void UpdateMoveSensitivity(float value)
        {
            m_MoveSensitivity = value;
        }

        public void UpdateRotateSensitivity(float value)
        {
            m_RotateSensitivity = value;
        }
    }
}
