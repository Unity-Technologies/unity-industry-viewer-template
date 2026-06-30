using UnityEngine;
using System;
using System.Collections;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace Unity.Industry.Viewer.VR
{
    public class XRInitializer : MonoBehaviour
    {
        // Fixed Foveated Rendering strength applied once XR is running (0 = off … 1 = strongest).
        // Tunable: lower it if peripheral blur is objectionable; raise it to recover more GPU.
        const float k_FoveatedRenderingLevel = 1.0f;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        IEnumerator Start()
        {
            // Initialize the XR loader and wait until it's done
            yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

            // Check if the XR loader was successfully initialized
            if (XRGeneralSettings.Instance.Manager.activeLoader == null)
            {
                // Log an error if the XR initialization failed
                Debug.LogError("Initializing XR failed. Check that you have the XR plugin installed in your project.");
            }
            else
            {
                // Start the XR subsystems if the loader was successfully initialized
                XRGeneralSettings.Instance.Manager.StartSubsystems();

                // Let the display subsystem come fully online, then request foveated rendering.
                yield return null;
                ApplyFoveatedRendering();
            }
        }

        // Requests Fixed Foveated Rendering on the running display subsystem. Enabling the OpenXR
        // "SRP Foveation" API in project settings is not sufficient on its own — the foveation level
        // must be set at runtime, otherwise it stays 0 (no foveation, full-resolution shading
        // everywhere). Verify via OVR Metrics: foveation_level should report > 0 after this runs.
        void ApplyFoveatedRendering()
        {
            var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            var display = loader != null ? loader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
            if (display == null)
            {
                Debug.LogWarning("[XRInitializer] No running XRDisplaySubsystem; foveated rendering not set.");
                return;
            }

            display.foveatedRenderingLevel = k_FoveatedRenderingLevel;
            Debug.Log($"[XRInitializer] Foveated rendering level set to {display.foveatedRenderingLevel}.");
        }

        // Called when the MonoBehaviour is destroyed
        private void OnDestroy()
        {
            // Stop the XR subsystems
            XRGeneralSettings.Instance.Manager.StopSubsystems();
            // Deinitialize the XR loader
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
        }
    }
}