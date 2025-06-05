using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Unity.Industry.Viewer.Navigation.VR
{
    // This script manages VR interactions in a Unity project.
    // It handles input actions for single, double, and press activations for both left and right hand controllers.
    // The script manages event subscriptions for these actions and notifies subscribers when actions are performed.
    // It also tracks controller movements and updates subscribers accordingly.
    // The script integrates with Unity's MonoBehaviour for lifecycle management and uses Unity's XR Interaction Toolkit for VR interactions.
    public class VRInteractionController : MonoBehaviour
    {
        // Event for single activation
        private static event Action<Ray, int> OnSingleActivatePressed;
        private static event Action<Ray, bool, int> OnPressActivatePressed; 
        private static event Action<Ray, int> OnDoubleActivatePressed;
        private static event Action<Ray, int> OnControllerMoved;
        
        // Dictionary to store subscribers for single activation
        private static readonly Dictionary<MonoBehaviour, Action<Ray, int>> SingleActivateSubscribers = new();
        private static readonly Dictionary<MonoBehaviour, Action<Ray, int>> DoubleActivateSubscribers = new();
        private static readonly Dictionary<MonoBehaviour, Action<Ray, int>> ControllerMovedSubscribers = new();
        private static readonly Dictionary<MonoBehaviour, Action<Ray, bool, int>> PressActivateSubscribers = new();
        
        public static VRInteractionController Instance;

        #region Left Hand
        [Header("Left Hand")]
        [SerializeField]
        private InputActionProperty m_LeftSingleActivateActionProperty;
        
        [SerializeField]
        private InputActionProperty m_LeftDoubleActivateActionProperty;
        
        [SerializeField]
        private InputActionProperty m_LeftPressActivateActionProperty;
        
        private InputAction m_LeftSingleActivateAction;
        
        private InputAction m_LeftDoubleActivateAction;
        
        private InputAction m_LeftPressActivateAction;

        #endregion

        #region Right Hand
        [Header("Right Hand")]
        [SerializeField]
        private InputActionProperty m_RightSingleActivateActionProperty;
        
        [SerializeField]
        private InputActionProperty m_RightDoubleActivateActionProperty;
        
        [SerializeField]
        private InputActionProperty m_RightPressActivateActionProperty;
        
        private InputAction m_RightSingleActivateAction;
        
        private InputAction m_RightDoubleActivateAction;
        
        private InputAction m_RightPressActivateAction;

        #endregion
        
        public XRRayInteractor LeftRayInteractor => leftRayInteractor;
        public XRRayInteractor RightRayInteractor => rightRayInteractor;
        
        private XRRayInteractor leftRayInteractor;
        private XRRayInteractor rightRayInteractor;

        private void OnEnable()
        {
            m_LeftDoubleActivateAction?.Enable();
            m_RightDoubleActivateAction?.Enable();
            m_LeftSingleActivateAction?.Enable();
            m_RightSingleActivateAction?.Enable();
            m_LeftPressActivateAction?.Enable();
            m_RightPressActivateAction?.Enable();
        }

        // Initialize the script and set up input actions for left and right hand controllers.
        private void Start()
        {
            Instance = this;

            var rayInteractors = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            leftRayInteractor = rayInteractors.FirstOrDefault(x => x.handedness == InteractorHandedness.Left);
            rightRayInteractor = rayInteractors.FirstOrDefault(x => x.handedness == InteractorHandedness.Right);

            if (leftRayInteractor != null)
            {
                m_LeftSingleActivateAction = m_LeftSingleActivateActionProperty.reference != null ? m_LeftSingleActivateActionProperty.reference.action :
                    m_LeftSingleActivateActionProperty.action;
            
                m_LeftDoubleActivateAction = m_LeftDoubleActivateActionProperty.reference != null ? m_LeftDoubleActivateActionProperty.reference.action :
                    m_LeftDoubleActivateActionProperty.action;
                
                m_LeftPressActivateAction = m_LeftPressActivateActionProperty.reference != null ? m_LeftPressActivateActionProperty.reference.action :
                    m_LeftPressActivateActionProperty.action;
            
                m_LeftSingleActivateAction.performed += OnLeftSingleActivateAction;
            
                m_LeftDoubleActivateAction.performed += OnLeftDoubleActivateAction;
                
                m_LeftPressActivateAction.performed += OnLeftPressActivateAction;
            }

            if (rightRayInteractor != null)
            {
                m_RightSingleActivateAction = m_RightSingleActivateActionProperty.reference != null ? m_RightSingleActivateActionProperty.reference.action :
                    m_RightSingleActivateActionProperty.action;
            
                m_RightDoubleActivateAction = m_RightDoubleActivateActionProperty.reference != null ? m_RightDoubleActivateActionProperty.reference.action :
                    m_RightDoubleActivateActionProperty.action;
                
                m_RightPressActivateAction = m_RightPressActivateActionProperty.reference != null ? m_RightPressActivateActionProperty.reference.action :
                    m_RightPressActivateActionProperty.action;
            
                m_RightSingleActivateAction.performed += OnRightSingleActivateAction;
            
                m_RightDoubleActivateAction.performed += OnRightDoubleActivateAction;
                
                m_RightPressActivateAction.performed += OnRightPressActivateAction;
            }
        }

        // Update the controller movement and notify subscribers.
        private void Update()
        {
            if(ControllerMovedSubscribers.Count == 0) return;
            if (leftRayInteractor != null)
            {
                OnControllerMoved?.Invoke(new Ray(leftRayInteractor.transform.position, leftRayInteractor.transform.forward), leftRayInteractor.gameObject.GetInstanceID());
            }
            
            if (rightRayInteractor != null)
            {
                OnControllerMoved?.Invoke(new Ray(rightRayInteractor.transform.position, rightRayInteractor.transform.forward), rightRayInteractor.gameObject.GetInstanceID());
            }
        }

        // Disable input actions when the script is disabled.
        private void OnDisable()
        {
            m_LeftDoubleActivateAction?.Disable();
            m_RightDoubleActivateAction?.Disable();
            m_LeftSingleActivateAction?.Disable();
            m_RightSingleActivateAction?.Disable();
            m_LeftPressActivateAction?.Disable();
            m_RightPressActivateAction?.Disable();
        }

        // Unsubscribe from events and clear dictionaries when the script is destroyed.
        private void OnDestroy()
        {
            Instance = null;

            if (leftRayInteractor != null)
            {
                m_LeftSingleActivateAction.performed -= OnLeftSingleActivateAction;
            
                m_LeftDoubleActivateAction.performed -= OnLeftDoubleActivateAction;
                
                m_LeftPressActivateAction.performed -= OnLeftPressActivateAction;
            }

            if (rightRayInteractor != null)
            {
                m_RightSingleActivateAction.performed -= OnRightSingleActivateAction;
            
                m_RightDoubleActivateAction.performed -= OnRightDoubleActivateAction;
                
                m_RightPressActivateAction.performed -= OnRightPressActivateAction;
            }
            
            SingleActivateSubscribers.Clear();
            DoubleActivateSubscribers.Clear();
            OnSingleActivatePressed = null;
            OnDoubleActivatePressed = null;
            OnPressActivatePressed = null;
        }

        private void OnRightPressActivateAction(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            OnPressActivatePressed?.Invoke(new Ray(rightRayInteractor.transform.position, rightRayInteractor.transform.forward), action.ReadValueAsButton(), rightRayInteractor.gameObject.GetInstanceID());
        }

        private void OnLeftPressActivateAction(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            OnPressActivatePressed?.Invoke(new Ray(leftRayInteractor.transform.position, leftRayInteractor.transform.forward), action.ReadValueAsButton(), leftRayInteractor.gameObject.GetInstanceID());
        }

        private void OnRightDoubleActivateAction(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            
            OnDoubleActivatePressed?.Invoke(new Ray(rightRayInteractor.transform.position, rightRayInteractor.transform.forward), rightRayInteractor.gameObject.GetInstanceID());
        }

        private void OnRightSingleActivateAction(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            OnSingleActivatePressed?.Invoke(new Ray(rightRayInteractor.transform.position, rightRayInteractor.transform.forward), rightRayInteractor.gameObject.GetInstanceID());
        }

        private void OnLeftDoubleActivateAction(InputAction.CallbackContext action)
        {

            if(action.phase != InputActionPhase.Performed) return;
            OnDoubleActivatePressed?.Invoke(new Ray(leftRayInteractor.transform.position, leftRayInteractor.transform.forward), leftRayInteractor.gameObject.GetInstanceID());
        }

        private void OnLeftSingleActivateAction(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            OnSingleActivatePressed?.Invoke(new Ray(leftRayInteractor.transform.position, leftRayInteractor.transform.forward), leftRayInteractor.gameObject.GetInstanceID());
        }
        
        public static void SubscribeSingleActivate(MonoBehaviour subscriber, Action<Ray, int> action)
        {
            if (!SingleActivateSubscribers.TryAdd(subscriber, action)) return;
            OnSingleActivatePressed += action;
            UpdateActionStates();
        }
        
        public static void UnsubscribeSingleActivate(MonoBehaviour subscriber)
        {
            if (!SingleActivateSubscribers.TryGetValue(subscriber, out var action)) return;
            OnSingleActivatePressed -= action;
            SingleActivateSubscribers.Remove(subscriber);
            UpdateActionStates();
        }
        
        public static void SubscribePressActivate(MonoBehaviour subscriber, Action<Ray, bool, int> action)
        {
            if (!PressActivateSubscribers.TryAdd(subscriber, action)) return;
            OnPressActivatePressed += action;
            UpdateActionStates();
        }
        
        public static void UnsubscribePressActivate(MonoBehaviour subscriber)
        {
            if (!PressActivateSubscribers.TryGetValue(subscriber, out var action)) return;
            OnPressActivatePressed -= action;
            PressActivateSubscribers.Remove(subscriber);
            UpdateActionStates();
        }
        
        public static void SubscribeDoubleActivate(MonoBehaviour subscriber, Action<Ray, int> action)
        {
            if (!DoubleActivateSubscribers.TryAdd(subscriber, action)) return;
            OnDoubleActivatePressed += action;
            UpdateActionStates();
        }
        
        public static void UnsubscribeDoubleActivate(MonoBehaviour unsubscriber)
        {
            if (!DoubleActivateSubscribers.TryGetValue(unsubscriber, out var action)) return;
            OnDoubleActivatePressed -= action;
            DoubleActivateSubscribers.Remove(unsubscriber);
            UpdateActionStates();
        }
        
        public static void SubscribeControllerMoved(MonoBehaviour subscriber, Action<Ray, int> action)
        {
            if (!ControllerMovedSubscribers.TryAdd(subscriber, action)) return;
            OnControllerMoved += action;
        }
        
        public static void UnsubscribeControllerMoved(MonoBehaviour unsubscriber)
        {
            if (!ControllerMovedSubscribers.TryGetValue(unsubscriber, out var action)) return;
            OnControllerMoved -= action;
            ControllerMovedSubscribers.Remove(unsubscriber);
        }

        // Update the action states based on the number of subscribers.
        private static void UpdateActionStates()
        {
            if (SingleActivateSubscribers.Count > 0)
            {
                Instance.m_LeftSingleActivateAction?.Enable();
                Instance.m_RightSingleActivateAction?.Enable();
            } else if(SingleActivateSubscribers.Count == 0)
            {
                Instance.m_LeftSingleActivateAction?.Disable();
                Instance.m_RightSingleActivateAction?.Disable();
            }
            
            if(PressActivateSubscribers.Count > 0)
            {
                Instance.m_LeftPressActivateAction?.Enable();
                Instance.m_RightPressActivateAction?.Enable();
            } else if(PressActivateSubscribers.Count == 0)
            {
                Instance.m_LeftPressActivateAction?.Disable();
                Instance.m_RightPressActivateAction?.Disable();
            }
            
            if (DoubleActivateSubscribers.Count > 0)
            {
                Instance.m_LeftDoubleActivateAction?.Enable();
                Instance.m_RightDoubleActivateAction?.Enable();
            } else if(DoubleActivateSubscribers.Count == 0)
            {
                Instance.m_LeftDoubleActivateAction?.Disable();
                Instance.m_RightDoubleActivateAction?.Disable();
            }
        }
    }
}
