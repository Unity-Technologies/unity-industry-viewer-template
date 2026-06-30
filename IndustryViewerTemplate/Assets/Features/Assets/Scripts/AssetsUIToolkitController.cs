using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Localization;
using System.Text;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Assets
{
    // This script is the controller for managing and displaying asset information in the Unity UI Toolkit.
    // It handles the initialization and binding of UI elements, as well as the registration of event handlers.
    // The script manages the display of asset details, including name, type, status, and version information.
    // It also handles downloading assets, updating to the latest version, and loading associated datasets and files.
    // The script integrates with Unity Cloud services and responds to various events from the AssetsController and IdentityController.
    public class AssetsUIToolkitController : AssetsUIBaseController
    {
        public const string k_LastSelectedOrgKey = "LastSelectedOrg";
        private const string k_LoginButtonName = "LoginButton";
        
        // Event handler for when an asset icon fails to load
        
        // UI Assets for the assets view
        [SerializeField]
        protected UIDocument m_AssetsUIDocument;
        
        [SerializeField]
        private VisualTreeAsset m_OrganizationPopoverTemplate;
        
        [SerializeField]
        protected VisualTreeAsset m_AssetItemTemplate;
        
        [SerializeField]
        private StyleSheet m_AssetsStyleSheet;
        
        // Scriptable object for asset placeholder icons
        [SerializeField]
        protected AssetPlaceHolderScriptableObject m_AssetPlaceHolderScriptableObject;

        [SerializeField]
        private VisualTreeAsset m_AssetCreationTemplate;

        [SerializeField]
        private VisualTreeAsset m_AssetCreationRequestsPopupTemplate;

        [SerializeField]
        private StyleSheet m_AssetsCreationStyleSheet;

        // UI elements for the assets view

        private AssetCreationUIToolkitController m_AssetCreationUIToolkitController;
        
        private Coroutine m_SearchPauseCoroutine;
        private string m_SearchText;
        private WaitForSeconds m_SearchPauseWait = new WaitForSeconds(1f);

        #region Localization

        [SerializeField]
        protected LocalizedString m_SelectOrganizationLocalizedString;
        [SerializeField]
        protected LocalizedString m_OrganizationPlaceholderLocalizedString;
        [SerializeField]
        protected LocalizedString m_NoProjectFoundLocalizedString;
        [SerializeField]
        protected LocalizedString m_NoOrganizationFoundLocalizedString;
        [SerializeField]
        protected LocalizedString m_NewVersionAvailableLocalizedString;
        [SerializeField]
        protected LocalizedString m_ViewNewVersionLocalizedString;

        #endregion

        protected override void Awake()
        {
            base.Awake();
            if (SharedUIManager.Instance == null)
            {
                _ = new SharedUIManager(m_AssetsUIDocument,
                    m_NoProjectFoundLocalizedString,
                    m_NoOrganizationFoundLocalizedString,
                    m_SelectOrganizationLocalizedString,
                    m_OrganizationPlaceholderLocalizedString,
                    m_NewVersionAvailableLocalizedString,
                    m_ViewNewVersionLocalizedString,
                    m_OrganizationPopoverTemplate,
                    m_AssetItemTemplate,
                    m_AssetPlaceHolderScriptableObject);
            }
            
            m_AssetsUIDocument.rootVisualElement.AddStyleSheetIfMissing(m_AssetsStyleSheet);
        }

        // Initialization
        protected override void Start()
        {
            base.Start();
            SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
        }

        // Cleanup and uninitialization
        protected override void OnDestroy()
        {
            base.OnDestroy();
            IdentityController.AuthenticationStateChangedEvent -= OnAuthStateChanged;
            UninitializeUI();
            // Dispose AFTER UninitializeUI: Dispose() nulls SharedUIManager.Instance, and
            // UninitializeUI skips its entire unsubscribe block when Instance is null — disposing
            // first would silently leak every grid/search/sorting/button callback subscription.
            // Null-conditional guards the known destroy-order case where the singleton is already gone.
            SharedUIManager.Instance?.Dispose();
        }
        
        private async void ShowSelectOrgButton()
        {
            var loginButton = SharedUIManager.Instance.IdentityContainer.Q<ActionButton>(k_LoginButtonName);
            if (loginButton != null)
            {
                loginButton.style.display = DisplayStyle.Flex;
                loginButton.SetEnabled(true);
                loginButton.clicked -= LoginButtonOnClicked;
                loginButton.clicked += LoginButtonOnClicked;
                loginButton.accent = true;
                loginButton.selected = true;
                loginButton.label = await m_SelectOrganizationLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            }
        }

        protected override void RegisterCallbacks()
        {
            SharedUIManager.OrganizationSelected -= OnOrganizationSelected;
            SharedUIManager.OrganizationSelected += OnOrganizationSelected;
            
            SharedUIManager.AssetProjectSelected -= OnAssetProjectSelected;
            SharedUIManager.AssetProjectSelected += OnAssetProjectSelected;
            
            SharedUIManager.AssetCollectionSelected -= AssetCollectionSelected;
            SharedUIManager.AssetCollectionSelected += AssetCollectionSelected;

            SharedUIManager.AssetSelected -= OnAssetSelectedOnUI;
            SharedUIManager.AssetSelected += OnAssetSelectedOnUI;
            
            AssetsController.OrganizationsLoaded -= OnOrganizationListReceived;
            AssetsController.OrganizationsLoaded += OnOrganizationListReceived;
            
            AssetsController.AssetDeselected -= OnAssetDeselected;
            AssetsController.AssetDeselected += OnAssetDeselected;
            
            AssetsController.AssetsLoaded -= OnAssetsFilteredInBatch;
            AssetsController.AssetsLoaded += OnAssetsFilteredInBatch;
        }

        private void OnOrganizationSelected(IOrganization organization)
        {
            SetNewAssetButtonState();

            if (organization == null) return;
            m_SearchText = string.Empty;
            if (m_SearchPauseCoroutine != null)
            {
                StopCoroutine(m_SearchPauseCoroutine);
                m_SearchPauseCoroutine = null;
            }
            SharedUIManager.Instance.AssetProjectScrollList?.Clear();
            SharedUIManager.Instance?.ClearGridView();
            AssetsController.RequestAssetProjects(organization, OnAssetProjectsLoaded);
            AssetsController.RequestAssets.Invoke(true, m_SearchText);
        }

        private void OnAssetProjectSelected(AssetProjectInfo? assetProject)
        {
            SetNewAssetButtonState();

            if (!assetProject.HasValue) return;
            m_SearchText = string.Empty;
            if (m_SearchPauseCoroutine != null)
            {
                StopCoroutine(m_SearchPauseCoroutine);
                m_SearchPauseCoroutine = null;
            }
            AssetsController.GetAssetCollectionsForProject.Invoke(assetProject.Value, OnCollectionsLoaded);
            AssetsController.RequestAssets.Invoke(false, m_SearchText);
        }

        protected override void UnregisterCallbacks()
        {
            SharedUIManager.OrganizationSelected -= OnOrganizationSelected;
            SharedUIManager.AssetProjectSelected -= OnAssetProjectSelected;
            SharedUIManager.AssetCollectionSelected -= AssetCollectionSelected;
            SharedUIManager.AssetSelected -= OnAssetSelectedOnUI;
            AssetsController.OrganizationsLoaded -= OnOrganizationListReceived;
            AssetsController.AssetDeselected -= OnAssetDeselected;
            AssetsController.AssetsLoaded -= OnAssetsFilteredInBatch;
        }

        private void OnAssetSelectedOnUI(AssetInfo assetInfo)
        {
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                AssetsController.AssetSelected?.Invoke(assetInfo);
            }
        }

        private void SetNewAssetButtonState()
        {
            var disabled = IdentityController.GuestMode
                || NetworkDetector.RequestedOfflineMode
                || NetworkDetector.IsOffline
                || SharedUIManager.Organization == null
                || SharedUIManager.AssetProjectInfo == null
                || Application.platform == RuntimePlatform.WebGLPlayer; // File browser is not supported on WebGL
            
            SharedUIManager.Instance.NewAssetButton?.SetEnabled(!disabled);
        }

        protected override void OnNetworkStatusChanged(bool connected)
        {
            SetNewAssetButtonState();

            if (!connected)
            {
                if (!NetworkDetector.RequestedOfflineMode)
                {
                    SharedUIManager.Instance.AssetsContainer?.SetEnabled(false);
                    SharedUIManager.Instance.OrganizationButton?.SetEnabled(false);
                    return;
                }
                m_AssetInfoUIBaseController?.ClearUI();
                SharedUIManager.SelectedAsset = null;
                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    //if user request offline mode, clear all UI
                    AssetsController.AssetDeselected?.Invoke();
                }
                
                if (!m_Initialized) return;
                m_Initialized = false;
                IdentityController.AuthenticationStateChangedEvent -= OnAuthStateChanged;
                
                UninitializeUI();
                UnregisterCallbacks();
                return;
            }
            
            SharedUIManager.Instance.AssetsContainer?.SetEnabled(true);
            SharedUIManager.Instance.OrganizationButton?.SetEnabled(true);
            if (SharedUIManager.Instance.OrganizationButton != null)
            {
                if (SharedUIManager.Instance.OrganizationButton.style.display == DisplayStyle.Flex ||
                    SharedUIManager.Instance.OrganizationButton.resolvedStyle.display == DisplayStyle.Flex)
                {
                    SharedUIManager.Instance.AssetsContainer.DisplayOn();
                }
            }
            
            if(m_Initialized) return;
            m_AssetInfoUIBaseController?.ClearUI();
            SharedUIManager.SelectedAsset = null;
            m_Initialized = true;
            IdentityController.AuthenticationStateChangedEvent -= OnAuthStateChanged;
            IdentityController.AuthenticationStateChangedEvent += OnAuthStateChanged;
            RegisterCallbacks();
            InitializeUI();
        }

        // Uninitialization
        protected override void UninitializeUI()
        {
            AssetIconLoadFailed -= OnAssetIconLoadedFailed;

            var sharedUIManagerInstance = SharedUIManager.Instance;
            if (sharedUIManagerInstance != null)
            {
                sharedUIManagerInstance.AMButton.clicked -= OnAMButtonClicked;
                sharedUIManagerInstance.OrganizationButton.clicked -= OnOrganizationButtonClicked;
                sharedUIManagerInstance.AssetGridView.selectionChanged -= OnAssetSelectedOnGrid;
                sharedUIManagerInstance.AssetGridView.bindItem -= AssetGridBindItem;
                sharedUIManagerInstance.AssetGridView.unbindItem -= AssetGridUnbindItem;
                sharedUIManagerInstance.AssetGridView.parent.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
                sharedUIManagerInstance.AssetGridView.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);

                sharedUIManagerInstance.SearchBar?.UnregisterValueChangingCallback(OnSearchBarValueChanging);
                sharedUIManagerInstance.SearchBar?.UnregisterValueChangedCallback(OnSearchBarValueChanged);

                sharedUIManagerInstance.SortingDropdown?.UnregisterValueChangedCallback(OnSortingDropdownValueChanged);
                sharedUIManagerInstance.NewAssetButton.clicked -= OnNewAssetButtonClicked;

                sharedUIManagerInstance.RefreshAssetButton.clicked -= OnRefreshButtonClicked;
            }

            m_AssetInfoUIBaseController?.UnregisterCallbacks();
            m_AssetCreationUIToolkitController?.Dispose();
            m_AssetCreationUIToolkitController = null;
        }

        // Event handler for when the authentication state changes
        private void OnAuthStateChanged(AuthenticationState state)
        {
            SetNewAssetButtonState();

            var loginButton = SharedUIManager.Instance.IdentityContainer.Q<ActionButton>(k_LoginButtonName);
            if (loginButton != null)
            {
                loginButton.clicked -= LoginButtonOnClicked;
            }
            
            if(SharedUIManager.Instance.AssetsContainer.style.display == DisplayStyle.None) return;
            if (state is not (AuthenticationState.AwaitingLogout or AuthenticationState.LoggedOut)) return;
            OnUserLoggedOut();
        }

        // Initialization of the UI
        protected override async void InitializeUI()
        {
            SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
            
            AssetIconLoadFailed -= OnAssetIconLoadedFailed;
            AssetIconLoadFailed += OnAssetIconLoadedFailed;
            
            SharedUIManager.Instance.OrganizationButton.label = await SharedUIManager.Instance.OrganizationPlaceholder.GetTitleLocalizedStringForAppUIAsync();
            SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.None;
            SharedUIManager.Instance.OrganizationButton.clicked -= OnOrganizationButtonClicked;
            SharedUIManager.Instance.OrganizationButton.clicked += OnOrganizationButtonClicked;

            SharedUIManager.Instance.AssetGridView.parent.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.AssetGridView.parent.RegisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.AssetGridView.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.AssetGridView.RegisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
            SharedUIManager.Instance.ClearGridView();
            
            RefreshGridViewSize();

            SharedUIManager.Instance.SearchBar.UnregisterValueChangedCallback(OnSearchBarValueChanged);
            SharedUIManager.Instance.SearchBar.RegisterValueChangedCallback(OnSearchBarValueChanged);
            SharedUIManager.Instance.SearchBar.UnregisterValueChangingCallback(OnSearchBarValueChanging);
            SharedUIManager.Instance.SearchBar.RegisterValueChangingCallback(OnSearchBarValueChanging);
            
            SharedUIManager.Instance.SortingDropdown.bindItem = SortingBindItem;
            SharedUIManager.Instance.SortingDropdown.sourceItems = AssetSortingExtensions.AssetTypeList();
            SharedUIManager.Instance.SortingDropdown?.UnregisterValueChangedCallback(OnSortingDropdownValueChanged);
            SharedUIManager.Instance.SortingDropdown?.RegisterValueChangedCallback(OnSortingDropdownValueChanged);
            SharedUIManager.Instance.SortingDropdown?.SetValueWithoutNotify(new []{0});

            SharedUIManager.Instance.NewAssetButton.clicked -= OnNewAssetButtonClicked;
            SharedUIManager.Instance.NewAssetButton.clicked += OnNewAssetButtonClicked;

            SharedUIManager.Instance.RefreshAssetButton.clicked -= OnRefreshButtonClicked;
            SharedUIManager.Instance.RefreshAssetButton.clicked += OnRefreshButtonClicked;
            
            SharedUIManager.Instance.RefreshAssetButton?.SetEnabled(true);

            SharedUIManager.Instance.AMButton.clicked -= OnAMButtonClicked;
            SharedUIManager.Instance.AMButton.clicked += OnAMButtonClicked;

            SharedUIManager.Instance.PathText.text = string.Empty;
            
            SharedUIManager.Instance.AssetGridView.bindItem -= AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.bindItem += AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem -= AssetGridUnbindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem += AssetGridUnbindItem;
            SharedUIManager.Instance.AssetGridView.selectionChanged -= OnAssetSelectedOnGrid;
            SharedUIManager.Instance.AssetGridView.selectionChanged += OnAssetSelectedOnGrid;

            SetBackgroundTint(false);
            InitializeExtraUIController();
        }

        protected override void OnAMButtonClicked()
        {
            var stringToOpen = string.Empty;
            if (SharedUIManager.SelectedAsset.HasValue)
            {
                stringToOpen = $"https://cloud.unity.com/home/organizations/{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.OrganizationId}/projects/{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectId}/assets?assetId={SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId}:{SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetVersion.ToString()}";
            } else if (SharedUIManager.AssetProjectInfo.HasValue)
            {
                stringToOpen =
                    $"https://cloud.unity.com/home/organizations/{SharedUIManager.AssetProjectInfo.Value.AssetProject.Descriptor.OrganizationId}/projects/{SharedUIManager.AssetProjectInfo.Value.AssetProject.Descriptor.ProjectId}/assets";
            }
            else if(AssetsController.SelectedOrganization != null)
            {
                stringToOpen = $"https://cloud.unity.com/home/organizations/{AssetsController.SelectedOrganization.Id}/assets/all";
            }
            
            if(string.IsNullOrEmpty(stringToOpen)) return;
            
            Application.OpenURL(stringToOpen);
        }

        private static void OnRefreshButtonClicked()
        {
            SharedUIManager.Instance?.ClearGridView();
            AssetsController.AssetDeselected?.Invoke();
            AssetsController.AssetSearch?.Invoke(SharedUIManager.Instance.SearchBar.value);
        }

        public override void SetPathText(AssetInfo? assetInfo, AssetProjectInfo? assetProject, IAssetCollection collection)
        {
            StringBuilder sb = new StringBuilder();
            if (assetProject.HasValue)
            {
                sb.Append(assetProject.Value.Properties.Value.Name);
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

                sb.Append(FormatLastPathPart(assetInfo.Value.Properties.Value.Name));
            }
            
            SharedUIManager.Instance.PathText.text = sb.ToString();
            
            if(assetInfo == null) return;
            
            var itemsSource = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
            if (itemsSource == null)
            {
                return;
            }
            
            int index = itemsSource.FindIndex(x => x.Asset.Descriptor.AssetId == assetInfo.Value.Asset.Descriptor.AssetId);
            if (index == -1)
            {
                return;
            }
            itemsSource[index] = assetInfo.Value;
        }

        protected override void DisplayItem(AssetInfo asset, VisualElement item)
        {
            UpdateItemProperties(item, asset.Properties.Value.Name, asset.Properties.Value.Type, asset.Properties.Value.AuthoringInfo.Created, out var iconPlaceHolder);
            HandleAssetThumbnail(asset, iconPlaceHolder);
        }

        protected override void InitializeExtraUIController()
        {
            m_AssetsUIDocument.rootVisualElement.AddStyleSheetIfMissing(m_AssetsCreationStyleSheet);

            m_AssetInfoUIBaseController ??= new AssetsInfoUIToolkitController();
            m_AssetInfoUIBaseController.RegisterCallbacks();

            m_AssetCreationUIToolkitController ??= new AssetCreationUIToolkitController(
                SharedUIManager.Instance.AssetsContainer, m_AssetCreationTemplate, m_AssetCreationRequestsPopupTemplate);
        }

        private void OnNewAssetButtonClicked()
        {
            if (m_AssetCreationUIToolkitController.IsVisible())
            {
                return;
            }
            
            m_AssetCreationUIToolkitController.Show();
        }

        #region Assets UI
        
        // Handles the selection of an asset in the grid view
        protected override void OnAssetSelectedOnGrid(IEnumerable<object> obj)
        {
            if (m_AssetCreationUIToolkitController.IsVisible())
            {
                m_AssetCreationUIToolkitController.Close();
            }
            
            base.OnAssetSelectedOnGrid(obj);
        }

        protected override void DeselectExisting(AssetInfo assetInfo)
        {
            if(SceneManager.GetActiveScene() != gameObject.scene) return;
            if (AssetsController.SelectedAsset != null)
            {
                if(assetInfo == AssetsController.SelectedAsset.Value) return;
                AssetsController.AssetDeselected?.Invoke();
            }
        }

        protected override void HandleAssetThumbnail(AssetInfo assetInfo, VisualElement iconPlaceHolder)
        {
            if (assetInfo.Properties.Value.PreviewFileDescriptor != null)
            {
                HandleThumbnailDownload(iconPlaceHolder, assetInfo.Asset.Descriptor.AssetId.GetHashCode(), bindingId =>
                {
                    _ = TextureDownload.DownloadThumbnail(assetInfo.Asset, textureResult =>
                    {
                        OnThumbnailDownloaded(iconPlaceHolder, bindingId, textureResult, assetInfo.Properties.Value.Type);
                    });
                });
            }
            else
            {
                AssetIconLoadFailed.Invoke(iconPlaceHolder, assetInfo.Properties.Value.Type);
            }
        }
        
        private void OnAssetsFiltered(IAsset asset)
        {
            if(asset == null) return;
            
            SharedUIManager.Instance.AssetGridView.itemsSource ??= new List<IAsset>();
            
            var itemsSource = SharedUIManager.Instance.AssetGridView.itemsSource as List<IAsset>;
            
            if(itemsSource.Contains(asset)) return;
            
            itemsSource?.Add(asset);
            
            SharedUIManager.Instance.AssetGridView.itemsSource = itemsSource;
        }
        
        private void OnAssetsFilteredInBatch(List<AssetInfo> assets)
        {
            SharedUIManager.Instance.AssetGridView.itemsSource = assets;
        }

        protected override void SortingChanged(SortingType sortingType, string searchText)
        {
            AssetsController.AssetDeselected?.Invoke();
            AssetsController.UpdateSortingType?.Invoke(sortingType, searchText);
        }

        protected override void UpdateSearchResult(string value)
        {
            if(!string.IsNullOrEmpty(m_SearchText) && !string.Equals(value, m_SearchText) && m_SearchPauseCoroutine != null)
            {
                StopCoroutine(m_SearchPauseCoroutine);
                m_SearchPauseCoroutine = null;
            }
            m_SearchText = value;
            m_SearchPauseCoroutine = StartCoroutine(WaitForInputFinished());
        }

        private IEnumerator WaitForInputFinished()
        {
            yield return m_SearchPauseWait;
            AssetsController.AssetSearch?.Invoke(m_SearchText);
        }

        #endregion
        
        #region Collections UI

        protected override ActionButton ReturnAssetProjectButton(AssetProjectInfo assetProjectInfo)
        {
            var newAssetProjectButton = new ActionButton()
            {
                tooltip = assetProjectInfo.Properties.Value.Name,
                label = assetProjectInfo.Properties.Value.Name,
                userData = assetProjectInfo,
                quiet = true
            };
            return newAssetProjectButton;
        }

        #endregion
        
        #region Organization UI
        protected override void OnOrganizationSelectionChanged(IEnumerable<object> item)
        {
            base.OnOrganizationSelectionChanged(item);
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.Flex;
            }
            
            var loginButton = SharedUIManager.Instance.IdentityContainer.Q<ActionButton>(k_LoginButtonName);
            if (loginButton != null)
            {
                loginButton.style.display = DisplayStyle.None;
                loginButton.clicked -= LoginButtonOnClicked;
            }
            
            if(IdentityController.GuestMode) return;
            PlayerPrefs.SetString(k_LastSelectedOrgKey, AssetsController.SelectedOrganization.Id.ToString());
        }

        // Handles the loading of organizations and updates the UI accordingly
        protected override async void OnOrganizationListReceived(List<IOrganization> listOfOrg)
        {
            base.OnOrganizationListReceived(listOfOrg);

            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (SharedUIManager.Instance.AssetsContainer.style.display == DisplayStyle.Flex)
                {
                    SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;
                }
            }
            
            if (AssetsController.SelectedOrganization == null)
            {
                SharedUIManager.Instance.OrganizationButton.label = await 
                    SharedUIManager.Instance.OrganizationPlaceholder.GetTitleLocalizedStringForAppUIAsync();
            }
            else
            {
                SharedUIManager.Instance.OrganizationButton.label = AssetsController.SelectedOrganization.Name;
            }
            
            if (IdentityController.GuestMode)
            {
                var selectedOrg = listOfOrg.First();
                SelectOrganization(selectedOrg);
                return;
            }
            
            if (!PlayerPrefs.HasKey(k_LastSelectedOrgKey))
            {
                ShowSelectOrgButton();
                return;
            }

            var lastId = PlayerPrefs.GetString(k_LastSelectedOrgKey);
            
            if (!listOfOrg.Any(org =>
                    string.Equals(org.Id.ToString(), lastId))) return;
            {
                var selectedOrg = listOfOrg.FirstOrDefault(org =>  string.Equals(org.Id.ToString(), lastId));
                SelectOrganization(selectedOrg);
            }

            return;
        }

        public void SelectOrganization(IOrganization organization)
        {
            SharedUIManager.Instance.OrganizationButton.label = organization.Name;
            UpdateUIOnOrganizationSelected();
            SharedUIManager.Organization = organization;
            SharedUIManager.Instance.IdentityContainer.style.display = DisplayStyle.None;
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.Flex;
            }
        }

        private void LoginButtonOnClicked()
        {
            OnOrganizationButtonClicked();
        }
        #endregion
    }
}
