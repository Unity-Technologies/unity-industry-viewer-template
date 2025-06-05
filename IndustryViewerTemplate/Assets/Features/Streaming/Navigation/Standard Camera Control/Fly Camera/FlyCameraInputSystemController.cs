using UnityEngine;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine.InputSystem.Processors;
#endif

namespace Unity.Industry.Viewer.Navigation.FlyCamera
{
    [DefaultExecutionOrder(-50)]
    public class FlyCameraInputSystemController : CameraInputSystemController
    {        
        public override void UpdateMovementVector(Vector3 value)
        {
            m_MovementVector = value * m_MoveSensitivity;
            
            if (m_MovementVector == m_LastMovingAction) return;
            m_LastMovingAction = m_MovementVector;
            m_Camera.MoveInLocalDirection(m_MovementVector);
        }
    }
}
