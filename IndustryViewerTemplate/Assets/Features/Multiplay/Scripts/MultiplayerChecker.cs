using System;
using UnityEngine;
#if ENABLE_MULTIPLAY
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Identity;
using UnityEngine.SceneManagement;
#endif

namespace Unity.Industry.Viewer.Multiplay
{
    [DefaultExecutionOrder(Int32.MinValue)]
    public class MultiplayerChecker : MonoBehaviour
    {
        private void Awake()
        {
#if !ENABLE_MULTIPLAY
            gameObject.SetActive(false);
#else
            if (gameObject.scene == SceneManager.GetSceneByBuildIndex(0))
            {
                NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            }
            else if (ShouldDisable())
            {
                gameObject.SetActive(false);
            }
#endif
        }

#if ENABLE_MULTIPLAY
        private void OnNetworkStatusChanged(bool connected)
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;

            if (ShouldDisable() || NetworkDetector.RequestedOfflineMode)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(connected);
        }

        private bool ShouldDisable()
        {
#if ENABLE_GUEST_MODE
            return IdentityController.GuestMode || (NetworkDetector.IsOffline && !NetworkDetector.RequestedOfflineMode);
#else
            return NetworkDetector.IsOffline && !NetworkDetector.RequestedOfflineMode;
#endif
        }
#endif
    }
}