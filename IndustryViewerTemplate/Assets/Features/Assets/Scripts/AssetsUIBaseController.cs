using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using System.Threading.Tasks;

namespace Unity.Industry.Viewer.Assets
{
    /// <summary>
    /// The `AssetsUIBaseController` is an abstract base class for managing the UI interactions 
    /// and behaviors related to assets in the Unity application. It provides a framework for 
    /// handling asset-related UI elements, such as asset projects, collections, and grid views.
    /// </summary>
    /// <remarks>
    /// This class is designed to be extended by concrete implementations that define specific 
    /// behaviors for asset management. It includes methods for initialization, UI updates, 
    /// event handling, and asset selection.
    /// </remarks>
    
    [DefaultExecutionOrder(-50)]
    public abstract class AssetsUIBaseController : MonoBehaviour
    {
        public static Action<VisualElement, AssetType> AssetIconLoadFailed;
        public static event Action<IOrganization> OnAssetProjectsLoadedEvent;

        protected bool m_Initialized;
        
        public AssetInfoUIBaseController AssetInfoUIController => m_AssetInfoUIBaseController;
        
        protected AssetInfoUIBaseController m_AssetInfoUIBaseController;
        
        protected virtual void Awake()
        {
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
        }

        protected virtual void Start()
        {
            IdentityController.TriggerLogout += OnUserLoggedOut;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        protected virtual void OnDestroy()
        {
            IdentityController.TriggerLogout -= OnUserLoggedOut;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            UnregisterCallbacks();
        }

        private void OnUserLoggedOut(bool obj)
        {
            if(SharedUIManager.Instance.OrganizationButton == null) return;
            SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.None;;
        }

        protected abstract void RegisterCallbacks();

        protected abstract void UnregisterCallbacks();

        protected abstract void InitializeUI();

        protected abstract void UninitializeUI();

        protected abstract void OnNetworkStatusChanged(bool connected);
        
        protected abstract void OnAMButtonClicked();

        protected abstract void InitializeExtraUIController();

        protected void RefreshGridViewSize()
        {
            var newCountPerRow = m_AssetInfoUIBaseController == null? SharedUIManager.MaxAssetPerRow : 
                m_AssetInfoUIBaseController.IsVisible() ? SharedUIManager.MaxAssetPerRowWithInfo : SharedUIManager.MaxAssetPerRow;
            if (newCountPerRow != SharedUIManager.Instance.AssetGridView.columnCount)
            {
                SharedUIManager.Instance.AssetGridView.columnCount = newCountPerRow;
            }
        }

        protected void AssetCollectionSelected(IAssetCollection selectedCollection)
        {
            if(selectedCollection == null) return;
            SetPathText(null, SharedUIManager.AssetProjectInfo.Value, selectedCollection);
        }

        protected async void OnAssetProjectsLoaded(IOrganization organization, List<AssetProjectInfo> assetProjects)
        {
            //Clear asset Projects
            SharedUIManager.Instance.AssetProjectScrollList?.Clear();
            
            if(assetProjects == null || assetProjects.Count == 0)
            {
                var text = new Text
                {
                    text = await SharedUIManager.Instance.NoProjectsFound.GetTitleLocalizedStringForAppUIAsync()
                };
                SharedUIManager.Instance.AssetProjectScrollList?.Add(text);
                OnAssetProjectsLoadedEvent?.Invoke(organization);
                return;
            }

            foreach (var assetProject in assetProjects)
            {
                var newAssetProjectButton = ReturnAssetProjectButton(assetProject);
                newAssetProjectButton.icon = "Down-Arrow";
                newAssetProjectButton.AddToClassList(SharedUIManager.k_AssetProjectButtonClass);

                newAssetProjectButton.AddToClassList(SharedUIManager.k_AssetProjectButtonNoSubLevelClass);
                
                newAssetProjectButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
                newAssetProjectButton.selected = false;

                newAssetProjectButton.clicked += () =>
                {
                    OnAssetProjectButtonClick(assetProject, newAssetProjectButton);
                };
                
                SharedUIManager.Instance?.AssetProjectScrollList?.Add(newAssetProjectButton);
            }

            OnAssetProjectsLoadedEvent?.Invoke(organization);
        }

        public void OnAssetProjectButtonClick(AssetProjectInfo assetProject, ActionButton newAssetProjectButton)
        {
            RefreshListViewButton(newAssetProjectButton);
            SharedUIManager.Instance?.ClearGridView();
            SharedUIManager.Instance?.SearchBar?.SetValueWithoutNotify(string.Empty);
            AssetProjectSelected(assetProject);
        }

        protected abstract ActionButton ReturnAssetProjectButton(AssetProjectInfo assetProject);

        private void AssetProjectSelected(AssetProjectInfo assetProject)
        {
            SetPathText(null, assetProject, null);
            SharedUIManager.AssetProjectInfo = assetProject;
            SharedUIManager.AssetCollection = null;
        }
        
        protected void OnCollectionsLoaded(List<IAssetCollection> collections)
        {
            if(collections == null || collections.Count == 0) return;
            var projectId = collections.First().Descriptor.ProjectId;

            var projectButton = SharedUIManager.Instance.AssetProjectScrollList
                .Query<ActionButton>()
                .ToList()
                .First(button => button.userData is AssetProjectInfo project && project.AssetProject.Descriptor.ProjectId == projectId);

            if (projectButton.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
            {
                OpenProjectNode(collections, projectButton);
            }
            else
            {
                CloseProjectNode(projectButton);
            }
        }

        public static void CloseProjectNode(ActionButton projectButton)
        {
            var allCollectionsButton = SharedUIManager.Instance.AssetProjectScrollList.Query<ActionButton>().Where(x => x.userData is IAssetCollection).ToList();

            foreach (var collectionButton in allCollectionsButton)
            {
                var collection = collectionButton.userData as IAssetCollection;
                var assetProject = projectButton.userData as AssetProjectInfo?;
                if (collection.Descriptor.ProjectId == assetProject.Value.AssetProject.Descriptor.ProjectId)
                {
                    collectionButton.RemoveFromHierarchy();
                }
            }

            projectButton.RemoveFromClassList(SharedUIManager.k_ProjectButtonOpenClass);
            if (!projectButton.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
            {
                projectButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
            }
        }

        private static void OpenProjectNode(List<IAssetCollection> collections, ActionButton projectButton)
        {
            var collectionButtonDict = new Dictionary<IAssetCollection, ActionButton>();

            while (collections.Count > 0)
            {
                var collection = collections.First();
                ActionButton parentCollectionButton = null;
                if (!collection.ParentPath.IsEmpty)
                {
                    if (!collectionButtonDict.Keys.Any(x => string.Equals(x.Descriptor.Path, collection.ParentPath.ToString())))
                    {
                        collections.Remove(collection);
                        collections.Insert(collections.Count, collection);
                        continue;
                    }
                    var parentCollection = collectionButtonDict.Keys.First(x => string.Equals(x.Descriptor.Path, collection.ParentPath.ToString()));
                    parentCollectionButton = collectionButtonDict[parentCollection];
                }

                var isEmpty = !collections.Any(x => !x.ParentPath.IsEmpty && x.ParentPath == collection.Descriptor.Path);
                var collectionButton = CreateCollectionButton(projectButton, collection, isEmpty);

                if (parentCollectionButton == null)
                {
                    var index = SharedUIManager.Instance.AssetProjectScrollList.IndexOf(projectButton);
                    SharedUIManager.Instance.AssetProjectScrollList.Insert(index + 1, collectionButton);
                }
                else
                {
                    int index = SharedUIManager.Instance.AssetProjectScrollList.IndexOf(parentCollectionButton);
                    SharedUIManager.Instance.AssetProjectScrollList.Insert(index + 1, collectionButton);
                }

                collectionButtonDict.Add(collection, collectionButton);
                collectionButton.style.display = collection.ParentPath.IsEmpty ? DisplayStyle.Flex : DisplayStyle.None;
                collections.Remove(collection);
            }

            if (projectButton.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
            {
                projectButton.RemoveFromClassList(SharedUIManager.k_ProjectButtonCloseClass);
            }

            if (!projectButton.ClassListContains(SharedUIManager.k_ProjectButtonOpenClass))
            {
                projectButton.AddToClassList(SharedUIManager.k_ProjectButtonOpenClass);
            }
        }

        private static ActionButton CreateCollectionButton(ActionButton projectButton, IAssetCollection collection, bool isEmpty)
        {
            var collectionButton = new ActionButton()
            {
                label = collection.Name,
                tooltip = collection.Name,
                userData = collection,
                quiet = true
            };

            collectionButton.AddToClassList(SharedUIManager.k_AssetProjectButtonSubLevelClass);
            collectionButton.selected = false;
            collectionButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
            collectionButton.icon = isEmpty ? string.Empty : "Down-Arrow";
            collectionButton.AddToClassList(SharedUIManager.k_AssetProjectButtonClass);

            int indentLevel = collection.ParentPath.IsEmpty ? 1 : collection.ParentPath.GetPathComponents().Length + 1;

            collectionButton.style.paddingLeft = indentLevel * 16;

            collectionButton.clicked += () =>
            {
                RefreshListViewButton(collectionButton);

                if (collectionButton.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
                {
                    OpenCollectionNode(collectionButton);
                }
                else if (collectionButton.ClassListContains(SharedUIManager.k_ProjectButtonOpenClass))
                {
                    CloseCollectionNode(collectionButton);
                }

                SharedUIManager.Instance?.ClearGridView();
                SharedUIManager.Instance?.SearchBar?.SetValueWithoutNotify(string.Empty);
                SharedUIManager.SetAssetProjectInfoWithoutNotify(projectButton.userData as AssetProjectInfo?);
                SharedUIManager.AssetCollection = collection;
            };

            return collectionButton;
        }

        public static void CloseCollectionNode(ActionButton collectionButton)
        {
            collectionButton.RemoveFromClassList(SharedUIManager.k_ProjectButtonOpenClass);
            collectionButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);

            var allCollectionsButton = SharedUIManager.Instance.AssetProjectScrollList.Query<ActionButton>().Where(x => x.userData is IAssetCollection).ToList();

            var currentCollectionData = collectionButton.userData as IAssetCollection;

            foreach (var btn in allCollectionsButton)
            {
                var btnCollectionData = btn.userData as IAssetCollection;
                if (!btnCollectionData.ParentPath.IsEmpty &&
                    btnCollectionData.ParentPath.Contains(currentCollectionData.Descriptor.Path))
                {
                    if (btn.ClassListContains(SharedUIManager.k_ProjectButtonOpenClass))
                    {
                        btn.RemoveFromClassList(SharedUIManager.k_ProjectButtonOpenClass);
                    }

                    if (!btn.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
                    {
                        btn.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
                    }
                    btn.style.display = DisplayStyle.None;
                }
            }
        }

        public static void OpenCollectionNode(ActionButton collectionButton)
        {
            collectionButton.RemoveFromClassList(SharedUIManager.k_ProjectButtonCloseClass);
            collectionButton.AddToClassList(SharedUIManager.k_ProjectButtonOpenClass);

            var currentCollectionData = collectionButton.userData as IAssetCollection;

            var childCollectionButtons = SharedUIManager.Instance.AssetProjectScrollList
                .Query<ActionButton>()
                .Where(button => button.userData is IAssetCollection assetCollection
                       && !assetCollection.ParentPath.IsEmpty
                       && assetCollection.ParentPath == currentCollectionData.Descriptor.Path)
                .ToList();

            foreach (var childCollectionButton in childCollectionButtons)
            {
                childCollectionButton.style.display = DisplayStyle.Flex;
            }
        }

        public static void RefreshListViewButton(ActionButton selectedButton)
        {
            foreach (var actionButton in SharedUIManager.Instance.AssetProjectScrollList.Query<ActionButton>().ToList())
            {
                actionButton.selected = false;
            }

            selectedButton.selected = true;
        }

        protected virtual void OnAssetSelectedOnGrid(IEnumerable<object> obj)
        {
            if (obj == null || !obj.Any())
            {
                SharedUIManager.ClearSelectionOnGrid();
                return;
            }
            var selectedAsset = (obj.First() as AssetInfo?);

            if (SharedUIManager.SelectedAsset.HasValue && SharedUIManager.SelectedAsset.Value == selectedAsset)
            {
                return;
            }
            
            DeselectExisting(selectedAsset.Value);
            
            SharedUIManager.ClearSelectionOnGrid();
            
            SetPathText(selectedAsset, SharedUIManager.AssetProjectInfo, SharedUIManager.AssetCollection);
            
            SharedUIManager.SelectedAsset = selectedAsset;
            
            var index = SharedUIManager.Instance.AssetGridView.itemsSource.IndexOf(selectedAsset);
            var item = SharedUIManager.Instance.AssetGridView.Q(SharedUIManager.ItemNameFromIndex(index));
            
            if (item != null)
            {
                RefreshGridItem(item, index);
            }
        }
        
        protected abstract void DeselectExisting(AssetInfo assetInfo);

        public abstract void SetPathText(AssetInfo? assetInfo, AssetProjectInfo? assetProject,
            IAssetCollection collection);
        
        private void RefreshGridItem(VisualElement item, int index)
        {            
            AssetInfo? asset = SharedUIManager.Instance.AssetGridView.itemsSource[index] as AssetInfo?;
            if(asset == null) return;

            SelectItemClass(item, asset.Value);
            
            item.name = SharedUIManager.ItemNameFromIndex(index);
            DisplayItem(asset.Value, item);
        }

        protected abstract void DisplayItem(AssetInfo asset, VisualElement item);
        
        protected void OnAssetIconLoadedFailed(VisualElement iconVE, AssetType type)
        {
            var icon = SharedUIManager.Instance.AssetPlaceHolderScriptableObject.GetAssetTypeIcon(type);
            iconVE.style.backgroundImage = icon;
        }
        
        protected void AssetGridBindItem(VisualElement gridVisualElement, int index)
        {
            RefreshGridItem(gridVisualElement, index);
        }

        protected void AssetGridUnbindItem(VisualElement gridVisualElement, int index)
        {
            // Clear the binding ID to invalidate any pending downloads
            gridVisualElement.userData = null;
        }

        private void SelectItemClass(VisualElement item, AssetInfo assetInfo)
        {
            if (SharedUIManager.SelectedAsset.HasValue)
            {
                SharedUIManager.SetItemAsSelected(item, SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId &&
                                                        SharedUIManager.SelectedAsset.Value.Asset.Descriptor.ProjectDescriptor == assetInfo.Asset.Descriptor.ProjectDescriptor);
            }
            else
            {
                SharedUIManager.SetItemAsSelected(item, false);
            }
        }
        
        protected abstract void HandleAssetThumbnail(AssetInfo asset, VisualElement iconPlaceHolder);
        
        protected void HandleThumbnailDownload(VisualElement iconPlaceHolder, int downloadKey, Action<int> downloadAction)
        {
            // downloadKey is the asset's TextureDownload cache key (AssetId hashcode). If this cell
            // was showing a different asset, cancel that still-in-flight download. The previous code
            // stored a monotonic counter here, so CancelDownload never matched a cache key (no-op).
            if (iconPlaceHolder.userData is int existingKey && existingKey != downloadKey)
            {
                TextureDownload.CancelDownload(existingKey);
            }

            iconPlaceHolder.userData = downloadKey;
            downloadAction(downloadKey);
        }
        
        protected void OnThumbnailDownloaded(VisualElement iconPlaceHolder, int bindingId, Texture2D textureResult, AssetType assetType)
        {
            // Validate that this visual element is still bound to the same asset
            // by checking if the binding ID matches
            if (iconPlaceHolder.userData is int currentBindingId && 
                currentBindingId == bindingId)
            {
                if (textureResult != null)
                {
                    iconPlaceHolder.style.backgroundImage = textureResult;
                }
                else
                {
                    AssetIconLoadFailed.Invoke(iconPlaceHolder, assetType);
                }
            }
        }
        
        protected void OnGridGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshGridViewSize();
        }
        
        protected void OnSearchBarValueChanged(ChangeEvent<string> evt)
        {
            if(SharedUIManager.Instance.SearchBar.value == evt.newValue) return;
            SharedUIManager.Instance?.ClearGridView();
            UpdateSearchResult(evt.newValue);
        }
        
        protected void OnSearchBarValueChanging(ChangingEvent<string> evt)
        {
            SharedUIManager.Instance?.ClearGridView();
            UpdateSearchResult(evt.newValue);
        }
        
        protected void OnUserLoggedOut()
        {
            SharedUIManager.Instance.AssetProjectScrollList?.Clear();
            SharedUIManager.Instance?.ClearGridView();
            SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
            SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.None;
            SharedUIManager.Instance?.SearchBar?.SetValueWithoutNotify(string.Empty);
            SharedUIManager.Instance.PathText.text = string.Empty;
        }
        
        protected abstract void UpdateSearchResult(string value);
        
        protected void OnAssetDeselected()
        {
            SharedUIManager.Instance.AssetGridView.ClearSelection();
            SharedUIManager.ClearSelectionOnGrid();
            SetPathText(null, SharedUIManager.AssetProjectInfo, SharedUIManager.AssetCollection);
        }
        
        protected static void SortingBindItem(DropdownItem dropdownItem, int index)
        {
            SortingType sortingType = (SharedUIManager.Instance.SortingDropdown.sourceItems as SortingType[])[index];

            var text = dropdownItem.Q<LocalizedTextElement>();
            if (text == null)
            {
                return;
            }
            text.text = sortingType.GetValueAsString();
        }
        
        protected void OnSortingDropdownValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            if(evt.newValue == null || !evt.newValue.Any()) return;
            SortingType sortingType = (SharedUIManager.Instance.SortingDropdown.sourceItems as SortingType[])[evt.newValue.First()];
            SharedUIManager.Instance?.ClearGridView();
            SharedUIManager.SelectedAsset = null;
            SortingChanged(sortingType, SharedUIManager.Instance.SearchBar.value);
        }
        
        protected abstract void SortingChanged(SortingType sortingType, string searchText);
        
        private static void OnLocaleChanged(Locale obj)
        {
            SharedUIManager.Instance.SortingDropdown?.Refresh();
        }
        
        protected static void ReturnCollectionPathForText(string[] paths, bool bold, ref StringBuilder sb)
        {
            for(var i = 0; i < paths.Length; i++)
            {
                if (i == paths.Length - 1 && bold)
                {
                    sb.Append(FormatLastPathPart(paths[i]));
                }
                else
                {
                    sb.Append(paths[i]);
                }
                if(i != paths.Length - 1)
                {
                    sb.Append(" / ");
                }
            }
        }

        protected static string FormatLastPathPart(string value) => $"<b><color=#E9E9E9>{value}</color></b>";

        protected void UpdateItemProperties(VisualElement item, string assetName, AssetType assetType, DateTime creationDate, out VisualElement iconPlaceHolder)
        {
            var itemUI = item.Q<VisualElement>("ItemUI");
            iconPlaceHolder = itemUI.Q<VisualElement>("IconPlaceHolder");
            var assetNameLabel = item.Q<Text>();
            
            var assetTypeLabel = item.Q<Text>("AssetTypeLabel");
            assetTypeLabel.text = assetType.GetAssetTypeAsString();

            var assetLastUpdateLabel = item.Q<Text>("AssetLastUpdatedLabel");
            var currentUtcTime = DateTime.UtcNow;
            var timeSpan = currentUtcTime - creationDate;
            
            if (timeSpan.TotalSeconds < 60)
            {
                assetLastUpdateLabel.text = $"{(int)timeSpan.TotalSeconds}s ago";
            }
            else if (timeSpan.TotalMinutes < 60)
            {
                assetLastUpdateLabel.text = $"{(int)timeSpan.TotalMinutes}m ago";
            }
            else if (timeSpan.TotalHours < 24)
            {
                assetLastUpdateLabel.text = $"{(int)timeSpan.TotalHours}h ago";
            }
            else if (timeSpan.TotalDays < 30)
            {
                assetLastUpdateLabel.text = $"{(int)timeSpan.TotalDays}d ago";
            }
            else if (timeSpan.TotalDays < 365)
            {
                assetLastUpdateLabel.text = $"{(int)(timeSpan.TotalDays / 30)}mo ago";
            }
            else
            {
                assetLastUpdateLabel.text = $"{timeSpan.TotalDays / 365:F1}y ago";
            }
            
            assetNameLabel.text = assetName;
        }
        
        protected virtual async void OnOrganizationListReceived(List<IOrganization> listOfOrg)
        {
            SharedUIManager.Instance.OrganizationButton.userData = listOfOrg;
            
            SharedUIManager.Instance.AssetProjectScrollList.Clear();
            SharedUIManager.Instance.ClearGridView();

            if (listOfOrg == null || listOfOrg.Count == 0)
            {
                SharedUIManager.Instance.OrganizationButton.SetEnabled(false);
                SharedUIManager.Instance.OrganizationButton.label = await 
                    SharedUIManager.Instance.NoOrganizationsFound.GetTitleLocalizedStringForAppUIAsync();
                SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
                SetBackgroundTint(true);
                return;
            }

            SetBackgroundTint(false);
            SharedUIManager.Instance.OrganizationButton.SetEnabled(true);
        }
        
        protected void SetBackgroundTint(bool isDark)
        {
            var backgroundVE = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>("BackgroundVE");
            backgroundVE.style.unityBackgroundImageTintColor = isDark ? new Color(0.5f, 0.5f, 0.5f, 1f) : new Color(1f, 1f, 1f, 1f);
        }

        protected void UpdateUIOnOrganizationSelected()
        {
            //Clear asset projects, collection list and assets
            SharedUIManager.Instance.AssetProjectScrollList?.Clear();
            SharedUIManager.Instance?.ClearGridView();
            SharedUIManager.Instance?.SearchBar?.SetValueWithoutNotify(string.Empty);
            SharedUIManager.Instance?.OrganizationPopover?.Dismiss();
            SharedUIManager.Instance.PathText.text = string.Empty;
        }
        
        protected void OnOrganizationButtonClicked()
        {
            var organizationPopover = SharedUIManager.Instance.OrganizationPopoverTemplate.Instantiate().Children().First();
            ListView organizationListView = organizationPopover.Q<ListView>();
            organizationListView.makeItem = MakeOrganizationItem;
            organizationListView.bindItem = OrganizationListBindItem;
            List<IOrganization> listOfOrg = SharedUIManager.Instance.OrganizationButton.userData as List<IOrganization>;
            organizationListView.itemsSource = listOfOrg;
            organizationListView.selectionChanged += OnOrganizationSelectionChanged;

            SharedUIManager.Instance.OrganizationPopover = Popover
                .Build(SharedUIManager.Instance.OrganizationButton, organizationPopover)
                .SetOutsideClickDismiss(true)
                .SetArrowVisible(false)
                .SetPlacement(PopoverPlacement.BottomStart)
                .SetOffset(7)
                .SetCrossOffset(-8);

            SharedUIManager.Instance.OrganizationPopover.Show();
        }
        
        private static VisualElement MakeOrganizationItem()
        {
            var newText = new Text()
            {
                size = TextSize.L,
            };
            newText.AddToClassList("OrganizationSelection");
            newText.AddToClassList("cursor--pointer");
            return newText;
        }
        
        protected virtual void OnOrganizationSelectionChanged(IEnumerable<object> item)
        {
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>("IdentityContainer").style.display = DisplayStyle.None;
            UpdateUIOnOrganizationSelected();
            TextureDownload.ClearCache();
            SharedUIManager.Instance.OrganizationButton.label = string.Empty;
            IOrganization selectedOrg = item.First() as IOrganization;
            SharedUIManager.Organization = selectedOrg;
            SharedUIManager.AssetProjectInfo = null;
            SharedUIManager.AssetCollection = null;
            SharedUIManager.SelectedAsset = null;
            SharedUIManager.Instance.OrganizationButton.label = selectedOrg.Name;
            m_AssetInfoUIBaseController?.ClearUI();
        }

        private void OrganizationListBindItem(VisualElement veText, int index)
        {
            List<IOrganization> listOfOrg = SharedUIManager.Instance.OrganizationButton.userData as List<IOrganization>;
            var org = listOfOrg[index];
            var text = (veText as Text);
            text.text = org.Name;
            text.tooltip = org.Name;
            SetOrganizationItemClass(org, veText);
        }

        private static void SetOrganizationItemClass(IOrganization org, VisualElement item)
        {
            if(SharedUIManager.Organization == null) return;
            if(SharedUIManager.Organization == org)
            {
                if (!item.ClassListContains(SharedUIManager.k_ItemSelectedClass))
                {
                    item.AddToClassList(SharedUIManager.k_ItemSelectedClass);
                }
            }
            else
            {
                if (item.ClassListContains(SharedUIManager.k_ItemSelectedClass))
                {
                    item.RemoveFromClassList(SharedUIManager.k_ItemSelectedClass);
                }
            }
        }
    }
}
