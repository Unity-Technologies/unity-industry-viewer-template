using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.Industry.Viewer.Shared
{
    [DefaultExecutionOrder(-250)]
    public class NetworkDetector : MonoBehaviour
    {
        public static bool RequestedOfflineMode
        {
            get => _requestedOfflineMode;
            set
            {
                _requestedOfflineMode = value;
                if (_instance != null)
                {
                    _instance._isConnected = false;
                }
                _instance?.StopAllCoroutines();
                if (!value)
                {
                    _instance?.StartCoroutine(_instance.PingServer());
                }
                else
                {
                    // Force offline mode
                    OnNetworkStatusChanged?.Invoke(false);
                }
            }
        }
        
        private static bool _requestedOfflineMode = false;
        
        public static Action<bool> OnNetworkStatusChanged;
        public static bool IsOffline => !_instance._isConnected;

        private static NetworkDetector _instance;
        private const string PingAddress = "https://www.google.com"; // Cloudflare DNS
        private const float PingInterval = 10f; // 10 seconds
        private readonly WaitForSeconds _pingInterval = new(PingInterval);
        private bool _isConnected;

#if UNITY_EDITOR
        [SerializeField] private bool m_SimulateOffLine = false;
#endif
        private void Awake()
        {
            _instance = this;
        }

        private IEnumerator Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            yield return new WaitForSeconds(1f);
            _isConnected = true;
            OnNetworkStatusChanged?.Invoke(true);
            yield break;
#endif
            
            if (RequestedOfflineMode)
            {
                OnNetworkStatusChanged?.Invoke(false);
                Debug.Log("Offline mode requested.");
                yield break;
            }
            yield return PingServer();
            StartCoroutine(PingServerRoutine());
        }

        private IEnumerator PingServerRoutine()
        {
            while (true)
            {
                yield return _pingInterval;
                yield return PingServer();
            }
        }

        private IEnumerator PingServer()
        {
#if UNITY_EDITOR
            if (m_SimulateOffLine)
            {
                OnNetworkStatusChanged?.Invoke(false);
                Debug.Log($"Ping to {PingAddress} failed.");
                yield break;
            }
#endif
            
            using (UnityWebRequest request = UnityWebRequest.Get(PingAddress))
            {
                yield return request.SendWebRequest();

                bool success = request.result == UnityWebRequest.Result.Success;

                if (success)
                {
                    if (_isConnected) yield break;
                    _isConnected = true;
                    OnNetworkStatusChanged?.Invoke(true);
                    Debug.Log($"Ping to {PingAddress} successful.");
                }
                else
                {
                    if (!_isConnected) yield break;
                    _isConnected = false;
                    OnNetworkStatusChanged?.Invoke(false);
                    Debug.Log($"Ping to {PingAddress} failed. Error: {request.error}");
                }
            }
        }
    }
}