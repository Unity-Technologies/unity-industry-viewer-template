using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.SceneManagement;
using Button = Unity.AppUI.UI.Button;

namespace Unity.Industry.Viewer.Assets
{
    // This script is the controller for managing and displaying asset information in the Unity UI Toolkit.
    // It handles the initialization and binding of UI elements, as well as the registration of event handlers.
    // The script manages the display of asset details, including name, type, status, and version information.
    // It also handles downloading assets, updating to the latest version, and loading associated datasets and files.
    // The script integrates with Unity Cloud services and responds to various events from the AssetsController and IdentityController.
    public class AssetsInfoUIToolkitController : AssetInfoUIBaseController
    {
        private const string k_DownloadAssetButtonName = "DownloadAssetButton";
        private const string k_DownloadProgressName = "DownloadProgress";
        private const string k_UpdateVersionVEName = "UpdateVersionVE";
        private const string k_DatasetParentName = "DatasetParent";
        private const string k_SelectAllCheckBoxName = "SelectAllCheckBox";
        
        private static readonly string[] pixyzSupportedFormats = new string[]
        {
            ".catpart", ".catproduct", ".cgr", ".3dxml",
            ".jt", ".prt", ".asm", ".sldprt",
            ".sldasm", ".igs", ".iges", ".stp",
            ".step", ".x_t", ".x_b", ".par",
            ".psm", ".3ds", ".obj", ".stl",
            ".ply", ".fbx", ".dae", ".wrl",
            ".vrml", ".u3d", ".ifc", ".rvt",
            ".dwg", ".dxf", ".e57", ".las",
            ".laz", ".pts", ".ptx", ".xyz",
            ".pxz", ".sat", ".sab", ".wire",
            ".ipt", ".iam", ".nwd", ".nwc",
            ".rfa", ".rcp", ".rcs", ".vpb",
            ".model", ".session", ".catshape",
            ".neu", ".xas", ".xpr", ".pvs",
            ".pvz", ".csb", ".glb", ".gltf",
            ".gds", ".p_t", ".wrl", ".vrml",
            ".p_b", ".xmt", ".xmt_txt", ".xmt_bin",
            ".pdf", ".plmxml", ".prc", ".3dm",
            ".rvm", ".skp", ".pwd", ".stpz",
            ".stepz", ".stpx", ".stpxz", ".usd",
            ".usdz", ".usda", ".usdc", ".vda"
        };
        
        private static readonly string[] defaultImageFormats = new string[]
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".gif"
        };

        // UI elements
        private ActionButton m_DownloadAssetButton;
        public VisualElement UpdateVersionVE => m_UpdateVersionVE;
        private VisualElement m_UpdateVersionVE, m_DatasetParent;
        private LinearProgress m_DownloadProgress;
        private Queue<IFile> m_DownloadingFiles;
        public Button UpdateVersionButton => m_UpdateVersionButton;
        private Button m_UpdateVersionButton;
        private Checkbox m_SelectAllCheckBox;

        public Action UpdateVersionButtonAction;
        
        // Constructor to initialize the UI and register event handlers
        public AssetsInfoUIToolkitController() : base()
        {
            m_DownloadAssetButton = m_AssetInfoPanelRoot.Q<ActionButton>(k_DownloadAssetButtonName);
#if UNITY_WEBGL
            m_DownloadAssetButton.style.display = DisplayStyle.None;
#endif
            m_DownloadProgress = m_AssetInfoPanelRoot.Q<LinearProgress>(k_DownloadProgressName);
            m_UpdateVersionVE = m_AssetInfoPanelRoot.Q<VisualElement>(k_UpdateVersionVEName);
            m_UpdateVersionButton = m_UpdateVersionVE.Q<Button>();
            m_UpdateVersionVE.style.display = DisplayStyle.None;
            m_UpdateVersionVE.ClearClassList();
            
            m_DatasetParent = m_AssetInfoPanelRoot.Q<VisualElement>(k_DatasetParentName);
            m_SelectAllCheckBox = m_AssetInfoPanelRoot.Q<Checkbox>(k_SelectAllCheckBoxName);
        }

        public sealed override void RegisterCallbacks()
        {
            AssetsController.AssetSelected -= AssetSelected;
            AssetsController.AssetSelected += AssetSelected;
            
            AssetsController.AssetVersionsLoaded -= OnAssetVersionsLoaded;
            AssetsController.AssetVersionsLoaded += OnAssetVersionsLoaded;
            
            SharedUIManager.OrganizationSelected -= OnOrganizationSelected;
            SharedUIManager.OrganizationSelected += OnOrganizationSelected;
            
            SharedUIManager.AssetProjectSelected -= OnAssetProjectSelected;
            SharedUIManager.AssetProjectSelected += OnAssetProjectSelected;
            
            AssetsController.AssetDeselected -= OnDeselectAsset;
            AssetsController.AssetDeselected += OnDeselectAsset;
            
            m_DownloadAssetButton.clicked -= OnDownloadAssetButtonPressed;
            m_DownloadAssetButton.clicked += OnDownloadAssetButtonPressed;
            
            m_UpdateVersionButton.clicked -= OnUpdateVersionButtonPressed;
            m_UpdateVersionButton.clicked += OnUpdateVersionButtonPressed;
            
            m_AssetVersionDropdown.UnregisterValueChangedCallback(OnVersionDropdownValueChanged);
            m_AssetVersionDropdown.RegisterValueChangedCallback(OnVersionDropdownValueChanged);
            
            m_AssetStatusDropdown.UnregisterValueChangedCallback(OnAssetStatusDropdownValueChanged);
            m_AssetStatusDropdown.RegisterValueChangedCallback(OnAssetStatusDropdownValueChanged);
            
            m_SelectAllCheckBox.UnregisterValueChangedCallback(OnSelectAllCheckBoxValueChanged);
            m_SelectAllCheckBox.RegisterValueChangedCallback(OnSelectAllCheckBoxValueChanged);
            
            m_AssetVersionDropdown.bindItem -= AssetVersionDropdownBindItem;
            m_AssetVersionDropdown.bindItem += AssetVersionDropdownBindItem;

            m_AssetVersionDropdown.bindTitle -= AssetVersionDropdownBindTitle;
            m_AssetVersionDropdown.bindTitle += AssetVersionDropdownBindTitle;
            
            m_AssetStatusDropdown.bindItem -= StatusBindItem;
            m_AssetStatusDropdown.bindTitle -= StatusBindItem;
            m_AssetStatusDropdown.makeItem -= StatusMakeItem;
            m_AssetStatusDropdown.makeTitle -= StatusMakeItem;
            
            m_AssetStatusDropdown.bindItem += StatusBindItem;
            m_AssetStatusDropdown.bindTitle += StatusBindItem;
            m_AssetStatusDropdown.makeItem += StatusMakeItem;
            m_AssetStatusDropdown.makeTitle += StatusMakeItem;
            
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
            IdentityController.AuthenticationStateChangedEvent += OnAuthenticationStateChanged;
        }

        public override void UnregisterCallbacks()
        {
            AssetsController.AssetSelected -= AssetSelected;
            AssetsController.AssetVersionsLoaded -= OnAssetVersionsLoaded;
            SharedUIManager.OrganizationSelected -= OnOrganizationSelected;
            AssetsController.AssetDeselected -= OnDeselectAsset;
            SharedUIManager.AssetProjectSelected -= OnAssetProjectSelected;
            
            m_AssetVersionDropdown.bindTitle -= AssetVersionDropdownBindTitle;
            m_AssetVersionDropdown.bindItem -= AssetVersionDropdownBindItem;
            m_DownloadAssetButton.clicked -= OnDownloadAssetButtonPressed;
            m_UpdateVersionButton.clicked -= OnUpdateVersionButtonPressed;
            
            m_AssetStatusDropdown.bindItem -= StatusBindItem;
            m_AssetStatusDropdown.bindTitle -= StatusBindItem;
            m_AssetStatusDropdown.makeItem -= StatusMakeItem;
            m_AssetStatusDropdown.makeTitle -= StatusMakeItem;
            
            m_AssetVersionDropdown.UnregisterValueChangedCallback(OnVersionDropdownValueChanged);
            
            m_AssetStatusDropdown.UnregisterValueChangedCallback(OnAssetStatusDropdownValueChanged);
            m_SelectAllCheckBox.UnregisterValueChangedCallback(OnSelectAllCheckBoxValueChanged);
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
        }

        // Disposes of the controller and unregisters event handlers
        public override void DisposeUI()
        {
            UnregisterCallbacks();
        }

        private void OnSelectAllCheckBoxValueChanged(ChangeEvent<CheckboxState> evt)
        {
            var allCheckBoxes = m_DatasetParent.Query<FileSelectionCheckBox>().ToList();
            foreach (var checkBox in allCheckBoxes)
            {
                checkBox.SetValueWithoutNotify(evt.newValue);
            }
            ChangeDownloadButtonState();
        }

        private void ChangeDownloadButtonState()
        {
            if (IdentityController.GuestMode || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                m_DownloadAssetButton.SetEnabled(false);
                return;
            }
            var allCheckBoxes = m_DatasetParent.Query<FileSelectionCheckBox>().ToList();
            var anyChecked = allCheckBoxes.Any(x => x.value == CheckboxState.Checked);
            m_DownloadAssetButton.SetEnabled(anyChecked);
        }

        private void OnAssetStatusDropdownValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            if (m_AssetStatusDropdown.sourceItems == null || evt.newValue == null || !evt.newValue.Any())
            {
                return;
            }
            var currentSelectedIndex = evt.newValue.First();
            var newStatus = (AssetStatus)m_AssetStatusDropdown.sourceItems[currentSelectedIndex];
            
            if (newStatus == AssetStatus.Withdrawn)
            {
                //Directly update the status
                _ = UpdateAssetStatus(newStatus, true);
                return;
            }

            var item = m_AssetStatusDropdown.Q<DropdownItem>(newStatus.GetValueAsString(false));
            var currentAssetStatusIndex = Array.IndexOf(AssetStatusExtensions.GetAssetStatuses(), SharedUIManager.SelectedAsset.Value.Properties.Value.StatusName.GetAssetStatusFromString());
            if (!item.enabledSelf)
            {
                m_AssetStatusDropdown.SetValueWithoutNotify(new []{currentAssetStatusIndex});
            }
            else
            {
                var currentAssetStatus = SharedUIManager.SelectedAsset.Value.Properties.Value.StatusName.GetAssetStatusFromString();

                switch (currentAssetStatus)
                {
                    case AssetStatus.Draft when newStatus == AssetStatus.Inreview:
                        _ = UpdateAssetStatus(newStatus, true);
                        break;
                    case AssetStatus.Draft when newStatus == AssetStatus.Approved:
                    {
                        var listOfAssetStatus = new List<AssetStatus>() { AssetStatus.Inreview, AssetStatus.Approved };
                        _ = UpdateAssetStatuses(listOfAssetStatus);
                        break;
                    }
                    case AssetStatus.Draft when newStatus == AssetStatus.Rejected:
                    {
                        var listOfAssetStatus = new List<AssetStatus>() { AssetStatus.Inreview, AssetStatus.Rejected };
                        _ = UpdateAssetStatuses(listOfAssetStatus);
                        break;
                    }
                    case AssetStatus.Draft:
                    {
                        if(newStatus == AssetStatus.Published)
                        {
                            var listOfAssetStatus = new List<AssetStatus>() { AssetStatus.Inreview, AssetStatus.Approved, AssetStatus.Published };
                            _ = UpdateAssetStatuses(listOfAssetStatus);
                        }

                        break;
                    }
                    case AssetStatus.Inreview when newStatus is AssetStatus.Approved or AssetStatus.Rejected:
                        _ = UpdateAssetStatus(newStatus, true);
                        break;
                    case AssetStatus.Inreview:
                    {
                        if (newStatus == AssetStatus.Published)
                        {
                            var listOfAssetStatus = new List<AssetStatus>() { AssetStatus.Approved, AssetStatus.Published };
                            _ = UpdateAssetStatuses(listOfAssetStatus);
                        }

                        break;
                    }
                    case AssetStatus.Approved:
                    {
                        if(newStatus == AssetStatus.Published)
                        {
                            _ = UpdateAssetStatus(newStatus, true);
                        }

                        break;
                    }
                    case AssetStatus.Rejected:
                        break;
                    case AssetStatus.Published:
                        break;
                    case AssetStatus.Withdrawn:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return;

            async Task UpdateAssetStatuses(List<AssetStatus> statuses)
            {
                for (var i = 0; i < statuses.Count; i++)
                {
                    await UpdateAssetStatus(statuses[i], i == statuses.Count - 1);
                }
            }

            async Task UpdateAssetStatus(AssetStatus newStatus, bool refresh)
            {
                await SharedUIManager.SelectedAsset.Value.Asset.UpdateStatusAsync(newStatus.GetValueAsString(true), CancellationToken.None);
                //Wait for backend to update the status
                float elapsed = 0f;
                while (elapsed < 0.5f)
                {
                    await Task.Yield();
                    elapsed += Time.deltaTime;
                }
                if(!refresh) return;
                IAssetRepository repository = IdentityController.GuestMode? PlatformServices.ServiceAccountAssetRepository : PlatformServices.AssetRepository;
                //Query asset make sure the status is updated
                var assetProject = await repository.GetAssetProjectAsync(SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectDescriptor, CancellationToken.None);
                var latestVersion = await assetProject.GetAssetWithLatestVersionAsync(SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId, CancellationToken.None);
                var property = await latestVersion.GetPropertiesAsync(CancellationToken.None);
                AssetsController.AssetSelected?.Invoke(new AssetInfo()
                {
                    Asset = latestVersion,
                    Properties = property
                });
            }
        }

        private void OnVersionDropdownValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            if(evt.newValue == null || !evt.newValue.Any()) return;
            var assets = (m_AssetVersionDropdown.sourceItems as List<AssetInfo>);
            if (assets == null) return;
            var asset = assets[evt.newValue.FirstOrDefault()];
            
            if (SceneManager.GetActiveScene() != SharedUIManager.Instance.AssetsUIDocument.gameObject.scene) return;
            
            if (evt.newValue.First() == 0)
            {
                AssetsController.ParentAssetSelected?.Invoke(null);
            }
            else
            {
                AssetsController.ParentAssetSelected?.Invoke(asset);
            }
            SharedUIManager.SelectedAsset = asset;
        }

        // Handles asset deselection
        protected override void OnDeselectAsset()
        {
            base.OnDeselectAsset();
            AssetsController.NewVersionAvailable -= NewVersionAvailable;
        }
        
        private void OnAssetProjectSelected(AssetProjectInfo? arg1)
        {
            if(IsVisible() && (!SharedUIManager.SelectedAsset.HasValue || (arg1.HasValue && SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectDescriptor == arg1.Value.AssetProject.Descriptor))) return;
            CloseInfoPanel();
        }

        // Handles organization selection
        private void OnOrganizationSelected(IOrganization arg1)
        {
            if(IsVisible() && (!SharedUIManager.SelectedAsset.HasValue ||SharedUIManager.SelectedAsset.Value.Asset.Descriptor.OrganizationId == arg1.Id)) return;
            CloseInfoPanel();
        }

        // Handles authentication state changes
        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            if(state is AuthenticationState.AwaitingLogout or AuthenticationState.LoggedOut)
            {
                CloseInfoPanel();
            }
        }

        // Handles the update version button press
        private void OnUpdateVersionButtonPressed()
        {
            UpdateVersionButtonAction?.Invoke();
        }

        private void UpdateVersion()
        {
            m_UpdateVersionVE.style.display = DisplayStyle.None;
            UpdateToLatestVersion();
        }
        
        // Handles asset versions loading
        private void OnAssetVersionsLoaded(List<AssetInfo> assets)
        {
            if(AssetsController.SelectedParentAsset.HasValue) return;
            m_AssetVersionDropdown.SetEnabled(assets.Count > 1);
            if (assets.Count == 0)
            {
                m_AssetVersionDropdown.defaultMessage = string.Empty;
                return;
            }
            m_AssetVersionDropdown.SetValueWithoutNotify(null);
            m_AssetVersionDropdown.userData = null;
            m_AssetVersionDropdown.sourceItems = null;
            
            m_AssetVersionDropdown.sourceItems = assets;
            m_AssetVersionDropdown.SetEnabled(true);
            m_AssetVersionDropdown.SetValueWithoutNotify(new [] {0});
        }

        // Handles the download asset button press
        private void OnDownloadAssetButtonPressed()
        {
            m_AssetVersionDropdown.SetEnabled(false);
            m_DownloadAssetButton.SetEnabled(false);
            m_SelectAllCheckBox.SetEnabled(false);
            m_DatasetParent.Query<FileSelectionCheckBox>().ToList().ForEach(x => x.SetEnabled(false));
            
            var destinationPath = string.Empty;
            if(Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                destinationPath = Path.Combine(Application.persistentDataPath, AssetsController.SelectedAsset.Value.Asset.Descriptor.AssetId.ToString());
            }
            else
            {
                //It will download to the desktop, if you want to change the destination path, you can use a native file dialog plugin
                destinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{AssetsController.SelectedAsset.Value.Asset.Descriptor.AssetId}");
            }
            
            var allCheckBoxes = m_DatasetParent.Query<FileSelectionCheckBox>().ToList().Where(x => x.value == CheckboxState.Checked);
            m_DownloadingFiles = new Queue<IFile>(allCheckBoxes.Select(x => x.userData as IFile));
            _ = OnRequestDownloadFile(destinationPath);
        }

        // Clears the UI elements
        public override void ClearUI()
        {
            base.ClearUI();
            if (m_UpdateVersionVE != null)
            {
                m_UpdateVersionVE.style.display = DisplayStyle.None;
                m_UpdateVersionVE.ClearClassList();
            }

            m_DatasetParent?.Clear();

            m_SelectAllCheckBox?.SetValueWithoutNotify(CheckboxState.Unchecked);
        }
        
        private void AssetVersionDropdownBindTitle(DropdownItem arg1, IEnumerable<int> arg2)
        {
            if(arg2 == null || !arg2.Any()) return;
            AssetVersionDropdownBindItem(arg1, arg2.First());
        }

        protected override void AssetVersionDropdownBindItem(DropdownItem arg1, int arg2)
        {
            var assets = m_AssetVersionDropdown.sourceItems as List<AssetInfo>;
            if (assets == null || assets.Count == 0)
            {
                return;
            }
            if (arg2 < 0 || arg2 >= assets.Count)
            {
                return;
            }
            
            var asset = assets[arg2];
            
            if(!asset.Properties.HasValue) return;
            
            int verNum = asset.Properties.Value.FrozenSequenceNumber;
            var localVersionLocalizedString = new LocalizedString(k_AssetLocalisedTable, k_VersionKey);
            localVersionLocalizedString.Add("num", new IntVariable{ Value = verNum});
            
            var text = arg1.Q<LocalizedTextElement>();
            text.SetBinding("text", localVersionLocalizedString);
        }

        // Handles the close button press
        protected override void CloseInfoPanel()
        {
            base.CloseInfoPanel();
            if (SceneManager.GetActiveScene() == SharedUIManager.Instance.AssetsUIDocument.gameObject.scene)
            {
                AssetsController.AssetDeselected?.Invoke();
                AssetsController.NewVersionAvailable -= NewVersionAvailable;
            }
        }

        // Handles asset selection
        public override void AssetSelected(AssetInfo assetInfo)
        {
            if (!AssetsController.SelectedParentAsset.HasValue)
            {
                m_AssetVersionDropdown.SetValueWithoutNotify(null);
                m_AssetVersionDropdown.userData = null;
                m_AssetVersionDropdown.sourceItems = null;
                m_AssetVersionDropdown.SetEnabled(false);
            }
            
            base.AssetSelected(assetInfo);
            m_UpdateVersionVE.style.display = DisplayStyle.None;
            m_UpdateVersionVE.ClearClassList();
            if (SceneManager.GetActiveScene() == SharedUIManager.Instance.AssetsUIDocument.gameObject.scene)
            {
                if (!AssetsController.SelectedParentAsset.HasValue)
                {
                    AssetsController.AssetVersionRequest?.Invoke(assetInfo.Asset);
                }
                if(SharedUIManager.Instance.AssetsRoot.resolvedStyle.display == DisplayStyle.None) return;
                AssetsController.NewVersionAvailable += NewVersionAvailable;
            }
        }

        // Handles new version availability
        private void NewVersionAvailable(AssetInfo newVersion)
        {
            AssetsController.NewVersionAvailable -= NewVersionAvailable;
            ShowNewVersionVE();
        }

        public void ShowNewVersionVE()
        {
            m_UpdateVersionVE.style.display = DisplayStyle.Flex;
            var existingClasses = m_UpdateVersionVE.GetClasses().ToList();
            if (existingClasses.Count != 0) return;
            m_UpdateVersionVE.AddToClassList("NewVersionAvailable");
            m_UpdateVersionButton.parent.style.display = DisplayStyle.Flex;
            m_UpdateVersionButton.SetEnabled(true);
            UpdateVersionButtonAction = UpdateVersion;
            m_UpdateVersionVE.Q<Icon>().iconName = "info";
            var text = m_UpdateVersionVE.Q<Text>();
            text.ClearBinding("text");
            text.SetBinding("text", SharedUIManager.Instance.NewVersionAvailable);
            var reviewButton = m_UpdateVersionVE.Q<Button>();
            reviewButton.ClearBinding("title");
            reviewButton.SetBinding("title", SharedUIManager.Instance.ViewNewVersion);
        }
        
        // Updates to the latest version of the asset
        private void UpdateToLatestVersion()
        {
            if(!AssetsController.NewerVersionAsset.HasValue) return;
            var asset = AssetsController.NewerVersionAsset.Value;
            SharedUIManager.SelectedAsset = asset;
            AssetsController.AssetSelected?.Invoke(asset);
        }
        
        private async Task OnRequestDownloadFile(string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            while (m_DownloadingFiles.Count != 0)
            {
                var file = m_DownloadingFiles.Dequeue();
                var directory = Path.GetDirectoryName(file.Descriptor.Path);
                var finalDirectoryPath = Path.Combine(destinationFolder, directory);
                if (!Directory.Exists(finalDirectoryPath))
                {
                    Directory.CreateDirectory(finalDirectoryPath);
                }
                var finalPath = Path.Combine(finalDirectoryPath, Path.GetFileName(file.Descriptor.Path));
                var progress = new Progress<HttpProgress>(DownloadProcess);
                await using var fileStream = File.OpenWrite(finalPath);
                await file.DownloadAsync(fileStream, progress, CancellationToken.None);
            }
            
            void DownloadProcess(HttpProgress progress)
            {
                float downloadProgress = progress.DownloadProgress ?? 0f;
                DownloadProgressChanged(downloadProgress);
            }
        }
        
        // Handles download progress changes
        private void DownloadProgressChanged(float progress)
        {
            //Debug.Log($"Progress: {progress} for {filePath}");
            if (progress < 1f)
            {
                m_DownloadAssetButton.style.display = DisplayStyle.None;
                m_DownloadProgress.style.display = DisplayStyle.Flex;
                m_DownloadProgress.value = progress;
            }
            else if(progress >= 1f)
            {
                if (m_DownloadingFiles.Count != 0) return;
                m_AssetVersionDropdown.SetEnabled(true);
                m_DownloadAssetButton.SetEnabled(true);
                
                m_SelectAllCheckBox.SetEnabled(true);
                m_DatasetParent.Query<FileSelectionCheckBox>().ToList().ForEach(x => x.SetEnabled(true));
                
                m_DownloadProgress.style.display = DisplayStyle.None;
                m_DownloadAssetButton.style.display = DisplayStyle.Flex;
            }
        }

        protected override void StatusBindItem(DropdownItem item, int index)
        {
            if(!SharedUIManager.SelectedAsset.HasValue) return;
            var currentAssetStatus = SharedUIManager.SelectedAsset.Value.Properties.Value.StatusName.GetAssetStatusFromString();
            UpdateStatusBinding(currentAssetStatus, item, index);
        }

        // Updates the UI elements with the asset details
        protected override void UpdateAssetUI(AssetInfo assetInfo)
        {
            base.UpdateAssetUI(assetInfo);
            m_AssetNameLabel.text = assetInfo.Properties.Value.Name;
            m_AssetTypeLabel.SetBinding("text", assetInfo.Properties.Value.Type.GetAssetTypeAsString());
            AssignTags(assetInfo.Properties.Value.Tags.ToList());
            var fileTabs = m_Tabs.items[2];
            (fileTabs as TabItem)?.SetEnabled(true);
            
            m_AssetStatusDropdown.sourceItems = AssetStatusExtensions.GetAssetStatuses();
            var index = Array.IndexOf(AssetStatusExtensions.GetAssetStatuses(), assetInfo.Properties.Value.StatusName.GetAssetStatusFromString());
            m_AssetStatusDropdown.SetValueWithoutNotify(new []{index});
            m_AssetStatusDropdown.SetEnabled(!IdentityController.GuestMode);
            
            m_DownloadAssetButton.SetEnabled(false);
            
            AssetsController.GetLinkedProjects?.Invoke(assetInfo, OnRetrieveLinkedProjects);

            UpdateAssetUpdateLabel(assetInfo.Properties.Value.AuthoringInfo.Updated);
            
            UpdateAssetCreationLabel(assetInfo.Properties.Value.AuthoringInfo.Created);
            
            _ = GetAssetFilesInformation();
            
            _ = AssignName(assetInfo.Properties.Value.AuthoringInfo.UpdatedBy, assetInfo.Properties.Value.AuthoringInfo.CreatedBy);
            
            _ = TextureDownload.DownloadThumbnail(assetInfo.Asset, OnTextureDownloaded);
            return;

            async Task GetAssetFilesInformation()
            {
                m_DatasetParent?.Clear();
                m_SelectAllCheckBox?.SetValueWithoutNotify(CheckboxState.Unchecked);
                int totalFiles = 0;
                long totalSize = 0;

                var currentConfig = assetInfo.Asset.CacheConfiguration;
                currentConfig.DatasetCacheConfiguration = new DatasetCacheConfiguration()
                {
                    CacheProperties = true,
                    CacheFileList = true,
                    FileCacheConfiguration = new FileCacheConfiguration()
                    {
                        CacheProperties = true
                    }
                };
                
                var newAsset = await assetInfo.Asset.WithCacheConfigurationAsync(currentConfig, CancellationToken.None);
                
                var allDatasets = newAsset.ListDatasetsAsync(Range.All, CancellationToken.None);
                
                await foreach (var dataset in allDatasets)
                {
                    var datasetProperty = await dataset.GetPropertiesAsync(CancellationToken.None);
                    if(!datasetProperty.IsVisible) continue;
                    var newDatasetButton = new ActionButton()
                    {
                        label = datasetProperty.Name,
                    };
                    newDatasetButton.AddToClassList("datasetTitle");
                    newDatasetButton.AddToClassList(SharedUIManager.k_ActionButtonArrowClass);
                    newDatasetButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);

                    newDatasetButton.name = dataset.Descriptor.DatasetId.ToString();
                    newDatasetButton.clicked += () =>
                    {
                        bool toOpen = false;
                        if (newDatasetButton.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
                        {
                            newDatasetButton.RemoveFromClassList(SharedUIManager.k_ProjectButtonCloseClass);
                            newDatasetButton.AddToClassList(SharedUIManager.k_ProjectButtonOpenClass);
                            toOpen = true;
                        } else if (newDatasetButton.ClassListContains(SharedUIManager.k_ProjectButtonOpenClass))
                        {
                            newDatasetButton.RemoveFromClassList(SharedUIManager.k_ProjectButtonOpenClass);
                            newDatasetButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
                        }

                        var fileListParent = m_DatasetParent.Q<VisualElement>($"{dataset.Descriptor.DatasetId}-files");
                        if(fileListParent == null) return;
                        fileListParent.style.display = toOpen ? DisplayStyle.Flex : DisplayStyle.None;
                    };
                    m_DatasetParent?.Add(newDatasetButton);
                    var files = dataset.ListFilesAsync(Range.All, CancellationToken.None);
                    await foreach (var file in files)
                    {
                        totalFiles++;
                        var fileProperty = await file.GetPropertiesAsync(CancellationToken.None);
                        totalSize += fileProperty.SizeBytes;
                        var fileListParent = m_DatasetParent.Q<VisualElement>($"{file.Descriptor.DatasetId}-files");
                        if (fileListParent == null)
                        {
                            fileListParent = new VisualElement
                            {
                                name = $"{file.Descriptor.DatasetId}-files",
                                style =
                                {
                                    display = DisplayStyle.None
                                }
                            };
                            m_DatasetParent?.Add(fileListParent);
                        }
                        var newFileCheckBox = new FileSelectionCheckBox()
                        {
                            label = Path.GetFileName(file.Descriptor.Path),
                            userData = file
                        };
                        
                        var fileExtension = Path.GetExtension(file.Descriptor.Path);
                        
                        if(pixyzSupportedFormats.Contains(fileExtension))
                        {
                            newFileCheckBox.AddToClassList("file-selection-model");
                        } else if(defaultImageFormats.Contains(fileExtension))
                        {
                            newFileCheckBox.AddToClassList("file-selection-pic");
                        }
                        else
                        {
                            newFileCheckBox.AddToClassList("file-selection-other");
                        }

                        newFileCheckBox.RegisterValueChangedCallback(evt =>
                        {
                            ChangeDownloadButtonState();
                        });
                        
                        fileListParent.Add(newFileCheckBox);
                    }
                }
                
                string[] sizeUnits = { "Bytes", "kB", "MB", "GB", "TB" };
                double size = totalSize;
                int unitIndex = 0;

                //for more accurate result, you can use 1024 instead of 1000
                while (size >= 1000 && unitIndex < sizeUnits.Length - 1)
                {
                    size /= 1000;
                    unitIndex++;
                }
                m_AssetFilesSizeLabel.text = $"{size:0.00} {sizeUnits[unitIndex]}";
                m_AssetFilesLabel.text = totalFiles.ToString();
            }
        }

        // Assigns the name of the user
        private async Task AssignName(UserId updatedBy, UserId createdBy)
        {
            m_AssetUpdateByLabel.text = await GetNameByID(updatedBy);
            m_AssetCreateByLabel.text = await GetNameByID(createdBy);
        }
        
        // Gets the name of the user by ID
        private static async Task<string> GetNameByID(UserId id)
        {
            //Get Name by ID
            var memberInfo = await AssetsController.SelectedOrganization.GetMemberAsync(id);
            return memberInfo.Name;
        }
    }
}
