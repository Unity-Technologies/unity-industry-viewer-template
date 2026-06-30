using Unity.AppUI.UI;
using Unity.Industry.Viewer.Multiplay;
using UnityEngine;
using UnityEngine.UIElements;
using Avatar = Unity.AppUI.UI.Avatar;
using Unity.Netcode;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Shared;
using System;
using Unity.Industry.Viewer.Assets;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.Core;

namespace Unity.Industry.Viewer.VR
{
    public class XRMultiplayUIController : MultiplayUIController
    {
        private const string k_AvatarClientsParentName = "ClientsParent";
        private const string k_FirstNameLabelName = "First-Name-Label";
        private const string k_InMultiplayClassName = "in-multiplay";
        private const string k_AssetTitle = "AssetTitle";
        
        [SerializeField]
        private UIDocument m_UiDocument;
        
        private VisualElement m_ClientsParent;
        private Text m_FirstNameLabel;
        private Text m_TitleText;
        
        [SerializeField]
        private XRControllerMenu m_PresentationXRControllerMenu;
        
        [SerializeField] private Texture2D m_PresentationIcon;
        
        private XRPanel.AlertXRPanel m_PresentationPanel;

        protected override void Start()
        {
            base.Start();
            MultiplayController.JoinPresentation += OnJoinPresentation;
            MultiplayController.EndPresentation += OnEndPresentation;
        }

        private void LateUpdate()
        {
            if(m_PlayerAvatars == null || m_PlayerAvatars.Count <= 1) return;
            // m_MyAvatar (the local player) is added without a NetworkPlayerController in userData,
            // so skip entries whose cast is null instead of dereferencing it every frame. The local
            // user is in VR in this controller, so excluding it doesn't change the all-in-VR result.
            bool isAllInVR = m_PlayerAvatars.Values
                .Select(avatar => avatar.userData as NetworkPlayerController)
                .Where(playerController => playerController != null)
                .All(playerController => playerController.IsInVR.Value);
            m_PresentationModeButton.style.display = isAllInVR ? DisplayStyle.None: DisplayStyle.Flex;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            MultiplayController.JoinPresentation -= OnJoinPresentation;
            MultiplayController.EndPresentation -= OnEndPresentation;
        }

        private void OnEndPresentation(ulong id)
        {
            foreach (var avatar in m_PlayerAvatars.Values)
            {
                avatar.outlineColor = Optional<Color>.none;
            }
        }

        private void OnJoinPresentation(ulong id)
        {
            if (m_PlayerAvatars.TryGetValue(id, out Avatar avatar))
            {
                var currentBackgroundColor = avatar.backgroundColor.Value;
                avatar.outlineColor = new Optional<Color>(GetInvertedColor(currentBackgroundColor));
            }
            
            Color GetInvertedColor(Color color)
            {
                return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a);
            }
        }

        protected override void InitializeUI()
        {
            m_UiDocument.rootVisualElement.AddStyleSheetIfMissing(m_MultiplayStyleSheet);

            m_TitleText = m_UiDocument.rootVisualElement.Q<Text>(k_AssetTitle);
            
            m_PresentationXRControllerMenu ??= new XRControllerMenu();
            m_PresentationXRControllerMenu.Initialize();
            m_PresentationModeButton = new XRRoundButton()
            {
                IconTexture = m_PresentationIcon
            };
            
            m_PresentationXRControllerMenu.Add(m_PresentationModeButton);
            m_PresentationModeButton.clicked += OnPresentationModeButtonClicked;
            m_PresentationModeButton.style.display = DisplayStyle.None;
            
            m_ClientsParent = m_UiDocument.rootVisualElement.Q(k_AvatarClientsParentName);
            m_ClientsParent.style.display = DisplayStyle.None;
            m_MyAvatar = m_UiDocument.rootVisualElement.Q<Avatar>(k_AvatarName);
            m_OriginalAvatarColor = m_MyAvatar.backgroundColor.Value;
            m_MyAvatar.style.display = DisplayStyle.None;
            m_FirstNameLabel = m_UiDocument.rootVisualElement.Q<Text>(k_FirstNameLabelName);
            m_FirstNameLabel.style.display = DisplayStyle.None;
        }

        protected override void OnSessionJoinedFailed(string message)
        {
            var textMessage = message.Contains("lobby is full") ? m_Toast_SessionFullLocalizedString.GetTitleLocalizedStringForAppUI()
                : m_Toast_SessionJoinFailedLocalizedString.GetTitleLocalizedStringForAppUI();
            var toast = XRToastPanel.Build(textMessage, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
            toast.Show();
        }

        protected override void OnPresentationModeButtonClicked()
        {
            m_PresentationPanel?.Dismiss();
            base.OnPresentationModeButtonClicked();
        }

        protected override void ShowPresentationDialog(NetworkPlayerController myOwnPlayerObject, NetworkPlayerController presenter,
            LocalizedString title, LocalizedString description, LocalizedString primaryButtonText, Action primaryAction)
        {
            m_PresentationPanel = new XRPanel.AlertXRPanel(
                title.GetTitleLocalizedStringForAppUI(),
                description.GetTitleLocalizedStringForAppUI());
            m_PresentationPanel.SetPrimaryButton(primaryButtonText.GetTitleLocalizedStringForAppUI(), primaryAction);
            m_PresentationPanel.SetCancelButton(m_DismissLocalizedString.GetTitleLocalizedStringForAppUI());
            
            m_PresentationPanel.Shown += OnXRPanelShown;
            m_PresentationPanel.Show();
        }

        protected override void InitializePresentationDialog()
        {
            m_PresentationPanel?.Dismiss();
            m_PresentationPanel = new XRPanel.AlertXRPanel(
                m_StartPresentationTitleLocalizedString.GetTitleLocalizedStringForAppUI(),
                m_StartPresentationDescriptionLocalizedString.GetTitleLocalizedStringForAppUI());
            
            m_PresentationPanel.SetPrimaryButton(m_StartLocalizedString.GetTitleLocalizedStringForAppUI(), () =>
            {
                MultiplayController.InitializePresentationMode?.Invoke();
            });
            
            m_PresentationPanel.SetCancelButton(m_CancelLocalizedString.GetTitleLocalizedStringForAppUI());
            
            m_PresentationPanel.Shown += OnXRPanelShown;
            m_PresentationPanel.Show();
        }

        private void OnXRPanelShown(XRPanel.CustomXRPanel obj)
        {
            m_PresentationPanel.Shown -= OnXRPanelShown;
            isModalOpened = true;
            m_PresentationPanel.Dismissed += OnXRPanelDismissed;
        }

        private void OnXRPanelDismissed(XRPanel.CustomXRPanel obj)
        {
            m_PresentationPanel.Dismissed -= OnXRPanelDismissed;
            isModalOpened = false;
            m_PresentationPanel = null;
        }

        protected override void OnClientStopped(bool id)
        {
            base.OnClientStopped(id);
            if (m_TitleText.ClassListContains(k_InMultiplayClassName))
            {
                m_TitleText.RemoveFromClassList(k_InMultiplayClassName);
            }
            m_ClientsParent.style.display = DisplayStyle.None;
            m_MyAvatar.style.display = DisplayStyle.None;
            m_FirstNameLabel.style.display = DisplayStyle.None;
        }
        
        protected override void OnClientConnected(ulong id, GameObject playerObject)
        {
            base.OnClientConnected(id, playerObject);
            if (NetworkManager.Singleton.LocalClientId == id)
            {
                m_MyAvatar.style.display = DisplayStyle.Flex;
                m_FirstNameLabel.style.display = DisplayStyle.Flex;
            }
            if (!m_TitleText.ClassListContains(k_InMultiplayClassName))
            {
                m_TitleText.AddToClassList(k_InMultiplayClassName);
            }
        }
        
        protected override void OnClientDisconnected(ulong id)
        {
            base.OnClientDisconnected(id);
            if (m_ClientsParent.childCount == 0)
            {
                m_ClientsParent.style.display = DisplayStyle.None;
            }
        }

        protected override void OnNameChanged(ulong id, string username)
        {
            base.OnNameChanged(id, username);
            if (NetworkManager.Singleton.LocalClientId == id)
            {
                m_FirstNameLabel.text = username.Split(" ")[0];
            }
        }

        protected override void ShowRequestToJoinDialog(NetworkPlayerController playerController, Action joinAction)
        {
            m_PresentationPanel?.Dismiss();
            m_PresentationPanel = new XRPanel.AlertXRPanel(
                m_JoinPresentationTitleLocalizedString.GetTitleLocalizedStringForAppUI(),
                m_JoinPresentationDescriptionLocalizedString.GetTitleLocalizedStringForAppUI());

            m_PresentationPanel.DescriptionText.variables = new object[]
            {
                new Dictionary<string, object>()
                {
                    { "name", playerController.PlayerName.Value.Value }
                }
            };
            
            m_PresentationPanel.SetPrimaryButton(m_JoinLocalizedString.GetTitleLocalizedStringForAppUI(), joinAction);
            
            m_PresentationPanel.SetCancelButton(m_CancelLocalizedString.GetTitleLocalizedStringForAppUI());
            m_PresentationPanel.Shown += OnXRPanelShown;
            m_PresentationPanel.Show();
        }

        protected override void OnAskToJoinLayout(AssetInfo assetInfo)
        {
            var askToJoinLayoutPanel = new XRPanel.AlertXRPanel(
                m_JoinNewLayoutSessionTitleLocalizedString.GetTitleLocalizedStringForAppUI(),
                m_JoinNewLayoutSessionDescriptionLocalizedString.GetTitleLocalizedStringForAppUI());
            
            askToJoinLayoutPanel.SetPrimaryButton(m_JoinLocalizedString.GetTitleLocalizedStringForAppUI(),
                () =>
                {
                    AssetsController.AssetSelected?.Invoke(assetInfo);
                });
            
            askToJoinLayoutPanel.SetCancelButton(m_CancelLocalizedString.GetTitleLocalizedStringForAppUI());
            askToJoinLayoutPanel.Show();
        }

        protected override void AddOtherClientAvatar(ulong id, NetworkPlayerController playerObject)
        {
            var avatar = CreateAvatar(id, playerObject);
            if (m_ClientsParent.childCount > 0)
            {
                avatar.style.marginRight = new Length(13, LengthUnit.Pixel);
            }
            avatar.style.height = m_MyAvatar.resolvedStyle.height;
            avatar.style.width = m_MyAvatar.resolvedStyle.width;
            m_ClientsParent.Insert(m_ClientsParent.childCount, avatar);
            m_ClientsParent.style.display = DisplayStyle.Flex;
        }
    }
}
