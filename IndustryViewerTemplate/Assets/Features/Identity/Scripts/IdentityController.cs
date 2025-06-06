using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Cloud.Identity;
using Unity.Cloud.Common;
using Unity.Industry.Viewer.Shared;

namespace Unity.Industry.Viewer.Identity
{
    // This script manages user authentication and identity in Unity.
    // It handles login, logout, and guest login processes using a composite authenticator.
    // The script updates and provides user information, and manages authentication state changes.
    // It includes offline mode handling and event triggers for authentication state and user info updates.
    [DefaultExecutionOrder(-200)]
    public class IdentityController : MonoBehaviour
    {
        public static bool GuestMode;
        public static IUserInfo UserInfo => m_UserInfo;
        private static IUserInfo m_UserInfo;
        public static event Action<AuthenticationState> AuthenticationStateChangedEvent;
        public static event Action<IUserInfo> UserInfoUpdatedEvent; 
        public static Action TriggerLogin;
        public static Action TriggerGuestLogin;
        public static Action<bool> TriggerLogout;
        public static Action TriggerCancelLogin;
        
        ICompositeAuthenticator m_CompositeAuthenticator;
        IUserInfoProvider m_UserInfoProvider => m_CompositeAuthenticator;
        
        bool m_IsInitialized;
        
        void Awake()
        {
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
        }

        private void OnNetworkStatusChanged(bool connected)
        {
            if(m_IsInitialized) return;
            m_IsInitialized = true;
            
            if (!connected)
            {
                return;
            }
            
            var platformService = FindAnyObjectByType<PlatformServicesInitialization>();
            platformService?.StartServices();
            
            if(platformService == null) return;
            
            //Subscribe to events
            m_CompositeAuthenticator = PlatformServices.CompositeAuthenticator;
            m_CompositeAuthenticator.AuthenticationStateChanged += OnAuthenticationStateChanged;
            TriggerLogin += OnTriggerLogin;
            TriggerLogout += OnTriggerLogout;
            TriggerCancelLogin += OnTriggerCancelLogin;
            TriggerGuestLogin += OnTriggerGuestLogin;
            
            AuthenticationStateChangedEvent?.Invoke(m_CompositeAuthenticator.AuthenticationState);
        }

        void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            if(NetworkDetector.IsOffline) return;
            m_CompositeAuthenticator.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            TriggerLogin -= OnTriggerLogin;
            TriggerLogout -= OnTriggerLogout;
            TriggerCancelLogin -= OnTriggerCancelLogin;
            TriggerGuestLogin -= OnTriggerGuestLogin;
        }

        private void OnTriggerGuestLogin()
        {
            GuestMode = true;
            OnAuthenticationStateChanged(AuthenticationState.LoggedIn);
        }

        private void OnTriggerCancelLogin()
        {
            try
            {
                m_CompositeAuthenticator.CancelLogin();
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException)
                {
                    Debug.LogError(ex.Message);
                }
                throw;
            }
        }

        private void OnTriggerLogin()
        {
            try
            {
                GuestMode = false;
                m_CompositeAuthenticator.LoginAsync();
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException
                    or AuthenticationFailedException)
                {
                    Debug.LogError(ex.Message);
                }
                throw;
            }
        }
        
        private void OnTriggerLogout(bool clearCache)
        {
            try
            {
                if (GuestMode)
                {
                    OnAuthenticationStateChanged(AuthenticationState.LoggedOut);
                    return;
                }
                m_CompositeAuthenticator.LogoutAsync(clearCache);
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException
                    or AuthenticationFailedException)
                {
                    Debug.LogError(ex.Message);
                }
                throw;
            }
        }

        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            AuthenticationStateChangedEvent?.Invoke(state);
            if(state == AuthenticationState.LoggedIn)
            {
                _ = UpdateUserInfo();
            }
            else
            {
                GuestMode = false;
                m_UserInfo = null;
            }
        }

        async Task UpdateUserInfo()
        {
            if (GuestMode)
            {
                UserInfoUpdatedEvent?.Invoke(null);
                return;
            }
            
            m_UserInfo = await m_UserInfoProvider.GetUserInfoAsync();
            UserInfoUpdatedEvent?.Invoke(m_UserInfo);
        }
        
        public static string GetInitials(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return string.Empty;
            }

            var nameParts = fullName.Split(' ');
            if (nameParts.Length < 2)
            {
                return nameParts[0][0].ToString().ToUpper();
            }

            return $"{nameParts[0][0].ToString().ToUpper()}{nameParts[^1][0].ToString().ToUpper()}";
        }
    }
}
