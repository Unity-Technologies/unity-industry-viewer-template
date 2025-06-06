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

namespace Unity.Industry.Viewer.Navigation.OrbitCamera
{
    [DefaultExecutionOrder(-50)]
    public class OrbitCameraInputSystemController : MonoBehaviour
    {
        private const string k_KeyboardAndMouseName = "Mouse";
        private const string k_TouchName = "Touchscreen";
     
        [SerializeField]
        private float m_PanThreshold = 5f;
        
        [SerializeField]
        private float m_PinchThreshold = 5f;
        
        [SerializeField]
        private float m_OribitThreshold = 5f;
        
        [SerializeField]
        private InputActionProperty m_OrbitActionProperty;
        
        [SerializeField]
        private InputActionProperty m_PanActionProperty;
        
        [SerializeField]
        private InputActionProperty m_ZoomActionProperty;
        
        [SerializeField]
        private InputActionProperty m_MultiTouchActionProperty;
        
        [SerializeField]
        FreeOrbitCamera m_Camera;
        
        private InputAction m_OrbitAction;
        private InputAction m_PanAction;
        private InputAction m_ZoomAction;
        private InputAction m_MultiTouchAction;
        
        Vector2 m_OrbitVector = Vector2.zero;
        Vector2 m_PanVector = Vector2.zero;
        
        DoubleBounds? m_MainBounds;
        DoubleBounds? m_CurrentBounds;

        private bool m_Initialized;
        
        WaitForEndOfFrame m_WaitForEndOfFrame = new WaitForEndOfFrame();
        
        private bool m_UpdateRotation;
        
        public float ZoomSensitivity => m_ZoomSensitivity;
        public float PanSensitivity => m_PanSensitivity;
        public float OrbitSensitivity => m_OrbitSensitivity;
        
        private float m_ZoomSensitivity = 1f;
        private float m_PanSensitivity = 1f;
        private float m_OrbitSensitivity = 1f;
        
        private Vector2 previousTouchPosition0;
        private Vector2 previousTouchPosition1;
        
        private bool isPinching = false;
        private bool isPanning = false;
        private float boundsFactor;

        private StreamingModelController m_StreamingModelController;
        
        private void OnEnable()
        {
            m_OrbitVector = Vector2.zero;
            m_PanVector = Vector2.zero;
            
            m_OrbitAction?.Enable();
            m_PanAction?.Enable();
            m_ZoomAction?.Enable();
            m_MultiTouchAction?.Enable();
            
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
                Ray ray = m_StreamingModelController.ActiveCamera.ViewportPointToRay(new Vector3(0.5f,0.5f,0));
                RaycastStreamingModel(ray);
            }
        }

        private void Start()
        {
            if (m_Initialized) return;
            
            m_Initialized = true;
                
            m_OrbitAction = m_OrbitActionProperty.reference != null ? m_OrbitActionProperty.reference.action : m_OrbitActionProperty.action;
            m_OrbitAction.performed += OnOrbit;
            m_OrbitAction.canceled += OnOrbitCanceled;
            m_OrbitAction.Enable();
            
            //Apply the scale processor to the move action on WebGL as the input is too sensitive
#if UNITY_WEBGL && !UNITY_EDITOR
            m_OrbitAction.ApplyParameterOverride((ScaleVector2Processor p) => p.x, 0.4f);
            m_OrbitAction.ApplyParameterOverride((ScaleVector2Processor p) => p.y, 0.4f);
#endif
            
            m_PanAction = m_PanActionProperty.reference != null ? m_PanActionProperty.reference.action : m_PanActionProperty.action;
            m_PanAction.performed += OnPan;
            m_PanAction.canceled += OnPanCanceled;
            m_PanAction.Enable();
            
            m_ZoomAction = m_ZoomActionProperty.reference != null ? m_ZoomActionProperty.reference.action : m_ZoomActionProperty.action;
            m_ZoomAction.performed += OnZoom;
            m_ZoomAction.Enable();
            
            m_MultiTouchAction = m_MultiTouchActionProperty.reference != null ? m_MultiTouchActionProperty.reference.action : m_MultiTouchActionProperty.action;
            m_MultiTouchAction.performed += OnMultiTouch;
            m_MultiTouchAction.Enable();
        }

        private void Update()
        {
            if (m_MainBounds is not null)
            {
                var newBounds = StreamingUtils.ReturnBounds(m_MainBounds.Value);
                m_Camera.Utility.SetClipPlane(newBounds);
            }
        }

        private void OnDoubleTapActionInvoked(Vector3 position)
        {
            if (m_StreamingModelController.ActiveCamera == null)
            {
                return;
            }
            if(IsPointerOverUI())  return;
            Ray ray = m_StreamingModelController.ActiveCamera.ScreenPointToRay(position);
            RaycastStreamingModel(ray);
        }

        private void RaycastStreamingModel(Ray ray)
        {
            if(m_StreamingModelController == null) 
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
                    Vector3 maxPoint = Vector3.Max(m_Camera.Transform.position, raycastResult.Point.ToVector3());
                    Vector3 minPoint = 2 * m_Camera.Transform.position - maxPoint;

                    // Define the bounds
                    var bounds = new DoubleBounds(raycastResult.Point, new double3(maxPoint - minPoint));
                    SetLookAt(bounds);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        }

        private void OnDestroy()
        {
            if (m_OrbitAction != null)
            {
                m_OrbitAction.performed -= OnOrbit;
                m_OrbitAction.canceled -= OnOrbitCanceled;
            }
            
            if (m_PanAction != null)
            {
                m_PanAction.performed -= OnPan;
                m_PanAction.canceled -= OnPanCanceled;
            }
            
            if (m_ZoomAction != null)
            {
                m_ZoomAction.performed -= OnZoom;
            }
            
            if (m_MultiTouchAction != null)
            {
                m_MultiTouchAction.performed -= OnMultiTouch;
            }
        }

        private void OnDisable()
        {
            NavigationController.PauseCameraControl -= PauseCameraControl;
            InteractionController.UnsubscribeDoubleTap(this);
            EnhancedTouchSupport.Disable();
            
            m_OrbitAction?.Disable();
            m_PanAction?.Disable();
            m_ZoomAction?.Disable();
            m_MultiTouchAction?.Disable();
        }

        private void PauseCameraControl(bool shouldPause)
        {
            m_OrbitVector = Vector2.zero;
            m_PanVector = Vector2.zero;
            
            if (shouldPause)
            {
                m_OrbitAction?.Disable();
                m_PanAction?.Disable();
                m_ZoomAction?.Disable();
                m_MultiTouchAction?.Disable();
            }
            else
            {
                m_OrbitAction?.Enable();
                m_PanAction?.Enable();
                m_ZoomAction?.Enable();
                m_MultiTouchAction?.Enable();
            }
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
        }

        private void OnMultiTouch(InputAction.CallbackContext obj)
        {
            if(!obj.performed) return;
            
            if(Touch.activeTouches.Count < 2) return;

            var touch0 = Touchscreen.current.touches[0];
            var touch1 = Touchscreen.current.touches[1];

            //if(EventSystem.current.IsPointerOverGameObject(touch0.touchId.ReadValue()) || EventSystem.current.IsPointerOverGameObject(touch1.touchId.ReadValue())) return;

            if (touch0.phase.ReadValue() == TouchPhase.Began || touch1.phase.ReadValue() == TouchPhase.Began)
            {
                previousTouchPosition0 = touch0.position.ReadValue();
                previousTouchPosition1 = touch1.position.ReadValue();
                isPinching = false;
                isPanning = false;
            }
            
            // Check if both touches are in progress
            if (touch0.phase.ReadValue() != TouchPhase.Moved ||
                touch1.phase.ReadValue() != TouchPhase.Moved) return;

            // Calculate the previous and current positions of the touches

            // Calculate the previous and current distances between the touches
            float prevTouchDeltaMag = (previousTouchPosition0 - previousTouchPosition1).magnitude;
            float touchDeltaMag = (touch0.position.ReadValue() - touch1.position.ReadValue()).magnitude;

            // Calculate the difference in distances between each frame
            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            // If the distance between the touches has changed, it's a pinch gesture
            if (Mathf.Abs(deltaMagnitudeDiff) > m_PinchThreshold && !isPanning)
            {
                // Pinch detected, adjust zoom
                isPinching = true;
                float pinchAmount = deltaMagnitudeDiff * 0.1f; // Adjust the sensitivity as needed
                pinchAmount *= m_ZoomSensitivity * -1f * 0.1f;
                m_Camera.MoveOnLookAtAxis(pinchAmount);
            }
            else if(!isPinching)
            {
                var touch0Delta = touch0.position.ReadValue() - previousTouchPosition0;
                var touch1Delta = touch1.position.ReadValue() - previousTouchPosition1;
                // Drag detected, adjust camera position
                Vector2 touchDelta = (touch0Delta + touch1Delta) / 2;
                
                if (touchDelta.magnitude > m_PanThreshold)
                {
                    isPanning = true;
                    m_PanVector = touchDelta;
                    m_PanVector *= m_PanSensitivity * -1f * (boundsFactor/1000f);
                    m_Camera.Pan(m_PanVector);
                }
            }
            
            previousTouchPosition0 = touch0.position.ReadValue();
            previousTouchPosition1 = touch1.position.ReadValue();
        }
        
        private void OnZoom(InputAction.CallbackContext inputAction)
        {
            if(!inputAction.performed) return;
            StartCoroutine(IsHoverUI());
            return;
            
            IEnumerator IsHoverUI()
            {
                var deviceLayout = inputAction.control.device.layout;
#if UNITY_WEBGL
                yield return null;
#else
                yield return m_WaitForEndOfFrame;
#endif
                if(IsPointerOverUI()) yield break;

                float zoomValue = 0f;
                
                switch (deviceLayout)
                {
                    case k_KeyboardAndMouseName:
                        zoomValue = inputAction.ReadValue<float>();
                        break;
                    
                    case k_TouchName:
                        zoomValue = inputAction.ReadValue<float>();
                        break;
                    
                    default:
                        yield break;
                }
                zoomValue *= m_ZoomSensitivity;
                m_Camera.MoveOnLookAtAxis(zoomValue);
            }
        }
        
        private void OnPanCanceled(InputAction.CallbackContext inputAction)
        {
            m_PanVector = Vector2.zero;
            m_Camera.Pan(m_PanVector);
        }
        
        private void OnPan(InputAction.CallbackContext inputAction)
        {
            if (!inputAction.performed)
            {
                return;
            }
            
            StartCoroutine(IsHoverUI());
            return;
            
            IEnumerator IsHoverUI()
            {
#if UNITY_WEBGL
                yield return null;
#else
                yield return m_WaitForEndOfFrame;
#endif
                if(IsPointerOverUI()) yield break;

                var value = inputAction.ReadValue<Vector2>();
                m_PanVector = value * m_Camera.GetDistanceFromLookAt();
                
                m_PanVector *= m_PanSensitivity;
                m_Camera.Pan(m_PanVector);
            }
        }
        
        private void OnOrbit(InputAction.CallbackContext inputAction)
        {
            if(!inputAction.performed) return;
            StartCoroutine(IsHoverUI());
            return;
            
            IEnumerator IsHoverUI()
            {
                var deviceLayout = inputAction.control.device.layout;
#if UNITY_WEBGL
                yield return null;
#else
                yield return m_WaitForEndOfFrame;
#endif
                if(IsPointerOverUI()) yield break;
                
                Vector2 value;
                
                switch (deviceLayout)
                {
                    case k_KeyboardAndMouseName:
                        value = inputAction.ReadValue<Vector2>();
                        m_OrbitVector.x = value.y;
                        m_OrbitVector.y = value.x;
                        break;
                    
                    case k_TouchName:
                        var touches = Touch.activeTouches;
                        if (touches.Count != 1)
                        {
                            yield break;
                        }
                        
                        var touch0 = Touchscreen.current.touches[0];
                            
                        value = touch0.delta.ReadValue().magnitude > m_OribitThreshold ? inputAction.ReadValue<Vector2>() : Vector2.zero;
                            
                        m_OrbitVector.x = value.y;
                        m_OrbitVector.y = value.x;
                        break;
                    
                    default:
                        yield break;
                }
                
                m_OrbitVector *= m_OrbitSensitivity;
                
                m_Camera.OrbitAroundLookAt(m_OrbitVector);
            }
        }
        
        private void OnOrbitCanceled(InputAction.CallbackContext inputAction)
        {
            m_OrbitVector = Vector2.zero;
            m_Camera.OrbitAroundLookAt(m_OrbitVector);
        }
        
        public void SetView(DoubleBounds bounds)
        {
            m_CurrentBounds = bounds;
            //m_MainBounds = bounds;
            if(!gameObject.activeSelf) return;
            m_Camera.Utility.SetView(bounds);
            SetLookAt(bounds);
        }

        public void HomeView()
        {
            //SetSpeedSettings(m_MainBounds.Value);
            if(!m_MainBounds.HasValue) return;
            m_Camera?.Utility?.SetView(m_MainBounds.Value);
            SetLookAt(m_MainBounds.Value);
        }

        public void SetBoundSettings(DoubleBounds bounds)
        {
            boundsFactor = (float)math.length(bounds.Size);
        }

        public void SetLookAt(DoubleBounds bounds, bool zoom = false)
        {
            var t = m_Camera.Transform;
            //m_Camera.SetCameraSpeedSettings(bounds);
            
            Vector3 newPosition = t.position;
            if (zoom)
            {
                Vector3 direction = (t.position - ((Bounds)bounds).center).normalized;
                newPosition = ((Bounds)bounds).center + direction * 5.0f;
            }
            
            m_Camera.ResetTracking(newPosition, ((Bounds)bounds).center);
        }

        public void UpdateView(DoubleBounds bounds)
        {
            m_MainBounds = bounds;
        }
        
        public void UpdateZoomSensitivity(float value)
        {
            m_ZoomSensitivity = value;
        }
        
        public void UpdatePanSensitivity(float value)
        {
            m_PanSensitivity = value;
        }

        public void UpdateOrbitSensitivity(float value)
        {
            m_OrbitSensitivity = value;
        }
    }
}
