using System;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.Industry.Viewer.Shared;
using TextOverflow = UnityEngine.UIElements.TextOverflow;
using System.Text;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using UnityEngine.SceneManagement;

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
        
        protected bool m_Initialized;
        
        public AssetInfoUIBaseController AssetInfoUIController => m_AssetInfoUIBaseController;
        
        protected AssetInfoUIBaseController m_AssetInfoUIBaseController;
        
        protected void Awake()
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

        protected void OnAssetProjectsLoaded(List<AssetProjectInfo> assetProjects)
        {
            //Clear asset Projects
            SharedUIManager.Instance.AssetProjectScrollList?.Clear();
            
            if(assetProjects == null || assetProjects.Count == 0)
            {
                var text = new Text();
                text.SetBinding("text", SharedUIManager.Instance.NoProjectsFound);
                SharedUIManager.Instance.AssetProjectScrollList?.Add(text);
                return;
            }

            bool firstItem = true;
            
            foreach (var assetProject in assetProjects)
            {
                if (!firstItem)
                {
                    SharedUIManager.Instance?.AssetProjectScrollList?.Add(new Divider());
                }

                firstItem = false;

                var newAssetProjectButton = ReturnAssetProjectButton(assetProject);
                
                newAssetProjectButton.AddToClassList(SharedUIManager.k_ActionButtonArrowClass);
                newAssetProjectButton.AddToClassList(SharedUIManager.k_AssetProjectButtonClass);

                newAssetProjectButton.AddToClassList(SharedUIManager.k_AssetProjectButtonNoSubLevelClass);
                
                newAssetProjectButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
                
                newAssetProjectButton.AddToClassList(SharedUIManager.k_ProjectAssetActionButtonNotSelectedClass);

                newAssetProjectButton.clicked += () =>
                {
                    RefreshListViewButton(newAssetProjectButton);
                    
                    SharedUIManager.Instance?.ClearGridView();
                    
                    SharedUIManager.Instance?.SearchBar?.SetValueWithoutNotify(string.Empty);
                    
                    AssetProjectSelected(assetProject);
                };
                
                SharedUIManager.Instance?.AssetProjectScrollList?.Add(newAssetProjectButton);
            }
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
            //Clear collection list
            if(collections == null || collections.Count == 0)
            {
                return;
            }
            Dictionary<IAssetCollection, ActionButton> collectionButtonDict = new Dictionary<IAssetCollection, ActionButton>();
            
            var selected = SharedUIManager.Instance.AssetProjectScrollList.Query<ActionButton>().ToList().First(x =>
                x.userData is AssetProjectInfo project && project.AssetProject.Descriptor.ProjectId ==
                collections.First().Descriptor.ProjectId);

            if (selected.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
            {
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
                    
                    var collectionButton = new ActionButton()
                    {
                        label = collection.Name,
                        tooltip = collection.Name,
                        userData = collection
                    };
                    
                    collectionButton.AddToClassList(SharedUIManager.k_AssetProjectButtonSubLevelClass);
                    collectionButton.AddToClassList(SharedUIManager.k_ProjectAssetActionButtonNotSelectedClass);
                    collectionButton.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
                    collectionButton.AddToClassList(SharedUIManager.k_ActionButtonArrowClass);
                    collectionButton.AddToClassList(SharedUIManager.k_AssetProjectButtonClass);

                    if (!collections.Any(x => !x.ParentPath.IsEmpty && x.ParentPath == collection.Descriptor.Path))
                    {
                        collectionButton.AddToClassList(SharedUIManager.k_ProjectAssetActionButtonLastLevelClass);
                    }
                    
                    int indentLevel = collection.ParentPath.IsEmpty? 1 : collection.ParentPath.GetPathComponents().Length + 1;
                    
                    collectionButton.style.paddingLeft = 10 + indentLevel * 20;
                    
                    collectionButton.clicked += () =>
                    {
                        RefreshListViewButton(collectionButton);
                        
                        if (collectionButton.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
                        {
                            collectionButton.RemoveFromClassList(SharedUIManager.k_ProjectButtonCloseClass);
                            collectionButton.AddToClassList(SharedUIManager.k_ProjectButtonOpenClass);
                            
                            var currentCollectionData = collectionButton.userData as IAssetCollection;
                            
                            var allCollectionsButton = SharedUIManager.Instance.AssetProjectScrollList.Query<ActionButton>().
                                Where(x => x.userData is IAssetCollection assetCollection 
                                           && !assetCollection.ParentPath.IsEmpty && assetCollection.ParentPath == currentCollectionData.Descriptor.Path).ToList();
                            
                            foreach (var btn in allCollectionsButton)
                            {
                                btn.style.display = DisplayStyle.Flex;
                            }
                        } else if (collectionButton.ClassListContains(SharedUIManager.k_ProjectButtonOpenClass))
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

                        SharedUIManager.Instance?.ClearGridView();
                        SharedUIManager.Instance?.SearchBar?.SetValueWithoutNotify(string.Empty);
                        
                        SharedUIManager.SetAssetProjectInfoWithoutNotify(selected.userData as AssetProjectInfo?);
                        SharedUIManager.AssetCollection = collection;
                    };

                    if (parentCollectionButton == null)
                    {
                        var index = SharedUIManager.Instance.AssetProjectScrollList.IndexOf(selected);
                        SharedUIManager.Instance.AssetProjectScrollList.Insert(index + 1, collectionButton);
                    }
                    else
                    {
                        int index = SharedUIManager.Instance.AssetProjectScrollList.IndexOf(parentCollectionButton);
                        SharedUIManager.Instance.AssetProjectScrollList.Insert(index + 1, collectionButton);
                    }
                    
                    collectionButtonDict.Add(collection, collectionButton);
                    collectionButton.style.display = collection.ParentPath.IsEmpty? DisplayStyle.Flex : DisplayStyle.None;
                    collections.Remove(collection);
                }

                if (selected.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
                {
                    selected.RemoveFromClassList(SharedUIManager.k_ProjectButtonCloseClass);
                }

                if (!selected.ClassListContains(SharedUIManager.k_ProjectButtonOpenClass))
                {
                    selected.AddToClassList(SharedUIManager.k_ProjectButtonOpenClass);
                }
            }
            else
            {
                //Close Collection List
                var allCollectionsButton = SharedUIManager.Instance.AssetProjectScrollList.Query<ActionButton>().Where(x => x.userData is IAssetCollection).ToList();

                foreach (var collectionButton in allCollectionsButton)
                {
                    var collection = collectionButton.userData as IAssetCollection;
                    var assetProject = selected.userData as AssetProjectInfo?;
                    if (collection.Descriptor.ProjectId == assetProject.Value.AssetProject.Descriptor.ProjectId)
                    {
                        collectionButton.RemoveFromHierarchy();
                    }
                }
                        
                selected.RemoveFromClassList(SharedUIManager.k_ProjectButtonOpenClass);
                if(!selected.ClassListContains(SharedUIManager.k_ProjectButtonCloseClass))
                {
                    selected.AddToClassList(SharedUIManager.k_ProjectButtonCloseClass);
                }
            }
        }
        
        protected static void RefreshListViewButton(ActionButton selectedButton)
        {
            foreach (var actionButton in SharedUIManager.Instance.AssetProjectScrollList.Query<ActionButton>().ToList())
            {
                if (actionButton.ClassListContains(SharedUIManager.k_ItemSelectedClass))
                {
                    actionButton.RemoveFromClassList(SharedUIManager.k_ItemSelectedClass);
                }

                if (!actionButton.ClassListContains(SharedUIManager.k_ProjectAssetActionButtonNotSelectedClass))
                {
                    actionButton.AddToClassList(SharedUIManager.k_ProjectAssetActionButtonNotSelectedClass);
                }
            }
            selectedButton.RemoveFromClassList(SharedUIManager.k_ProjectAssetActionButtonNotSelectedClass);
            selectedButton.AddToClassList(SharedUIManager.k_ItemSelectedClass);
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

        protected abstract void SetPathText(AssetInfo? assetInfo, AssetProjectInfo? assetProject,
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
        
        private static VisualElement MakeOrganizationItem()
        {
            var newText = new Text()
            {
                size = TextSize.L,
                style =
                {
                    paddingLeft = new Length(20f, LengthUnit.Pixel),
                    overflow = Overflow.Hidden,
                    flexWrap = Wrap.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    color = Color.white
                },
            };
            return newText;
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
            SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.None;
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
            var localizedString = sortingType.GetValueAsString();
            if (localizedString == null)
            {
                return;
            }

            var text = dropdownItem.Q<LocalizedTextElement>();
            if (text == null)
            {
                return;
            }
            text.ClearBinding("text");
            text.SetBinding("text", localizedString);
        }
        
        protected void OnSortingDropdownValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
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
                    sb.Append("<b>" + paths[i] + "</b>");
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
        
        protected void UpdateItemProperties(VisualElement item, string assetName, AssetType assetType, DateTime creationDate, out VisualElement iconPlaceHolder)
        {
            var itemUI = item.Q<VisualElement>("ItemUI");
            iconPlaceHolder = itemUI.Q<VisualElement>("IconPlaceHolder");
            var assetNameLabel = item.Q<Text>();
            
            var assetTypeLabel = item.Q<Text>("AssetTypeLabel");
            assetTypeLabel.SetBinding("text", assetType.GetAssetTypeAsString());

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
        
        protected virtual void OnOrganizationListReceived(List<IOrganization> listOfOrg)
        {
            SharedUIManager.Instance.OrganizationButton.userData = listOfOrg;
            SharedUIManager.Instance.OrganizationButton.ClearBinding("label");
            
            SharedUIManager.Instance.AssetProjectScrollList.Clear();
            SharedUIManager.Instance.ClearGridView();
            
            if (listOfOrg == null || listOfOrg.Count == 0)
            {
                SharedUIManager.Instance.OrganizationButton.SetEnabled(false);
                SharedUIManager.Instance.OrganizationButton.SetBinding("label", SharedUIManager.Instance.NoOrganizationsFound);
                SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.None;
                return;
            }
            SharedUIManager.Instance.OrganizationButton.SetEnabled(true);
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
            var organizationPopover = SharedUIManager.Instance.OrganizationPopoverTemplate.Instantiate();
            ListView organizationListView = organizationPopover.Q<ListView>();
            
            organizationListView.makeItem = MakeOrganizationItem;
            organizationListView.bindItem = OrganizationListBindItem;
            List<IOrganization> listOfOrg = SharedUIManager.Instance.OrganizationButton.userData as List<IOrganization>;
            organizationListView.itemsSource = listOfOrg;
            organizationListView.selectionChanged += OnOrganizationSelectionChanged;
            organizationListView.fixedItemHeight = 36;
            
            SharedUIManager.Instance.OrganizationPopover = Popover
                .Build(SharedUIManager.Instance.OrganizationButton, organizationPopover).SetOutsideClickDismiss(true)
                .SetArrowVisible(false).SetCrossOffset(-25);
            
            SharedUIManager.Instance.OrganizationPopover?.Show();
        }
        
        protected virtual void OnOrganizationSelectionChanged(IEnumerable<object> item)
        {
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>("IdentityContainer").style.display = DisplayStyle.None;
            UpdateUIOnOrganizationSelected();
            TextureDownload.ClearCache();
            SharedUIManager.Instance.OrganizationButton.ClearBinding("label");
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
