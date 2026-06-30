using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using System.Text;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Identity;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(-50)]
    public class OfflineModeAssetsUIController : AssetsUIBaseController
    {
        private const string k_AssetRoot = "AssetRoot";
        
        private VisualElement m_AssetCreationPanelRoot;
        
        private AuthenticationState m_AuthenticationState;
        
        private List<(string OrgName, string OrgId)> m_Organizations;

        private VisualElement m_NoAssetTextParent;
        
        private Text m_NoAssetText;
        
        [SerializeField]
        private LocalizedString m_NoAssetsFoundForUserString;
        
        [SerializeField]
        private LocalizedString m_SelectOrganizationHereString;

        private Popover m_tips;

        protected override void Start()
        {
            IdentityController.AuthenticationStateChangedEvent += OnAuthenticationStateChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            m_NoAssetTextParent = SharedUIManager.Instance.AssetsContainer;
        }

        protected override void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            UnregisterCallbacks();
            UninitializeUI();
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
            m_AssetInfoUIBaseController?.DisposeUI();
        }

        private void OnActiveSceneChanged(Scene arg0, Scene newActiveScene)
        {
            m_NoAssetTextParent = newActiveScene == gameObject.scene ? SharedUIManager.Instance.AssetsContainer: SharedUIManager.Instance.AssetsContainer.Q<VisualElement>(k_AssetRoot);
            if (m_NoAssetText != null)
            {
                StartCoroutine(WaitForUIUpdate());
            }
            return;
            
            System.Collections.IEnumerator WaitForUIUpdate()
            {
                yield return new WaitForSeconds(0.5f);
                m_NoAssetTextParent.Add(m_NoAssetText);
                if (SharedUIManager.Instance.IdentityContainer.resolvedStyle.display == DisplayStyle.Flex)
                {
                    SharedUIManager.Instance.IdentityContainer.DisplayOff();
                }
                SharedUIManager.Instance.AssetsContainer.Q<VisualElement>(k_AssetRoot).DisplayOff();
                SharedUIManager.Instance.AssetsContainer.DisplayOn();
            }
        }

        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            m_AuthenticationState = state;
            if (m_AuthenticationState == AuthenticationState.LoggedIn && SceneManager.GetActiveScene() == gameObject.scene)
            {
                if (m_NoAssetText != null)
                {
                    SharedUIManager.Instance.AssetsContainer.DisplayOn();
                    SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_AssetRoot).DisplayOff();
                    SharedUIManager.Instance.IdentityContainer.DisplayOff();
                }
            }
            if (m_AuthenticationState == AuthenticationState.LoggedOut || m_AuthenticationState == AuthenticationState.AwaitingLogout)
            {
                OnUserLoggedOut();
            }
        }

        protected override void RegisterCallbacks()
        {
            SharedUIManager.AssetProjectSelected -= OnAssetProjectSelected;
            SharedUIManager.AssetProjectSelected += OnAssetProjectSelected;
            
            SharedUIManager.AssetCollectionSelected -= OnAssetCollectionSelected;
            SharedUIManager.AssetCollectionSelected += OnAssetCollectionSelected;
            
            OfflineModeAssetsController.AllOrganizationFound -= OnOrganizationListReceived;
            OfflineModeAssetsController.AllOrganizationFound += OnOrganizationListReceived;
            
            OfflineModeAssetsController.AssetProjectsLoaded -= OnAssetProjectsLoaded;
            OfflineModeAssetsController.AssetProjectsLoaded += OnAssetProjectsLoaded;
            
            OfflineModeAssetsController.AssetsLoaded -= OnAssetsLoaded;
            OfflineModeAssetsController.AssetsLoaded += OnAssetsLoaded;
            
            OfflineModeAssetsController.AssetDeselected -= OnAssetDeselected;
            OfflineModeAssetsController.AssetDeselected += OnAssetDeselected;
            
            OfflineModeAssetsController.AssetOffloaded -= OnAssetOffloaded;
            OfflineModeAssetsController.AssetOffloaded += OnAssetOffloaded;
        }

        protected override void UnregisterCallbacks()
        {
            SharedUIManager.AssetProjectSelected -= OnAssetProjectSelected;
            SharedUIManager.AssetCollectionSelected -= OnAssetCollectionSelected;
            
            OfflineModeAssetsController.AllOrganizationFound -= OnOrganizationListReceived;
            OfflineModeAssetsController.AssetProjectsLoaded -= OnAssetProjectsLoaded;
            OfflineModeAssetsController.AssetsLoaded -= OnAssetsLoaded;
            OfflineModeAssetsController.AssetDeselected -= OnAssetDeselected;
            OfflineModeAssetsController.AssetOffloaded -= OnAssetOffloaded;
        }
        
        private void OnAssetOffloaded(AssetInfo obj)
        {
            SharedUIManager.Instance.PathText.text = string.Empty;
            m_AssetInfoUIBaseController.ClearUI();
        }

        private void OnAssetCollectionSelected(IAssetCollection assetCollection)
        {
            SetPathText(null, SharedUIManager.AssetProjectInfo, assetCollection);
        }

        private void OnAssetProjectSelected(AssetProjectInfo? assetProject)
        {
            if(!assetProject.HasValue) return;
            OfflineModeAssetsController.RequestAssetCollections.Invoke(assetProject, OnCollectionsLoaded);
        }

        protected override void InitializeUI()
        {
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                if (!SharedUIManager.Instance.OrganizationButton.IsDisplayOn())
                {
                    SharedUIManager.Instance.OrganizationButton.DisplayOn();
                }
            }
            
            AssetIconLoadFailed -= OnAssetIconLoadedFailed;
            AssetIconLoadFailed += OnAssetIconLoadedFailed;
            
            SharedUIManager.Instance.AMButton.clicked -= OnAMButtonClicked;
            SharedUIManager.Instance.AMButton.clicked += OnAMButtonClicked;
            SharedUIManager.Instance.OrganizationButton.clicked -= OnOrganizationButtonClicked;
            SharedUIManager.Instance.OrganizationButton.clicked += OnOrganizationButtonClicked;
            
            SharedUIManager.Instance.AssetGridView.bindItem -= AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.bindItem += AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem -= AssetGridUnbindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem += AssetGridUnbindItem;
            SharedUIManager.Instance.AssetGridView.selectionChanged -= OnAssetSelectedOnGrid;
            SharedUIManager.Instance.AssetGridView.selectionChanged += OnAssetSelectedOnGrid;

            SharedUIManager.OrganizationSelected -= OnOrganizationSet;
            SharedUIManager.OrganizationSelected += OnOrganizationSet;
            
            SharedUIManager.Instance.SearchBar.UnregisterValueChangedCallback(OnSearchBarValueChanged);
            SharedUIManager.Instance.SearchBar.RegisterValueChangedCallback(OnSearchBarValueChanged);
            SharedUIManager.Instance.SearchBar.UnregisterValueChangingCallback(OnSearchBarValueChanging);
            SharedUIManager.Instance.SearchBar.RegisterValueChangingCallback(OnSearchBarValueChanging);
            
            SharedUIManager.Instance.AssetGridView.parent.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.AssetGridView.parent.RegisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.AssetGridView.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.AssetGridView.RegisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.SortingDropdown?.UnregisterValueChangedCallback(OnSortingDropdownValueChanged);
            SharedUIManager.Instance.SortingDropdown?.RegisterValueChangedCallback(OnSortingDropdownValueChanged);
            
            InitializeExtraUIController();
        }

        protected override void UninitializeUI()
        {
            AssetIconLoadFailed -= OnAssetIconLoadedFailed;
            m_tips?.Dismiss();
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                if (!PlayerPrefs.HasKey(AssetsUIToolkitController.k_LastSelectedOrgKey))
                {
                    SharedUIManager.Instance.IdentityContainer.DisplayOn();
                }
            }
            var sharedUIManagerInstance = SharedUIManager.Instance;
            if (sharedUIManagerInstance != null)
            {
                sharedUIManagerInstance.OrganizationButton.clicked -= OnOrganizationButtonClicked;
                sharedUIManagerInstance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_AssetRoot).DisplayOn();
                sharedUIManagerInstance.AssetGridView.bindItem -= AssetGridBindItem;
                sharedUIManagerInstance.AssetGridView.unbindItem -= AssetGridUnbindItem;
                sharedUIManagerInstance.AssetGridView.selectionChanged -= OnAssetSelectedOnGrid;

                sharedUIManagerInstance.AMButton.clicked -= OnAMButtonClicked;

                SharedUIManager.OrganizationSelected -= OnOrganizationSet;

                sharedUIManagerInstance.SearchBar.UnregisterValueChangedCallback(OnSearchBarValueChanged);
                sharedUIManagerInstance.SearchBar.UnregisterValueChangingCallback(OnSearchBarValueChanging);

                sharedUIManagerInstance.AssetGridView.parent.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
                sharedUIManagerInstance.AssetGridView.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
                sharedUIManagerInstance.SortingDropdown?.UnregisterValueChangedCallback(OnSortingDropdownValueChanged);
            }
        }

        private void OnOrganizationSet(IOrganization obj)
        {
            SharedUIManager.Instance.OrganizationButton.label = obj.Name;
        }

        protected override void OnNetworkStatusChanged(bool connected)
        {
            if (connected)
            {
                UninitializedCallBacks();
                return;
            }

            if (!NetworkDetector.RequestedOfflineMode)
            {
                m_AssetInfoUIBaseController?.ClearUI();
                SharedUIManager.SelectedAsset = null;
                UninitializedCallBacks();
                return;
            }
            
            if (!m_Initialized)
            {
                m_Initialized = true;
                RegisterCallbacks();
                InitializeUI();
            }
            m_AssetInfoUIBaseController?.ClearUI();
            SharedUIManager.SelectedAsset = null;
            RefreshUI();
            return;
            
            void UninitializedCallBacks()
            {
                m_NoAssetText?.RemoveFromHierarchy();
                m_NoAssetText = null;
                
                if(!m_Initialized) return;
                SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.None;
                m_AssetInfoUIBaseController?.UnregisterCallbacks();
                m_Initialized = false;
                UnregisterCallbacks();
                UninitializeUI();
            }
        }

        private void OnAssetsLoaded(List<AssetInfo> allAssets)
        {
            SharedUIManager.Instance.AssetsContainer.SetEnabled(true);
            SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.Flex;
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_AssetRoot).DisplayOn();
            SharedUIManager.Instance.ClearGridView();
            
            if(allAssets == null || allAssets.Count == 0) return;
            SharedUIManager.Instance.AssetGridView.itemsSource = allAssets;
        }
        
        private async void RefreshUI()
        {
            if (m_AuthenticationState != AuthenticationState.LoggedIn)
            {
                return;
            }
            SharedUIManager.Instance.RefreshAssetButton.SetEnabled(false);
            SharedUIManager.Instance.PathText.text = string.Empty;
            SharedUIManager.Instance.ClearGridView();
            SharedUIManager.Instance.OrganizationButton.label = await 
                SharedUIManager.Instance.OrganizationPlaceholder.GetTitleLocalizedStringForAppUIAsync();

            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;
                SharedUIManager.Instance.AssetsContainer.DisplayOn();
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_AssetRoot).DisplayOff();
            }
        }

        protected override void OnAMButtonClicked()
        {
            var stringToOpen = string.Empty;
            if (SharedUIManager.SelectedAsset.HasValue)
            {
                stringToOpen = $"https://cloud.unity.com/home/organizations/{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.OrganizationId.ToString()}/projects/{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectId.ToString()}/assets?assetId={SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId.ToString()}:{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetVersion.ToString()}";
            } else if (SharedUIManager.AssetProjectInfo.HasValue)
            {
                stringToOpen =
                    $"https://cloud.unity.com/home/organizations/{SharedUIManager.AssetProjectInfo.Value.AssetProject.Descriptor.OrganizationId.ToString()}/projects/{SharedUIManager.AssetProjectInfo.Value.AssetProject.Descriptor.ProjectId.ToString()}/assets";
            }
            else if(SharedUIManager.Organization != null)
            {
                stringToOpen = $"https://cloud.unity.com/home/organizations/{SharedUIManager.Organization.Id}/assets/all";
            }

            if (string.IsNullOrEmpty(stringToOpen))
            {
                return;
            }
            
            Application.OpenURL(stringToOpen);
        }
        
        protected override async void OnOrganizationListReceived(List<IOrganization> listOfOrg)
        {
            base.OnOrganizationListReceived(listOfOrg);
            if (listOfOrg == null || listOfOrg.Count == 0)
            {
                if (m_NoAssetText != null) return;
                m_NoAssetText = new Text(await m_NoAssetsFoundForUserString.GetTitleLocalizedStringForAppUIAsync())
                {
                    size = TextSize.XXXL,
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        position = Position.Absolute,
                        top = 0,
                        bottom = 0,
                        left = 0,
                        right = 0
                    },
                    pickingMode = PickingMode.Ignore
                };
                m_NoAssetTextParent.Add(m_NoAssetText);
                if (SharedUIManager.Instance.IdentityContainer.resolvedStyle.display == DisplayStyle.Flex)
                {
                    SharedUIManager.Instance.IdentityContainer.DisplayOff();
                }
                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    SharedUIManager.Instance.AssetsContainer.DisplayOn();
                    SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_AssetRoot).DisplayOff();
                }
                return;
            }

            SharedUIManager.Instance.OrganizationButton.label = await 
                SharedUIManager.Instance.OrganizationPlaceholder.GetTitleLocalizedStringForAppUIAsync();
        }

        protected override void InitializeExtraUIController()
        {
            //New Asset information controller
            m_AssetInfoUIBaseController ??= new OfflineAssetInfoController();
            m_AssetInfoUIBaseController.RegisterCallbacks();
            ShowOrgSelectionTips();
        }

        private async void ShowOrgSelectionTips()
        {
            if (SceneManager.GetActiveScene() != gameObject.scene || 
                SharedUIManager.Instance.OrganizationButton.resolvedStyle.display != DisplayStyle.Flex) return;

            var offlineAsset = StreamingUtils.ReturnOfflineAssets();
            
            if(offlineAsset == null || offlineAsset.Count == 0) return;
            
            var contentView = new Text(await m_SelectOrganizationHereString.GetTitleLocalizedStringForAppUIAsync())
            {
                style =
                {
                    marginBottom = 12,
                    marginTop = 12,
                    marginLeft = 12,
                    marginRight = 12,
                    height = 20,
                    minWidth = 286
                }
            };
            
            var orgIcon = SharedUIManager.Instance.OrganizationButton.Q<Icon>("appui-actionbutton__icon");
            m_tips = Popover
                .Build(orgIcon, contentView)
                .SetPlacement(PopoverPlacement.BottomStart)
                .SetArrowVisible(true)
                .SetModalBackdrop(false)
                .SetOutsideClickDismiss(false)
                .SetOffset(3)
                .SetCrossOffset(-12);

            m_tips.Show();
            SharedUIManager.Instance.OrganizationButton.clicked += OrganizationButtonOnClicked;
            return;

            void OrganizationButtonOnClicked()
            {
                SharedUIManager.Instance.OrganizationButton.clicked -= OrganizationButtonOnClicked;
                m_tips.Dismiss();
            }
        }
        
        protected override ActionButton ReturnAssetProjectButton(AssetProjectInfo assetProjectInfo)
        {
            var newAssetProjectButton = new ActionButton()
            {
                tooltip = ((OfflineAssetProject)assetProjectInfo.AssetProject).OfflineAssetProjectName,
                label = ((OfflineAssetProject)assetProjectInfo.AssetProject).OfflineAssetProjectName,
                userData = assetProjectInfo,
                quiet = true
            };
            return newAssetProjectButton;
        }

        protected override void DeselectExisting(AssetInfo assetInfo)
        {
            
        }

        public override void SetPathText(AssetInfo? assetInfo, AssetProjectInfo? assetProject, IAssetCollection collection)
        {
            StringBuilder sb = new StringBuilder();
            if (assetProject.HasValue)
            {
                if (assetProject.Value.AssetProject is OfflineAssetProject project)
                {
                    sb.Append(project.OfflineAssetProjectName);
                }
                else
                {
                    return;
                }
            }
            
            if (collection != null)
            {
                sb.Append(" / ");
                ReturnCollectionPathForText(collection.Descriptor.Path.GetPathComponents(), assetInfo == null, ref sb);
            }

            if (assetInfo.HasValue)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" / ");
                }

                sb.Append(FormatLastPathPart(((OfflineAsset)assetInfo.Value.Asset).OfflineAssetInfo.assetName));
            }
            
            SharedUIManager.Instance.PathText.text = sb.ToString();
            
            if(assetInfo == null) return;
            
            var itemsSource = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
            if (itemsSource == null)
            {
                return;
            }
            
            int index = itemsSource.FindIndex(x => x.Asset.Descriptor.AssetId == assetInfo.Value.Asset.Descriptor.AssetId
                                                   && x.Asset.Descriptor.ProjectId == assetInfo.Value.Asset.Descriptor.ProjectId &&
                                                   x.Asset.Descriptor.OrganizationId == assetInfo.Value.Asset.Descriptor.OrganizationId && 
                                                   x.Asset.Descriptor.AssetVersion == assetInfo.Value.Asset.Descriptor.AssetVersion);
            if (index == -1)
            {
                return;
            }
            itemsSource[index] = assetInfo.Value;
        }

        protected override void DisplayItem(AssetInfo assetInfo, VisualElement item)
        {
            if(assetInfo.Asset is not OfflineAsset offlineAsset) return;
            UpdateItemProperties(item, offlineAsset.OfflineAssetInfo.assetName, offlineAsset.OfflineAssetInfo.assetType, offlineAsset.OfflineAssetInfo.created, out var iconPlaceHolder);
            HandleAssetThumbnail(assetInfo, iconPlaceHolder);
        }

        protected override void HandleAssetThumbnail(AssetInfo assetInfo, VisualElement iconPlaceHolder)
        {
            if (assetInfo.Asset is not OfflineAsset offlineAsset)
            {
                return;
            }

            if (!string.IsNullOrEmpty(offlineAsset.OfflineAssetInfo.previewPic))
            {
                HandleThumbnailDownload(iconPlaceHolder, offlineAsset.Descriptor.AssetId.GetHashCode(), bindingId =>
                {
                    _ = TextureDownload.DownloadThumbnail(offlineAsset.Descriptor.AssetId.GetHashCode(), offlineAsset.Descriptor.AssetVersion.ToString(), offlineAsset.OfflineAssetInfo.previewPic, textureResult =>
                    {
                        OnThumbnailDownloaded(iconPlaceHolder, bindingId, textureResult, offlineAsset.OfflineAssetInfo.assetType);
                    });
                });
            }
            else
            {
                AssetIconLoadFailed.Invoke(iconPlaceHolder, offlineAsset.OfflineAssetInfo.assetType);
            }
        }

        protected override void UpdateSearchResult(string value)
        {
            OfflineModeAssetsController.SearchAssets?.Invoke(value);
        }

        protected override void SortingChanged(SortingType sortingType, string searchText)
        {
            OfflineModeAssetsController.AssetDeselected?.Invoke();
            OfflineModeAssetsController.SortingTypeChangedEvent?.Invoke(sortingType, searchText);
        }
    }
}
