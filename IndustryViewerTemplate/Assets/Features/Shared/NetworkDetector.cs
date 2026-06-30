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
                    // Resume connectivity polling: an immediate check plus the recurring routine
                    // (mirrors Start). Previously only the one-shot PingServer ran, so the 10s
                    // poll never restarted after toggling back online and reconnections/drops
                    // went undetected for the rest of the session.
                    _instance?.StartCoroutine(_instance.PingServer());
                    _instance?.StartCoroutine(_instance.PingServerRoutine());
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
        // Guard against _instance being null (queried before Awake) or destroyed (teardown).
        // Treats "no detector" as offline, matching the pre-first-ping default (_isConnected == false).
        public static bool IsOffline => _instance == null || !_instance._isConnected;

        private static NetworkDetector _instance;
        private const string PingAddress = "https://www.google.com"; // Cloudflare DNS
        private const float PingInterval = 10f; // 10 seconds
        private readonly WaitForSeconds _pingInterval = new(PingInterval);
        private bool _isConnected;
        private bool _hasInitializedNetworkStatus;

#if UNITY_EDITOR
        [SerializeField] private bool m_SimulateOffLine = false;
        private bool _lastSimulateOffline;
#endif
        private void Awake()
        {
#if UNITY_EDITOR
            _lastSimulateOffline = m_SimulateOffLine;
#endif
            _instance = this;
        }

        private void OnDestroy()
        {
            // Clear the static reference so IsOffline and the RequestedOfflineMode setter don't
            // dereference a destroyed instance after this detector is torn down.
            if (_instance == this)
            {
                _instance = null;
            }
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
                    if (_isConnected && _hasInitializedNetworkStatus) yield break;
                    _isConnected = true;
                    _hasInitializedNetworkStatus = true;
                    OnNetworkStatusChanged?.Invoke(true);
                    Debug.Log($"Ping to {PingAddress} successful.");
                }
                else
                {
                    if (!_isConnected && _hasInitializedNetworkStatus) yield break;
                    _isConnected = false;
                    _hasInitializedNetworkStatus = true;
                    OnNetworkStatusChanged?.Invoke(false);
                    Debug.Log($"Ping to {PingAddress} failed. Error: {request.error}");
                }
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if(!Application.isPlaying) return;
            if (_lastSimulateOffline != m_SimulateOffLine)
            {
                _lastSimulateOffline = m_SimulateOffLine;
                OnNetworkStatusChanged?.Invoke(!m_SimulateOffLine);
            }
        }
#endif
    }
}