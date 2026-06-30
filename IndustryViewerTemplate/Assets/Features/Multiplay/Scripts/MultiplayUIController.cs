using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.Core;
using Unity.AppUI.UI;
using Unity.Netcode;
using Avatar = Unity.AppUI.UI.Avatar;
using System.Linq;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Streaming;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Assets;
using Unity.Services.Multiplayer;

namespace Unity.Industry.Viewer.Multiplay
{
    // This script manages the UI for multiplayer sessions in Unity.
    // It handles the display and interaction of player avatars and presentation mode controls.
    // The script updates the UI based on player connection events, color changes, and name changes.
    // It includes event handlers for button clicks and modal dialogs for presentation mode actions.
    // The script integrates with the MultiplayController to manage session-related UI updates and interactions.
    public class MultiplayUIController : MonoBehaviour
    {
        // Constants for UI elements and classes
        protected const string k_AvatarName = "IdentityAvatar";
        private const string k_TopRightBarName = "TopRightBar";
        private const string k_MultiplayIconClass = "MultiplayIcon";
        
        // Variables for UI elements and styles
        private UIDocument m_UIDocument => SharedUIManager.Instance.AssetsUIDocument;
        protected IconButton m_PresentationModeButton;
        protected Dictionary<ulong, Avatar> m_PlayerAvatars;
        protected Avatar m_MyAvatar;
        private const float DoubleClickTime = 0.3f;
        private float m_LastClickTime;
        private IEventHandler m_LastClickedElement;

        [SerializeField]
        protected StyleSheet m_MultiplayStyleSheet;
        
        Modal m_PresentationModal;
        protected bool isModalOpened;
        
        protected Color m_OriginalAvatarColor;
        
        private VisualElement m_Divider;

        #region Localisation

        [SerializeField]
        protected LocalizedString m_Toast_SessionFullLocalizedString;

        [SerializeField]
        protected LocalizedString m_Toast_SessionJoinFailedLocalizedString;

        [SerializeField]
        protected LocalizedString m_JoinPresentationTitleLocalizedString;

        [SerializeField]
        protected LocalizedString m_JoinPresentationDescriptionLocalizedString;

        [SerializeField]
        protected LocalizedString m_JoinLocalizedString;

        [SerializeField]
        protected LocalizedString m_DismissLocalizedString;

        [SerializeField]
        private LocalizedString m_PresentationModeLocalizedString;

        [SerializeField]
        private LocalizedString m_EndPresentationTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_EndPresentationDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_EndLocalizedString;

        [SerializeField]
        private LocalizedString m_LeavePresentationTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_LeavePresentationDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_LeaveLocalizedString;

        [SerializeField]
        private LocalizedString m_AskToJoinPresentationTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_AskToJoinPresentationDescriptionLocalizedString;

        [SerializeField]
        protected LocalizedString m_StartPresentationTitleLocalizedString;

        [SerializeField]
        protected LocalizedString m_StartPresentationDescriptionLocalizedString;

        [SerializeField]
        protected LocalizedString m_StartLocalizedString;

        [SerializeField]
        protected LocalizedString m_CancelLocalizedString;
        
        [SerializeField]
        protected LocalizedString m_JoinNewLayoutSessionTitleLocalizedString;
        
        [SerializeField]
        protected LocalizedString m_JoinNewLayoutSessionDescriptionLocalizedString;

        #endregion
        
        // initialization of the UI elements and event handlers
        protected virtual void Start()
        {
            m_PlayerAvatars ??= new Dictionary<ulong, Avatar>();
            MultiplayController.AskToJoinLayout += OnAskToJoinLayout;
            MultiplayController.OnClientConnected += OnClientConnected;
            MultiplayController.OnClientDisconnected += OnClientDisconnected;
            MultiplayController.RequestToJoinPresentation += OnRequestToJoinPresentation;
            MultiplayController.OnSessionJoinedFailed += OnSessionJoinedFailed;
            NetworkPlayerController.OnColorChanged += OnColorChanged;
            NetworkPlayerController.OnNameChanged += OnNameChanged;
            NetworkManager.Singleton.OnClientStopped += OnClientStopped;
            StartCoroutine(InitializeEvent());
            InitializeUI();
            
            return;
            
            IEnumerator InitializeEvent()
            {
                while (MultiplayerService.Instance == null)
                {
                    yield return null;
                }
                MultiplayerService.Instance.SessionAdded += OnSessionAdded;
                MultiplayerService.Instance.SessionRemoved += OnSessionRemoved;
            }
        }

        // cleanup of event handlers and UI elements
        protected virtual void OnDestroy()
        {
            // NetworkManager.Singleton can already be destroyed during teardown (guarded the same
            // way elsewhere, e.g. MultiplayController); dereferencing it unguarded throws and skips
            // the remaining cleanup below.
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
            }
            NetworkPlayerController.OnColorChanged -= OnColorChanged;
            NetworkPlayerController.OnNameChanged -= OnNameChanged;
            MultiplayController.AskToJoinLayout -= OnAskToJoinLayout;
            MultiplayController.RequestToJoinPresentation -= OnRequestToJoinPresentation;
            MultiplayController.OnClientConnected -= OnClientConnected;
            MultiplayController.OnClientDisconnected -= OnClientDisconnected;
            MultiplayController.OnSessionJoinedFailed -= OnSessionJoinedFailed;
            if (MultiplayerService.Instance != null)
            {
                MultiplayerService.Instance.SessionAdded -= OnSessionAdded;
                MultiplayerService.Instance.SessionRemoved -= OnSessionRemoved;
            }
            if (m_PresentationModeButton != null)
            {
                m_PresentationModeButton.clicked -= OnPresentationModeButtonClicked;
                m_PresentationModeButton.RemoveFromHierarchy();
            }
            if (m_MyAvatar != null)
            {
                m_MyAvatar.backgroundColor = new Optional<Color>(m_OriginalAvatarColor);
            }
            
            m_Divider?.RemoveFromHierarchy();
            m_Divider = null;

            if (m_PlayerAvatars != null)
            {
                foreach (var playerAvatar in m_PlayerAvatars.Values)
                {
                    if(playerAvatar == m_MyAvatar) continue;
                    playerAvatar.RemoveFromHierarchy();
                }
            }
            
            m_PlayerAvatars?.Clear();
            
            if(m_UIDocument == null) return;
            
            m_UIDocument.rootVisualElement.RemoveStyleSheetIfPresent(m_MultiplayStyleSheet);
        }

        private void OnSessionRemoved(ISession obj)
        {
            if(m_PresentationModeButton == null) return;
            m_PresentationModeButton.style.display = DisplayStyle.None;
        }

        private void OnSessionAdded(ISession obj)
        {
            if(m_PresentationModeButton == null) return;
            StartCoroutine(WaitForInit());
            
            return;

            IEnumerator WaitForInit()
            {
                //Wait for 1 second to make sure the session is fully initialized
                yield return new WaitForSeconds(1f);
                m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        protected virtual async void OnAskToJoinLayout(AssetInfo assetInfo)
        {
            var askToChangeDialog = new AlertDialog()
            {
                title = await m_JoinNewLayoutSessionTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await m_JoinNewLayoutSessionDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Default
            };
            
            askToChangeDialog.SetPrimaryAction(99, await m_JoinLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                AssetsController.AssetSelected?.Invoke(assetInfo);
            });
            
            askToChangeDialog.SetCancelAction(0, await m_CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());
            
            var modal = Modal.Build(SharedUIManager.Instance.AssetsContainer, askToChangeDialog);

            modal.Show();
        }

        protected virtual void OnClientStopped(bool obj)
        {
            foreach (var mPlayerAvatar in m_PlayerAvatars.Keys)
            {
                if(m_PlayerAvatars[mPlayerAvatar] == m_MyAvatar) continue;
                m_PlayerAvatars[mPlayerAvatar].RemoveFromHierarchy();
            }
            
            if (m_MyAvatar != null)
            {
                m_MyAvatar.backgroundColor = new Optional<Color>(m_OriginalAvatarColor);
            }
            m_Divider?.RemoveFromHierarchy();
            m_Divider = null;
            m_PlayerAvatars?.Clear();
        }

        // event handler for session join failure
        protected virtual async void OnSessionJoinedFailed(string message)
        {
            var textMessage = message.Contains("lobby is full") ? await m_Toast_SessionFullLocalizedString.GetTitleLocalizedStringForAppUIAsync() : await m_Toast_SessionJoinFailedLocalizedString.GetTitleLocalizedStringForAppUIAsync();

            var toast = Toast.Build(m_MyAvatar, textMessage, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);

            toast.Show();
        }

        // event handler for request to join presentation
        private void OnRequestToJoinPresentation()
        {
            foreach (var avater in m_PlayerAvatars.Values)
            {
                NetworkPlayerController playerController = (NetworkPlayerController) avater.userData;
                if (!playerController.IsPresenter.Value) continue;
                ShowRequestToJoinDialog(playerController, JoinAction);
                return;
                
                void JoinAction()
                {
                    //Make sure the player is still the presenter, there is a case that presenter might have end
                    //presentation before the client joined
                    if (!playerController.IsPresenter.Value) return;
                    MultiplayController.JoinPresentation?.Invoke(playerController.OwnerClientId);
                }
            }
        }

        protected virtual async void ShowRequestToJoinDialog(NetworkPlayerController playerController, Action joinAction)
        {
            m_PresentationModal?.Dismiss();
            
            var requestToJoinDialog = new AlertDialog()
            {
                title = await m_JoinPresentationTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await m_JoinPresentationDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Default
            };

            var descriptionLabel = requestToJoinDialog.Q<LocalizedTextElement>("appui-dialog__content");
            descriptionLabel.variables = new object[]
            {
                new Dictionary<string, object>()
                {
                    {"name", playerController.PlayerName.Value.Value}
                }
            };
                    
            requestToJoinDialog.SetPrimaryAction(98, await m_JoinLocalizedString.GetTitleLocalizedStringForAppUIAsync(), joinAction);
                    
            requestToJoinDialog.SetCancelAction(0, await m_CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());
                    
            m_PresentationModal = Modal.Build(m_PresentationModeButton, requestToJoinDialog);
            m_PresentationModal.shown += OnModalShown;

            m_PresentationModal.Show();
        }

        // event handler for client disconnection
        protected virtual void OnClientDisconnected(ulong id)
        {
            if (!m_PlayerAvatars.TryGetValue(id, out var avatar)) return;
            avatar.UnregisterCallback<ClickEvent>(OnAvatarIconClick);
            avatar.RemoveFromHierarchy();
            m_PlayerAvatars.Remove(id);
            if (m_PresentationModeButton != null)
            {
                m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if(m_PlayerAvatars.Count <= 1)
            {
                m_Divider?.RemoveFromHierarchy();
                m_Divider = null;
            }
        }

        // event handler for color change
        private void OnColorChanged(ulong id, Color color)
        {
            Avatar avatar;
            if (m_PlayerAvatars.TryGetValue(id, out avatar))
            {
                avatar.backgroundColor = new Optional<Color>(color);
            }
            else
            {
                if(NetworkManager.Singleton.LocalClientId == id)
                {
                    m_MyAvatar.backgroundColor = new Optional<Color>(color);
                    m_PlayerAvatars.Add(id, m_MyAvatar);
                    m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                }
                else
                {
                    avatar = CreateAvatar(id, null);
                    var avatarParent = m_MyAvatar.parent;
                    avatarParent.Insert(avatarParent.childCount, avatar);
                    avatar.backgroundColor = new Optional<Color>(color);
                }
            }
        }
        
        // event handler for name change
        protected virtual void OnNameChanged(ulong id, string username)
        {
            if (m_PlayerAvatars.TryGetValue(id, out var avatar))
            {
                avatar.Q<Text>().text = IdentityController.GetInitials(username);
                avatar.tooltip = username;
            }
            else
            {
                if (NetworkManager.Singleton.LocalClientId == id)
                {
                    m_PlayerAvatars.Add(id, m_MyAvatar);
                    m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                }
                else
                {
                    avatar = CreateAvatar(id, null);
                    var avatarParent = m_MyAvatar.parent;
                    avatarParent.Insert(avatarParent.childCount, avatar);
                    avatar.tooltip = username;
                }
                
                avatar.Q<Text>().text = IdentityController.GetInitials(username);
            }
        }

        // event handler for client connection
        protected virtual void OnClientConnected(ulong id, GameObject playerObject)
        {
            if(m_MyAvatar == null) return;
            
            var networkPlayerController = playerObject.GetComponent<NetworkPlayerController>();
            
            if (m_PlayerAvatars.TryGetValue(id, out var playerAvatar))
            {
                playerAvatar.userData = networkPlayerController;
                UpdateAvatarUI(playerObject);
                return;
            }

            if (NetworkManager.Singleton.LocalClientId == id)
            {
                m_MyAvatar.userData = networkPlayerController;
                m_PlayerAvatars.Add(id, m_MyAvatar);
            }
            else
            {
                AddOtherClientAvatar(id, networkPlayerController);
            }

            if (m_PresentationModeButton != null)
            {
                m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            UpdateAvatarUI(playerObject);
        }

        protected virtual void AddOtherClientAvatar(ulong id, NetworkPlayerController playerObject)
        {
            var avatar = CreateAvatar(id, playerObject);
            var avatarParent = m_MyAvatar.parent;
            if (m_Divider == null)
            {
                m_Divider = new VisualElement();
                m_Divider.AddToClassList("AvatarDivider");
                avatarParent.Add(m_Divider);
            }
            avatarParent.Add(avatar);
        }
        
        // update the UI for the player avatar
        private void UpdateAvatarUI(GameObject playerObject)
        {
            var playerController = playerObject.GetComponent<NetworkPlayerController>();
            if (playerController == null) return;
            var avatar = m_PlayerAvatars[playerController.OwnerClientId];
            var username = playerController.PlayerName.Value.ToString();
            if (!string.IsNullOrEmpty(username))
            {
                avatar.tooltip = username;
                avatar.Q<Text>().text = IdentityController.GetInitials(username);
                playerController.UpdatePlayerNameLabel(username);
            }
            
            var color = playerController.PlayerColor.Value;

            if (color != default)
            {
                avatar.backgroundColor = new Optional<Color>(color);
                playerController.UpdatePlayerMeshColor(color);
            }
            
            InitialInteraction(playerController, avatar);
        }

        // initial interaction for the player avatar
        private void InitialInteraction(NetworkPlayerController player, Avatar avatar)
        {
            if(player.firstInitialization) return;
            if (player.IsOwner) return;
            player.firstInitialization = true;
            avatar.RegisterCallback<ClickEvent>(OnAvatarIconClick);
        }
        
        // event handler for avatar icon click
        private void OnAvatarIconClick(ClickEvent evt)
        {
            var currentTime = Time.time;
            if (m_LastClickedElement != null && m_LastClickedElement == evt.target && currentTime - m_LastClickTime < DoubleClickTime)
            {
                NetworkPlayerController playerObject = (evt.target as VisualElement)?.userData as NetworkPlayerController;
                if (playerObject == null)
                {
                    return;
                }
                
                Vector3 targetPosition = playerObject.transform.position;
                
                if (playerObject.IsInVR.Value && playerObject.EyeLevelInVR.Value > 0)
                {
                    targetPosition.y -= playerObject.EyeLevelInVR.Value;
                }
                NavigationController.PlayerTranslateTo?.Invoke(targetPosition, playerObject.transform.rotation);
            }
            m_LastClickTime = currentTime;
            m_LastClickedElement = evt.target;
        }

        // create the player avatar
        protected Avatar CreateAvatar(ulong id, NetworkPlayerController playerObject)
        {
            var avatar = new Avatar
            {
                userData = playerObject,
                size = Size.L,
                variant = AvatarVariant.Circular
            };
            
            m_PlayerAvatars.Add(id, avatar);

            avatar.AddToClassList(k_MultiplayIconClass);
            
            var avatarNameLabel = new Text
            {
                style =
                {
                    color = new StyleColor(Color.white)
                },
                pickingMode = PickingMode.Ignore
            };

            avatar.Add(avatarNameLabel);
            
            return avatar;
        }

        protected virtual async void InitializeUI()
        {
            m_UIDocument.rootVisualElement.AddStyleSheetIfMissing(m_MultiplayStyleSheet);
            
            var topRightBar = m_UIDocument.rootVisualElement.Q<VisualElement>(k_TopRightBarName);
            
            m_MyAvatar = topRightBar.Q<Avatar>(k_AvatarName);
            m_OriginalAvatarColor = m_MyAvatar.backgroundColor.Value;
            m_PresentationModeButton = new IconButton()
            {
                name = "PresentationModeButton",
                icon = "presentation"
            };
            
            m_PresentationModeButton.AddToClassList(k_MultiplayIconClass);
            
            m_PresentationModeButton.Children().First().style.display = DisplayStyle.Flex;

            m_PresentationModeButton.clicked += OnPresentationModeButtonClicked;
            
            int avatarIndex = m_MyAvatar.parent.IndexOf(m_MyAvatar);
            m_MyAvatar.parent.Insert(avatarIndex, m_PresentationModeButton);
            m_PresentationModeButton.style.display = DisplayStyle.None;
            
            m_PresentationModeButton.tooltip = await 
                m_PresentationModeLocalizedString.GetTitleLocalizedStringForAppUIAsync();
        }

        // event handler for presentation mode button click
        protected virtual void OnPresentationModeButtonClicked()
        {
            m_PresentationModal?.Dismiss();
            
            bool isPresenting = false;
            NetworkPlayerController myOwnPlayerObject = null;
            NetworkPlayerController presenter = null;
            foreach (var playerAvatar in m_PlayerAvatars.Values)
            {
                if (playerAvatar.userData == null)
                {
                    Debug.Log("Player avatar user data is null ");
                    continue;
                }
                
                NetworkPlayerController playerController = (NetworkPlayerController) playerAvatar.userData;
                
                if (playerController == null)
                {
                    Debug.Log("Player controller is null ");
                    continue;
                }
                if (playerController.IsPresenter.Value)
                {
                    presenter = playerController;
                    isPresenting = true;
                }

                if (playerController.IsOwner)
                {
                    myOwnPlayerObject = playerController;
                }
            }

            if (myOwnPlayerObject == null)
            {
                Debug.Log("My own player object is null");
                return;
            }
            
            if (isPresenting)
            {
                //If there is an ongoing presentation
                LocalizedString title = null; 
                LocalizedString description = null;
                LocalizedString primaryButtonText = null;
                Action primaryAction = null;
                
                if (myOwnPlayerObject.IsPresenter.Value)
                {
                    //End presentation mode
                    title = m_EndPresentationTitleLocalizedString;
                    description = m_EndPresentationDescriptionLocalizedString;
                    primaryButtonText = m_EndLocalizedString;
                    primaryAction = () => MultiplayController.EndPresentation?.Invoke(myOwnPlayerObject.OwnerClientId);
                } else if (myOwnPlayerObject.InPresentation.Value)
                {
                    //Leave presentation mode
                    title = m_LeavePresentationTitleLocalizedString;
                    description = m_LeavePresentationDescriptionLocalizedString;
                    primaryButtonText = m_LeaveLocalizedString;
                    primaryAction = () => MultiplayController.EndPresentation?.Invoke(myOwnPlayerObject.OwnerClientId);
                }
                else
                {
                    //Ask to join presentation mode
                    title = m_AskToJoinPresentationTitleLocalizedString;
                    description = m_AskToJoinPresentationDescriptionLocalizedString;
                    primaryButtonText = m_JoinLocalizedString;
                    primaryAction = () => MultiplayController.JoinPresentation?.Invoke(presenter.OwnerClientId);
                }
                
                ShowPresentationDialog(myOwnPlayerObject, presenter, title, description, primaryButtonText, primaryAction);
                return;
            }
            
            //Initial presentation
            InitializePresentationDialog();
        }

        protected virtual async void InitializePresentationDialog()
        {
            var presentationModeDialog = new AlertDialog()
            {
                title = await m_StartPresentationTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await m_StartPresentationDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Default
            };
            
            presentationModeDialog.SetPrimaryAction(96, await m_StartLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                () =>
                {
                    MultiplayController.InitializePresentationMode?.Invoke();
                });
            
            presentationModeDialog.SetCancelAction(0, await m_CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());
            
            m_PresentationModal = Modal.Build(m_PresentationModeButton, presentationModeDialog);
            m_PresentationModal.shown += OnModalShown;
            
            m_PresentationModal.Show();
        }

        protected virtual async void ShowPresentationDialog(NetworkPlayerController myOwnPlayerObject, 
            NetworkPlayerController presenter, LocalizedString title, LocalizedString description, 
            LocalizedString primaryButtonText, Action primaryAction)
        {
            AlertDialog newDialog = null;
                
            newDialog = new AlertDialog()
            {
                title = await title.GetTitleLocalizedStringForAppUIAsync(),
                description = await description.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Default
            };
            
            
            newDialog.SetPrimaryAction(97, await primaryButtonText.GetTitleLocalizedStringForAppUIAsync(), primaryAction);
                
            newDialog.SetCancelAction(0,  await m_DismissLocalizedString.GetTitleLocalizedStringForAppUIAsync());
                
            m_PresentationModal = Modal.Build(m_PresentationModeButton, newDialog);
            m_PresentationModal.shown += OnModalShown;
            m_PresentationModal.Show();
        }

        private void OnModalShown(Modal obj)
        {
            m_PresentationModal.shown -= OnModalShown;
            isModalOpened = true;
            m_PresentationModal.dismissed += M_PresentationModalOnDismissed;
        }

        private void M_PresentationModalOnDismissed(Modal arg1, DismissType arg2)
        {
            m_PresentationModal.dismissed -= M_PresentationModalOnDismissed;
            isModalOpened = false;
        }
    }
}
