using System.Collections.Generic;
using System.Collections;
using Unity.Industry.Viewer.Assets;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using Unity.Industry.Viewer.Shared;
using System.Linq;
using Unity.Industry.Viewer.Identity;
using UnityEngine.UIElements;
using System;
using Unity.AppUI.Core;
using Unity.AppUI.UI;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.VR
{
    public class StreamSceneVRUIController : StreamSceneUIController
    {
        [SerializeField]
        private UIDocument m_StreamingSceneUIDocument;
        
        private const string k_OfflineModeContainerName = "OfflineModeContainer";
        
        [SerializeField]
        private XRControllerMenu m_ExitXRControllerMenu;
        
        [SerializeField]
        private XRControllerMenu m_SaveXRControllerMenu;

        [SerializeField] private Texture2D m_ExitIcon;
        [SerializeField] private Texture2D m_SaveIcon;
        
        private XRRoundButton m_ExitButton;
        private XRRoundButton m_SaveButton;

        private Dictionary<MonoBehaviour, bool> m_PreviousSceneBehaviours;
        private Renderer m_GrabberRenderer;
        private Collider m_GrabberCollider;
        
        private XRInAppSettingUIController m_xrInAppSettingUIController;

        private XRPanel.CustomXRPanel m_XRSavePanel;
        
        [SerializeField]
        LayerMask m_UILayerMask;

        private VisualElement m_OfflineModeContainer;
        
        private BoxCollider m_BoxCollider;
        
        private bool m_IsLoadingPanelVisible;

        // VR: the App UI TouchSliderFloat can't be dragged with controllers, so use the regular
        // SliderFloat (arrows / input field) for the coincidence-bias strength control instead.
        protected override bool CoincidenceBiasUsesTouchSlider => false;

        protected override void Start()
        {
            base.Start();
            if (Camera.main.backgroundColor != Color.clear)
            {
                Camera.main.clearFlags = CameraClearFlags.Skybox;
            }
            m_BoxCollider = SharedUIManager.Instance.AssetsUIDocument.gameObject.GetComponent<BoxCollider>();
            m_BoxCollider.enabled = false;
            AssetsController.AssetSelected -= OnAssetSelected;
            AssetsController.AssetSelected += OnAssetSelected;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_TitleVersionLocalizedString.StringChanged -= OnTitleVersionLocalizedStringOnStringChanged;
            m_BoxCollider.enabled = true;
            AssetsController.AssetSelected -= OnAssetSelected;
        }

        private void OnTitleVersionLocalizedStringOnStringChanged(string value)
        {
            m_TitleText.text = value;
        }

        protected override void InitializeUI()
        {
            m_TitleText = m_StreamingSceneUIDocument.rootVisualElement.Q<Text>(k_AssetTitle);
            AssignAssetNameTitle(StreamingModelController.StreamingAsset.Value);
            
            m_NewVersionButton =
                m_StreamingSceneUIDocument.rootVisualElement.Q<ActionButton>(k_NewVersionButtonName);
            m_NewVersionButton.style.display = DisplayStyle.None;
            m_NewVersionButton.clicked += OnNewVersionButtonPress;
            m_AssetLoader = m_StreamingSceneUIDocument.rootVisualElement.Q<CircularProgress>();
            m_AssetLoader.DisplayOff();
            m_OfflineModeContainer = m_StreamingSceneUIDocument.rootVisualElement.Q(k_OfflineModeContainerName);
            m_OfflineModeContainer.style.display = NetworkDetector.RequestedOfflineMode ? DisplayStyle.Flex : DisplayStyle.None;
            
            DisablePreviousSceneUI();

            var allBehaviours = SharedUIManager.Instance.AssetsUIDocument.transform.parent
                .GetComponents<MonoBehaviour>();
            
            m_PreviousSceneBehaviours ??= new Dictionary<MonoBehaviour, bool>();
            foreach (var behaviour in allBehaviours)
            {
                m_PreviousSceneBehaviours.Add(behaviour, behaviour.enabled);
                behaviour.enabled = false;
            }

            m_GrabberRenderer = SharedUIManager.Instance.AssetsUIDocument.transform.parent.GetComponent<Renderer>();
            m_GrabberCollider = SharedUIManager.Instance.AssetsUIDocument.transform.parent.GetComponent<Collider>();
            if (m_GrabberRenderer != null)
            {
                m_GrabberRenderer.enabled = false;
            }

            if (m_GrabberCollider != null)
            {
                m_GrabberCollider.enabled = false;
            }
            
            m_ExitXRControllerMenu ??= new XRControllerMenu();
            m_ExitXRControllerMenu.Initialize();

            m_ExitButton = new XRRoundButton()
            {
                IconTexture = m_ExitIcon
            };

            m_ExitButton.clicked += OnBackButton;
            
            m_ExitXRControllerMenu.Add(m_ExitButton);
            
            m_SaveXRControllerMenu ??= new XRControllerMenu();
            m_SaveXRControllerMenu.Initialize();
            
            m_SaveButton = new XRRoundButton()
            {
                IconTexture = m_SaveIcon
            };
            m_SaveButton.clicked += SaveLayoutButtonOnClicked;
            m_SaveXRControllerMenu.Insert(m_SaveXRControllerMenu.Count, m_SaveButton);
            m_SaveButton.SetEnabled(false);
        }

        protected override void AssignAssetNameTitle(AssetInfo asset)
        {
            string assetName = string.Empty;
            var version = 0;
            if (asset.Asset is OfflineAsset offlineAsset)
            {
                assetName = offlineAsset.OfflineAssetInfo.assetName;
                version = offlineAsset.OfflineAssetInfo.assetVersion;
            }
            else
            {
                assetName = asset.Properties.Value.Name;
                version = asset.Properties.Value.FrozenSequenceNumber;
            }
            
            m_TitleVersionLocalizedString.Arguments = new object[]
            {
                new Dictionary<string, object>()
                {
                    {"name", assetName},
                    {"num", version}
                }
            };
            
            m_TitleVersionLocalizedString.StringChanged -= OnTitleVersionLocalizedStringOnStringChanged;
            m_TitleVersionLocalizedString.StringChanged += OnTitleVersionLocalizedStringOnStringChanged;
        }

        protected override void DisablePreviousSceneUI()
        {
            base.DisablePreviousSceneUI();
            var customAvatar = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<CustomAvatar>();
            customAvatar.style.display = DisplayStyle.None;
            m_xrInAppSettingUIController ??= FindFirstObjectByType<XRInAppSettingUIController>();
            m_xrInAppSettingUIController?.TwoDSettingsButtonDisplay(false);
        }

        protected override void OnNewVersionAvailable(AssetInfo newVersionAsset)
        {
            if(NetworkDetector.IsOffline) return;
            AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
            
            var xrAlertPanel = new XRPanel.AlertXRPanel(
                m_NewVersionTitleLocalizedString.GetTitleLocalizedStringForAppUI(),
                m_NewVersionDescriptionLocalizedString.GetTitleLocalizedStringForAppUI());
            xrAlertPanel.SetPrimaryButton(m_SwitchLocalizedString.GetTitleLocalizedStringForAppUI(), () =>
            {
                AssetsController.AssetSelected?.Invoke(AssetsController.NewerVersionAsset.Value);
            });
            
            xrAlertPanel.SetSecondaryButton(m_DismissLocalizedString.GetTitleLocalizedStringForAppUI(), () =>
            {
                m_NewVersionButton.style.display = DisplayStyle.Flex;
                m_NewVersionButton.SetEnabled(true);
            });
            
            xrAlertPanel.Show();
        }

        protected override void ShowPickSourceDialogHandler(AssetInfo onlineAssetInfo, AssetInfo offlineAssetInfo,
            string targetName, string anchorId)
        {
            bool loadingPanelVisible = LoadingUIPanel.IsLoadingPanelVisible;
            if (loadingPanelVisible)
            {
                LoadingUIPanel.HideLoadingPanel?.Invoke(ShowPanelAction);
                return;
            }
            
            ShowPanelAction();
            return;
            
            void ShowPanelAction()
            {
                var xrAlertPanel = new XRPanel.AlertXRPanel(
                    m_AddTitle.GetTitleLocalizedStringForAppUI(),
                    m_AddDescription.GetTitleLocalizedStringForAppUI());
                xrAlertPanel.SetPrimaryButton(m_CloudOption.GetTitleLocalizedStringForAppUI(), () =>
                {
                    StreamingModelController.AddStreamModel?.Invoke(onlineAssetInfo, targetName, null, anchorId);
                });
                
                xrAlertPanel.TitleText.variables = new object[]
                {
                    new Dictionary<string, object>()
                    {
                        { "asset", onlineAssetInfo.Properties.Value.Name }
                    }
                };
                
                xrAlertPanel.PrimaryButton.icon = "broadcast";
                xrAlertPanel.SetSecondaryButton(m_LocalOption.GetTitleLocalizedStringForAppUI(), () =>
                {
                    StreamingModelController.AddStreamModel?.Invoke(offlineAssetInfo, targetName, null, anchorId);
                });
                xrAlertPanel.SetCancelButton(m_CancelOption.GetTitleLocalizedStringForAppUI());
            
                xrAlertPanel.Dismissed += OnAddModelPanelDismissed;
                xrAlertPanel.Show();
            }
            
            void OnAddModelPanelDismissed(XRPanel.CustomXRPanel obj)
            {
                obj.Dismissed -= OnAddModelPanelDismissed;
                StreamingModelController.PauseAddingModel = false;
                if (loadingPanelVisible)
                {
                    LoadingUIPanel.ShowLoadingPanel?.Invoke(null);
                }
            }
        }

        protected override void ShowFailToAddModelToastHandler()
        {
            var toast = XRToastPanel
                .Build(m_AssetLoadFailureToast.GetTitleLocalizedStringForAppUI(),
                    NotificationDuration.Short).SetStyle(NotificationStyle.Negative);
            toast.Show();
        }

        protected override void ShowNoWritePermissionError()
        {
            var toast = XRToastPanel
                .Build(m_NoWritePermissionLocalizedString.GetTitleLocalizedStringForAppUI(),
                    NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
            toast.Show();
        }

        protected override void ShowSaveCompletedToast(string messageText)
        {
            var toast = XRToastPanel
                .Build(messageText,
                    NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
            toast.Show();
        }

        protected override void OnNetworkStatusChanged(bool connected)
        {
            if (connected)
            {
                m_OfflineModeContainer.style.display = DisplayStyle.None;
            }
            else
            {
                m_OfflineModeContainer.style.display = NetworkDetector.RequestedOfflineMode? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (m_NewVersionButton != null && m_NewVersionButton.style.display == DisplayStyle.Flex)
            {
                m_NewVersionButton.SetEnabled(connected);
            }
            if (!connected || IdentityController.GuestMode)
            {
                m_SaveButton?.SetEnabled(false);
                return;
            }
            var enableSaveButton = HasMultiStreamingModels();
            m_SaveButton?.SetEnabled(enableSaveButton);
        }

        protected override IEnumerator CheckTransformController()
        {
#if UNITY_WEBGL
            yield return null;
#else
            m_WaitForEndOfFrame ??= new WaitForEndOfFrame();
            yield return m_WaitForEndOfFrame;
#endif
            var disableSaveButton =
                !HasMultiStreamingModels()
                || NetworkDetector.IsOffline
                || NetworkDetector.RequestedOfflineMode
                || IdentityController.GuestMode;

            m_SaveButton?.SetEnabled(!disableSaveButton);

            CheckSaveButtonState();
        }

        protected override void SaveLayoutButtonOnClicked()
        {
            var savePanel = m_SavePanelTemplate.Instantiate().Children().First();
            m_XRSavePanel = new XRPanel.CustomXRPanel(string.Empty);
            XRPanel.Build(m_XRSavePanel, savePanel).SetBackground(false).SetCloseButton(false).Build();
            m_XRSavePanel.Shown += OnXRSavePanelShown;
            m_XRSavePanel.Show();
        }

        private void OnXRSavePanelShown(XRPanel.CustomXRPanel panel)
        {
            m_XRSavePanel.Shown -= OnXRSavePanelShown;
            InitializeSaveLayoutPanel(panel.Content);
        }

        protected override void SaveLayoutPanelCancelButtonOnClicked()
        {
            m_XRSavePanel?.Dismiss();
        }

        protected override void ConfirmSaveLayoutButtonOnClicked()
        {
            m_XRSavePanel.Dismissed += OnXRSavePanelDismissed;
            m_XRSavePanel.Dismiss();
            return;

            void OnXRSavePanelDismissed(XRPanel.CustomXRPanel obj)
            {
                m_XRSavePanel.Dismissed -= OnXRSavePanelDismissed;
                SaveLayoutAction();
            }
        }

        protected override IEnumerator CaptureScreenShot(Action<Texture2D> callback)
        {
            var originalMask = Camera.main.cullingMask;
            Camera.main.cullingMask = originalMask & ~m_UILayerMask.value;
            yield return new WaitForEndOfFrame();
            var screenShot = ScreenCapture.CaptureScreenshotAsTexture();
            yield return new WaitForEndOfFrame();
            Camera.main.cullingMask = originalMask;
            ScaleTexture(screenShot, 320, 180, out var scaledTexture);
            yield return new WaitForEndOfFrame();
            callback?.Invoke(scaledTexture);
        }

        protected override void OnBackButton()
        {
            var xrAlertPanel = new XRPanel.AlertXRPanel(
                m_ExitTitleLocalizedString.GetTitleLocalizedStringForAppUI(),
                m_ExitDescriptionLocalizedString.GetTitleLocalizedStringForAppUI());
            xrAlertPanel.SetPrimaryButton(m_ExitLocalizedString.GetTitleLocalizedStringForAppUI(), () =>
            {
                StreamSceneController.ExitSceneConfirmed?.Invoke();
            });
            
            xrAlertPanel.SetCancelButton(m_CancelOption.GetTitleLocalizedStringForAppUI());
            xrAlertPanel.Show();
        }
        
        public void BehaviourEnabled(bool value)
        {
            if (m_PreviousSceneBehaviours == null)
            {
                return;
            }
            foreach (var behaviour in m_PreviousSceneBehaviours.Keys)
            {
                behaviour.enabled = value;
            }
            
            if (m_GrabberRenderer != null)
            {
                m_GrabberRenderer.enabled = value;
            }

            if (m_GrabberCollider != null)
            {
                m_GrabberCollider.enabled = value;
            }
        }

        protected override void ExitSceneUIHandler()
        {
            if (m_ExitButton != null)
            {
                m_ExitButton.clicked -= OnBackButton;
                m_ExitButton?.RemoveFromHierarchy();
                m_ExitButton = null;
            }
            
            if (m_SaveButton != null)
            {
                m_SaveButton.clicked -= SaveLayoutButtonOnClicked;
                m_SaveButton?.RemoveFromHierarchy();
                m_SaveButton = null;
            }

            ReenablePreviousSceneUI();
            foreach (var monoBehaviour in m_PreviousSceneBehaviours.Keys)
            {
                monoBehaviour.enabled = m_PreviousSceneBehaviours[monoBehaviour];
            }
            m_PreviousSceneBehaviours.Clear();
            m_PreviousSceneBehaviours = null;
            
            if (m_GrabberRenderer != null)
            {
                m_GrabberRenderer.enabled = true;
            }

            if (m_GrabberCollider != null)
            {
                m_GrabberCollider.enabled = true;
            }
            
            m_NewVersionButton.clicked -= OnNewVersionButtonPress;
        }

        protected override void ReenablePreviousSceneUI()
        {
            base.ReenablePreviousSceneUI();
            var customAvatar = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<CustomAvatar>();
            customAvatar.style.display = DisplayStyle.Flex;
            m_xrInAppSettingUIController ??= FindFirstObjectByType<XRInAppSettingUIController>();
            m_xrInAppSettingUIController.TwoDSettingsButtonDisplay(true);
        }
    }
}
