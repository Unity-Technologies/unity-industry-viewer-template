using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Streaming;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Unity.Industry.Viewer.Identity;
using Unity.Cloud.Assets;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using System.Threading;
using Unity.Cloud.Common;

namespace Unity.Industry.Viewer.Multiplay
{
    // This script manages multiplayer sessions in Unity using Unity Services and Netcode.
    // It handles session creation, joining, and player management, including adding and removing player objects.
    // The script also manages presentation mode, allowing players to join or end presentations.
    // It integrates with the IdentityController for offline mode handling and user authentication.
    // The script includes event handlers for connection events and session state changes.
    public class MultiplayController : MonoBehaviour
    {
        // Event handlers for multiplayer session events
        public static Action<ulong, GameObject> OnClientConnected;
        public static Action<ulong> OnClientDisconnected;
        public static Action InitializePresentationMode;
        public static Action RequestToJoinPresentation;
        public static Action<ulong> JoinPresentation;
        public static Action<ulong> EndPresentation;
        public static Action<string> OnSessionJoinedFailed;
        public static Action<string, string, string, string> SearchNewLayout;
        public static Action<AssetInfo> AskToJoinLayout;
        
        private string m_SessionName;// = "TestSession";
        [SerializeField, Range(2, 8)]
        private int m_MaxPlayers = 4;
        
        private ISession m_CurrentSession;
        
        public Dictionary<ulong, GameObject> PlayerObjects => m_PlayerObjects;
        
        private Dictionary<ulong, GameObject> m_PlayerObjects = new Dictionary<ulong, GameObject>();
        
        private List<Color> m_UsedColors;
        
        [SerializeField]
        private GameObject transformControllerObjectPrefab;
        
        NetworkObject m_TransformNetworkObject;

        [SerializeField] private GameObject modelSyncPrefab;
        
        NetworkObject m_ModelSyncNetworkObject;
        
        /// <summary>
        /// Initializes the multiplayer session by checking offline mode, streaming asset, and Unity services state.
        /// If not in offline mode and streaming asset is available, it initializes Unity services and signs in anonymously if not already signed in.
        /// Finally, it sets the session name and attempts to create or join a session.
        /// </summary>
        private void Start()
        {
            SearchNewLayout += OnSearchNewLayout;
            NetworkDetector.OnNetworkStatusChanged += NetworkStatusChanged;
            if(NetworkDetector.RequestedOfflineMode) return;
            _ = Initializing();
        }
        
        /// <summary>
        /// Cleans up event handlers and network connections when the object is destroyed.
        /// If the application is in offline mode, it returns early.
        /// Unsubscribes from network connection events and multiplayer session events.
        /// Calls LeaveSession to leave the current multiplayer session asynchronously.
        /// </summary>
        private void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= NetworkStatusChanged;
            SearchNewLayout -= OnSearchNewLayout;
            AssetsController.AssetSelected -= OnAssetSelected;
            if(NetworkDetector.IsOffline) return;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
            }
            
            OnClientConnected -= OnAddClientGameObject;
            OnClientDisconnected -= OnRemoveClientGameObject;
            EndPresentation -= OnEndPresentation;
            
            InitializePresentationMode -= OnInitializePresentationMode;
            JoinPresentation -= OnJoinPresentation;
            
            LeaveSession().Forget();
        }

        private void NetworkStatusChanged(bool connected)
        {
            _ = !connected ? LeaveSession() : Initializing();
        }

        private async Task Initializing()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized &&
                UnityServices.State != ServicesInitializationState.Initializing)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            
            m_SessionName = StreamingUtils.ReturnHashName(StreamingModelController.StreamingAsset.Value.Asset) + StreamingModelController.StreamingAssetVersion;
            
            Debug.Log("Initialized " + m_SessionName);
            
            _ = CreateOrJoinSession();
        }

        private void OnSearchNewLayout(string orgId, string projectId, string assetId, string assetVersion)
        {
            _ = SearchAsset();
            return;

            async Task SearchAsset()
            {
                IAssetRepository m_AssetRepository = IdentityController.GuestMode? PlatformServices.ServiceAccountAssetRepository : PlatformServices.AssetRepository;
                ProjectDescriptor projectDescriptor =
                    new ProjectDescriptor(new OrganizationId(orgId), new ProjectId(projectId));
                AssetDescriptor assetDescriptor = new AssetDescriptor(projectDescriptor, new AssetId(assetId),
                    new AssetVersion(assetVersion));
                var asset = await m_AssetRepository.GetAssetAsync(assetDescriptor, CancellationToken.None);
                if (asset == null)
                {
                    Debug.Log("Asset not found");
                    return;
                }

                var property = await asset.GetPropertiesAsync(CancellationToken.None);
                
                if(StreamingModelController.StreamingAsset.Value.Asset.Descriptor.AssetId == asset.Descriptor.AssetId
                   && StreamingModelController.StreamingAsset.Value.Asset.Descriptor.ProjectDescriptor == asset.Descriptor.ProjectDescriptor)
                {
                    Debug.Log("Asset already selected");
                    return;
                }
                
                AskToJoinLayout.Invoke(new AssetInfo()
                {
                    Asset = asset,
                    Properties = property
                });
            }
        }

        /// <summary>
        /// Creates or joins a multiplayer session asynchronously.
        /// Configures session options, subscribes to necessary events, and attempts to create or join a session.
        /// If successful, logs the session details; otherwise, logs an error and invokes the session join failure event.
        /// </summary>
        private async Task CreateOrJoinSession()
        {
            if(m_CurrentSession != null) return;
            
            var options = new SessionOptions()
            {
                Name = m_SessionName,
                MaxPlayers = m_MaxPlayers
            }.WithDistributedAuthorityNetwork();
            
            NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
            NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
            
            InitializePresentationMode -= OnInitializePresentationMode;
            InitializePresentationMode += OnInitializePresentationMode;
            
            JoinPresentation -= OnJoinPresentation;
            JoinPresentation += OnJoinPresentation;
            
            EndPresentation -= OnEndPresentation;
            EndPresentation += OnEndPresentation;
            
            OnClientConnected -= OnAddClientGameObject;
            OnClientConnected += OnAddClientGameObject;
            
            OnClientDisconnected -= OnRemoveClientGameObject;
            OnClientDisconnected += OnRemoveClientGameObject;
            
            try
            {
                MultiplayerService.Instance.SessionAdded -= InstanceOnSessionAdded;
                MultiplayerService.Instance.SessionAdded += InstanceOnSessionAdded;
                await MultiplayerService.Instance.CreateOrJoinSessionAsync(m_SessionName, options);
            } catch (Exception e)
            {
                OnSessionJoinedFailed.Invoke(e.Message);
                Debug.LogError("Failed to create or join session: " + e.Message);
            }
            
            return;
            
            void InstanceOnSessionAdded(ISession session)
            {
                MultiplayerService.Instance.SessionAdded -= InstanceOnSessionAdded;
                
                AssetsController.AssetSelected -= OnAssetSelected;
                AssetsController.AssetSelected += OnAssetSelected;
                
                Debug.Log("Session Added: " + session.Id + " Name: " + session.Name);
                if (string.Equals(session.Name, m_SessionName))
                {
                    m_CurrentSession = session;
                }

                if (NetworkDetector.IsOffline)
                {
                    LeaveSession().Forget();
                }
            }
        }

        private void OnAssetSelected(AssetInfo assetInfo)
        {
            if(m_CurrentSession == null) return;
            var sessionName = StreamingUtils.ReturnHashName(assetInfo.Asset) + StreamingModelController.StreamingAssetVersion;;
            //New session needed leave current session and rejoin
            var syncModelTransform = FindFirstObjectByType<SyncModelTransform>();
            syncModelTransform?.NotifySessionChanged(assetInfo);
            _ = LeaveSessionAndRejoin();
            return;

            async Task LeaveSessionAndRejoin()
            {
                MultiplayerService.Instance.SessionRemoved += InstanceOnSessionRemoved;
                // Wait for a bit to ensure the message is sent
                float elapsed = 0f;
                while (elapsed < 1f)
                {
                    await Task.Yield();
                    elapsed += Time.deltaTime;
                }
                await m_CurrentSession.LeaveAsync();
            }
            
            void InstanceOnSessionRemoved(ISession obj)
            {
                MultiplayerService.Instance.SessionRemoved -= InstanceOnSessionRemoved;
                m_PlayerObjects.Clear();
                m_CurrentSession = null;
                m_SessionName = sessionName;
                _ = CreateOrJoinSession();
            }
        }

        // Handles the end presentation event by checking if the leaver is the presenter.
        private void OnEndPresentation(ulong leaverClientId)
        {
            var playerObject = m_PlayerObjects[leaverClientId];

            if (!playerObject.TryGetComponent(out NetworkPlayerController playerController)) return;
            if (playerController.IsPresenter.Value)
            {
                playerController.EndPresentation();
            }
            else
            {
                foreach (var otherPlayerObject in m_PlayerObjects.Values)
                {
                    if (!otherPlayerObject.TryGetComponent(out playerController)) continue;
                    if (!playerController.IsOwner)
                    {
                        playerController.AvatarMeshControl(true);
                        continue;
                    }
                    playerController.LeavePresentation();
                }
            }
        }
        
        // Handles the join presentation event by checking if the presenter is the presenter.
        private void OnJoinPresentation(ulong presenterClientId)
        {
            if(!m_PlayerObjects.TryGetValue(presenterClientId, out var presenterObject)) return;
            foreach (var playerObject in m_PlayerObjects.Values)
            {
                if (!playerObject.TryGetComponent(out NetworkPlayerController playerController)) continue;
                if (!playerController.IsOwner)
                {
                    playerController.AvatarMeshControl(false);
                    continue;
                }
                
                playerController.JoinPresentation(presenterObject);
            }
        }
        
        private void OnInitializePresentationMode()
        {
            foreach (var playerObject in m_PlayerObjects.Values)
            {
                if (!playerObject.TryGetComponent(out NetworkPlayerController playerController)) continue;
                if(!playerController.IsOwner)continue;
                playerController.InitializePresentationMode();
            }
        }

        // Handles the removal of a client game object by removing the player object from the dictionary.
        private void OnRemoveClientGameObject(ulong key)
        {
            if (!m_PlayerObjects.TryGetValue(key, out var playerObject)) return;
            var controller = playerObject.GetComponent<NetworkPlayerController>();
            if (m_UsedColors != null)
            {
                if (m_UsedColors.Contains(controller.PlayerColor.Value))
                {
                    m_UsedColors.Remove(controller.PlayerColor.Value);
                }
            }
            m_PlayerObjects.Remove(key);
        }

        // Handles the addition of a client game object by adding the player object to the dictionary.
        private void OnAddClientGameObject(ulong key, GameObject playerObject)
        {
            m_PlayerObjects.TryAdd(key, playerObject);
            
            if (NetworkManager.Singleton.LocalClient.IsSessionOwner)
            {
                m_UsedColors ??= new List<Color>();
                Color playerColor = GenerateUniqueColor();
                m_UsedColors.Add(playerColor);
                var playerController = playerObject.GetComponent<NetworkPlayerController>();
                playerController.UpdatePlayerColor(playerColor);
            }
        }

        // Handles network connection events by checking if a client has connected or disconnected.
        private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData eventData)
        {
            if (eventData.EventType == ConnectionEvent.ClientConnected)
            {
                if (!NetworkManager.Singleton.LocalClient.IsSessionOwner || m_TransformNetworkObject != null || m_ModelSyncNetworkObject != null) return;

                var transformController = Instantiate(transformControllerObjectPrefab);
                SceneManager.MoveGameObjectToScene(transformController, gameObject.scene);
                if (transformController.TryGetComponent(out m_TransformNetworkObject))
                {
                    m_TransformNetworkObject.Spawn(true);
                }
                
                var modelSyncObject = Instantiate(modelSyncPrefab);
                if(modelSyncObject.TryGetComponent(out m_ModelSyncNetworkObject))
                {
                    m_ModelSyncNetworkObject.Spawn(true);
                }
            }

            if (eventData.EventType == ConnectionEvent.PeerDisconnected)
            {
                OnClientDisconnected.Invoke(eventData.ClientId);
            }
        }

        // Leaves the current multiplayer session asynchronously.
        private async Task LeaveSession()
        {
            if (m_CurrentSession == null) return;
            try
            {
                MultiplayerService.Instance.SessionRemoved += InstanceOnSessionRemoved;
                await m_CurrentSession.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to leave session: " + e.Message);
            }

            return;
            
            void InstanceOnSessionRemoved(ISession obj)
            {
                MultiplayerService.Instance.SessionRemoved -= InstanceOnSessionRemoved;
                m_PlayerObjects?.Clear();
                m_TransformNetworkObject = null;
                m_ModelSyncNetworkObject = null;
                m_CurrentSession = null;
            }
        }
        
        // Generates a unique color for each player by checking if the color is too similar to any other player's color.
        private Color GenerateUniqueColor()
        {
            Color newColor;
            do
            {
                newColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            } while (IsColorTooSimilar(newColor));

            return newColor;
            
            bool IsColorTooSimilar(Color newColor)
            {
                foreach (var color in m_UsedColors)
                {
                    if (Vector3.Distance(new Vector3(newColor.r, newColor.g, newColor.b), new Vector3(color.r, color.g, color.b)) < 0.5f)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
    
    public static class TaskExtensions
    {
        public static void Forget(this Task task) { }
    }
}
