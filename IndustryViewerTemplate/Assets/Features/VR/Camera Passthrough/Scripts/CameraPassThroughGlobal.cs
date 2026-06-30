using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace Unity.Industry.Viewer.VR.CameraPassThrough
{
    public static class CameraPassThroughGlobal
    {
        public const string k_CameraPassThroughToggleName = "CameraPassThroughToggle";
        public static bool isCameraPassThroughEnabled = false;
        public static bool InMRMode = false;
        public const string k_CameraPassThroughEnabledKey = "CameraPassThroughEnabled";
        private static Color originalBackgroundColor;
        private static CameraClearFlags originalCameraFlags;
        
        
        public static void ToggleCameraPassThrough(bool newValue)
        {
            if (Camera.main == null) return;
            if (!Camera.main.transform.TryGetComponent(out ARCameraManager ARCameraManager))
            {
                ARCameraManager = Camera.main.gameObject.AddComponent<ARCameraManager>();
            }
            ARCameraManager.enabled = newValue;
            
            ARSession ARSession = Object.FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
            if (ARSession == null)
            {
                var arSessionObject = new GameObject("ARSession");
                arSessionObject.SetActive(false);
                ARSession = arSessionObject.AddComponent<ARSession>();
                ARSession.gameObject.AddComponent<ARInputManager>();
            }

            if (newValue)
            {
                ARSession.gameObject.SetActive(true);
            }
            
            ARSession.enabled = newValue;
            if (newValue)
            {
                originalCameraFlags = Camera.main.clearFlags;
                if (originalCameraFlags == CameraClearFlags.Color || originalCameraFlags == CameraClearFlags.SolidColor)
                {
                    originalBackgroundColor = Camera.main.backgroundColor;
                }
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.clear;
                ControllerVisibility(false);
                
            }
            else
            {
                Camera.main.clearFlags = originalCameraFlags;
                Camera.main.backgroundColor = originalBackgroundColor;
                ControllerVisibility(true);
            }
        }
        
        private static void ControllerVisibility(bool visibility)
        {
            var inputModalityManager = Object.FindFirstObjectByType<XRInputModalityManager>(FindObjectsInactive.Include);
            if (inputModalityManager == null) return;
            if (inputModalityManager.leftController != null &&
                inputModalityManager.leftController.activeSelf)
            {
                RendererEnable(inputModalityManager.leftController, visibility);
            }

            if (inputModalityManager.rightController != null &&
                inputModalityManager.rightController.activeSelf)
            {
                RendererEnable(inputModalityManager.rightController, visibility);
            }

            if (inputModalityManager.leftHand != null &&
                inputModalityManager.leftHand.activeSelf)
            {
                RendererEnable(inputModalityManager.leftHand, visibility);
            }

            if (inputModalityManager.rightHand != null &&
                inputModalityManager.rightHand.activeSelf)
            {
                RendererEnable(inputModalityManager.rightHand, visibility);
            }
            return;
            
            void RendererEnable(GameObject go, bool newState)
            {
                foreach (var rendererComponent in go.GetComponentsInChildren<Renderer>())
                {
                    if(rendererComponent is LineRenderer) continue;
                    rendererComponent.enabled = newState;
                }
            }
        }
    }
}
