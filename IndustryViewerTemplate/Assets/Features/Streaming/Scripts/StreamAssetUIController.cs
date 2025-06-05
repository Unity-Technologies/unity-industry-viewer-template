using System;
using UnityEngine.Localization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.AppUI.Core;
using Unity.Industry.Viewer.Assets;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Unity.Industry.Viewer.Shared;
using Newtonsoft.Json;
using Unity.Industry.Viewer.Identity;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using Button = Unity.AppUI.UI.Button;
using Task = System.Threading.Tasks.Task;

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
            m_DownloadAssetAction?.Invoke(m_Asset.Value);
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
        
        [SerializeField]
        StyleSheet m_StreamingAssetBarStyle, m_assetItemStyle;
        
        private const string k_AssetInfoPanelRootName = "AssetInfoContainer";
        private const string k_AssetInfoPanelName = "AssetInfoPanelRoot";
        private const string k_StreamAssetButtonName = "StreamAssetButton";
        private const string k_OffloadAssetButtonName = "OffloadAssetButton";
        private const string k_CopyLinkButtonName = "CopyLinkButton";
        private const string k_AssetInfoPanelTopName = "AssetInfoPanelTop";
        private const string k_DownloadStreamingAssetButtonName = "DownloadStreamingAssetButton";
        private const string k_DirectDownload3DDSButtonName = "DirectDownload3DDSButton";
        private const string k_3DDSButtonName = "3DDSButton";

        [SerializeField]
        private VisualTreeAsset m_AssetItem3DDSUITemplate;
        
        [SerializeField]
        private VisualTreeAsset m_StreamingAssetBarTemplate;
        
        private VisualElement m_AssetInfoPanelRoot;
        private VisualElement m_AssetInfoPanel;
        private VisualElement m_AssetInfoPanelTop;
        
        private ActionButton m_StreamButton;
        public ActionButton OffloadAssetButton => m_OffloadAssetButton;
        private ActionButton m_OffloadAssetButton;
        private ActionButton m_CopyButton;
        
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

        #region Localisation

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
        
        [SerializeField]
        private LocalizedString m_DirectStreamingTitleLocalizedString;
        
        [SerializeField]
        private LocalizedString m_DirectStreamingDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_CopyLinkToClipboardLocalizedString;
        
        #endregion
        
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
            
            if (m_CopyButton != null)
            {
                m_CopyButton.clicked -= OnCopyLinkButtonPress;
            }
            if (AssetsInfoUIToolkitController != null)
            {
                AssetsInfoUIToolkitController.UpdateVersionButtonAction = null;
            }
            
#if !UNITY_WEBGL || UNITY_EDITOR
            DownloadStreamingDataController.KeepExistingAssets -= OnToAskToKeepExistingAssets;
            CancelDownloads();
            
            OfflineModeAssetsController.AssetSelected -= AssetSelected;
            
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
        private void OnToAskToKeepExistingAssets(string assetName, int requiredVersion, int localVersion, Action<bool> keepActionCallback)
        {
            if (m_KeepOrUpdateLocalAssetDescriptionLocalizedString.TryGetValue("name", out var assetNameValue))
            {
                ((StringVariable)assetNameValue).Value = assetName;
            }
            else
            {
                m_KeepOrUpdateLocalAssetDescriptionLocalizedString.Add("name", new StringVariable() {Value = assetName});
            }
                
            if (m_KeepOrUpdateLocalAssetDescriptionLocalizedString.TryGetValue("localVersion", out var localVersionValue))
            {
                ((IntVariable)localVersionValue).Value = localVersion;
            }
            else
            {
                m_KeepOrUpdateLocalAssetDescriptionLocalizedString.Add("localVersion", new IntVariable() {Value = localVersion});
            }
                
            if (m_KeepOrUpdateLocalAssetDescriptionLocalizedString.TryGetValue("requiredVersion", out var requiredVersionValue))
            {
                ((IntVariable)requiredVersionValue).Value = requiredVersion;
            }
            else
            {
                m_KeepOrUpdateLocalAssetDescriptionLocalizedString.Add("requiredVersion", new IntVariable() {Value = requiredVersion});
            }
            
            var dialog = new CustomAlertDialog(m_DownloadAssetTitleLocalizedString, m_KeepOrUpdateLocalAssetDescriptionLocalizedString)
            {
                variant = AlertSemantic.Confirmation
            };
            
            dialog.SetPrimaryAction(m_PreserveLocalizedString, true, () =>
            {
                keepActionCallback?.Invoke(true);
            });

            dialog.SetSecondaryAction(m_RemoveLocalizedString, false, () =>
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
            
            if (IdentityController.GuestMode)
            {
                directDownloadAssetButton.style.display = DisplayStyle.None;
            }
            else
            {
                if (assetInfo.Value.Asset is OfflineAsset || Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    directDownloadAssetButton.style.display = DisplayStyle.None;
                    //directDownloadAssetButton.SetEnabled(false);
                }
                else
                {
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
                    var hasDownloaded = StreamingUtils.CheckHasLocalAsset(assetInfo.Value.Asset, out var ver);
                    if (hasDownloaded)
                    {
                        var versionMatched = ver == assetInfo.Value.Properties.Value.FrozenSequenceNumber;
                        directDownloadAssetButton.AddToClassList(versionMatched? "downloaded" : "incorrectVersion");
                        if (!versionMatched)
                        {
                            //Enable download button
    #if !UNITY_WEBGL || UNITY_EDITOR
                            directDownloadAssetButton.SetEnabled(true);
                            directDownloadAssetButton.clicked -= controller.OnDownloadAsset;
                            directDownloadAssetButton.clicked += controller.OnDownloadAsset;
    #endif
                        }
                        else
                        {
                            //directDownloadAssetButton.style.display = DisplayStyle.None;
                            directDownloadAssetButton.SetEnabled(false);
                        }
                    }
                    else
                    {
                        directDownloadAssetButton.AddToClassList("notDownloaded");
                        //Enable download button
    #if !UNITY_WEBGL || UNITY_EDITOR
                        directDownloadAssetButton.SetEnabled(true);
                        directDownloadAssetButton.clicked -= controller.OnDownloadAsset;
                        directDownloadAssetButton.clicked += controller.OnDownloadAsset;
    #endif
                    }
                }
            }
            
            directStreamButton.style.display = DisplayStyle.Flex;
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
        
        public void ShowStreamingAssetDownload(AssetInfo assetInfo)
        {
            var currentVersionNumber = assetInfo.Properties.Value.FrozenSequenceNumber;
            string hashFolderName = StreamingUtils.ReturnHashName(assetInfo.Asset);
            
            if (!Directory.Exists(StreamingUtils.LocalStreamingAssetPath))
            {
                Directory.CreateDirectory(StreamingUtils.LocalStreamingAssetPath);
            }
            var matchingFolders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolderName + "*");
            m_DownloadStreamingAssetButton.HideProgress();
            m_DownloadStreamingAssetButton.style.display = DisplayStyle.Flex;
            m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);
            
            m_DownloadStreamingAssetButton.userData = assetInfo;
            if (matchingFolders == null || matchingFolders.Length == 0)
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
                
                m_DownloadStreamingAssetButton.style.display = canDownloadOfflineAsset ? DisplayStyle.Flex : DisplayStyle.None;
                m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);

                m_OffloadAssetButton.style.display = DisplayStyle.None;
                AssetsInfoUIToolkitController.UpdateVersionVE.ClearClassList();
                
                if (AssetsController.NewerVersionAsset.HasValue &&
                    AssetsController.NewerVersionAsset.Value == assetInfo)
                {
                    //Show the update version button
                    AssetsInfoUIToolkitController.ShowNewVersionVE();
                }
                else
                {
                    AssetsInfoUIToolkitController.UpdateVersionVE.style.display = DisplayStyle.None;
                }
            }
            else
            {
                if (StreamingModelController.StreamingAsset.HasValue)
                {
                    if((StreamingModelController.StreamingAsset.Value.Asset is OfflineAsset) && StreamingModelController.StreamingAsset.Value.Asset.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId &&
                        StreamingModelController.StreamingAsset.Value.Asset.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor)
                    {
                        m_DownloadStreamingAssetButton.SetEnabled(false);
                        m_OffloadAssetButton.SetEnabled(false);
                        AssetsInfoUIToolkitController.UpdateVersionVE.style.display = DisplayStyle.None;
                        return;
                    }
                }
                
                var firstOrDefault = matchingFolders.FirstOrDefault();
                var directoryName = new DirectoryInfo(firstOrDefault).Name;
                m_DownloadStreamingDataControllers ??= new Dictionary<IAsset, DownloadStreamingDataController>();
                if (directoryName.Contains("_temp") && !m_DownloadStreamingDataControllers.Keys.Any(x => x.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId &&
                        x.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor))
                {
                    m_DownloadStreamingAssetButton.style.display = DisplayStyle.Flex;
                    m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);
                    m_OffloadAssetButton.style.display = DisplayStyle.None;
                }
                else
                {
                    if (m_DownloadStreamingDataControllers.Keys.Any(x => x.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId 
                        && x.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor))
                    {
                        m_DownloadStreamingAssetButton.style.display = DisplayStyle.Flex;
                        m_DownloadStreamingAssetButton.ShowProgress();
                        m_OffloadAssetButton.style.display = DisplayStyle.None;
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
                        
                        AssetsInfoUIToolkitController.UpdateVersionVE.style.display = DisplayStyle.Flex;
                        AssetsInfoUIToolkitController.UpdateVersionVE.ClearClassList();
                        
                        AssetsInfoUIToolkitController.UpdateVersionVE.AddToClassList(localVersion == currentVersionNumber? "LocalUpToUpdate" : "UpdateLocalVersion");
                        if (localVersion == currentVersionNumber)
                        {
                            AssetsInfoUIToolkitController.UpdateVersionButton.parent.style.display =
                                DisplayStyle.None;
                            AssetsInfoUIToolkitController.UpdateVersionVE.Q<Icon>().iconName = "download";
                            var text = AssetsInfoUIToolkitController.UpdateVersionVE.Q<Text>();
                            text.ClearBinding("text");
                            text.SetBinding("text", m_ReadyForLocalStreamingLocalizedString);
                        }
                        else
                        {
                            AssetsInfoUIToolkitController.UpdateVersionButton.parent.style.display =
                                DisplayStyle.Flex;
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
                            text.ClearBinding("text");
                            text.SetBinding("text", m_UpdateLocalStreamingLocalizedString);
                            var button = AssetsInfoUIToolkitController.UpdateVersionVE.Q<Button>();
                            button.ClearBinding("title");
                            button.SetBinding("title", m_UpdateLocalizedString);
                            //Also change the download text to update on button
                        }
                        
                        m_DownloadStreamingAssetButton.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
                        m_DownloadStreamingAssetButton.SetEnabled(!IdentityController.GuestMode);
                        m_OffloadAssetButton.style.display = currentVersionNumber == localVersion && m_DownloadStreamingAssetButton.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }
        }

        private void OnRemoveCacheButtonPress()
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
            
            var dialog = new CustomAlertDialog(m_RemoveAssetTitleLocalizedString, description)
            {
                variant = AlertSemantic.Confirmation
            };
            
            dialog.SetPrimaryAction(m_RemoveLocalizedString, true, () =>
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
                dialog.SetSecondaryAction(m_PreserveLocalizedString, false, RemoveDownloadedAsset);
            }
            
            dialog.SetCancelAction(m_CancelLocalizedString);
            m_TopActionModal = Modal.Build(m_AssetInfoPanelTop, dialog);
            m_TopActionModal.dismissed += TopActionModalOnDismissed;

            m_TopActionModal.Show();
            
            return;
            void ToastOnshown(Toast obj)
            {
                obj.shown -= ToastOnshown;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_Toast_AssetRemovedLocalizedString);
            }
            
            void TopActionModalOnDismissed(Modal arg1, DismissType arg2)
            {
                m_TopActionModal.dismissed -= TopActionModalOnDismissed;
                m_TopActionModal = null;
            }

            void RemoveDownloadedAsset()
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
                
                var toast = Toast.Build(m_AssetInfoPanelTop, string.Empty, NotificationDuration.Short)
                    .SetStyle(NotificationStyle.Informative);
                
                toast.shown += ToastOnshown;
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
        
        private void OnDownloadCacheFinished(AssetInfo assetInfo)
        {
            StreamingUtils.MakeTempFolderComplete(assetInfo.Asset);
            
            Refresh3DDSAssetUI(assetInfo, true);
            if(m_DownloadStreamingDataControllers.TryGetValue(assetInfo.Asset, out var downloadStreamingDataController))
            {
                downloadStreamingDataController.DownloadProgress -= OnDownloadProgress;
                m_DownloadStreamingDataControllers.Remove(assetInfo.Asset);
            }
            
            var toast = Toast.Build(SharedUIManager.Instance.AssetGridView, string.Empty, NotificationDuration.Short)
                .SetStyle(NotificationStyle.Informative);
            
            toast.shown += FinishDownloadToast;

            toast.Show();

            if (SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId != assetInfo.Asset.Descriptor.AssetId ||
                SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectDescriptor != assetInfo.Asset.Descriptor.ProjectDescriptor)
            {
                return;
            }

            //User can be reviewing a different version of the asset.
            m_DownloadStreamingAssetButton.HideProgress();
            m_DownloadProgress.style.display = DisplayStyle.None;
            ShowStreamingAssetDownload(SharedUIManager.SelectedAsset.Value);
            return;
            
            void FinishDownloadToast(Toast obj)
            {
                obj.shown -= FinishDownloadToast;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                
                if(m_Toast_FinishDownloadLocalizedString.TryGetValue("name", out var value))
                {
                    ((StringVariable) m_Toast_FinishDownloadLocalizedString["name"]).Value = assetInfo.Properties.Value.Name;
                }
                else
                {
                    m_Toast_FinishDownloadLocalizedString.Add("name", new StringVariable {Value = assetInfo.Properties.Value.Name});
                }
                text.SetBinding("text", m_Toast_FinishDownloadLocalizedString);
            }
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
            m_OffloadAssetButton.style.display = DisplayStyle.None;
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
            
            void ShowDownloadModal()
            {
                if (datasetToDownload == null)
                {
                    Debug.Log("No dataset found for asset " + assetToDownload.Asset.Descriptor.AssetId);
                    return;
                }
                var customDialog = new CustomAlertDialog(m_DownloadAssetTitleLocalizedString,
                    m_DownloadAssetDescriptionLocalizedString)
                {
                    variant = AlertSemantic.Confirmation
                };
                
                customDialog.SetPrimaryAction(m_DownloadLocalizedString, true, () =>
                {
                    StreamingUtils.RemoveCache(assetToDownload.Asset, CallbackAfterRemovedCache);
                    m_DownloadStreamingAssetButton?.SetEnabled(false);
                    var toast = Toast.Build(SharedUIManager.Instance.AssetGridView, string.Empty, NotificationDuration.Short)
                        .SetStyle(NotificationStyle.Informative);
                
                    toast.shown += DownloadingToast;

                    toast.Show();
                    
                    var downloadStreamingDataController = new DownloadStreamingDataController(assetToDownload, datasetToDownload);
                    m_DownloadStreamingDataControllers ??= new Dictionary<IAsset, DownloadStreamingDataController>();
                    m_DownloadStreamingDataControllers.TryAdd(assetToDownload.Asset, downloadStreamingDataController);
                    downloadStreamingDataController.DownloadProgress += OnDownloadProgress;
                });
                
                customDialog.SetCancelAction(m_CancelLocalizedString);
                
                var modal = Modal.Build(SharedUIManager.Instance.AssetGridView, customDialog);
                modal.dismissed += OnModalDismissed;

                modal.Show();
            }
            
            void OnModalDismissed(Modal arg1, DismissType arg2)
            {
                arg1.dismissed -= OnModalDismissed;
                SharedUIManager.Instance.AssetGridView.SetEnabled(true);
                if (arg2 == DismissType.Manual)
                {
                    var assetList = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
                    if(assetList == null) return;
                    var index = GetAssetIndex(assetList, assetToDownload);
                    
                    m_DownloadStreamingAssetButton?.SetEnabled(true);
                    directDownloadAssetButton?.SetEnabled(true);
                    if(AssetsInfoUIToolkitController.UpdateVersionVE.style.display == DisplayStyle.Flex &&
                       AssetsInfoUIToolkitController.UpdateVersionButton.parent.style.display == DisplayStyle.Flex)
                    {
                        AssetsInfoUIToolkitController.UpdateVersionButton.SetEnabled(true);
                    }
                }
            }
            
            void DownloadingToast(Toast obj)
            {
                obj.shown -= DownloadingToast;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_Toast_DownloadingAssetLocalizedString);
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
                var assetBar = m_StreamingAssetBarTemplate.Instantiate().Children().First();
                m_AssetInfoPanelTop.Add(assetBar);
                
                m_StreamButton = assetBar.Q<ActionButton>(k_StreamAssetButtonName);
                m_StreamButton.clicked += StreamAsset;
                
                m_CopyButton = assetBar.Q<ActionButton>(k_CopyLinkButtonName);
                m_CopyButton.clicked += OnCopyLinkButtonPress;
                m_CopyButton.SetEnabled(!IdentityController.GuestMode);
                
                m_DownloadStreamingAssetButton = assetBar.Q<ProgressActionButton>(k_DownloadStreamingAssetButtonName);
                m_DownloadProgress = m_DownloadStreamingAssetButton.Q<CircularProgress>();
                
                m_OffloadAssetButton = assetBar.Q<ActionButton>(k_OffloadAssetButtonName);
                
#if !UNITY_WEBGL || UNITY_EDITOR
                m_OffloadAssetButton.clicked += OnRemoveCacheButtonPress;
                m_DownloadStreamingAssetButton.clicked += OnDownloadButtonPress;
#elif UNITY_WEBGL && !UNITY_EDITOR
                m_DownloadStreamingAssetButton.style.display = DisplayStyle.None;
                m_DownloadStreamingAssetButton.HideProgress();
                m_DownloadProgress.style.display = DisplayStyle.None;
#endif
            }
            
            var icon = m_DownloadStreamingAssetButton.Q<Icon>();
            icon.style.display = DisplayStyle.Flex;

            if (m_StreamButton != null)
            {
                m_StreamButton.style.display = DisplayStyle.None;
            }
            
#if !UNITY_WEBGL || UNITY_EDITOR
            m_DownloadStreamingAssetButton?.HideProgress();
            if (m_DownloadStreamingAssetButton != null)
            {
                m_DownloadStreamingAssetButton.style.display = DisplayStyle.None;
            }

            if (m_OffloadAssetButton != null)
            {
                m_OffloadAssetButton.style.display = DisplayStyle.None;
            }
#endif
        }
        
        private void OnParentAssetSelected(AssetInfo? assetInfo)
        {
            if (assetInfo == null)
            {
                AssetSelected(AssetsController.SelectedAsset.Value);
                return;
            }
            AssetSelected(assetInfo.Value);
        }
        
        private void AssetSelected(AssetInfo assetInfo)
        {
            if (assetInfo.Asset == null || (m_currentOpenedAsset != null && assetInfo.Asset.Descriptor == m_currentOpenedAsset.Descriptor))
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
                
                OffloadAssetButton.style.display = DisplayStyle.Flex;
                
                if (offlineAsset.OfflineAssetInfo.layout)
                {
                    var layoutJson = Path.Combine(folder, StreamingUtils.LayoutJson);
                    if (File.Exists(layoutJson))
                    {
                        m_StreamButton.style.display = DisplayStyle.Flex;
                        m_StreamButton.ClearBinding("label");
                        m_StreamButton.SetBinding("label", m_LoadLayoutLocalizedString);
                        m_StreamButton.SetEnabled(true);
                    }
                }
                else
                {
                    var titleJson = Path.Combine(folder, StreamingUtils.TilesetJson);
                    if (File.Exists(titleJson))
                    {
                        ShowStreamButton();
                    }
                }
                return;
            }
            
            if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
            {
                m_StreamButton.style.display = DisplayStyle.Flex;
                m_StreamButton.ClearBinding("label");
                m_StreamButton.SetBinding("label", m_LoadLayoutLocalizedString);
                m_StreamButton.SetEnabled(true);
#if !UNITY_WEBGL || UNITY_EDITOR
                _ = GetSourceDataset(assetInfo);
#endif
                return;
            }
            m_StreamingDataset = null;
            _ = GetStreamableDataset(assetInfo);
        }
        
#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task GetSourceDataset(AssetInfo assetInfo)
        {
            m_SourceDataSet = await assetInfo.Asset.GetSourceDatasetAsync(CancellationToken.None);
            ShowStreamingAssetDownload(assetInfo);
        }
#endif
        
        private async Task GetStreamableDataset(AssetInfo assetInfo)
        {
            var cacheConfigurations = assetInfo.Asset.CacheConfiguration;
            cacheConfigurations.DatasetCacheConfiguration = new DatasetCacheConfiguration()
            {
                CacheProperties = true
            };
            var newAssetWithCacheConfig = await assetInfo.Asset.WithCacheConfigurationAsync(cacheConfigurations, CancellationToken.None);
            var listOfDatasets = newAssetWithCacheConfig.ListDatasetsAsync(Range.All, CancellationToken.None);
            
            bool hasStreamableDataset = false;
            IDataset sourceDataset = null;
            IDataset previewDataset = null;
            await foreach (var dataset in listOfDatasets)
            {
                var datasetProperties = await dataset.GetPropertiesAsync(CancellationToken.None);
                if (datasetProperties.SystemTags.Contains(StreamingUtils.SourceTag))
                {
                    sourceDataset = dataset;
                    continue;
                }

                if (datasetProperties.SystemTags.Contains(StreamingUtils.PreviewTag))
                {
                    previewDataset = dataset;
                    continue;
                }
                if (!datasetProperties.SystemTags.Contains(StreamingUtils.StreamableTag)) continue;
                m_StreamingDataset = dataset;
                hasStreamableDataset = true;
                ShowStreamButton();
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

        private void ShowStreamButton()
        {
            m_StreamButton.style.display = DisplayStyle.Flex;
            m_StreamButton.ClearBinding("label");
            m_StreamButton.SetBinding("label", m_StreamLocalizedString);
            m_StreamButton.SetEnabled(true);
        }

        private void OnCopyLinkButtonPress()
        {
            var link = $"https://cloud.unity.com/home/organizations/{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.OrganizationId}/projects/{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectId}/assets?assetId={SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId}:{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetVersion.ToString()}";
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(link);
#else
            GUIUtility.systemCopyBuffer = link;
    #endif
            var messageToast = Toast.Build(m_CopyButton, string.Empty, NotificationDuration.Short).SetStyle(NotificationStyle.Default);
            messageToast.shown += MessageToastShown;
            messageToast.Show();
            
            void MessageToastShown(Toast toast)
            {
                toast.shown -= MessageToastShown;
                var text = toast.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_CopyLinkToClipboardLocalizedString);
            }
        }

        private void DirectStreamAsset(AssetInfo assetInfo)
        {
            if (m_DirectStreamAssetModal != null)
            {
                return;
            }
            
            var dialog = new CustomAlertDialog(m_DirectStreamingTitleLocalizedString,
                m_DirectStreamingDescriptionLocalizedString)
            {
                variant = AlertSemantic.Confirmation
            };
            
            dialog.SetPrimaryAction(m_StreamLocalizedString, true, () =>
            {
                SharedUIManager.SelectedAsset = assetInfo;
                StreamAsset();
            });
            
            dialog.SetCancelAction(m_CancelLocalizedString);
            
            m_DirectStreamAssetModal = Modal.Build(SharedUIManager.Instance.AssetGridView, dialog);
            m_DirectStreamAssetModal.dismissed += ModalOndismissed;

            m_DirectStreamAssetModal.Show();
            return;
            
            void ModalOndismissed(Modal arg1, DismissType arg2)
            {
                arg1.dismissed -= ModalOndismissed;
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
            if (!SharedUIManager.SelectedAsset.HasValue || m_TopActionModal != null)
            {
                return;
            }
            
#if !UNITY_WEBGL || UNITY_EDITOR
            if (SharedUIManager.SelectedAsset.Value.Asset is not OfflineAsset)
            {
                bool hasLocalData = StreamingUtils.CheckHasLocalAsset(SharedUIManager.SelectedAsset.Value.Asset, out var ver);

                if (hasLocalData)
                {
                    var dialog = new CustomAlertDialog(m_PickDataTitleLocalizedString,
                        m_PickDataDescriptionLocalizedString)
                    {
                        variant = AlertSemantic.Default
                    };
                    
                    dialog.SetPrimaryAction(m_CloudLocalizedString, true, StartStreamingScene, "broadcast");
                    dialog.SetSecondaryAction(m_LocalLocalizedString, false, () =>
                    {
                        var offlineAsset =
                            StreamingUtils.ReturnOfflineAssetInfo(SharedUIManager.SelectedAsset.Value.Asset);
                        StreamingModelController.StreamingAsset = new AssetInfo()
                        {
                            Asset = offlineAsset,
                            Properties = null
                        };
                        MainSceneController.StartStreaming?.Invoke();
                    });
                    
                    dialog.SetCancelAction(m_CancelLocalizedString);
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
                if (SharedUIManager.SelectedAsset.Value.Asset is OfflineAsset)
                {
                    StreamingModelController.StreamingAsset = SharedUIManager.SelectedAsset.Value;
                }
                else
                {
                    StreamingModelController.StreamingAsset = AssetsController.SelectedParentAsset.HasValue == false ? SharedUIManager.SelectedAsset.Value : AssetsController.SelectedParentAsset.Value;
                }
                
                
                MainSceneController.StartStreaming?.Invoke();
            }
            
            void TopActionModalOnDismissed(Modal arg1, DismissType arg2)
            {
                m_TopActionModal.dismissed -= TopActionModalOnDismissed;
                m_TopActionModal = null;
            }
        }
    }
}
