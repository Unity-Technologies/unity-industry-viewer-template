using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared
{
    public class GamepadStick
    {
        private CameraInputSystemController m_CameraInputSystemController;

        readonly VisualElement m_GamepadBackground;
        readonly VisualElement m_GamepadHandle;
        readonly bool m_IsRightStick;
        readonly bool m_InvertStickYAxis, m_InvertStickXAxis;
        readonly float m_InitialStickMultiplier;
        private Vector2 m_StickStartPosition;
        private Vector2 m_JoystickDelta; // Between -1 and 1
        private Color m_SemiTransparent = new Color(1, 1, 1, 0.1f);
        private int m_PointerId = -1;

        public GamepadStick(VisualElement root, string backgroundName, string handleName, bool isRightStick, StickSettings settings)
        {
            m_GamepadBackground = root.Q<VisualElement>(backgroundName);
            m_GamepadHandle = root.Q<VisualElement>(handleName);

            m_GamepadHandle.RegisterCallback<PointerDownEvent>(OnStickDown);
            m_GamepadHandle.RegisterCallback<PointerMoveEvent>(OnStickMove);
            m_GamepadHandle.RegisterCallback<PointerUpEvent>(OnStickUp);
            
            m_GamepadBackground.RegisterCallback<GeometryChangedEvent>(GeometryChangedEvent);

            m_GamepadHandle.style.backgroundColor = m_SemiTransparent;
            m_IsRightStick = isRightStick;
            m_CameraInputSystemController = settings.cameraInputSystemController;
            m_InvertStickXAxis = settings.invertStickXAxis;
            m_InvertStickYAxis = settings.invertStickYAxis;
            m_InitialStickMultiplier = settings.initialStickMultiplier;
            TurnOnUI();
        }

        private void GeometryChangedEvent(GeometryChangedEvent evt)
        {
            if (m_PointerId == -1) return;
            ResetAll(m_PointerId);
        }

        public void TurnOnUI()
        {
            m_GamepadBackground.style.display = DisplayStyle.Flex;
        }

        public void ResetUI()
        {
            m_GamepadBackground.style.display = DisplayStyle.None;
        }
        public void UnregisterCallbacks()
        {
            m_GamepadHandle.UnregisterCallback<PointerDownEvent>(OnStickDown);
            m_GamepadHandle.UnregisterCallback<PointerMoveEvent>(OnStickMove);
            m_GamepadHandle.UnregisterCallback<PointerUpEvent>(OnStickUp);
            m_GamepadBackground.UnregisterCallback<GeometryChangedEvent>(GeometryChangedEvent);
        }

        void OnStickDown(PointerDownEvent evt)
        {
            m_PointerId = evt.pointerId;
            m_GamepadHandle.CapturePointer(evt.pointerId);
            m_StickStartPosition = evt.position;
            m_GamepadHandle.style.backgroundColor = Color.white;
        }

        void OnStickMove(PointerMoveEvent evt)
        {
            if (!m_GamepadHandle.HasPointerCapture(evt.pointerId)) return;
            var pointerCurrentPosition = (Vector2)evt.position;
            var pointerMaxDelta = (m_GamepadBackground.worldBound.size - m_GamepadHandle.worldBound.size) / 2;
            var pointerDelta = Clamp(pointerCurrentPosition - m_StickStartPosition, -pointerMaxDelta,
                pointerMaxDelta);
            m_GamepadHandle.transform.position = pointerDelta;
            m_JoystickDelta = pointerDelta / pointerMaxDelta;

            if (m_InvertStickYAxis)
                m_JoystickDelta.y = -m_JoystickDelta.y;

            if (m_InvertStickXAxis)
                m_JoystickDelta.x = -m_JoystickDelta.x;

            m_JoystickDelta *= m_InitialStickMultiplier;

            if (m_IsRightStick)
                m_CameraInputSystemController.UpdateRotateVector(new Vector3(m_JoystickDelta.y, m_JoystickDelta.x, 0));
            else
                m_CameraInputSystemController.UpdateMovementVector(new Vector3(m_JoystickDelta.x, 0, m_JoystickDelta.y));
        }

        void OnStickUp(PointerUpEvent evt)
        {
            ResetAll(evt.pointerId);
        }
        
        void ResetAll(int pointerId)
        {
            if (m_IsRightStick)
                m_CameraInputSystemController.UpdateRotateVector(Vector3.zero);
            else
                m_CameraInputSystemController.UpdateMovementVector(Vector3.zero);
            m_GamepadHandle.style.backgroundColor = m_SemiTransparent;
            ResetStick(pointerId, m_GamepadHandle, out m_JoystickDelta);
            m_PointerId = -1;
        }

        void ResetStick(int pointerId, VisualElement stick, out Vector2 delta)
        {
            stick.ReleasePointer(pointerId);
            stick.transform.position = Vector3.zero;
            delta = Vector2.zero;
        }

        Vector2 Clamp(Vector2 v, Vector2 min, Vector2 max) => new Vector2(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y));
    }

    public struct StickSettings
    {
        public bool invertStickXAxis;
        public bool invertStickYAxis;
        public float initialStickMultiplier;
        public CameraInputSystemController cameraInputSystemController;
    }
}
