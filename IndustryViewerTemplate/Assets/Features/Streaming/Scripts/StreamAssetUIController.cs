using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AppUI.Core;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using Button = Unity.AppUI.UI.Button;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamingUILayoutController : IDisposable
    {
        private AssetInfo? m_Asset;
        private Action<AssetInfo> m_DownloadAssetAction;
        private Action<AssetInfo> m_StreamAssetAction;
        
        private IconButton m_DownloadStreamingAssetButton;
        private ActionButton m_StreamButton;
        
        public StreamingUILayoutController(AssetInfo asset,
            IconButton downloadStreamingAssetButton,
            ActionButton streamButton,
            Action<AssetInfo> downloadAssetAction, Action<AssetInfo> streamAssetAction)
        {
            m_Asset = asset;
            m_DownloadStreamingAssetButton = downloadStreamingAssetButton;
            m_StreamButton = streamButton;
            m_DownloadAssetAction = downloadAssetAction;
            m_StreamAssetAction = streamAssetAction;
        }

        public void OnDownloadAsset()
        {
            if(!m_DownloadStreamingAssetButton.enabledSelf) return;
            m_DownloadStreamingAssetButton.SetEnabled(false);
            
            if (SharedUIManager.SelectedAsset.HasValue &&
                StreamAssetUIController.IsSameAssetButDifferentVersion(SharedUIManager.SelectedAsset.Value, m_Asset.Value))
            {
                m_DownloadAssetAction?.Invoke(SharedUIManager.SelectedAsset.Value);
            }
            else
            {
                m_DownloadAssetAction?.Invoke(m_Asset.Value);
            }
        }
        
        public void DirectStreamAsset()
        {
            if(!m_StreamButton.enabledSelf) return;
            m_StreamButton.SetEnabled(false);
            m_StreamAssetAction?.Invoke(m_Asset.Value);
        }

        public void Dispose()
        {
            m_Asset = null;
            m_DownloadAssetAction = null;
            m_StreamAssetAction = null;
        }
    }
    
    public class StreamAssetUIController : MonoBehaviour
    {
        public static Action<AssetInfo> DownloadCacheFinished;

        /// <summary>
        /// External implementation of DeepLink creation logic for reducing coupling.
        /// </summary>
        public static Action<VisualElement> DeepLinkCreationHandler;

        [SerializeField]
        StyleSheet m_StreamingAssetBarStyle, m_assetItemStyle;
        
        [SerializeField]
        private LocalizedString m_StreamingDataNotAvailableLocalizedString;
        
        public const string k_AssetInfoPanelRootName = "AssetInfoContainer";
        private const string k_AssetInfoPanelName = "AssetInfoPanelRoot";
        private const string k_StreamAssetButtonName = "StreamAssetButton";
        private const string k_OffloadAssetButtonName = "OffloadAssetButton";
        public const string k_CopyDeepLinkButtonName = "CopyDeepLinkButton";
        private const string k_AssetInfoPanelTopName = "AssetInfoPanelTop";
        private const string k_DownloadStreamingAssetButtonName = "DownloadStreamingAssetButton";
        private const string k_DirectDownload3DDSButtonName = "DirectDownload3DDSButton";
        private const string k_3DDSButtonName = "3DDSButton";
        private const string k_VerBoxName = "VerBox";

        [SerializeField]
        private VisualTreeAsset m_AssetItem3DDSUITemplate;
        
        [SerializeField]
        private VisualTreeAsset m_StreamingAssetBarTemplate;
        
        [SerializeField]
        private VisualTreeAsset m_DirectStreamAssetModalTemplate;

        private VisualElement m_AssetBar;
        private VisualElement m_AssetInfoPanelRoot;
        private VisualElement m_AssetInfoPanel;
        private VisualElement m_AssetInfoPanelTop;
        private ActionButton m_StreamButton;
        private ActionButton m_CopyDeepLinkButton;
        private ActionButton m_OffloadAssetButton;
        private ProgressActionButton m_DownloadStreamingAssetButton;
        private CircularProgress m_DownloadProgress;
        
        private Dictionary<IAsset, DownloadStreamingDataController> m_DownloadStreamingDataControllers;
        
        private IDataset m_StreamingDataset, m_SourceDataSet;
        
        bool hasInitiated = false;
        private IAsset m_currentOpenedAsset;

        private Modal m_DirectStreamAssetModal;

        private Modal m_TopActionModal;

        private AssetsInfoUIToolkitController AssetsInfoUIToolkitController
        {
            get
            {
                if (m_AssetsInfoUIToolkitController != null) return m_AssetsInfoUIToolkitController;
                var assetsInfoUIToolkitController = FindFirstObjectByType<AssetsUIToolkitController>();
                if (assetsInfoUIToolkitController != null)
                {
                    m_AssetsInfoUIToolkitController = assetsInfoUIToolkitController.AssetInfoUIController as AssetsInfoUIToolkitController;
                }
                return m_AssetsInfoUIToolkitController;
            }
            
            set => m_AssetsInfoUIToolkitController = value;
        }

        private AssetsInfoUIToolkitController m_AssetsInfoUIToolkitController;

        #region Localization

        [SerializeField]
        private LocalizedString m_RemoveAssetTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_RemoveAssetDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_RemoveLocalizedString;

        [SerializeField]
        private LocalizedString m_Toast_AssetRemovedLocalizedString;

        [SerializeField]
        private LocalizedString m_CancelLocalizedString;

        [SerializeField]
        private LocalizedString m_DownloadAssetTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_DownloadAssetDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_DownloadLocalizedString;

        [SerializeField]
        private LocalizedString m_Toast_DownloadingAssetLocalizedString;

        [SerializeField]
        private LocalizedString m_Toast_FinishDownloadLocalizedString;

        [SerializeField]
        private LocalizedString m_LoadLayoutLocalizedString;

        [SerializeField]
        private LocalizedString m_StreamLocalizedString;

        [SerializeField]
        private LocalizedString m_CloudLocalizedString;

        [SerializeField]
        private LocalizedString m_LocalLocalizedString;

        [SerializeField]
        private LocalizedString m_PickDataTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_PickDataDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_PreserveLocalizedString;

        [SerializeField]
        private LocalizedString m_RemoveLayoutAssetDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_RemoveReferencedAssetDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_KeepOrUpdateLocalAssetDescriptionLocalizedString;
        
        [SerializeField]
        private LocalizedString m_ReadyForLocalStreamingLocalizedString;
        
        [SerializeField]
        private LocalizedString m_UpdateLocalStreamingLocalizedString;
        
        [SerializeField]
        private LocalizedString m_UpdateLocalizedString;

        protected const string k_AssetLocalisedTable = "Shared";
        protected const string k_VersionKey = "Version Smart";

        #endregion

        private bool m_IsStreamFunctionalityActive = true;
        public bool IsStreamFunctionalityActive
        {
            get => m_IsStreamFunctionalityActive;
            set
            {
                m_IsStreamFunctionalityActive = value;

                if (value && m_StreamButton == null)
                {
                    ActivateStreamFunctionality();
                }

                if (m_StreamButton != null && !value)
                {
                    DeactivateStreamFunctionality();
                }
            }
        }

        private bool m_IsDownOffloadFunctionalityActive = true;
        public bool IsDownOffloadFunctionalityActive
        {
            get => m_IsDownOffloadFunctionalityActive;
            set
            {
                m_IsDownOffloadFunctionalityActive = value;
            }
        }

        private void Awake()
        {
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
        }
        
        // Start is called before the first frame update
        void Start()
        {
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Add(m_StreamingAssetBarStyle);
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Add(m_assetItemStyle);

            m_AssetInfoPanelRoot = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_AssetInfoPanelRootName);
            m_AssetInfoPanel = m_AssetInfoPanelRoot.Q<VisualElement>(k_AssetInfoPanelName);
            m_AssetInfoPanelTop = m_AssetInfoPanelRoot.Q<VisualElement>(k_AssetInfoPanelTopName);
            
            AssetsController.AssetSelected += AssetSelected;
            AssetsController.AssetDeselected += AssetDeselected;
            AssetsController.ParentAssetSelected += OnParentAssetSelected;
#if !UNITY_WEBGL || UNITY_EDITOR
            DownloadStreamingDataController.KeepExistingAssets += OnToAskToKeepExistingAssets;
            OfflineModeAssetsController.AssetSelected += AssetSelected;
            OfflineModeAssetsController.AssetDeselected += AssetDeselected;
            DownloadCacheFinished += OnDownloadCacheFinished;
#endif
        }

        private void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            AssetsController.AssetSelected -= AssetSelected;
            AssetsController.AssetDeselected -= AssetDeselected;
            AssetsController.ParentAssetSelected -= OnParentAssetSelected;
            if (SharedUIManager.Instance != null && SharedUIManager.Instance.AssetGridView != null)
            {
                SharedUIManager.Instance.AssetGridView.bindItem -= GridBindItem;
                SharedUIManager.Instance.AssetGridView.unbindItem -= GridUnbindItem;
            }

            if (m_StreamButton != null)
            {
                m_StreamButton.clicked -= StreamAsset;
            }

            if (m_CopyDeepLinkButton != null)
            {
                m_CopyDeepLinkButton.clicked -= OnCopyDeepLinkButtonPress;
            }
            if (AssetsInfoUIToolkitController != null)
            {
                AssetsInfoUIToolkitController.UpdateVersionButtonAction = null;
            }
            
#if !UNITY_WEBGL || UNITY_EDITOR
            DownloadStreamingDataController.KeepExistingAssets -= OnToAskToKeepExistingAssets;
            CancelDownloads();
            
            OfflineModeAssetsController.AssetSelected -= AssetSelected;
            OfflineModeAssetsController.AssetDeselected -= AssetDeselected;
            
            DownloadCacheFinished -= OnDownloadCacheFinished;
            
            if (m_DownloadStreamingAssetButton != null)
            {
                m_DownloadStreamingAssetButton.clicked -= OnDownloadButtonPress;
            }
            
            if (m_OffloadAssetButton != null)
            {
                m_OffloadAssetButton.clicked -= OnRemoveCacheButtonPress;
            }
#endif
        }

        private void DeactivateStreamFunctionality()
        {
            m_StreamButton.DisplayOff();
            m_StreamButton.clicked -= StreamAsset;
            m_StreamButton = null;
        }

        private void ActivateStreamFunctionality()
        {
            m_StreamButton = m_AssetBar.Q<ActionButton>(k_StreamAssetButtonName);
            m_StreamButton.clicked += StreamAsset;
            m_StreamButton.DisplayOff();
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private void CancelDownloads()
        {
            if (m_DownloadStreamingDataControllers != null)
            {
                DownloadCacheFinished -= OnDownloadCacheFinished;
                foreach (var value in m_DownloadStreamingDataControllers.Values)
                {
                    value.CancelTask();
                    value.DownloadProgress -= OnDownloadProgress;
                }
                m_DownloadStreamingDataControllers.Clear();
            }
        }
#endif

        private void OnNetworkStatusChanged(bool connected)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!connected)
            {
                CancelDownloads();
            }
#endif
            if (!connected && !NetworkDetector.RequestedOfflineMode)
            {
                //If disconnected
                if (SharedUIManager.Instance.AssetGridView == null) return;
                SharedUIManager.Instance.AssetGridView.bindItem -= GridBindItem;
                SharedUIManager.Instance.AssetGridView.unbindItem -= GridUnbindItem;
                return;
            }
            //If connected
            if (SharedUIManager.Instance.AssetGridView == null) return;
#if !UNITY_WEBGL || UNITY_EDITOR
            DownloadCacheFinished -= OnDownloadCacheFinished;
            DownloadCacheFinished += OnDownloadCacheFinished;
#endif
            SharedUIManager.Instance.AssetGridView.bindItem -= GridBindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem -= GridUnbindItem;
            SharedUIManager.Instance.AssetGridView.bindItem += GridBindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem += GridUnbindItem;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private async void OnToAskToKeepExistingAssets(string assetName, int requiredVersion, int localVersion, Action<bool> keepActionCallback)
        {
            var dialog = new AlertDialog()
            {
                title = await m_DownloadAssetTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await m_KeepOrUpdateLocalAssetDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Confirmation
            };

            var descriptionLabel = dialog.Q<LocalizedTextElement>("appui-dialog__content");
            descriptionLabel.variables = new object[]
            {
                new Dictionary<string, object>()
                {
                    {"name", assetName},
                    {"localVersion", localVersion},
                    {"requiredVersion", requiredVersion}
                }
            };
            
            dialog.SetPrimaryAction(99, await m_PreserveLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                keepActionCallback?.Invoke(true);
            });
            
            dialog.SetSecondaryAction(98, await m_RemoveLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                keepActionCallback?.Invoke(false);
            });
            
            var modal = Modal.Build(m_AssetInfoPanelTop, dialog);
            
            modal.Show();
        }
#endif
        
        private void GridBindItem(VisualElement item, int index)
        {
            if(index < 0) return;
            AssetInfo? assetInfo = SharedUIManager.Instance.AssetGridView.itemsSource[index] as AssetInfo?;
            var ui = item.Q<VisualElement>("3DDSAssetUILayout");
            StreamingUILayoutController controller = null;
            if (ui == null)
            {
                ui = m_AssetItem3DDSUITemplate.Instantiate().Children().First();
                ui.name = "3DDSAssetUILayout";
                ui.contentContainer.style.position = Position.Absolute;
                var itemUI = item.Q<VisualElement>("ItemUI");
                itemUI.Add(ui);
            }
            
            var directDownloadAssetButton = item.Q<IconButton>(k_DirectDownload3DDSButtonName);
            
            var directStreamButton = item.Q<ActionButton>(k_3DDSButtonName);
            
            if(ui.userData != null && ui.userData is StreamingUILayoutController existingController)
            {
                existingController.Dispose();
                ui.userData = null;
            }
            
            controller = new StreamingUILayoutController(assetInfo.Value, 
                directDownloadAssetButton, directStreamButton,
                DownloadAsset, DirectStreamAsset);

            ui.userData = controller;
            
            directDownloadAssetButton.style.display = DisplayStyle.Flex;
            directDownloadAssetButton.clicked -= controller.OnDownloadAsset;
            directDownloadAssetButton.clicked += controller.OnDownloadAsset;
            if (IdentityController.GuestMode)
            {
                directDownloadAssetButton.style.display = DisplayStyle.None;
            }
            else
            {
                if (assetInfo.Value.Asset is OfflineAsset || Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    directDownloadAssetButton.DisplayOff();
                }
                else
                {
                    if (directDownloadAssetButton.ClassListContains("downloaded"))
                    {
                        directDownloadAssetButton.RemoveFromClassList("downloaded");
                    }
                    if (directDownloadAssetButton.ClassListContains("notDownloaded"))
                    {
                        directDownloadAssetButton.RemoveFromClassList("notDownloaded");
                    }
                    if (directDownloadAssetButton.ClassListContains("incorrectVersion"))
                    {
                        directDownloadAssetButton.RemoveFromClassList("incorrectVersion");
                    }
                    var hasDownloaded = StreamingUtils.CheckHasLocalAsset(assetInfo.Value.Asset, false, out var ver);
                    if (hasDownloaded)
                    {
                        var versionMatched = ver == assetInfo.Value.Properties.Value.FrozenSequenceNumber;
                        directDownloadAssetButton.AddToClassList(versionMatched? "downloaded" : "incorrectVersion");
                        if (!versionMatched)
                        {
                            //Enable download button
    #if !UNITY_WEBGL || UNITY_EDITOR
                            directDownloadAssetButton.SetEnabled(true);
    #endif
                        }
                        else
                        {
                            directDownloadAssetButton.SetEnabled(false);
                        }
                    }
                    else
                    {
                        directDownloadAssetButton.AddToClassList("notDownloaded");
                        //Enable download button
    #if !UNITY_WEBGL || UNITY_EDITOR
                        directDownloadAssetButton.SetEnabled(true);
    #endif
                    }
                }
            }
            
            if (!m_IsDownOffloadFunctionalityActive)
            {
                directDownloadAssetButton.SetEnabled(false);
            }

            directStreamButton.SetDisplay(m_IsStreamFunctionalityActive);
            directStreamButton.clicked -= controller.DirectStreamAsset;
            directStreamButton.clicked += controller.DirectStreamAsset;
        }

        private void GridUnbindItem(VisualElement item, int index)
        {
            var itemUI = item.Q<VisualElement>("ItemUI");
            var _3ddsAssetUILayout = itemUI.Q<VisualElement>("3DDSAssetUILayout");
            if (_3ddsAssetUILayout.userData is not StreamingUILayoutController controller) return;
            var directDownloadAssetButton = item.Q<IconButton>(k_DirectDownload3DDSButtonName);
            
            if(directDownloadAssetButton.ClassListContains("downloaded"))
            {
                directDownloadAssetButton.RemoveFromClassList("downloaded");
            }
            if(directDownloadAssetButton.ClassListContains("notDownloaded"))
            {
                directDownloadAssetButton.RemoveFromClassList("notDownloaded");
            }
            if(directDownloadAssetButton.ClassListContains("incorrectVersion"))
            {
                directDownloadAssetButton.RemoveFromClassList("incorrectVersion");
            }
            directDownloadAssetButton.clicked -= controller.OnDownloadAsset;
            
            var directStreamButton = item.Q<ActionButton>(k_3DDSButtonName);
            directStreamButton.clicked -= controller.DirectStreamAsset;
            
            controller.Dispose();
            _3ddsAssetUILayout.userData = null;
        }

        private void AssetDeselected()
        {
            if (m_DownloadStreamingAssetButton != null)
            {
                m_DownloadStreamingAssetButton.userData = null;
            }
            m_currentOpenedAsset = null;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        
        public async void ShowStreamingAssetDownload(AssetInfo assetInfo)
        {
            if (!m_IsDownOffloadFunctionalityActive)
            {
                m_OffloadAssetButton.DisplayOff();
                m_DownloadStreamingAssetButton.DisplayOff();
                return;
            }

            var currentVersionNumber = assetInfo.Properties.Value.FrozenSequenceNumber;
            string hashFolderName = StreamingUtils.ReturnHashName(assetInfo.Asset);
            
            if (!Directory.Exists(StreamingUtils.LocalStreamingAssetPath))
            {
                Directory.CreateDirectory(StreamingUtils.LocalStreamingAssetPath);
            }

            var matchingFolders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolderName + "*");
            m_DownloadStreamingAssetButton.HideProgress();
            m_DownloadStreamingAssetButton.DisplayOn();
            m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);
            
            m_DownloadStreamingAssetButton.userData = assetInfo;
            if (matchingFolders == null || matchingFolders.Length == 0)
            {
                if (!StreamingModelController.StreamingAsset.HasValue)
                {
                    bool canDownloadOfflineAsset = false;
                    if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
                    {
                        canDownloadOfflineAsset = m_SourceDataSet != null;
                    }
                    else
                    {
                        canDownloadOfflineAsset = m_StreamingDataset != null;
                    }
                
                    m_DownloadStreamingAssetButton.SetDisplay(canDownloadOfflineAsset);
                }
                else
                {
                    m_DownloadStreamingAssetButton.DisplayOff();
                    _ = CanDownloadStreamingAsset(assetInfo);
                }

                m_OffloadAssetButton.DisplayOff();
                AssetsInfoUIToolkitController.UpdateVersionVE.ClearClassList();
                
                if (AssetsController.NewerVersionAsset.HasValue &&
                    AssetsController.NewerVersionAsset.Value == assetInfo)
                {
                    //Show the update version button
                    AssetsInfoUIToolkitController.ShowNewVersionVE();
                }
                else
                {
                    AssetsInfoUIToolkitController.UpdateVersionVE.DisplayOff();
                }
            }
            else
            {
                if (StreamingModelController.StreamingAsset.HasValue)
                {
                    if((StreamingModelController.StreamingAsset.Value.Asset is OfflineAsset)
                        && StreamingModelController.StreamingAsset.Value.Asset.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId
                        && StreamingModelController.StreamingAsset.Value.Asset.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor)
                    {
                        m_DownloadStreamingAssetButton.SetEnabled(false);
                        m_DownloadStreamingAssetButton.DisplayOff();
                        m_OffloadAssetButton.SetEnabled(false);
                        m_OffloadAssetButton.DisplayOff();
                        AssetsInfoUIToolkitController.UpdateVersionVE.DisplayOff();
                        return;
                    }
                }
                
                var firstOrDefault = matchingFolders.FirstOrDefault();
                var directoryName = new DirectoryInfo(firstOrDefault).Name;
                m_DownloadStreamingDataControllers ??= new Dictionary<IAsset, DownloadStreamingDataController>();
                if (directoryName.Contains("_temp") && !m_DownloadStreamingDataControllers.Keys.Any(x => x.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId &&
                        x.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor))
                {
                    m_DownloadStreamingAssetButton.DisplayOn();
                    m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);
                    m_OffloadAssetButton.DisplayOff();
                }
                else
                {
                    if (m_DownloadStreamingDataControllers.Keys.Any(x => x.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId 
                        && x.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor))
                    {
                        m_DownloadStreamingAssetButton.DisplayOn();
                        m_DownloadStreamingAssetButton.ShowProgress();
                        m_OffloadAssetButton.DisplayOff();
                    }
                    else
                    {
                        var localVersion = int.Parse(directoryName.Split('_').Last());
                        
                        bool enable = false;
                        if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
                        {
                            enable = currentVersionNumber != localVersion && m_SourceDataSet != null && !ContainsDownloadController(assetInfo.Asset);
                        }
                        else
                        {
                            enable = currentVersionNumber != localVersion && (m_StreamingDataset != null) && !ContainsDownloadController(assetInfo.Asset);
                        }

                            AssetsInfoUIToolkitController.UpdateVersionVE.DisplayOn();
                            AssetsInfoUIToolkitController.UpdateVersionVE.ClearClassList();

                            AssetsInfoUIToolkitController.UpdateVersionVE.AddToClassList(localVersion == currentVersionNumber ? "LocalUpToUpdate" : "UpdateLocalVersion");
                            if (localVersion == currentVersionNumber)
                            {
                                AssetsInfoUIToolkitController.UpdateVersionButton.parent.DisplayOff();
                                AssetsInfoUIToolkitController.UpdateVersionVE.Q<Icon>().iconName = "download";
                                var text = AssetsInfoUIToolkitController.UpdateVersionVE.Q<Text>();
                                text.text = await m_ReadyForLocalStreamingLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                            }
                            else
                            {
                                AssetsInfoUIToolkitController.UpdateVersionButton.parent.DisplayOn();
                                AssetsInfoUIToolkitController.UpdateVersionButtonAction = OnDownloadButtonPress;
                                if (m_DownloadStreamingDataControllers.Keys.Any(x => x.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId &&
                                        x.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor))
                                {
                                    AssetsInfoUIToolkitController.UpdateVersionButton.SetEnabled(false);
                                }
                                else
                                {
                                    AssetsInfoUIToolkitController.UpdateVersionButton.SetEnabled(true);
                                }
                                AssetsInfoUIToolkitController.UpdateVersionVE.Q<Icon>().iconName = "download";
                                var text = AssetsInfoUIToolkitController.UpdateVersionVE.Q<Text>();
                                text.text = await m_UpdateLocalStreamingLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                                var button = AssetsInfoUIToolkitController.UpdateVersionVE.Q<Button>();
                                button.title = await m_UpdateLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                                //Also change the download text to update on button
                            }

                        m_DownloadStreamingAssetButton.SetDisplay(enable);
                        m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);
                        var display = currentVersionNumber == localVersion && !m_DownloadStreamingAssetButton.IsDisplayOn() && m_IsDownOffloadFunctionalityActive;
                        m_OffloadAssetButton.SetDisplay(display);
                        m_OffloadAssetButton.SetEnabled(m_OffloadAssetButton.IsDisplayOn());
                    }
                }
            }

            async Task CanDownloadStreamingAsset(AssetInfo assetInfo)
            {
                var canDownload = assetInfo.Properties.Value.IsLayout() || await HasStreamableDataset(assetInfo);
                m_DownloadStreamingAssetButton.SetDisplay(canDownload);
            }
        }

        private async void OnRemoveCacheButtonPress()
        {
            if(m_TopActionModal != null) return;
            
            bool isLayoutAsset = false;
            string orgId = string.Empty;
            string projectId = string.Empty;
            string assetId = string.Empty;
            
            if (!SharedUIManager.SelectedAsset.HasValue) return;

            if (SharedUIManager.SelectedAsset.Value.Asset is OfflineAsset offlineAsset)
            {
                isLayoutAsset = offlineAsset.OfflineAssetInfo.layout;
            }
            else
            {
                isLayoutAsset = SharedUIManager.SelectedAsset.Value.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag);
            }
            
            orgId = SharedUIManager.SelectedAsset.Value.Asset.Descriptor.OrganizationId.ToString();
            projectId = SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectId.ToString();
            assetId = SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId.ToString();

            var isReferencedAssets = false;
            
            if (!isLayoutAsset)
            {
                isReferencedAssets = CheckIfAssetIsAReferenceOfOtherDownloadedLayoutAssets(orgId, projectId, assetId);
            }
            
            LocalizedString description;
            if (!isReferencedAssets)
            {
                description = isLayoutAsset
                    ? m_RemoveLayoutAssetDescriptionLocalizedString
                    : m_RemoveAssetDescriptionLocalizedString;
            }
            else
            {
                description = m_RemoveReferencedAssetDescriptionLocalizedString;
            }
            
            var dialog = new AlertDialog()
            {
                title = await m_RemoveAssetTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await description.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Confirmation
            };
            
            dialog.SetPrimaryAction(97, await m_RemoveLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                if (isLayoutAsset)
                {
                    //Remove all referenced assets
                    
                    string hashFolderName = StreamingUtils.ReturnHashName(SharedUIManager.SelectedAsset.Value.Asset);
                    
                    var matchingFolders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolderName + "*");
                    var offlineLayoutJson = Path.Combine(StreamingUtils.LocalStreamingAssetPath, matchingFolders.First(),
                        StreamingUtils.LayoutJson);
                    
                    if (!File.Exists(offlineLayoutJson)) return;
                    
                    var json = File.ReadAllText(offlineLayoutJson);
                    var layoutJson = JsonConvert.DeserializeObject<LayoutJson>(json);
                    
                    var assetsSource = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
                    
                    List<AssetInfo> offlineAssetInfos = new List<AssetInfo>();
                    StreamingUtils.FindAllOfflineAssets(ref offlineAssetInfos, out _);
                    
                    var layoutAssets = offlineAssetInfos.Where(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.tags != null && ((OfflineAsset)x.Asset).OfflineAssetInfo.tags.Contains(StreamingUtils.LayoutTag))
                        .Where(x => !string.Equals(((OfflineAsset)x.Asset).OfflineAssetInfo.organizationId, orgId) ||
                                    !string.Equals(((OfflineAsset)x.Asset).OfflineAssetInfo.projectId, projectId) ||
                                    !string.Equals(((OfflineAsset)x.Asset).OfflineAssetInfo.assetId, assetId))
                        .ToList();

                    List<LayoutModelEntity> layoutModelEntitiesToSkip = null;
                    
                    foreach (var assetToBeDeletedInThisLayout in layoutJson.LayoutModels)
                    {
                        //Loop through all layout models and remove the asset, but checking if the asset is referenced in other layout assets
                        foreach (var layoutAsset in layoutAssets)
                        {
                            hashFolderName = StreamingUtils.ReturnHashName(layoutAsset.Asset);
                            matchingFolders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolderName + "*");
                            var tempOfflineLayoutJsonPath = Path.Combine(StreamingUtils.LocalStreamingAssetPath, matchingFolders.First(),
                                StreamingUtils.LayoutJson);
                            if (!File.Exists(tempOfflineLayoutJsonPath)) continue;
                            var layoutInfoInOtherLayout = JsonConvert.DeserializeObject<LayoutJson>(File.ReadAllText(tempOfflineLayoutJsonPath));
                            
                            if (layoutInfoInOtherLayout.LayoutModels.Any(x => string.Equals(x.assetID, assetToBeDeletedInThisLayout.assetID) &&
                                                                    string.Equals(x.projectID, assetToBeDeletedInThisLayout.projectID) &&
                                                                    string.Equals(x.orgID, assetToBeDeletedInThisLayout.orgID)))
                            {
                                layoutModelEntitiesToSkip ??= new List<LayoutModelEntity>();
                                if(layoutModelEntitiesToSkip.Any(x => string.Equals(x.assetID, assetToBeDeletedInThisLayout.assetID) &&
                                                                    string.Equals(x.projectID, assetToBeDeletedInThisLayout.projectID) &&
                                                                    string.Equals(x.orgID, assetToBeDeletedInThisLayout.orgID)))
                                {
                                    continue;
                                }
                                layoutModelEntitiesToSkip.Add(assetToBeDeletedInThisLayout);
                            }
                        }
                    }
                    
                    foreach (var layoutModelEntity in layoutJson.LayoutModels)
                    {
                        //Check if the asset is referenced in other layout assets and skip it if it is
                        if(layoutModelEntitiesToSkip != null && layoutModelEntitiesToSkip.Any(x => string.Equals(x.assetID, layoutModelEntity.assetID) &&
                                                                    string.Equals(x.projectID, layoutModelEntity.projectID) &&
                                                                    string.Equals(x.orgID, layoutModelEntity.orgID)))
                        {
                            continue;
                        }
                        
                        StreamingUtils.RemoveCache(layoutModelEntity, null);
                        if(assetsSource == null) continue;

                        if (assetsSource.Any(x => x.Asset.Descriptor.AssetId.ToString() == layoutModelEntity.assetID &&
                                                  x.Asset.Descriptor.ProjectId.ToString() ==
                                                  layoutModelEntity.projectID &&
                                                  x.Asset.Descriptor.OrganizationId.ToString() ==
                                                  layoutModelEntity.orgID))
                        {
                            AssetInfo assetInfo = assetsSource.FirstOrDefault(x => x.Asset.Descriptor.AssetId.ToString() == layoutModelEntity.assetID &&
                                x.Asset.Descriptor.ProjectId.ToString() == layoutModelEntity.projectID &&
                                x.Asset.Descriptor.OrganizationId.ToString() == layoutModelEntity.orgID);
                        
                            if (assetInfo == null) continue;
                            var index = GetAssetIndex(assetsSource, assetInfo);
                            if (index == -1) continue;
                            var item = SharedUIManager.Instance.AssetGridView.Q(SharedUIManager.ItemNameFromIndex(index));
                            if (item == null) continue;
                            var directDownloadAssetButton = item.Q<IconButton>(k_DirectDownload3DDSButtonName);
                            UpdateButtonClassList(directDownloadAssetButton, assetInfo, false);
                        }
                    }
                }

                RemoveDownloadedAsset();
            });

            if (isLayoutAsset)
            {
                dialog.SetSecondaryAction(96, await m_PreserveLocalizedString.GetTitleLocalizedStringForAppUIAsync(), RemoveDownloadedAsset);
            }
            
            dialog.SetCancelAction(0, await m_CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());
            m_TopActionModal = Modal.Build(m_AssetInfoPanelTop, dialog);
            m_TopActionModal.dismissed += TopActionModalOnDismissed;

            m_TopActionModal.Show();
            
            return;
            
            void TopActionModalOnDismissed(Modal arg1, DismissType arg2)
            {
                m_TopActionModal.dismissed -= TopActionModalOnDismissed;
                m_TopActionModal = null;
            }

            async void RemoveDownloadedAsset()
            {
                StreamingUtils.RemoveCache(SharedUIManager.SelectedAsset.Value.Asset, CallbackAfterRemovedCache);
                
                if (!NetworkDetector.RequestedOfflineMode)
                {
                    ShowStreamingAssetDownload(SharedUIManager.SelectedAsset.Value);
                    Refresh3DDSAssetUI(SharedUIManager.SelectedAsset.Value, false);
                }
                else
                {
                    var offlineAsset = StreamingUtils.ReturnOfflineAsset(SharedUIManager.SelectedAsset.Value.Asset.Descriptor.OrganizationId.ToString(),
                        SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectId.ToString(),
                        SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId.ToString());
                    
                    OfflineModeAssetsController.AssetOffloaded?.Invoke(SharedUIManager.SelectedAsset.Value);
                    
                    if (offlineAsset == null)
                    {
                        SharedUIManager.SelectedAsset = null;
                    }
                }
                
                var toast = Toast.Build(m_AssetInfoPanelTop, await m_Toast_AssetRemovedLocalizedString.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Short)
                    .SetStyle(NotificationStyle.Informative);
                
                toast.Show();
            }
        }

        private bool CheckIfAssetIsAReferenceOfOtherDownloadedLayoutAssets(string orgId, string projectId, string assetId)
        {
            List<AssetInfo> offlineAssetInfos = new List<AssetInfo>();
            StreamingUtils.FindAllOfflineAssets(ref offlineAssetInfos, out _);
                
            var layoutAssets = offlineAssetInfos.Where(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.tags != null && ((OfflineAsset)x.Asset).OfflineAssetInfo.tags.Contains(StreamingUtils.LayoutTag))
                .Where(x => !string.Equals(((OfflineAsset)x.Asset).OfflineAssetInfo.organizationId, orgId) ||
                            !string.Equals(((OfflineAsset)x.Asset).OfflineAssetInfo.projectId, projectId) ||
                            !string.Equals(((OfflineAsset)x.Asset).OfflineAssetInfo.assetId, assetId))
                .ToList();
            
            foreach (var offlineAssetInfo in layoutAssets)
            {
                var hashFolderName = StreamingUtils.ReturnHashName(offlineAssetInfo.Asset);
                var matchingFolders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolderName + "*");
                var layoutJsonPath = Path.Combine(matchingFolders.First(), StreamingUtils.LayoutJson);
                if (!File.Exists(layoutJsonPath))
                {
                    continue;
                }
                var json = File.ReadAllText(layoutJsonPath);
                var layoutJson = JsonConvert.DeserializeObject<LayoutJson>(json);
                foreach (var layoutModelEntity in layoutJson.LayoutModels)
                {
                    if(string.Equals(layoutModelEntity.orgID, orgId) && string.Equals(layoutModelEntity.projectID, projectId) && string.Equals(layoutModelEntity.assetID, assetId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        
        private void OnDownloadButtonPress()
        {
            if (m_DownloadStreamingAssetButton.userData is not AssetInfo assetInfo) return;
            m_DownloadStreamingAssetButton.SetEnabled(false);
            if (AssetsInfoUIToolkitController.UpdateVersionVE.style.display == DisplayStyle.Flex)
            {
                AssetsInfoUIToolkitController.UpdateVersionButton.SetEnabled(false);
            }
            DownloadAsset(assetInfo);
        }
        
        private bool ContainsDownloadController(IAsset asset)
        {
            if(m_DownloadStreamingDataControllers == null) return false;
            foreach (var key in m_DownloadStreamingDataControllers.Keys)
            {
                if (string.Equals(key.Descriptor.AssetId.ToString(), asset.Descriptor.AssetId.ToString()) &&
                    string.Equals(key.Descriptor.ProjectId.ToString(), asset.Descriptor.ProjectId.ToString()) &&
                    string.Equals(key.Descriptor.OrganizationId.ToString(), asset.Descriptor.OrganizationId.ToString()))
                {
                    return true;
                }
            }
            return false;
        }
        
        private async void OnDownloadCacheFinished(AssetInfo assetInfo)
        {
            StreamingUtils.MakeTempFolderComplete(assetInfo.Asset);
            
            Refresh3DDSAssetUI(assetInfo, true);
            if(m_DownloadStreamingDataControllers.TryGetValue(assetInfo.Asset, out var downloadStreamingDataController))
            {
                downloadStreamingDataController.DownloadProgress -= OnDownloadProgress;
                m_DownloadStreamingDataControllers.Remove(assetInfo.Asset);
            }
            
            var toast = Toast.Build(SharedUIManager.Instance.AssetGridView, await m_Toast_FinishDownloadLocalizedString.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Short)
                .SetStyle(NotificationStyle.Informative);
            
            var messageLabel = toast.view.Q<LocalizedTextElement>(UIUtility.k_ToastMessageName);
            messageLabel.variables = new object[]
            {
                new Dictionary<string, object>()
                {
                    { "name", assetInfo.Properties.Value.Name }
                }
            };

            toast.Show();

            if (!SharedUIManager.SelectedAsset.HasValue ||
                SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId != assetInfo.Asset.Descriptor.AssetId ||
                SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectDescriptor != assetInfo.Asset.Descriptor.ProjectDescriptor)
            {
                return;
            }

            //User can be reviewing a different version of the asset.
            m_DownloadStreamingAssetButton.HideProgress();
            m_DownloadProgress.style.display = DisplayStyle.None;
            ShowStreamingAssetDownload(SharedUIManager.SelectedAsset.Value);
        }
        
#endif
        
        private void OnDownloadProgress(IAsset asset, float progress)
        {
            if (!SharedUIManager.SelectedAsset.HasValue ||
                m_AssetInfoPanel == null ||
                (SharedUIManager.SelectedAsset.HasValue && SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId != asset.Descriptor.AssetId) ||
                 (SharedUIManager.SelectedAsset.HasValue && SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId == asset.Descriptor.AssetId
                                                         && SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectDescriptor != asset.Descriptor.ProjectDescriptor) ||
                 (m_AssetInfoPanel != null && m_AssetInfoPanel.style.display == DisplayStyle.None))
            {
                return;
            }
            m_DownloadStreamingAssetButton.style.display = DisplayStyle.Flex;
            m_DownloadStreamingAssetButton?.ShowProgress();
            m_DownloadProgress.value = Mathf.Min(progress, 1);
            if (AssetsInfoUIToolkitController.UpdateVersionVE.style.display == DisplayStyle.Flex)
            {
                AssetsInfoUIToolkitController.UpdateVersionButton.SetEnabled(false);
            }
        }
        
        private void CallbackAfterRemovedCache()
        {
            if (NetworkDetector.IsOffline) return;
            if(m_DownloadStreamingAssetButton == null) return;
            m_DownloadStreamingAssetButton.style.display = DisplayStyle.Flex;
            m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);
            m_OffloadAssetButton.DisplayOff();
        }

        private int GetAssetIndex(List<AssetInfo> assetList, AssetInfo assetToDownload)
        {
            return assetList.FindIndex(x => x.Asset.Descriptor.AssetId == assetToDownload.Asset.Descriptor.AssetId &&
                                            x.Asset.Descriptor.ProjectDescriptor == assetToDownload.Asset.Descriptor.ProjectDescriptor);
        }
        
        private IconButton GetDirectDownloadButton(int index)
        {
            if (index < 0) return null;

            var name = SharedUIManager.ItemNameFromIndex(index);
            var assetItem = SharedUIManager.Instance.AssetGridView.Q(name);
            return assetItem?.Q<IconButton>(k_DirectDownload3DDSButtonName);
        }
        
        private void DisableDirectDownloadButton(IconButton button)
        {
            button?.SetEnabled(false);
        }
        
        private void DownloadAsset(AssetInfo assetToDownload)
        {
            SharedUIManager.Instance.AssetGridView.SetEnabled(false);
            var assetList = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
            if(assetList == null) return;
            var index = GetAssetIndex(assetList, assetToDownload);
            var directDownloadAssetButton = GetDirectDownloadButton(index);
            DisableDirectDownloadButton(directDownloadAssetButton);
            
            IDataset datasetToDownload = null;
            _ = GetRightDataset();
            return;

            async Task GetRightDataset()
            {
                if (assetToDownload.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
                {
                    datasetToDownload = await assetToDownload.Asset.GetSourceDatasetAsync(CancellationToken.None);
                }
                else
                {
                    var cacheConfigurations = assetToDownload.Asset.CacheConfiguration;
                    cacheConfigurations.DatasetCacheConfiguration = new DatasetCacheConfiguration()
                    {
                        CacheProperties = true
                    };
                    var newAssetWithCacheConfig = await assetToDownload.Asset.WithCacheConfigurationAsync(cacheConfigurations, CancellationToken.None);
                    var allDatasets = newAssetWithCacheConfig.ListDatasetsAsync(Range.All, CancellationToken.None);
                    await foreach (var dataset in allDatasets)
                    {
                        var datasetProperties = await dataset.GetPropertiesAsync(CancellationToken.None);
                        if (!datasetProperties.SystemTags.Contains(StreamingUtils.StreamableTag)) continue;
                        datasetToDownload = dataset;
                        break;
                    }
                }

                ShowDownloadModal();
            }
            
            async void ShowDownloadModal()
            {
                if (datasetToDownload == null)
                {
                    Debug.Log("No dataset found for asset " + assetToDownload.Asset.Descriptor.AssetId);
                    return;
                }
                var customDialog = new AlertDialog()
                {
                    title = await m_DownloadAssetTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                    description = await m_DownloadAssetDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                    variant = AlertSemantic.Confirmation
                };
                
                customDialog.SetPrimaryAction(95, await m_DownloadLocalizedString.GetTitleLocalizedStringForAppUIAsync(), PrimaryAction);
                
                customDialog.SetCancelAction(0, await m_CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());
                
                var modal = Modal.Build(SharedUIManager.Instance.AssetGridView, customDialog);
                modal.dismissed += OnModalDismissed;

                modal.Show();
            }

            async void PrimaryAction()
            {
                StreamingUtils.RemoveCache(assetToDownload.Asset, CallbackAfterRemovedCache);
                m_DownloadStreamingAssetButton?.SetEnabled(false);

                var toast = Toast.Build(SharedUIManager.Instance.AssetGridView, await m_Toast_DownloadingAssetLocalizedString.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Short)
                    .SetStyle(NotificationStyle.Informative);

                toast.Show();
                    
                var downloadStreamingDataController = new DownloadStreamingDataController(assetToDownload, datasetToDownload);
                m_DownloadStreamingDataControllers ??= new Dictionary<IAsset, DownloadStreamingDataController>();
                m_DownloadStreamingDataControllers.TryAdd(assetToDownload.Asset, downloadStreamingDataController);
                downloadStreamingDataController.DownloadProgress += OnDownloadProgress;
            }
            
            void OnModalDismissed(Modal arg1, DismissType arg2)
            {
                arg1.dismissed -= OnModalDismissed;
                SharedUIManager.Instance.AssetGridView.SetEnabled(true);
                if (arg2 == DismissType.Manual)
                {
                    var assetList = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
                    if(assetList == null) return;
                    m_DownloadStreamingAssetButton?.SetEnabled(true);
                    directDownloadAssetButton?.SetEnabled(true);
                    if(AssetsInfoUIToolkitController.UpdateVersionVE.style.display == DisplayStyle.Flex &&
                       AssetsInfoUIToolkitController.UpdateVersionButton.parent.style.display == DisplayStyle.Flex)
                    {
                        AssetsInfoUIToolkitController.UpdateVersionButton.SetEnabled(true);
                    }
                }
            }
        }
        
        private void Refresh3DDSAssetUI(AssetInfo assetInfo, bool downloaded)
        {
            var assetsItem = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
            if (assetsItem == null) return;
            
            var index = GetAssetIndex(assetsItem, assetInfo);
            if (index == -1) return;
            
            var item = SharedUIManager.Instance.AssetGridView.Q(SharedUIManager.ItemNameFromIndex(index));
            if (item == null) return;
                
            var directDownloadAssetButton = item.Q<IconButton>(k_DirectDownload3DDSButtonName);
            
            UpdateButtonClassList(directDownloadAssetButton, assetInfo, downloaded);
        }
        
        private static bool IsVersionMismatch(AssetInfo assetInfo)
        {
            var assetsItem = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
            var assetFromSource = assetsItem?.FirstOrDefault(x => x.Asset.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId
                                                                  && x.Asset.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor);
            
            return assetFromSource != null && assetFromSource.Value.Properties.Value.FrozenSequenceNumber != assetInfo.Properties.Value.FrozenSequenceNumber;
        }
        
        private void UpdateButtonClassList(IconButton button, AssetInfo assetInfo, bool downloaded)
        {
            RemoveClass(button, "notDownloaded");
            RemoveClass(button, "incorrectVersion");
            RemoveClass(button, "downloaded");
            if (downloaded)
            {
                if (IsVersionMismatch(assetInfo))
                {
                    AddClass(button, "incorrectVersion");
                    button.SetEnabled(true);
                }
                else
                {
                    AddClass(button, "downloaded");
                }
            }
            else
            {
                button.SetEnabled(true);
                AddClass(button, "notDownloaded");
            }
            
            return;
            
            void RemoveClass(IconButton button, string className)
            {
                if (button.ClassListContains(className))
                {
                    button.RemoveFromClassList(className);
                }
            }

            void AddClass(IconButton button, string className)
            {
                if (!button.ClassListContains(className))
                {
                    button.AddToClassList(className);
                }
            }
        }

        private void RefreshUI()
        {
            if (!hasInitiated)
            {
                hasInitiated = true;
                m_AssetBar = m_StreamingAssetBarTemplate.Instantiate().Children().First();
                m_AssetInfoPanelTop.Add(m_AssetBar);

                if (m_IsStreamFunctionalityActive)
                {
                    ActivateStreamFunctionality();
                }
                
                m_CopyDeepLinkButton = m_AssetBar.Q<ActionButton>(k_CopyDeepLinkButtonName);
                m_CopyDeepLinkButton.clicked += OnCopyDeepLinkButtonPress;
                
                m_DownloadStreamingAssetButton = m_AssetBar.Q<ProgressActionButton>(k_DownloadStreamingAssetButtonName);
                m_DownloadProgress = m_DownloadStreamingAssetButton.Q<CircularProgress>();
                
                m_OffloadAssetButton = m_AssetBar.Q<ActionButton>(k_OffloadAssetButtonName);
                
#if !UNITY_WEBGL || UNITY_EDITOR
                m_OffloadAssetButton.clicked += OnRemoveCacheButtonPress;
                m_DownloadStreamingAssetButton.clicked += OnDownloadButtonPress;
#elif UNITY_WEBGL && !UNITY_EDITOR
                m_DownloadStreamingAssetButton.DisplayOff();
                m_DownloadStreamingAssetButton.HideProgress();
                m_DownloadProgress.DisplayOff();
#endif
            }

            var icon = m_DownloadStreamingAssetButton.Q<Icon>();
            icon.DisplayOn();

            m_StreamButton?.DisplayOff();

#if !UNITY_WEBGL || UNITY_EDITOR
            m_DownloadStreamingAssetButton?.HideProgress();
            m_DownloadStreamingAssetButton?.DisplayOff();
            m_OffloadAssetButton?.DisplayOff();
#endif
        }

        private void OnParentAssetSelected(AssetInfo? assetInfo)
        {
            if (assetInfo == null)
            {
                return;
            }

            AssetSelected(assetInfo.Value);
        }
        
        private void AssetSelected(AssetInfo assetInfo)
        {
            if (assetInfo.Asset == null
                || (m_currentOpenedAsset != null && assetInfo.Asset.Descriptor == m_currentOpenedAsset.Descriptor))
            {
                return;
            }

            m_currentOpenedAsset = assetInfo.Asset;
            RefreshUI();

            if (assetInfo.Asset is OfflineAsset offlineAsset)
            {
                var hashFolderName = StreamingUtils.ReturnHashName(assetInfo.Asset);
                var matchingFolders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolderName + "*");
                if(matchingFolders.Length == 0) return;
                if (matchingFolders.All(x => new DirectoryInfo(x).Name.Contains("_temp")))
                {
                    return;
                }
                
                var folder = matchingFolders.FirstOrDefault(x => (new DirectoryInfo(x).Name).Contains("_temp") == false);
                if (folder == null) return;
                
                m_OffloadAssetButton.SetEnabled(true);
                m_OffloadAssetButton.DisplayOn();

                if (offlineAsset.OfflineAssetInfo.layout)
                {
                    var layoutJson = Path.Combine(folder, StreamingUtils.LayoutJson);
                    if (File.Exists(layoutJson))
                    {
                        ShowAndEnableStreamLayoutButton();
                    }
                }
                else
                {
                    var titleJson = Path.Combine(folder, StreamingUtils.TilesetJson);
                    if (File.Exists(titleJson))
                    {
                        ShowAndEnableStreamButton();
                    }
                }
                return;
            }

            if (assetInfo.Properties.Value.IsLayout())
            {
                ShowAndEnableStreamLayoutButton();
#if !UNITY_WEBGL || UNITY_EDITOR
                _ = GetSourceDataset(assetInfo);
#endif
                return;
            }

            m_StreamingDataset = null;
            _ = ProcessDatasets(assetInfo);
        }
        
#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task GetSourceDataset(AssetInfo assetInfo)
        {
            m_SourceDataSet = await assetInfo.Asset.GetSourceDatasetAsync(CancellationToken.None);
            ShowStreamingAssetDownload(assetInfo);
        }
#endif

        private static async IAsyncEnumerable<(IDataset dataset, DatasetProperties properties)> GetAllDatasetsAndProperties(AssetInfo assetInfo)
        {
            var cacheConfigurations = assetInfo.Asset.CacheConfiguration;
            cacheConfigurations.DatasetCacheConfiguration = new DatasetCacheConfiguration()
            {
                CacheProperties = true
            };

            var newAssetWithCacheConfig = await assetInfo.Asset.WithCacheConfigurationAsync(cacheConfigurations, CancellationToken.None);
            var listOfDatasets = newAssetWithCacheConfig.ListDatasetsAsync(Range.All, CancellationToken.None);

            await foreach (var dataset in listOfDatasets)
            {
                yield return (dataset, await dataset.GetPropertiesAsync(CancellationToken.None));
            }
        }

        private static async Task<IDataset> GetStreamableDataset(AssetInfo assetInfo)
        {
            await foreach (var datasetAndProperties in GetAllDatasetsAndProperties(assetInfo))
            {
                if (datasetAndProperties.properties.IsStreamable())
                {
                    return datasetAndProperties.dataset;
                }
            }

            return null;
        }

        public static async Task<bool> HasStreamableDataset(AssetInfo assetInfo)
        {
            return await GetStreamableDataset(assetInfo) != null;
        }

        private async Task ProcessDatasets(AssetInfo assetInfo)
        {
            bool hasStreamableDataset = false;
            IDataset sourceDataset = null;
            IDataset previewDataset = null;

            await foreach (var datasetAndProperties in GetAllDatasetsAndProperties(assetInfo))
            {
                var datasetProperties = datasetAndProperties.properties;

                if (datasetProperties.IsSource())
                {
                    sourceDataset = datasetAndProperties.dataset;
                }
                else if (datasetProperties.IsPreview())
                {
                    previewDataset = datasetAndProperties.dataset;
                }
                else if (datasetProperties.IsStreamable())
                {
                    m_StreamingDataset = datasetAndProperties.dataset;
                    hasStreamableDataset = true;
                }

                if (hasStreamableDataset && sourceDataset != null && previewDataset != null)
                {
                    break;
                }
            }

            if (hasStreamableDataset)
            {
                ShowAndEnableStreamButton();
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            ShowStreamingAssetDownload(assetInfo);
#endif
            
            if(hasStreamableDataset) return;

            //If there is no 3DDS transformation, then trigger 3DDS transformation
            /*if (sourceDataset != null)
            {
                bool hasIn3DdsTransformation = false;
                IAsyncEnumerable<ITransformation> transformations = sourceDataset.ListTransformationsAsync(Range.All, CancellationToken.None);
                await foreach(var transformation in transformations)
                {
                    if (transformation.Status is TransformationStatus.Pending or TransformationStatus.Running &&
                        transformation.WorkflowType == WorkflowType.Data_Streaming)
                    {
                        hasIn3DdsTransformation = true;
                        return;
                    }
                }

                if (hasIn3DdsTransformation)
                {
                    //Show("Transformation is already in progress");
                }
                else
                {
                    Debug.Log("Triggering 3DDS Transformation");
                    AssetsController.Trigger3DDSTransformation.Invoke(asset, b =>
                    {
                        Debug.Log("Done " + b);
                    });
                }
            }*/
            
//#if UNITY_WEBGL && !UNITY_EDITOR
            return;
//#endif
            
            if (sourceDataset != null)
            {
                var hasGLB = await StreamingUtils.HasGLBFile(sourceDataset);
                if (hasGLB)
                {
#if !UNITY_WEBGL || UNITY_EDITOR
                    ShowStreamingAssetDownload(assetInfo);
#endif
                    return;
                }
            }
            
            if (previewDataset != null)
            {
                var hasGLB = await StreamingUtils.HasGLBFile(previewDataset);
                if (hasGLB)
                {
#if !UNITY_WEBGL || UNITY_EDITOR
                    ShowStreamingAssetDownload(assetInfo);
#endif
                    return;
                }
            }
            return;
        }

        private async void ShowAndEnableStreamButton()
        {
            if (m_StreamButton == null) return;

            m_StreamButton.DisplayOn();
            m_StreamButton.label = await m_StreamLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            m_StreamButton.SetEnabled(true);
        }

        private async void ShowAndEnableStreamLayoutButton()
        {
            if (m_StreamButton == null) return;

            m_StreamButton.DisplayOn();
            m_StreamButton.label = await m_LoadLayoutLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            m_StreamButton.SetEnabled(true);
        }

        private void OnCopyDeepLinkButtonPress()
        {
            DeepLinkCreationHandler?.Invoke(m_CopyDeepLinkButton);
        }

        private void DirectStreamAsset(AssetInfo assetInfo)
        {
            if (m_DirectStreamAssetModal != null)
            {
                return;
            }

            var directionStreamPopup = m_DirectStreamAssetModalTemplate.Instantiate().Children().First();
            
            m_DirectStreamAssetModal = Modal.Build(SharedUIManager.Instance.AssetGridView, directionStreamPopup);
            m_DirectStreamAssetModal.dismissed += ModalOndismissed;
            m_DirectStreamAssetModal.shown += DirectStreamAssetModalOnShown;
            
            m_DirectStreamAssetModal.Show();
            return;
            
            async void DirectStreamAssetModalOnShown(Modal obj)
            {
                m_DirectStreamAssetModal.shown -= DirectStreamAssetModalOnShown;
                var closeButton = obj.contentView.Q<IconButton>();
                closeButton.clicked += () =>
                {
                    m_DirectStreamAssetModal.Dismiss();
                };

                var verBox = obj.contentView.Q<Text>(k_VerBoxName);
                
                var assetLabel = obj.contentView.Q<Text>("Asset-Label");

                var versionDropdown = obj.contentView.Q<Dropdown>();
                versionDropdown.bindItem = DirectStreamDropdownBindItem;
                versionDropdown.bindTitle = DirectStreamDropdownBindTitle;
                var isLayout = false;
                
                var actionButton = obj.contentView.Q<ActionButton>();
                
                versionDropdown.SetEnabled(false);
                
                if (assetInfo.Asset is OfflineAsset offlineAsset)
                {
                    isLayout = offlineAsset.OfflineAssetInfo.layout;
                    assetLabel.text = offlineAsset.OfflineAssetInfo.assetName;
                    versionDropdown.sourceItems = new List<AssetInfo>() { assetInfo };
                    versionDropdown.SetValueWithoutNotify(new int[] {0});
                    verBox.text = $"Ver. {offlineAsset.OfflineAssetInfo.assetVersion}";
                    actionButton.SetEnabled(true);
                }
                else
                {
                    assetLabel.text = assetInfo.Properties.Value.Name;
                    verBox.text = $"Ver. {assetInfo.Properties.Value.FrozenSequenceNumber}";
                    isLayout = assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag);
                    AssetsController.AssetVersionRequest.Invoke(assetInfo.Asset, OnAssetVersionsLoaded);
                    if (!isLayout)
                    {
                        _ = CheckStreamableDataset(assetInfo);
                    }
                    actionButton.SetEnabled(isLayout);
                }
                
                actionButton.label =
                    isLayout
                        ? await m_LoadLayoutLocalizedString.GetTitleLocalizedStringForAppUIAsync()
                        : await m_StreamLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                
                actionButton.clicked += () =>
                {
                    var assets = versionDropdown.sourceItems as List<AssetInfo>;
                    if (assets == null || assets.Count == 0)
                    {
                        return;
                    }
                    if (versionDropdown.value == null || !versionDropdown.value.Any())
                    {
                        return;
                    }
                    var selectedIndex = versionDropdown.value.First();
                    if (selectedIndex < 0 || selectedIndex >= assets.Count)
                    {
                        return;
                    }
                    var asset = assets[selectedIndex];
                    if (asset.Asset is not OfflineAsset)
                    {
                        if (selectedIndex != 0)
                        {
                            AssetsController.ParentAssetSelected?.Invoke(assets[0]);
                        }
                        AssetsController.AssetSelected?.Invoke(asset);
                    }
                    StreamAsset(asset);
                    m_DirectStreamAssetModal.Dismiss();
                };
                
                async void DirectStreamDropdownBindItem(DropdownItem arg1, int arg2)
                {
                    var assets = versionDropdown.sourceItems as List<AssetInfo>;
                    if (assets == null || assets.Count == 0)
                    {
                        return;
                    }
                    if (arg2 < 0 || arg2 >= assets.Count)
                    {
                        return;
                    }
                    
                    var asset = assets[arg2];
                    
                    int verNum = 0;
                    
                    if(asset.Asset is OfflineAsset offlineAsset)
                    {
                        verNum = offlineAsset.OfflineAssetInfo.assetVersion;
                    }
                    else
                    {
                        verNum = asset.Properties.Value.FrozenSequenceNumber;
                    }
            
                    var text = arg1.Q<LocalizedTextElement>();
                    text.text = $"@{k_AssetLocalisedTable}:{k_VersionKey}";

                    text.variables = new object[]
                    {
                        new Dictionary<string, object>()
                        {
                            { "num", verNum }
                        }
                    };
                }
                
                void DirectStreamDropdownBindTitle(DropdownItem arg1, IEnumerable<int> arg2)
                {
                    if(arg2 == null || !arg2.Any())
                    {
                        //arg1.label = versionDropdown.defaultMessage;
                        return;
                    }
                    DirectStreamDropdownBindItem(arg1, arg2.First());
                }
                
                void OnAssetVersionsLoaded(List<AssetInfo> assetInfos)
                {
                    versionDropdown.SetEnabled(true);
                    versionDropdown.sourceItems = assetInfos;
                    if (SharedUIManager.SelectedAsset.HasValue && IsSameAssetButDifferentVersion(SharedUIManager.SelectedAsset.Value, assetInfo))
                    {
                        var assetId = SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId;
                        var assetVersionId = SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetVersion;
                        var selectedIndex = assetInfos.FindIndex(x => x.Asset.Descriptor.AssetId == assetId &&
                                                                     x.Asset.Descriptor.AssetVersion == assetVersionId);
                        versionDropdown.SetValueWithoutNotify(selectedIndex >= 0
                            ? new int[] { selectedIndex }
                            : new int[] { 0 });
                        verBox.text = $"Ver.{SharedUIManager.SelectedAsset.Value.Properties.Value.FrozenSequenceNumber}";
                    }
                    else
                    {
                        versionDropdown.SetValueWithoutNotify(new int[] {0});
                    }
                    
                    versionDropdown.RegisterValueChangedCallback(OnDirectStreamDropdownValueChanged);
                }

                _ = TextureDownload.DownloadThumbnail(assetInfo.Asset, OnTextureDownloadedForDirectStream);
            }
            
            async Task CheckStreamableDataset(AssetInfo assetInfo)
            {
                var hasStreamableDataset = await HasStreamableDataset(assetInfo);
                var actionButton = m_DirectStreamAssetModal.contentView.Q<ActionButton>();
                actionButton.SetEnabled(hasStreamableDataset);
                if (!actionButton.enabledSelf)
                {
                    actionButton.tooltip = await m_StreamingDataNotAvailableLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                }
            }
            
            void OnDirectStreamDropdownValueChanged(ChangeEvent<IEnumerable<int>> evt)
            {
                if (evt.newValue == null || !evt.newValue.Any())
                {
                    return;
                }
                var versionDropdown = m_DirectStreamAssetModal.contentView.Q<Dropdown>();
                var assets = versionDropdown.sourceItems as List<AssetInfo>;
                if (assets == null || assets.Count == 0)
                {
                    return;
                }
                
                var selectedIndex = evt.newValue.First();
                if (selectedIndex < 0 || selectedIndex >= assets.Count)
                {
                    return;
                }
                
                
                var selectedAsset = assets[selectedIndex];
                var verBox = m_DirectStreamAssetModal.contentView.Q<Text>(k_VerBoxName);
                
                verBox.text = $"Ver.{selectedAsset.Properties.Value.FrozenSequenceNumber}";
                
                var actionButton = m_DirectStreamAssetModal.contentView.Q<ActionButton>();
                
                var isLayout = assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag);
                if (isLayout)
                {
                    actionButton.SetEnabled(true);
                }
                else
                {
                    actionButton.SetEnabled(false);
                    _ = CheckStreamableDataset(selectedAsset);
                }
                
                _ = TextureDownload.DownloadThumbnail(selectedAsset.Asset, OnTextureDownloadedForDirectStream);
            }
            
            void OnTextureDownloadedForDirectStream(Texture2D texture2D)
            {
                var image = m_DirectStreamAssetModal.contentView.Q<VisualElement>("Popup-Image");
                if (texture2D != null)
                {
                    image.style.backgroundImage = texture2D;
                }
            }
            
            void ModalOndismissed(Modal arg1, DismissType arg2)
            {
                arg1.dismissed -= ModalOndismissed;
                var versionDropdown = arg1.contentView.Q<Dropdown>();
                versionDropdown.UnregisterValueChangedCallback(OnDirectStreamDropdownValueChanged);
                if (arg2 == DismissType.Manual)
                {
                    var assetsItemSource = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
                    if (assetsItemSource == null) return;
                    
                    var index = GetAssetIndex(assetsItemSource, assetInfo);
                    if (index < 0) return;
                    
                    var item = SharedUIManager.Instance.AssetGridView.Q(SharedUIManager.ItemNameFromIndex(index));
                    if (item == null) return;
                    
                    var directStreamAssetButton = item.Q<ActionButton>(k_3DDSButtonName);
                    directStreamAssetButton?.SetEnabled(true);
                }

                m_DirectStreamAssetModal = null;
            }
        }
        
        private void StreamAsset()
        {
            StreamAsset(SharedUIManager.SelectedAsset);
        }

        
        private async void StreamAsset(AssetInfo? assetInfo)
        {
            if (!assetInfo.HasValue || m_TopActionModal != null || !m_IsStreamFunctionalityActive)
            {
                return;
            }
            
#if !UNITY_WEBGL || UNITY_EDITOR
            if (assetInfo.Value.Asset is not OfflineAsset)
            {
                bool hasLocalData = StreamingUtils.CheckHasLocalAsset(assetInfo.Value.Asset, true, out var ver);

                if (hasLocalData)
                {
                    var dialog = new AlertDialog()
                    {
                        title = await m_PickDataTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                        description = await m_PickDataDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                        variant = AlertSemantic.Default
                    };
                    
                    dialog.SetPrimaryAction(94, await m_CloudLocalizedString.GetTitleLocalizedStringForAppUIAsync(), StartStreamingScene);

                    dialog.primaryButton.leadingIcon = "broadcast";
                    
                    dialog.SetSecondaryAction(93, await m_LocalLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
                    {
                        var offlineAsset =
                            StreamingUtils.ReturnOfflineAssetInfo(assetInfo.Value.Asset);
                        StreamingModelController.StreamingAsset = new AssetInfo()
                        {
                            Asset = offlineAsset,
                            Properties = null
                        };
                        MainSceneController.StartStreaming?.Invoke();
                    });
                    
                    dialog.SetCancelAction(0, await m_CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());
                    m_TopActionModal = Modal.Build(m_StreamButton, dialog);
                    
                    m_TopActionModal.dismissed += TopActionModalOnDismissed;

                    m_TopActionModal.Show();
                }
                else
                {
                    StartStreamingScene();
                }
            }
            else
            {
                StartStreamingScene();
            }
#else
            StartStreamingScene();
#endif

            void StartStreamingScene()
            {
                StreamingModelController.StreamingAsset = assetInfo.Value;
                MainSceneController.StartStreaming?.Invoke();
            }
            
            void TopActionModalOnDismissed(Modal arg1, DismissType arg2)
            {
                m_TopActionModal.dismissed -= TopActionModalOnDismissed;
                m_TopActionModal = null;
            }
        }
        
        public static bool IsSameAssetButDifferentVersion(AssetInfo a, AssetInfo b)
        {
            if (a.Asset.Descriptor.AssetId == b.Asset.Descriptor.AssetId
                && a.Asset.Descriptor.ProjectId == b.Asset.Descriptor.ProjectId
                && a.Asset.Descriptor.OrganizationId == b.Asset.Descriptor.OrganizationId)
            {
                return true;
            }

            return false;
        }
    }
}
