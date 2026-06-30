using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;
using System.Collections;
#if !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.XR;
#endif

namespace Unity.Industry.Viewer.VR
{
    [DefaultExecutionOrder(int.MinValue)]
    public class ReparentToController : MonoBehaviour
    {
        [SerializeField]
        private Handedness m_Side;
        
        [SerializeField, Header("Controllers")]
        private Vector3 m_LocalControllerPosition = Vector3.zero;
        
        [SerializeField, Header("Hands")]
        private Vector3 m_LocalHandsPosition = Vector3.zero;
        
        private Scene m_Scene;
        
        private GameObject m_Parent;
        
        private XRInputModalityManager m_InputModalityManager;
        
        private TrackedPoseDriver m_AimPoseDriver;
        
        private ControlType m_InputType;
        
        [SerializeField]
        private bool m_CanBeDestroyed = true;
        
        private bool m_SwitchInProgress = false;
        
        private Vector3 m_LastParentPosition;
        private const float k_LargeJumpThresholdSqr = 1f;
        
        private void Awake()
        {
            if (m_Side == Handedness.Invalid)
            {
                gameObject.SetActive(false);
                return;
            }
            m_Scene = gameObject.scene;
            m_InputModalityManager ??= FindFirstObjectByType<XRInputModalityManager>();
            m_InputModalityManager.motionControllerModeStarted.AddListener(UseMotionController);
            m_InputModalityManager.motionControllerModeEnded.AddListener(StopFollowingMotionController);
            m_InputModalityManager.trackedHandModeStarted.AddListener(UseTrackedHandMode);
            m_InputModalityManager.trackedHandModeEnded.AddListener(StopFollowingHands);
            UpdateParent();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void LateUpdate()
        {
            if (m_Parent == null)
            {
                return;
            }

            Vector3 currentParentPosition = m_Parent.transform.position;
            
            // Detect large controller position jumps
            if (m_LastParentPosition != Vector3.zero)
            {
                float positionDeltaSqr = (currentParentPosition - m_LastParentPosition).sqrMagnitude;
                if (positionDeltaSqr > k_LargeJumpThresholdSqr)
                {
                    // Skip this frame to avoid incorrect positioning
                    m_LastParentPosition = currentParentPosition;
                    return;
                }
            }
            
            m_LastParentPosition = currentParentPosition;

            if (m_InputType == ControlType.MotionControllers)
            {
                var newPosition = m_Parent.transform.position + m_LocalControllerPosition;
                transform.SetPositionAndRotation(newPosition, m_Parent.transform.rotation);
            }
            else
            {
                var newPosition = m_Parent.transform.position + m_LocalHandsPosition;
                var dir = m_Parent.transform.position - Camera.main.transform.position;
                dir.y = 0; // Keep the direction horizontal
                dir.Normalize();
                if (dir == Vector3.zero)
                {
                    dir = Vector3.forward; // Fallback to a default direction if zero
                }
                var newRotation = Quaternion.LookRotation(dir, Vector3.up);
                transform.SetPositionAndRotation(newPosition, newRotation);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            m_InputModalityManager.motionControllerModeStarted.RemoveListener(UseMotionController);
            m_InputModalityManager.motionControllerModeEnded.RemoveListener(StopFollowingMotionController);
            m_InputModalityManager.trackedHandModeStarted.RemoveListener(UseTrackedHandMode);
            m_InputModalityManager.trackedHandModeEnded.RemoveListener(StopFollowingHands);
        }


        private void UseTrackedHandMode()
        {
            if(m_SwitchInProgress) return;
            m_SwitchInProgress = true;
            if (m_AimPoseDriver == null)
            {
                var handModel = m_Side == Handedness.Left ? m_InputModalityManager.leftHand : m_InputModalityManager.rightHand;
                var drivers = handModel.GetComponentsInChildren<TrackedPoseDriver>()
                    .Where(x => x.gameObject.name == "Aim Pose");
                var trackedPoseDrivers = drivers.ToList();
                if (trackedPoseDrivers.Any())
                {
                    m_AimPoseDriver = trackedPoseDrivers.First();
                }
                else
                {
                    Debug.LogWarning($"No tracked pose driver found for {m_Side} hand in TrackedHandMode.");
                    m_Parent = null;
                    m_SwitchInProgress = false;
                    return;
                }
            }
            m_InputType = ControlType.Hands;
            m_Parent  = m_AimPoseDriver.gameObject;
            if (m_Parent != null)
            {
                m_LastParentPosition = m_Parent.transform.position;
            }
            StartCoroutine(ResetSwitchProcessBoolean());
        }
        
        private void StopFollowingMotionController()
        {
            if(m_SwitchInProgress) return;
            m_InputType = ControlType.None;
            m_Parent = null;
            m_LastParentPosition = Vector3.zero;
        }
        
        private void StopFollowingHands()
        {
            if(m_SwitchInProgress) return;
            m_InputType = ControlType.None;
            m_Parent = null;
            m_LastParentPosition = Vector3.zero;
        }

        private void UseMotionController()
        {
            if(m_SwitchInProgress)return;
            m_SwitchInProgress = true;
            m_InputType = ControlType.MotionControllers;
            m_Parent = m_Side == Handedness.Left ? m_InputModalityManager.leftController : m_InputModalityManager.rightController;
            if (m_Parent != null)
            {
                m_LastParentPosition = m_Parent.transform.position;
            }
            StartCoroutine(ResetSwitchProcessBoolean());
        }

        private IEnumerator ResetSwitchProcessBoolean()
        {
            yield return new WaitForSeconds(0.2f);
            m_SwitchInProgress = false;
        }

        private void UpdateParent()
        {
            m_InputModalityManager ??= FindFirstObjectByType<XRInputModalityManager>();
            if(m_InputModalityManager == null) return;
            
            #if UNITY_EDITOR
            m_InputType = ControlType.MotionControllers;
            #else
            var targetCharacteristics = m_Side == Handedness.Left
                ? InputDeviceCharacteristics.Left: InputDeviceCharacteristics.Right;
            
            m_InputType = GetHandInputType(targetCharacteristics);
            #endif
            
            
            if (m_InputType == ControlType.MotionControllers)
            {
                UseMotionController();
            }
            else if (m_InputType == ControlType.Hands)
            {
                UseTrackedHandMode();
            }
            else
            {
                m_Parent = null;
            }
        }

        private void OnSceneUnloaded(Scene arg0)
        {
            if(!m_CanBeDestroyed) return;
            if(arg0 == m_Scene)
            {
                Destroy(gameObject);
            }
        }

#if !UNITY_EDITOR
        public ControlType GetHandInputType(InputDeviceCharacteristics characteristics)
        {
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                characteristics, devices);

            foreach (var device in devices)
            {
                if ((device.characteristics & InputDeviceCharacteristics.HandTracking) != 0)
                    return ControlType.Hands;
                if ((device.characteristics & InputDeviceCharacteristics.Controller) != 0)
                    return ControlType.MotionControllers;
            }
            return ControlType.None;
        }
#endif

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                UpdateParent();
            }
        }
        
        public void UpdateTrackedParentPosition()
        {
            if (m_Parent != null)
            {
                m_LastParentPosition = m_Parent.transform.position;
            }
        }
    }
}
