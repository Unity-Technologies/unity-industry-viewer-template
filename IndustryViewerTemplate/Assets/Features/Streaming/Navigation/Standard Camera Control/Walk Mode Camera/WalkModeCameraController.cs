﻿using UnityEngine;

namespace Unity.Industry.Viewer.Navigation.WalkModeCamera
{
    public class WalkModeCameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [SerializeField]
        bool m_ClampVerticalRotation = true;

        [SerializeField]
        float m_MinimumX = -90F;

        [SerializeField]
        float m_MaximumX = 90F;

        [SerializeField]
        bool m_Smooth;

        [SerializeField]
        float m_SmoothTime = 5f;

        [SerializeField]
        bool m_LockCursor = true;

        public bool isCameraInverted = false;

        bool m_CursorLockState = true;
        Quaternion m_CharacterTargetRot;
        float globalRotationFactor = 0.25f; // Otherwise, rotation is just too fast even with runtime sensitivity

        // null if there is no input. Any value when we received input.
        // This helps to determine if the mouse button is still pressed to hide or show the cursor.
        Vector2? m_MoveInput;

        private void RotateCamera()
        {
            if (m_MoveInput != null)
            {
                var yRot = m_MoveInput.Value.x * globalRotationFactor;
                var xRot = m_MoveInput.Value.y * globalRotationFactor;
                m_MoveInput = null;

                var currTrans = _camera.transform;
                currTrans.Rotate(((isCameraInverted) ? -xRot : xRot), yRot, 0);
                currTrans.rotation = Quaternion.Euler(currTrans.rotation.eulerAngles.x, currTrans.rotation.eulerAngles.y, 0);
                m_CharacterTargetRot = currTrans.rotation;

                if (m_ClampVerticalRotation)
                {
                    m_CharacterTargetRot = ClampRotationAroundXAxis(m_CharacterTargetRot);
                }

                if (m_Smooth)
                {
                    _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, m_CharacterTargetRot,
                        m_SmoothTime * Time.deltaTime);
                }
                else
                {
                    _camera.transform.rotation = m_CharacterTargetRot;
                }
            }
        }

        internal void InternalLockUpdate(bool isLockCursor)
        {
            if (!m_LockCursor || m_CursorLockState == isLockCursor)
                return;

            m_CursorLockState = isLockCursor;

            if (isLockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        Quaternion ClampRotationAroundXAxis(Quaternion q)
        {
            q.x /= q.w;
            q.y /= q.w;
            q.z /= q.w;
            q.w = 1.0f;

            var angleX = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.x);

            angleX = Mathf.Clamp(angleX, m_MinimumX, m_MaximumX);

            q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleX);

            return q;
        }

        public void OnViewInput(Vector2 moveInput)
        {
            m_MoveInput = moveInput;
            RotateCamera();
        }

        public void ApplyNewPosition(Vector3 newPos)
        {
            _camera.transform.position = newPos;
        }

        public Vector3 GetCameraPosition()
        {
            return _camera.transform.position;
        }

        public Quaternion GetCameraRotation()
        {
            return _camera.transform.rotation;
        }

        public void ApplyNewPositionRotation(Vector3 newPos, Vector3 newEuler)
        {
            m_MoveInput = null;
            
            _camera.transform.position = newPos;
            _camera.transform.rotation = Quaternion.Euler(newEuler.x, newEuler.y, 0f);
        }
    }
}
