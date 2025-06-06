using System;
using System.Threading.Tasks;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Industry.Viewer.Streaming;
using UnityEngine;

namespace Unity.Industry.Viewer.Navigation.WalkModeCamera
{
    [RequireComponent(typeof(CharacterController))]
    public class WalkModeMoveController : MonoBehaviour
    {
        [SerializeField]
        float m_WalkSpeed = 1;
        [SerializeField]
        float m_SprintSpeed = 3;
        [SerializeField]
        float m_JumpForce = 0.7f;
        [SerializeField]
        float m_StepUpSpeed = 0.8f; // the lower the value, the "smoother" the feel
        [SerializeField]
        float m_MaxFallSpeed = -30;
        [SerializeField]
        float m_GroundedRayLength = 1.5f; // will fire downcast 1.5m from character's feet, regardless of character height
        
        private StreamingModelController m_StreamingModelController;
        private RaycastResult m_GroundRayResult;

        public float CharacterHeight
        {
            get
            {
                if (m_Controller != null)
                    return m_Controller.height;
                return 1.7f;
            }
            set
            {
                if (m_Controller != null)
                    m_Controller.height = value;
            }
        }

        public float WalkSpeed { get => m_WalkSpeed; set => m_WalkSpeed = value; }

        CharacterController m_Controller;
        Task m_GetGroundTask;
        Vector3 m_MoveDirectionInput;
        Vector3 m_PlayerVelocity;

        bool m_IsGrounded;
        bool m_IsSprinting;
        bool m_IsJumpingInput;
        bool m_IsTeleporting;
        bool m_IsHoverTeleporting;
        bool m_NoGroundWarningShown;
        bool m_GoUpStep;
        float m_StepUpTime;
        float sensitivityFactor = 1f;
        
        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_Controller.height = CharacterHeight;
        }

        private void OnEnable()
        {
            m_StreamingModelController ??= FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
            m_IsTeleporting = false;
        }

        void FixedUpdate()
        {
            Move();
        }

        /// <summary>
        /// The main function to control movement
        /// </summary>
        void Move()
        {
            if (m_IsTeleporting)
                return;
            
            var currentTransform = transform;
            
            if (m_GetGroundTask?.IsCompleted ?? true)
            {
                _ = GetGroundAsync(currentTransform);
            }

            // y movement
            HandleJump();
            HandleGround();
            HandleStepUp();
            
            // x and z movement
            var desiredMove = XZMovement(currentTransform);
            
            transform.position += (desiredMove + m_PlayerVelocity) * Time.deltaTime;
        }

        void HandleJump()
        {
            // allows jumping mid-air or on no ground detected
            if (m_IsJumpingInput && m_PlayerVelocity.y <= 0.2f)
            {
                m_PlayerVelocity.y += Mathf.Sqrt(m_JumpForce * -3.0f * Physics.gravity.y);
                m_IsGrounded = false;
            }
            m_IsJumpingInput = false; // zeroing input to prevent infinity jumping
        }
        
        async Task GetGroundAsync(Transform currentTransform)
        {
            m_GetGroundTask = DownCast();
            await m_GetGroundTask;
            if (m_GroundRayResult.HasIntersected)
            {
                var feetPosition = currentTransform.position.y - m_Controller.height/2f;
                m_IsGrounded = m_Controller.isGrounded || feetPosition <= m_GroundRayResult.Point.y;
                m_GoUpStep = m_GroundRayResult.Distance < m_GroundedRayLength - 0.05f; // add epsilon to ignore miniscule steps
                if (!m_GoUpStep)
                {
                    m_StepUpTime = 0f; // reset
                }

                m_NoGroundWarningShown = false;
            }
            else
            {
                if (!m_NoGroundWarningShown)
                    HandleNoGroundWarning();
            }
        }
        
        async Task DownCast()
        {
            try
            {
                Ray ray = new Ray(GetDownCastFirePosition(), Vector3.down);
                m_GroundRayResult = await m_StreamingModelController.Stage.RaycastAsync((DoubleRay) ray, 10f);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        
        void HandleNoGroundWarning()
        {
            Debug.LogWarning("No Ground Warning");
            m_NoGroundWarningShown= true;
        }
        
        
        void HandleGround()
        {
            if (m_GroundRayResult.HasIntersected && m_IsGrounded && m_PlayerVelocity.y <= 0)
            {
                m_PlayerVelocity.y = 0;
            }
            else if (!m_GroundRayResult.HasIntersected && m_PlayerVelocity.y <= 0)
            {
                m_PlayerVelocity.y = 0;
            }
            else if (m_IsHoverTeleporting)
            {
                m_PlayerVelocity.y = 0;
            }
            else
            {
                m_PlayerVelocity.y += Physics.gravity.y * Time.deltaTime;
            }

            Mathf.Clamp(m_PlayerVelocity.y, m_MaxFallSpeed, m_MaxFallSpeed * -1f);
        }

        void HandleStepUp()
        {
            m_StepUpTime += Time.deltaTime * m_StepUpSpeed;
            if (m_GoUpStep)
            {
                var stepUpDistance = m_GroundedRayLength - m_GroundRayResult.Distance;
                transform.position += new Vector3(0f, Mathf.Lerp(0f, (float)stepUpDistance, m_StepUpTime), 0f);
            }
            m_GoUpStep = false;
        }

        Vector3 XZMovement(Transform currentTransform)
        {
            var dir = m_MoveDirectionInput.z * currentTransform.forward + m_MoveDirectionInput.x * currentTransform.right;
            dir = Vector3.ClampMagnitude(dir, 1); // making the same movement speed in all directions
            var normal = m_GroundRayResult.HasIntersected ? (Vector3)m_GroundRayResult.Normal : Vector3.up;
            dir = Vector3.ProjectOnPlane(dir, normal).normalized;
            // m_WalkSpeed gets set by the WalkCameraInputController.SetSpeedSettings
            var moveSpeed = (m_IsSprinting ? m_SprintSpeed : m_WalkSpeed) * sensitivityFactor;
            var desiredMove = Vector3.zero;
            desiredMove.x = dir.x * moveSpeed;
            desiredMove.z = dir.z * moveSpeed;

            return desiredMove;
        }

        Vector3 GetDownCastFirePosition()
        {
            var currPos = transform.position;
            var feetPos = currPos.y - m_Controller.height / 2f;
            currPos.y = feetPos + m_GroundedRayLength;
            return currPos;
        }
        
        public void OnMoveInput(Vector2 direction, float sensitivity)
        {
            sensitivityFactor = sensitivity;
            m_MoveDirectionInput.x = direction.x;
            m_MoveDirectionInput.z = direction.y;
        }

        public void OnMoveInputPressed()
        {
            m_IsHoverTeleporting = false;
        }

        public void OnJumpInput()
        {
            m_IsJumpingInput = true;
            m_IsHoverTeleporting = false;
        }

        public void OnSprintInput(bool isSprinting)
        {
            m_IsSprinting = isSprinting;
            m_IsHoverTeleporting = false;
        }

        public void OnTeleportInput(bool isTeleporting)
        {
            m_IsTeleporting = isTeleporting;
            m_IsHoverTeleporting = false;
        }
        
        public void OnHoverTeleportInput()
        {
            m_IsHoverTeleporting = true;
        }
    }
}
