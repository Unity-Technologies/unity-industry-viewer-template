using UnityEngine.UIElements;
using Unity.AppUI.UI;
using System;
using UnityEngine.Localization;
using System.Linq;
using Unity.AppUI.Core;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using UnityEngine;

namespace Unity.Industry.Viewer.Assets
{
    /*
     * SharedUIManager is a core UI management class for Unity Industry Viewer that handles:
     *
     * - Asset UI management including grid views, search bars, and project navigation
     * - Organization and project selection/management
     * - Asset selection and collection management
     * - Loading state handling with modals
     * - UI element references and initialization
     * - Grid view customization and item selection
     * - Responsive layout management for asset display
     *
     * The class implements IDisposable and maintains static instances for global access to
     * key UI components and selection states. It serves as a central hub for coordinating
     * UI interactions across the asset viewing system.
     */

    
    public class SharedUIManager : IDisposable
    {
        public const string k_GridItemName = "GridItem";
        private const string k_IdentityContainerName = "IdentityContainer";
        private const string k_AssetRootName = "AssetContainer";
        private const string k_TopLeftBarName = "TopLeftBar";
        private const string k_AssetProjecetScrollListName = "AssetProjectScrollList";
        private const string k_AssetGridViewName = "AssetGridView";
        private const string k_SearchBarName = "AssetSearchBar";
        private const string k_AMButtonButtonName = "AMButton";
        private const string k_PathTextName = "PathText";
        private const string k_AssetSearchSortDropdownName = "AssetSearchSortDropdown";
        private const string k_OrganizationButton = "OrganizationButton";
        
        private const string k_NewAssetButtonName = "NewAssetButton";
        private const string k_RefreshAssetButtonName = "RefreshAssetButton";

        #region Style Classes
        public const string k_GridAssetSelectedClass = "GridAssetSelected";
        public const string k_GridAssetNonSelectedClass = "GridAssetNonSelected";
        
        public const string k_ProjectButtonCloseClass = "ProjectButtonClose";
        public const string k_ProjectButtonOpenClass = "ProjectButtonOpen";
        public const string k_ActionButtonArrowClass = "ActionButtonArrow";
        public const string k_AssetProjectButtonClass = "ProjectAssetActionButton";

        public const string k_AssetProjectButtonNoSubLevelClass = "ProjectAssetActionButtonNoSubLevel";
        public const string k_AssetProjectButtonSubLevelClass = "ProjectAssetActionButtonSubLevel";
        public const string k_ProjectAssetActionButtonNotSelectedClass = "ProjectAssetActionButtonNotSelected";
        public const string k_ItemSelectedClass = "ItemSelected";
        public const string k_ProjectAssetActionButtonLastLevelClass = "ProjectAssetActionButtonLastLevel";
        #endregion
        
        private static SharedUIManager m_Instance;
        public static SharedUIManager Instance => m_Instance;
        
        private const int k_MaxAssetPerRow = 8;
        private const int k_MaxAssetPerRowWithInfo = 5;

        public static int MaxAssetPerRow;
        public static int MaxAssetPerRowWithInfo;
        
        public UIDocument AssetsUIDocument => m_AssetsUIDocument;
        private readonly UIDocument m_AssetsUIDocument;
        public VisualElement AssetsRoot { get; private set; }
        public VisualElement IdentityContainer { get; private set; }
        
        public Popover OrganizationPopover;

        public ActionButton OrganizationButton
        {
            get
            {
                if (m_OrganizationButton != null) return m_OrganizationButton;
                m_OrganizationButton = new ActionButton
                {
                    name = k_OrganizationButton
                };
                var topLeftBar = m_AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_TopLeftBarName);
                topLeftBar.Add(m_OrganizationButton);
                return m_OrganizationButton;
            }
        }

        private ActionButton m_OrganizationButton;
        public ScrollView AssetProjectScrollList { get; private set; }
        public GridView AssetGridView { get; private set; }
        public SearchBar SearchBar { get; private set; }
        public Dropdown SortingDropdown { get; private set; }
        public Text PathText { get; private set; }
        public IconButton NewAssetButton { get; private set; }
        public IconButton RefreshAssetButton { get; private set; }
        public IconButton AMButton { get; private set; }

        public VisualTreeAsset OrganizationPopoverTemplate { get; private set; }
        private readonly VisualTreeAsset m_AssetItemTemplate;

        public static Action<AssetInfo> AssetSelected;
        public static AssetInfo? SelectedAsset
        {
            get => m_SelectedAsset;
            set
            {
                m_SelectedAsset = value;
                if (value.HasValue)
                {
                    AssetSelected?.Invoke(m_SelectedAsset.Value);
                }
            }
        }
        private static AssetInfo? m_SelectedAsset;

        public static Action<AssetProjectInfo?> AssetProjectSelected;
        public static AssetProjectInfo? AssetProjectInfo
        {
            get => m_AssetProjectInfo;
            set
            {
                m_AssetProjectInfo = value;
                m_AssetCollection = null;
                if (value.HasValue)
                {
                    AssetProjectSelected?.Invoke(m_AssetProjectInfo.Value);
                }
            }
        }
        private static AssetProjectInfo? m_AssetProjectInfo;

        public static Action<IAssetCollection> AssetCollectionSelected;
        public static IAssetCollection AssetCollection
        {
            get => m_AssetCollection;
            set
            {
                m_AssetCollection = value;
                if (value != null)
                {
                    AssetCollectionSelected?.Invoke(m_AssetCollection);
                }
            }
        }
        private static IAssetCollection m_AssetCollection;

        public static Action<IOrganization> OrganizationSelected;
        public static IOrganization Organization
        {
            get => m_Organization;
            set
            {
                m_Organization = value;
                m_AssetProjectInfo = null;
                m_AssetCollection = null;
                m_SelectedAsset = null;
                if (value != null)
                {
                    OrganizationSelected?.Invoke(m_Organization);
                }
            }
        }
        
        private static IOrganization m_Organization;
        
        private static Modal m_LoadingModal;
        
        public AssetPlaceHolderScriptableObject AssetPlaceHolderScriptableObject { get; private set; }
        
        #region Localisation

        public readonly LocalizedString NoProjectsFound;
        public readonly LocalizedString NoOrganizationsFound;
        public readonly LocalizedString SelectOrganization;
        public readonly LocalizedString NewVersionAvailable;
        public readonly LocalizedString ViewNewVersion;

        #endregion

        public SharedUIManager(UIDocument assetsUIDocument,
            LocalizedString noProjectsFound,
            LocalizedString noOrganizationsFound,
            LocalizedString selectOrganization,
            LocalizedString newVersionAvailable,
            LocalizedString viewNewVersion,
            VisualTreeAsset organizationPopoverTemplate,
            VisualTreeAsset assetItemTemplate,
            AssetPlaceHolderScriptableObject assetPlaceHolderScriptableObject)
        {
            m_Instance = this;
            MaxAssetPerRow = k_MaxAssetPerRow;
            MaxAssetPerRowWithInfo = k_MaxAssetPerRowWithInfo;
            m_AssetsUIDocument = assetsUIDocument;
            AssetsRoot = assetsUIDocument.rootVisualElement.Q<VisualElement>(k_AssetRootName);
            AssetsRoot.RegisterCallback<GeometryChangedEvent>(OnAssetsRootChanged);
            AssetProjectScrollList = AssetsRoot.Q<ScrollView>(k_AssetProjecetScrollListName);
            AssetGridView = AssetsRoot.Q<GridView>(k_AssetGridViewName);
            SearchBar = AssetsRoot.Q<SearchBar>(k_SearchBarName);
            SortingDropdown = AssetsRoot.Q<Dropdown>(k_AssetSearchSortDropdownName);
            PathText = AssetsRoot.Q<Text>(k_PathTextName);
            NewAssetButton = AssetsRoot.Q<IconButton>(k_NewAssetButtonName);
            RefreshAssetButton = AssetsRoot.Q<IconButton>(k_RefreshAssetButtonName);
            AMButton = AssetsRoot.Q<IconButton>(k_AMButtonButtonName);
            NoProjectsFound = noProjectsFound;
            NoOrganizationsFound = noOrganizationsFound;
            SelectOrganization = selectOrganization;
            NewVersionAvailable = newVersionAvailable;
            ViewNewVersion = viewNewVersion;
            OrganizationPopoverTemplate = organizationPopoverTemplate;
            m_AssetItemTemplate = assetItemTemplate;
            AssetPlaceHolderScriptableObject = assetPlaceHolderScriptableObject;
            IdentityContainer = m_AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_IdentityContainerName);
            AssetGridView.makeItem = AssetGridViewItem;
        }

        private void OnAssetsRootChanged(GeometryChangedEvent evt)
        {
            AMButton.SetEnabled(!IdentityController.GuestMode);
        }

        public static void SetAssetProjectInfoWithoutNotify(AssetProjectInfo? assetProjectInfo)
        {
            m_AssetProjectInfo = assetProjectInfo;
        }

        public void ResetAssetGridColumn()
        {
            MaxAssetPerRow = k_MaxAssetPerRow;
            MaxAssetPerRowWithInfo = k_MaxAssetPerRowWithInfo;
        }
        
        public void SetAssetGridColumn(int defaultColumn, int columnWithInfo)
        {
            MaxAssetPerRow = defaultColumn;
            MaxAssetPerRowWithInfo = columnWithInfo;
        }

        public void ClearGridView()
        {
            AssetGridView.ClearSelectionWithoutNotify();
            AssetGridView.itemsSource?.Clear();
            AssetGridView.itemsSource = null;
            AssetGridView.Refresh();
        }
        
        private VisualElement AssetGridViewItem()
        {
            return m_AssetItemTemplate.Instantiate().Children().First();
        }

        public static void ShowLoadingModal(Action onShownCallback = null)
        {
            m_LoadingModal?.Dismiss();
            var process = new CircularProgress
            {
                size = Size.L
            };
            m_LoadingModal = Modal.Build(SharedUIManager.Instance.AssetsRoot, process);
            m_LoadingModal.shown += LoadingModalOnShown;

            m_LoadingModal.Show();
            return;
            
            void LoadingModalOnShown(Modal modal)
            {
                modal.shown -= LoadingModalOnShown;
                modal.contentView.parent.RemoveFromClassList("appui-modal__content");
                onShownCallback?.Invoke();
            }
        }

        public static void HideLoadingModal(Action callback = null)
        {
            if(m_LoadingModal == null) return;
            m_LoadingModal.dismissed += LoadingModalOnDismissed;
            m_LoadingModal?.Dismiss();
            return;
            
            void LoadingModalOnDismissed(Modal modal, DismissType dismissType)
            {
                modal.dismissed -= LoadingModalOnDismissed;
                m_LoadingModal = null;
                callback?.Invoke();
            }
        }
        
        public static void ClearSelectionOnGrid()
        {
            //Instance.AssetGridView?.ClearSelectionWithoutNotify();
            var selected = Instance.AssetGridView
                .Query<VisualElement>(className: k_GridAssetSelectedClass).ToList();
            foreach (var asset in selected)
            {
                SetItemAsSelected(asset,false);
            }
            SelectedAsset = null;
        }
        
        public static string ItemNameFromIndex(int i)
        {
            return $"{k_GridItemName}{i}";
        }
        
        public static void SetItemAsSelected(VisualElement item, bool selected)
        {
            if (selected)
            {
                if (item.ClassListContains(k_GridAssetNonSelectedClass))
                {
                    item.RemoveFromClassList(k_GridAssetNonSelectedClass);
                }
                    
                if (!item.ClassListContains(k_GridAssetSelectedClass))
                {
                    item.AddToClassList(k_GridAssetSelectedClass);
                }
            }
            else
            {
                if (item.ClassListContains(k_GridAssetSelectedClass))
                {
                    item.RemoveFromClassList(k_GridAssetSelectedClass);
                }
                    
                if (!item.ClassListContains(k_GridAssetNonSelectedClass))
                {
                    item.AddToClassList(k_GridAssetNonSelectedClass);
                }
            }
        }
        
        public void Dispose()
        {
            AssetsRoot.UnregisterCallback<GeometryChangedEvent>(OnAssetsRootChanged);
            m_Instance = null;
            AssetsRoot = null;
            AssetProjectScrollList = null;
            AssetGridView = null;
            SearchBar = null;
            SortingDropdown = null;
        }
    }
}
