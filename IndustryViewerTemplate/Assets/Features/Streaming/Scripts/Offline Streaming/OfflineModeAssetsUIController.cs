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
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(-50)]
    public class OfflineModeAssetsUIController : AssetsUIBaseController
    {
        private VisualElement m_AssetCreationPanelRoot;
        
        private AuthenticationState m_AuthenticationState;
        
        private List<(string OrgName, string OrgId)> m_Organizations;

        private void Start()
        {
            IdentityController.AuthenticationStateChangedEvent += OnAuthenticationStateChanged;
        }

        protected override void OnDestroy()
        {
            UnregisterCallbacks();
            UninitializeUI();
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
            m_AssetInfoUIBaseController?.DisposeUI();
        }

        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            m_AuthenticationState = state;
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
            AssetIconLoadFailed -= OnAssetIconLoadedFailed;
            AssetIconLoadFailed += OnAssetIconLoadedFailed;
            
            SharedUIManager.Instance.AMButton.clicked -= OnAMButtonClicked;
            SharedUIManager.Instance.AMButton.clicked += OnAMButtonClicked;
            SharedUIManager.Instance.OrganizationButton.clicked -= OnOrganizationButtonClicked;
            SharedUIManager.Instance.OrganizationButton.clicked += OnOrganizationButtonClicked;
            
            SharedUIManager.Instance.AssetGridView.bindItem -= AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.bindItem += AssetGridBindItem;
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
            
            SharedUIManager.Instance.OrganizationButton.clicked -= OnOrganizationButtonClicked;
            
            SharedUIManager.Instance.AssetGridView.bindItem -= AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.selectionChanged -= OnAssetSelectedOnGrid;
            
            SharedUIManager.Instance.AMButton.clicked -= OnAMButtonClicked;
            
            SharedUIManager.OrganizationSelected -= OnOrganizationSet;
            
            SharedUIManager.Instance.SearchBar.UnregisterValueChangedCallback(OnSearchBarValueChanged);
            SharedUIManager.Instance.SearchBar.UnregisterValueChangingCallback(OnSearchBarValueChanging);
            
            SharedUIManager.Instance.AssetGridView.parent.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.AssetGridView.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.SortingDropdown?.UnregisterValueChangedCallback(OnSortingDropdownValueChanged);
        }

        private void OnOrganizationSet(IOrganization obj)
        {
            SharedUIManager.Instance.OrganizationButton.ClearBinding("label");
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
            SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.Flex;
            SharedUIManager.Instance.ClearGridView();
            
            if(allAssets == null || allAssets.Count == 0) return;
            SharedUIManager.Instance.AssetGridView.itemsSource = allAssets;
        }
        
        private void RefreshUI()
        {
            if (m_AuthenticationState != AuthenticationState.LoggedIn)
            {
                return;
            }
            SharedUIManager.Instance.RefreshAssetButton.SetEnabled(false);
            SharedUIManager.Instance.PathText.text = string.Empty;
            SharedUIManager.Instance.ClearGridView();
            SharedUIManager.Instance.OrganizationButton.label = string.Empty;
            SharedUIManager.Instance.OrganizationButton.ClearBinding("label");
            SharedUIManager.Instance.OrganizationButton.SetBinding("label", SharedUIManager.Instance.SelectOrganization);

            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;
                SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.None;
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
        
        protected override void OnOrganizationListReceived(List<IOrganization> listOfOrg)
        {
            base.OnOrganizationListReceived(listOfOrg);
            if(listOfOrg == null || listOfOrg.Count == 0) return;
            SharedUIManager.Instance.OrganizationButton.SetBinding("label", SharedUIManager.Instance.SelectOrganization);
        }

        protected override void InitializeExtraUIController()
        {
            //New Asset information controller
            m_AssetInfoUIBaseController ??= new OfflineAssetInfoController();
            m_AssetInfoUIBaseController.RegisterCallbacks();
        }
        
        protected override ActionButton ReturnAssetProjectButton(AssetProjectInfo assetProjectInfo)
        {
            var newAssetProjectButton = new ActionButton()
            {
                tooltip = ((OfflineAssetProject)assetProjectInfo.AssetProject).OfflineAssetProjectName,
                label = ((OfflineAssetProject)assetProjectInfo.AssetProject).OfflineAssetProjectName,
                userData = assetProjectInfo,
            };
            return newAssetProjectButton;
        }

        protected override void DeselectExisting(AssetInfo assetInfo)
        {
            
        }

        protected void OnAssetSelectedOnGrid(AssetInfo assetInfo)
        {
            OfflineModeAssetsController.AssetSelected?.Invoke(assetInfo);
        }

        protected override void SetPathText(AssetInfo? assetInfo, AssetProjectInfo? assetProject, IAssetCollection collection)
        {
            StringBuilder sb = new StringBuilder();
            if (assetProject.HasValue)
            {
                if (assetProject.Value is OfflineAssetProject)
                {
                    sb.Append(((OfflineAssetProject)assetProject.Value.AssetProject).OfflineAssetProjectName);
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
                sb.Append("<b>" + ((OfflineAsset)assetInfo.Value.Asset).OfflineAssetInfo.assetName + "</b>");
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
                _ = TextureDownload.DownloadThumbnail(offlineAsset.Descriptor.AssetId.GetHashCode(), offlineAsset.OfflineAssetInfo.previewPic, textureResult =>
                {
                    if (textureResult != null)
                    {
                        iconPlaceHolder.style.backgroundImage = textureResult;
                    }
                    else
                    {
                        AssetIconLoadFailed.Invoke(iconPlaceHolder, offlineAsset.OfflineAssetInfo.assetType);
                    }
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
