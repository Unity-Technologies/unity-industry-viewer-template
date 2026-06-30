using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Streaming;
using System.Threading.Tasks;
using Unity.Industry.Viewer.Assets;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Vivox;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Unity.Industry.Viewer.Vivox
{
    // This script manages the integration of Vivox voice services in a Unity project.
    // It handles the initialization, login, and joining of Vivox channels.
    // The script manages participant events such as joining, leaving, and audio energy changes.
    // It includes platform-specific handling for Android permissions and Unity Editor testing.
    // The script integrates with Unity's MonoBehaviour for lifecycle management and asynchronous operations.
    public class VivoxController : MonoBehaviour
    {
        // Actions to notify when a channel is joined or left, and when participant audio energy changes
        public static Action ChannelJoined;
        public static Action ChannelLeft;
        public static Action<double> ParticipantAudioEnergyChanged;
        public bool IsInStreaming;
        private string m_SessionName;

#if UNITY_EDITOR
        [SerializeField]
        private bool m_LocalTestMode = false; // Flag for local test mode in the editor
#endif

        private VivoxParticipant m_LocalParticipant;

#if UNITY_ANDROID
        private PermissionCallbacks callbacks; // Callbacks for Android permissions
#endif

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        IEnumerator Start()
        {
            IsInStreaming = true;
            while (MultiplayerService.Instance == null)
            {
                yield return null;
            }
            MultiplayerService.Instance.SessionAdded += InstanceOnSessionAdded;
            MultiplayerService.Instance.SessionRemoved += InstanceOnSessionRemoved;
        }

        // Called when the MonoBehaviour is destroyed
        private void OnDestroy()
        {
            IsInStreaming = false;
            if (MultiplayerService.Instance != null)
            {
                MultiplayerService.Instance.SessionAdded -= InstanceOnSessionAdded;
                MultiplayerService.Instance.SessionRemoved -= InstanceOnSessionRemoved;
            }
            AssetsController.AssetSelected -= OnAssetSelected;
            // Unsubscribe from Vivox events
            if (VivoxService.Instance != null)
            {
                VivoxService.Instance.ChannelJoined -= OnChannelJoined;
                VivoxService.Instance.ChannelLeft -= OnChannelLeft;
                VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAddedToChannel;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemovedFromChannel;
            }
            if (m_LocalParticipant != null)
            {
                m_LocalParticipant.ParticipantAudioEnergyChanged -= OnParticipantAudioEnergyChanged;
            }

#if UNITY_ANDROID
            if (callbacks != null)
            {
                callbacks.PermissionGranted -= OnPermissionAccepted;
            }
#endif
            // Leave the session
            LeaveSession().Forget();
        }
        
        private void InstanceOnSessionRemoved(ISession obj)
        {
            _ = LeaveSession();
        }

        private void InstanceOnSessionAdded(ISession obj)
        {
#if UNITY_ANDROID
            if(Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                _ = InitializeService();
            }
            else
            {
                callbacks ??= new PermissionCallbacks();
                callbacks.PermissionGranted += OnPermissionAccepted;
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
            }
#elif UNITY_STANDALONE_OSX
            StartCoroutine(CheckPermission(() =>
            {
                _ = InitializeService();
            }));
#else
            _ = InitializeService();
#endif
        }

        // Called when the client starts
        private void OnClientStarted()
        {

        }
        

#if UNITY_STANDALONE_OSX
        IEnumerator CheckPermission(Action callback)
        {
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }
            
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) yield break;
            
            callback?.Invoke();
        }
#endif

#if UNITY_ANDROID
        // Called when the microphone permission is accepted
        private void OnPermissionAccepted(string featureName)
        {
#if VR_MODE
            NavigationController.RequestDefaultHomeView?.Invoke();
#endif
            if (string.Equals(featureName, Permission.Microphone))
            {
                _ = InitializeService();
            }
        }
#endif
        
        private void OnClientStopped(bool obj)
        {
            _ = LeaveSession();
        }

        // Leaves the current session
        private async Task LeaveSession()
        {
            if(string.IsNullOrEmpty(m_SessionName)) return;
            VivoxService.Instance.ChannelLeft += LocalChannelLeft;
            await VivoxService.Instance.LeaveAllChannelsAsync();
            m_SessionName = string.Empty;
            
            return;
            
            void LocalChannelLeft(string obj)
            {
                VivoxService.Instance.ChannelLeft -= LocalChannelLeft;
                _ = Logout();
            }

            async Task Logout()
            {
                await VivoxService.Instance.LogoutAsync();
            }
        }

        // Initializes the Vivox service
        private async Task InitializeService()
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

            await VivoxService.Instance.InitializeAsync();

            if (!VivoxService.Instance.IsLoggedIn)
            {
                var options = new LoginOptions
                {
                    DisplayName = AuthenticationService.Instance.PlayerId,
                    EnableTTS = false
                };
                VivoxService.Instance.LoggedIn += InstanceOnLoggedIn;
                await VivoxService.Instance.LoginAsync(options);
                return;
            }

            _ = InitializeChannel();
            return;
            
            void InstanceOnLoggedIn()
            {
                VivoxService.Instance.LoggedIn -= InstanceOnLoggedIn;
                _ = InitializeChannel();
            }
        }
        
        private void OnAssetSelected(AssetInfo assetInfo)
        {
            if(string.IsNullOrEmpty(m_SessionName)) return;
            var sessionName = StreamingUtils.ReturnHashName(assetInfo.Asset);
            if(string.Equals(sessionName, m_SessionName)) return;
            _ = LeaveSession();
        }

        private async Task InitializeChannel()
        {
            if(!IsInStreaming) return;
            if (VivoxService.Instance.ActiveChannels.Count > 0)
            {
                VivoxService.Instance.ChannelLeft += LocalChannelLeft;
                await VivoxService.Instance.LeaveChannelAsync(VivoxService.Instance.ActiveChannels.Keys.First());
                return;
            }
            
            // Subscribe to Vivox events
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAddedToChannel;
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAddedToChannel;
            
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemovedFromChannel;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemovedFromChannel;
            
            VivoxService.Instance.ChannelJoined -= OnChannelJoined;
            VivoxService.Instance.ChannelJoined += OnChannelJoined;
            
            VivoxService.Instance.ChannelLeft -= OnChannelLeft;
            VivoxService.Instance.ChannelLeft += OnChannelLeft;

            m_SessionName = StreamingUtils.ReturnHashName(StreamingModelController.StreamingAsset.Value.Asset) + StreamingModelController.StreamingAssetVersion;

#if UNITY_EDITOR
            if (m_LocalTestMode)
            {
                await VivoxService.Instance.JoinEchoChannelAsync(m_SessionName, ChatCapability.AudioOnly,
                    new ChannelOptions()
                    {
                        MakeActiveChannelUponJoining = true
                    });
            }
            else
            {
                await VivoxService.Instance.JoinGroupChannelAsync(m_SessionName, ChatCapability.AudioOnly,
                    new ChannelOptions()
                    {
                        MakeActiveChannelUponJoining = true
                    });
            }
#else
            await VivoxService.Instance.JoinGroupChannelAsync(m_SessionName, ChatCapability.AudioOnly,
                    new ChannelOptions()
                    {
                        MakeActiveChannelUponJoining = true
                    });
#endif
            
            void LocalChannelLeft(string obj)
            {
                VivoxService.Instance.ChannelLeft -= LocalChannelLeft;
                _ = InitializeChannel();
            }
        }

        // Called when a participant is removed from the channel
        private void OnParticipantRemovedFromChannel(VivoxParticipant participant)
        {
            if (!participant.IsSelf) return;
            m_LocalParticipant.ParticipantAudioEnergyChanged -= OnParticipantAudioEnergyChanged;
            m_LocalParticipant = null;
        }

        // Called when a participant is added to the channel
        private void OnParticipantAddedToChannel(VivoxParticipant participant)
        {
            if (!participant.IsSelf) return;
            m_LocalParticipant = participant;
            m_LocalParticipant.ParticipantAudioEnergyChanged += OnParticipantAudioEnergyChanged;
        }

        // Called when the participant's audio energy changes
        private void OnParticipantAudioEnergyChanged()
        {
            ParticipantAudioEnergyChanged?.Invoke(m_LocalParticipant.AudioEnergy);
        }

        // Called when a channel is left
        private void OnChannelLeft(string obj)
        {
            if (VivoxService.Instance.ActiveChannels.Count > 0) return;
            ChannelLeft?.Invoke();
        }

        // Called when a channel is joined
        private void OnChannelJoined(string obj)
        {
            if (NetworkDetector.RequestedOfflineMode || !IsInStreaming)
            {
                _ = LeaveSession();
                return;
            }
            
            AssetsController.AssetSelected -= OnAssetSelected;
            AssetsController.AssetSelected += OnAssetSelected;
            ChannelJoined?.Invoke();
        }
    }

    // Extension method to handle tasks without awaiting them
    public static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            task.ContinueWith(t => Debug.LogException(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}