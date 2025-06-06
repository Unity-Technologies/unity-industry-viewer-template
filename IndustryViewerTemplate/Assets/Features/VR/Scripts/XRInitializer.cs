using UnityEngine;
using System;
using System.Collections;
using UnityEngine.XR.Management;

namespace Unity.Industry.Viewer.Navigation.VR
{
    public class XRInitializer : MonoBehaviour
    {
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
            }
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