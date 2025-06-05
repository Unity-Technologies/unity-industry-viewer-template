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
using UnityEngine.Localization.SmartFormat.PersistentVariables;
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
        private const string k_AvatarName = "IdentityAvatar";
        private const string k_TopRightBarName = "TopRightBar";
        private const string k_MultiplayIconClass = "MultiplayIcon";
        
        // Variables for UI elements and styles
        private UIDocument m_UIDocument => SharedUIManager.Instance.AssetsUIDocument;
        private IconButton m_PresentationModeButton;
        private Dictionary<ulong, Avatar> m_PlayerAvatars;
        private Avatar m_MyAvatar;
        private const float DoubleClickTime = 0.3f;
        private float m_LastClickTime;
        private IEventHandler m_LastClickedElement;

        [SerializeField]
        private StyleSheet m_MultiplayStyleSheet;
        
        Modal m_PresentationModal;
        private bool isModalOpened;
        
        private Color m_OriginalAvatarColor;

        #region Localisation

        [SerializeField]
        private LocalizedString m_Toast_SessionFullLocalizedString;

        [SerializeField]
        private LocalizedString m_Toast_SessionJoinFailedLocalizedString;

        [SerializeField]
        private LocalizedString m_JoinPresentationTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_JoinPresentationDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_JoinLocalizedString;

        [SerializeField]
        private LocalizedString m_DismissLocalizedString;

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
        private LocalizedString m_StartPresentationTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_StartPresentationDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_StartLocalizedString;

        [SerializeField]
        private LocalizedString m_CancelLocalizedString;
        
        [SerializeField]
        private LocalizedString m_JoinNewLayoutSessionTitleLocalizedString;
        
        [SerializeField]
        private LocalizedString m_JoinNewLayoutSessionDescriptionLocalizedString;

        #endregion
        
        // initialization of the UI elements and event handlers
        void Start()
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
        private void OnDestroy()
        {
            NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
            NetworkPlayerController.OnColorChanged -= OnColorChanged;
            NetworkPlayerController.OnNameChanged -= OnNameChanged;
            MultiplayController.AskToJoinLayout -= OnAskToJoinLayout;
            MultiplayController.RequestToJoinPresentation -= OnRequestToJoinPresentation;
            MultiplayController.OnClientConnected -= OnClientConnected;
            MultiplayController.OnClientDisconnected -= OnClientDisconnected;
            MultiplayController.OnSessionJoinedFailed -= OnSessionJoinedFailed;
            MultiplayerService.Instance.SessionAdded -= OnSessionAdded;
            MultiplayerService.Instance.SessionRemoved -= OnSessionRemoved;
            if (m_PresentationModeButton != null)
            {
                m_PresentationModeButton.clicked -= OnPresentationModeButtonClicked;
                m_PresentationModeButton.RemoveFromHierarchy();
            }
            if (m_MyAvatar != null)
            {
                m_MyAvatar.backgroundColor = new Optional<Color>(m_OriginalAvatarColor);
            }

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
            
            if (m_UIDocument.rootVisualElement.styleSheets.Contains(m_MultiplayStyleSheet))
            {
                m_UIDocument.rootVisualElement.styleSheets.Remove(m_MultiplayStyleSheet);
            }
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

        private void OnAskToJoinLayout(AssetInfo assetInfo)
        {
            var askToChangeDialog = new CustomAlertDialog(m_JoinNewLayoutSessionTitleLocalizedString, 
                m_JoinNewLayoutSessionDescriptionLocalizedString)
            {
                title = " ",
                description = string.Empty,
                variant = AlertSemantic.Default
            };
            askToChangeDialog.SetPrimaryAction(m_JoinLocalizedString, true, () =>
            {
                AssetsController.AssetSelected?.Invoke(assetInfo);
            });
            askToChangeDialog.SetCancelAction(m_CancelLocalizedString);
            
            var modal = Modal.Build(SharedUIManager.Instance.AssetsRoot, askToChangeDialog);

            modal.Show();
        }

        private void OnClientStopped(bool obj)
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
            
            m_PlayerAvatars?.Clear();
        }

        // event handler for session join failure
        private void OnSessionJoinedFailed(string message)
        {
            if (message.Contains("lobby is full"))
            {
                var toast = Toast.Build(m_MyAvatar, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
                
                toast.shown += FullToastShown;

                toast.Show();
            } else {
                var toast = Toast.Build(m_MyAvatar, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
                
                toast.shown += FailToastShown;

                toast.Show();
            }
            
            return;
            void FullToastShown(Toast obj)
            {
                obj.shown -= FullToastShown;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_Toast_SessionFullLocalizedString);
            }
            
            void FailToastShown(Toast obj)
            {
                obj.shown -= FailToastShown;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_Toast_SessionJoinFailedLocalizedString);
            }
        }

        // event handler for request to join presentation
        private void OnRequestToJoinPresentation()
        {
            foreach (var avater in m_PlayerAvatars.Values)
            {
                GameObject playerObject = (GameObject) avater.userData;
                if (playerObject == null) return;
                if(!playerObject.TryGetComponent(out NetworkPlayerController playerController)) continue;
                if (playerController.IsPresenter.Value)
                {
                    m_PresentationModal?.Dismiss();
                
                    if (m_JoinPresentationDescriptionLocalizedString.TryGetValue("name", out var nameValue))
                    {
                        ((StringVariable) nameValue).Value = playerController.PlayerName.Value.Value;
                    }
                    else
                    {
                        m_JoinPresentationDescriptionLocalizedString.Add("name", new StringVariable() {Value = playerController.PlayerName.Value.Value});
                    }
                    
                    var requestToJoinDialog = new CustomAlertDialog(m_JoinPresentationTitleLocalizedString, m_JoinPresentationDescriptionLocalizedString)
                    {
                        title = " ",
                        description = string.Empty,
                        variant = AlertSemantic.Default
                    };
                    requestToJoinDialog.SetPrimaryAction(m_JoinLocalizedString, true, () =>
                    {
                        //Make sure the player is still the presenter, there is a case that presenter might have end
                        //presentation before the client joined
                        if(!playerController.IsPresenter.Value) return;
                        MultiplayController.JoinPresentation?.Invoke(playerController.OwnerClientId);
                    });
                    requestToJoinDialog.SetCancelAction(m_DismissLocalizedString);
                    m_PresentationModal = Modal.Build(m_PresentationModeButton, requestToJoinDialog);
                    m_PresentationModal.shown += OnModalShown;

                    m_PresentationModal.Show();
                }
            }
        }

        // event handler for client disconnection
        private void OnClientDisconnected(ulong id)
        {
            if (!m_PlayerAvatars.TryGetValue(id, out var avatar)) return;
            avatar.UnregisterCallback<ClickEvent>(OnAvatarIconClick);
            avatar.RemoveFromHierarchy();
            m_PlayerAvatars.Remove(id);
            m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
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
        private void OnNameChanged(ulong id, string username)
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
        private void OnClientConnected(ulong id, GameObject playerObject)
        {
            if(m_MyAvatar == null) return;
            if (m_PlayerAvatars.TryGetValue(id, out var playerAvatar))
            {
                playerAvatar.userData = playerObject;
                UpdateAvatarUI(playerObject);
                return;
            }

            if (NetworkManager.Singleton.LocalClientId == id)
            {
                m_MyAvatar.userData = playerObject;
                m_PlayerAvatars.Add(id, m_MyAvatar);
                m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                var avatar = CreateAvatar(id, playerObject);
                var avatarParent = m_MyAvatar.parent;
                avatarParent.Insert(avatarParent.childCount, avatar);
            }
            UpdateAvatarUI(playerObject);
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
                GameObject playerObject = (evt.target as VisualElement)?.userData as GameObject;
                if (playerObject == null)
                {
                    return;
                }
                NavigationController.PlayerTranslateTo?.Invoke(playerObject.transform.position, playerObject.transform.rotation);
            }
            m_LastClickTime = currentTime;
            m_LastClickedElement = evt.target;
        }

        // create the player avatar
        private Avatar CreateAvatar(ulong id, GameObject playerObject)
        {
            var avatar = new Avatar
            {
                userData = playerObject,
                size = Size.L,
                variant = AvatarVariant.Circular
            };
            
            m_PlayerAvatars.Add(id, avatar);

            avatar.AddToClassList(k_MultiplayIconClass);
            
            m_PresentationModeButton.style.display = m_PlayerAvatars.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            
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

        private void InitializeUI()
        {
            if (!m_UIDocument.rootVisualElement.styleSheets.Contains(m_MultiplayStyleSheet))
            {
                m_UIDocument.rootVisualElement.styleSheets.Add(m_MultiplayStyleSheet);
            }
            
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
            
            m_MyAvatar.parent.Add(m_PresentationModeButton);
            m_PresentationModeButton.style.display = DisplayStyle.None;
            
            m_PresentationModeButton.ClearBinding("tooltip");
            m_PresentationModeButton.SetBinding("tooltip", m_PresentationModeLocalizedString);
        }

        // event handler for presentation mode button click
        private void OnPresentationModeButtonClicked()
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

                if (!((GameObject) playerAvatar.userData).TryGetComponent(out NetworkPlayerController playerController))
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
                CustomAlertDialog newDialog = null;
                //If there is an ongoing presentation

                LocalizedString title = null; 
                LocalizedString description = null;
                LocalizedString primaryButtonText = null;
                
                if (myOwnPlayerObject.IsPresenter.Value)
                {
                    title = m_EndPresentationTitleLocalizedString;
                    description = m_EndPresentationDescriptionLocalizedString;
                    primaryButtonText = m_EndLocalizedString;
                } else if (myOwnPlayerObject.InPresentation.Value)
                {
                    title = m_LeavePresentationTitleLocalizedString;
                    description = m_LeavePresentationDescriptionLocalizedString;
                    primaryButtonText = m_LeaveLocalizedString;
                }
                else
                {
                    title = m_AskToJoinPresentationTitleLocalizedString;
                    description = m_AskToJoinPresentationDescriptionLocalizedString;
                    primaryButtonText = m_JoinLocalizedString;
                }
                
                newDialog = new CustomAlertDialog(title, description)
                {
                    variant = AlertSemantic.Default
                };
                
                if (myOwnPlayerObject.IsPresenter.Value)
                {
                    //End presentation mode
                    newDialog.SetPrimaryAction(primaryButtonText, true, () =>
                    {
                        MultiplayController.EndPresentation?.Invoke(myOwnPlayerObject.OwnerClientId);
                    });
                }
                else if(myOwnPlayerObject.InPresentation.Value)
                {
                    //Leave presentation mode
                    newDialog.SetPrimaryAction(primaryButtonText, true, () =>
                    {
                        MultiplayController.EndPresentation?.Invoke(myOwnPlayerObject.OwnerClientId);
                    });
                }
                else
                {
                    //Ask to join presentation mode
                    newDialog.SetPrimaryAction(primaryButtonText, true, () =>
                    {
                        MultiplayController.JoinPresentation?.Invoke(presenter.OwnerClientId);
                    });
                }
                newDialog.SetCancelAction(m_DismissLocalizedString);
                
                if(newDialog == null) return;
                
                m_PresentationModal = Modal.Build(m_PresentationModeButton, newDialog);
                m_PresentationModal.shown += OnModalShown;
                m_PresentationModal.Show();
                return;
            }
            
            //Initial presentation
            
            var presentationModeDialog = new CustomAlertDialog(m_StartPresentationTitleLocalizedString, m_StartPresentationDescriptionLocalizedString)
            {
                variant = AlertSemantic.Default
            };
            presentationModeDialog.SetPrimaryAction(m_StartLocalizedString, true, () =>
            {
                MultiplayController.InitializePresentationMode?.Invoke();
            });
            presentationModeDialog.SetCancelAction(m_CancelLocalizedString);
            m_PresentationModal = Modal.Build(m_PresentationModeButton, presentationModeDialog);
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
