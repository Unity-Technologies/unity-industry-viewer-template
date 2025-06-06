using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Unity.Industry.Viewer.Streaming
{
    // The InteractionController class manages user interactions such as tap, pointer move, and double tap.
    // It uses Unity's InputAction system to handle these interactions and allows other components to subscribe to these events.
    // The class includes methods to subscribe and unsubscribe from these interactions, and it updates the state of the input actions based on the number of subscribers.
    // The class also ensures that only one instance of the InteractionController exists at a time.
    [DefaultExecutionOrder(-100)]    
    public class InteractionController : MonoBehaviour
    {
        // Events for different types of interactions
        private static event Action<Vector3> OnTapInteract;
        private static event Action<Vector3> OnPointerMove;
        private static event Action<Vector3> OnDoubleTapInteract;

        // Dictionaries to keep track of subscribers for each interaction type
        private static readonly Dictionary<MonoBehaviour, Action<Vector3>> TapSubscribers = new();
        private static readonly Dictionary<MonoBehaviour, Action<Vector3>> PointerMoveSubscribers = new();
        private static readonly Dictionary<MonoBehaviour, Action<Vector3>> DoubleTapSubscribers = new();

        // Singleton instance of the InteractionController
        private static InteractionController Instance;

        // Serialized fields for input actions
        [SerializeField]
        private InputActionProperty m_InputCoordinatesProperty;

        [SerializeField]
        private InputActionProperty m_SelectActionProperty;

        [SerializeField]
        private InputActionProperty m_doubleTapActionProperty;

        // Input actions
        private InputAction m_SelectAction;
        private InputAction m_InputCoordinatesAction;
        private InputAction m_DoubleTapAction;

        // Vector to store input coordinates
        private Vector3 m_InputCoordinates;

        private void Awake()
        {
            // Set the singleton instance
            Instance = this;

            // Initialize input actions
            m_SelectAction = m_SelectActionProperty.reference != null ? m_SelectActionProperty.reference.action :
                m_SelectActionProperty.action;

            m_InputCoordinatesAction = m_InputCoordinatesProperty.reference != null ?
                m_InputCoordinatesProperty.reference.action : m_InputCoordinatesProperty.action;

            m_DoubleTapAction = m_doubleTapActionProperty.reference != null ? m_doubleTapActionProperty.reference.action :
                m_doubleTapActionProperty.action;
        }

        // Start is called before the first frame update
        void Start()
        {
            // Subscribe to input action events
            m_SelectAction.performed += OnSelectAction;
            m_InputCoordinatesAction.performed += OnInputCoordinates;
            m_DoubleTapAction.performed += OnDoubleTapAction;
        }

        // Called when the MonoBehaviour is destroyed
        private void OnDestroy()
        {
            // Clear the singleton instance
            Instance = null;

            // Unsubscribe from input action events
            m_SelectAction.performed -= OnSelectAction;
            m_InputCoordinatesAction.performed -= OnInputCoordinates;
            m_DoubleTapAction.performed -= OnDoubleTapAction;

            // Clear subscribers and events
            TapSubscribers.Clear();
            PointerMoveSubscribers.Clear();
            DoubleTapSubscribers.Clear();
            OnTapInteract = null;
            OnPointerMove = null;
            OnDoubleTapInteract = null;
        }

        // Called when the double tap action is performed
        private void OnDoubleTapAction(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            StartCoroutine(WaitForFrame());

            IEnumerator WaitForFrame()
            {
#if UNITY_WEBGL
                yield return null;
#else
                yield return new WaitForEndOfFrame();
#endif
                if (EventSystem.current != null)
                {
                    if(EventSystem.current.IsPointerOverGameObject(-1)) yield break;
                }
                OnDoubleTapInteract?.Invoke(m_InputCoordinates);
            }
        }

        // Called when the select action is performed
        private void OnSelectAction(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            StartCoroutine(WaitForFrame());

            IEnumerator WaitForFrame()
            {
#if UNITY_WEBGL
                yield return null;
#else
                yield return new WaitForEndOfFrame();
#endif
                if (EventSystem.current != null)
                {
                    if(EventSystem.current.IsPointerOverGameObject(-1)) yield break;
                }
                OnTapInteract?.Invoke(m_InputCoordinates);
            }
        }

        // Called when the input coordinates action is performed
        private void OnInputCoordinates(InputAction.CallbackContext action)
        {
            if(action.phase != InputActionPhase.Performed) return;
            if (action.valueType == typeof(Vector3))
            {
                m_InputCoordinates = action.ReadValue<Vector3>();
            } else if(action.valueType == typeof(Vector2))
            {
                m_InputCoordinates = action.ReadValue<Vector2>();
            }
            OnPointerMove?.Invoke(m_InputCoordinates);
        }

        // Subscribe to tap interactions
        public static void SubscribeTap(MonoBehaviour subscriber, Action<Vector3> action)
        {
            if (!TapSubscribers.TryAdd(subscriber, action)) return;
            OnTapInteract += action;
            UpdateActionStates();
        }

        // Unsubscribe from tap interactions
        public static void UnsubscribeTap(MonoBehaviour unsubscriber)
        {
            if (!TapSubscribers.TryGetValue(unsubscriber, out var action)) return;
            OnTapInteract -= action;
            TapSubscribers.Remove(unsubscriber);
            UpdateActionStates();
        }

        // Subscribe to pointer move interactions
        public static void SubscribePointerMove(MonoBehaviour subscriber, Action<Vector3> action)
        {
            if (!PointerMoveSubscribers.TryAdd(subscriber, action)) return;
            OnPointerMove += action;
            UpdateActionStates();
        }

        // Unsubscribe from pointer move interactions
        public static void UnsubscribePointerMove(MonoBehaviour unsubscriber)
        {
            if (!PointerMoveSubscribers.TryGetValue(unsubscriber, out var action)) return;
            OnPointerMove -= action;
            PointerMoveSubscribers.Remove(unsubscriber);
            UpdateActionStates();
        }

        // Subscribe to double tap interactions
        public static void SubscribeDoubleTap(MonoBehaviour subscriber, Action<Vector3> action)
        {
            if (!DoubleTapSubscribers.TryAdd(subscriber, action)) return;
            OnDoubleTapInteract += action;
            UpdateActionStates();
        }

        // Unsubscribe from double tap interactions
        public static void UnsubscribeDoubleTap(MonoBehaviour unsubscriber)
        {
            if (!DoubleTapSubscribers.TryGetValue(unsubscriber, out var action)) return;
            OnDoubleTapInteract -= action;
            DoubleTapSubscribers.Remove(unsubscriber);
            UpdateActionStates();
        }

        // Update the states of the input actions based on the number of subscribers
        private static void UpdateActionStates()
        {
            if (PointerMoveSubscribers.Count > 0)
            {
                Instance.m_InputCoordinatesAction?.Enable();
            } else if (PointerMoveSubscribers.Count == 0 && TapSubscribers.Count == 0 && DoubleTapSubscribers.Count == 0)
            {
                Instance.m_InputCoordinatesAction?.Disable();
            }

            if (TapSubscribers.Count > 0)
            {
                Instance.m_SelectAction?.Enable();
                Instance.m_InputCoordinatesAction?.Enable();
            }
            else if (TapSubscribers.Count == 0 && PointerMoveSubscribers.Count == 0 && DoubleTapSubscribers.Count == 0)
            {
                Instance.m_SelectAction?.Disable();
                Instance.m_InputCoordinatesAction?.Disable();
            }

            if (DoubleTapSubscribers.Count > 0)
            {
                Instance.m_DoubleTapAction?.Enable();
                Instance.m_InputCoordinatesAction?.Enable();
            }
            else if (DoubleTapSubscribers.Count == 0 && TapSubscribers.Count == 0 && PointerMoveSubscribers.Count == 0)
            {
                Instance.m_DoubleTapAction?.Disable();
                Instance.m_InputCoordinatesAction?.Disable();
            }
        }
    }
}