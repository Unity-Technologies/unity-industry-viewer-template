using UnityEngine;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
using Unity.Cloud.HighPrecision.Runtime;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Mathematics;

namespace Unity.Industry.Viewer.Navigation.WalkModeCamera
{
    [DefaultExecutionOrder(-50)]
    public class WalkCameraInputSystemController : CameraInputSystemController
    {
        [SerializeField]
        protected InputActionProperty m_RunActionProperty;
        [SerializeField]
        protected InputActionProperty m_JumpActionProperty;
        [SerializeField]
        private float m_MaxSpeedCeiling = 7f;
        [SerializeField]
        private float m_MaxSpeedFactor = 11f;

        private InputAction m_RunAction;
        private InputAction m_JumpAction;
        
        private WalkModeMoveController m_WalkModeMoveController;
        private WalkModeCameraController m_WalkModeCameraController;

        private Vector3 rotateVector;

        public WalkModeMoveController WalkModeMoveController => m_WalkModeMoveController;
        
        private CharacterController m_CharacterController;

        void Awake()
        {
            m_WalkModeMoveController = GetComponent<WalkModeMoveController>();
            m_WalkModeCameraController = GetComponent<WalkModeCameraController>();
            m_CharacterController = GetComponent<CharacterController>();
        }

        protected override void Start()
        {
            base.Start();
            
            m_RunAction = m_RunActionProperty.reference != null ? m_RunActionProperty.reference.action : m_RunActionProperty.action;
            m_RunAction.performed += OnRun;
            m_RunAction.canceled += OnRunCancel;
            m_RunAction.Enable();

            m_JumpAction = m_JumpActionProperty.reference != null ? m_JumpActionProperty.reference.action : m_JumpActionProperty.action;
            m_JumpAction.performed += OnJump;
            m_JumpAction.Enable();
        }

        new void OnEnable()
        {
            rotateVector = Vector3.zero;
            base.OnEnable();
            transform.position = m_WalkModeCameraController.GetCameraPosition();
            SetControllerYRotation();
        }

        new void OnDisable()
        {
            StopAllCoroutines();
            base.OnDisable();
        }

        new void Update()
        {
            if (rotateVector != Vector3.zero)
            {
                Vector2 rotateInput = new Vector2(rotateVector.y, rotateVector.x) * m_RotateSensitivity;
                m_WalkModeCameraController.OnViewInput(rotateInput);
                SetControllerYRotation();
            }
        }

        public override void GoTo(DoubleBounds bounds)
        {
            WalkModeMoveController.OnTeleportInput(true);
            StartCoroutine(TeleportTo(transform.position, ((Bounds)bounds).center, 0.5f));
        }

        IEnumerator TeleportTo(Vector3 start, Vector3 end, float duration)
        {
            float timeElapsed = 0;
            end.y = end.y + WalkModeMoveController.CharacterHeight / 2f;
            while (timeElapsed < duration)
            {
                transform.position = Vector3.Lerp(start, end, timeElapsed / duration);
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = end;
            WalkModeMoveController.OnTeleportInput(false);
        }


        protected override void OnMove(InputAction.CallbackContext inputAction)
        {
            var movement = inputAction.ReadValue<Vector2>();
            m_WalkModeMoveController.OnMoveInput(movement, m_MoveSensitivity);
        }
        
        protected override void OnMoveCanceled(InputAction.CallbackContext inputAction)
        {
            m_WalkModeMoveController.OnMoveInput(Vector2.zero, m_MoveSensitivity);
        }

        protected override void OnRotate(InputAction.CallbackContext inputAction)
        {
            var rotation = inputAction.ReadValue<Vector2>() * m_RotateSensitivity;
            m_WalkModeCameraController.OnViewInput(rotation);
            SetControllerYRotation();
        }
        
        protected override void OnRotateCanceled(InputAction.CallbackContext inputAction)
        {
            //cancel base class response
        }

        protected void OnRun(InputAction.CallbackContext inputAction)
        {
            m_WalkModeMoveController.OnSprintInput(true);
        }

        protected void OnRunCancel(InputAction.CallbackContext inputAction)
        {
            m_WalkModeMoveController.OnSprintInput(false);
        }

        protected void OnJump(InputAction.CallbackContext inputAction)
        {
            m_WalkModeMoveController.OnJumpInput();
        }

        private void SetControllerYRotation()
        {
            m_WalkModeMoveController.transform.rotation = Quaternion.Euler(0, m_WalkModeCameraController.GetCameraRotation().eulerAngles.y, 0);
        }

        public override void UpdateMovementVector(Vector3 value)
        {
            Vector2 moveInput = new Vector2(value.x, value.z);
            m_WalkModeMoveController.OnMoveInput(moveInput, m_MoveSensitivity);
        }

        public override void UpdateRotateVector(Vector3 value)
        {
            // Sensitivity multiplier happens in Update
            rotateVector = value;
        }

        public override void SetSpeedSettings(DoubleBounds bounds)
        {
            // Set walk speed by bounds
            var maxDistanceToMove = (float)math.length(bounds.Size);
            m_WalkModeMoveController.WalkSpeed = Mathf.Min(maxDistanceToMove / m_MaxSpeedFactor, m_MaxSpeedCeiling);
        }

        private void LateUpdate()
        {
            var finalPosition = transform.position;
            if (m_CharacterController != null)
            {
                finalPosition.y += m_CharacterController.height / 2f;
            }
            m_WalkModeCameraController.ApplyNewPosition(finalPosition);
        }
    }
}
