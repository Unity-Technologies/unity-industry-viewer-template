using System;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using System.Collections.Generic;
using Unity.Industry.Viewer.Shared;
using UnityEngine;

namespace Unity.Industry.Viewer.Assets
{
    /// <summary>
    /// The `AssetInfoUIBaseController` is an abstract base class that provides a framework 
    /// for managing the UI components and interactions related to asset information in a Unity application.
    /// </summary>
    /// <remarks>
    /// This class defines the structure and behavior for asset-related UI elements, such as asset details, 
    /// linked projects, tags, and dropdowns. It includes methods for updating, clearing, and managing 
    /// the visibility of the asset information panel. Derived classes must implement abstract methods 
    /// to handle specific behaviors like binding dropdown items and disposing resources.
    /// </remarks>
    
    public abstract class AssetInfoUIBaseController
    {
        // Constants for UI element names
        private const string k_AssetInfoPanelRootName = "AssetInfoContainer";
        private const string k_AssetInfoPanelCloseButtonName = "CloseButton";
        private const string k_TabsName = "Tabs";
        
        private const string k_InfoContainerName = "InfoContainer";
        private const string k_CommentContainerName = "CommentsContainer";
        private const string k_FilesContainerName = "FilesContainer";
        private const string k_ProjectsContainerName = "ProjectsContainer";
        private const string k_TagsContainerName = "TagsContainer";
        
        private const string k_AssetIconName = "Icon";
        private const string k_AssetNameName = "AssetName";
        private const string k_AssetStatusDropdownName = "AssetStatusDropdown";
        private const string k_AssetTypeName = "AssetType";
        private const string k_AssetFilesSizeName = "AssetFilesSize";
        private const string k_AssetFilesName = "AssetFiles";
        private const string k_AssetIDName = "AssetID";
        private const string k_AssetUpdateName = "AssetUpdate";
        private const string k_AssetUpdateByName = "AssetUpdateBy";
        private const string k_AssetCreateName = "AssetCreate";
        private const string k_AssetCreateByName = "AssetCreateBy";
        
        private const string k_AssetVersionDropdownName = "AssetVersionDropdown";
        private const string k_VerBoxName = "VerBox";
        
        #region Localisation
        protected const string k_SharedLocalisedTable = "Shared";
        protected const string k_VersionKey = "Version Smart";
        #endregion
        
        protected VisualElement m_AssetInfoPanelRoot, m_Icon, m_infoContainer, m_commentContainer, m_filesContainer,
            m_ProjectsContainer, m_TagsContainer;
        
        public IconButton CloseButton => m_CloseButton;
        
        private readonly IconButton m_CloseButton;
        
        protected Text m_AssetNameLabel, m_AssetTypeLabel, m_AssetUpdateLabel, m_AssetUpdateByLabel,
            m_AssetCreateLabel, m_AssetCreateByLabel, m_AssetIDLabel, m_AssetFilesSizeLabel, m_AssetFilesLabel;
        
        public Dropdown AssetVersionDropdown => m_AssetVersionDropdown;
        public Dropdown AssetStatusDropdown => m_AssetStatusDropdown;
        public Tabs PanelTabs => m_Tabs;

        public event Action<List<AssetInfo>> AssetVersionsLoaded;

        protected Dropdown m_AssetVersionDropdown;
        protected Dropdown m_AssetStatusDropdown;
        protected Tabs m_Tabs;
        protected Text m_versionBox;

        protected AssetInfoUIBaseController()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            m_AssetInfoPanelRoot = SharedUIManager.Instance.AssetsContainer.Q<VisualElement>(k_AssetInfoPanelRootName);
            m_AssetInfoPanelRoot.style.display = DisplayStyle.None;
            
            m_CloseButton = m_AssetInfoPanelRoot.Q<IconButton>(k_AssetInfoPanelCloseButtonName);
            m_CloseButton.clicked += CloseInfoPanel;
            
            m_Icon = m_AssetInfoPanelRoot.Q<VisualElement>(k_AssetIconName);
            
            m_AssetNameLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetNameName);
            m_AssetTypeLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetTypeName);
            m_AssetUpdateLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetUpdateName);
            m_AssetUpdateByLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetUpdateByName);
            m_AssetCreateLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetCreateName);
            m_AssetCreateByLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetCreateByName);
            m_AssetIDLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetIDName);
            m_AssetFilesLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetFilesName);
            m_AssetFilesSizeLabel = m_AssetInfoPanelRoot.Q<Text>(k_AssetFilesSizeName);
            m_AssetStatusDropdown = m_AssetInfoPanelRoot.Q<Dropdown>(k_AssetStatusDropdownName);
            m_AssetVersionDropdown = m_AssetInfoPanelRoot.Q<Dropdown>(k_AssetVersionDropdownName);
            m_versionBox = m_AssetInfoPanelRoot.Q<Text>(k_VerBoxName);
            
            m_Tabs = m_AssetInfoPanelRoot.Q<Tabs>(k_TabsName);
            m_Tabs.RegisterValueChangedCallback(OnTabsValueChanged);
            
            m_infoContainer = m_AssetInfoPanelRoot.Q<VisualElement>(k_InfoContainerName);
            m_commentContainer = m_AssetInfoPanelRoot.Q<VisualElement>(k_CommentContainerName);
            m_filesContainer = m_AssetInfoPanelRoot.Q<VisualElement>(k_FilesContainerName);
            
            m_ProjectsContainer = m_AssetInfoPanelRoot.Q<VisualElement>(k_ProjectsContainerName);
            m_TagsContainer = m_AssetInfoPanelRoot.Q<VisualElement>(k_TagsContainerName);
        }

        public virtual void AssetSelected(AssetInfo assetInfo)
        {
            ClearUI();
            m_AssetInfoPanelRoot.style.display = DisplayStyle.Flex;
            
            UpdateAssetUI(assetInfo);
        }

        protected virtual void OnDeselectAsset()
        {
            if(SharedUIManager.Instance.AssetsContainer.resolvedStyle.display == DisplayStyle.None) return;
            ClearUI();
        }

        protected virtual void UpdateAssetUI(AssetInfo assetInfo)
        {
            m_AssetIDLabel.text = assetInfo.Asset.Descriptor.AssetId.ToString();
            m_AssetTypeLabel.text = string.Empty;
            m_AssetStatusDropdown.SetValueWithoutNotify(null);
            m_AssetStatusDropdown.sourceItems = null;
            m_AssetUpdateByLabel.text = string.Empty;
            m_AssetCreateByLabel.text = string.Empty;
            m_Tabs.value = 0;
            m_Tabs.SetValueWithoutNotify(0);
            m_Icon.parent.DisplayOn();
        }

        protected void RaiseAssetVersionsLoadedEvent(List<AssetInfo> assets)
        {
            AssetVersionsLoaded?.Invoke(assets);
        }

        protected void OnRetrieveLinkedProjects(List<(string name, string id, bool source)> listOfProjects)
        {
            m_ProjectsContainer.Clear();
            if(listOfProjects == null) return;
            var sourceProject = listOfProjects.FirstOrDefault(x => x.source);

            var linkedProjectVE = new LinkedProjectVE
            {
                projectName = sourceProject.name,
                isSourceProject = true,
            };

            linkedProjectVE.SetColorFromProjectId(sourceProject.id);
            m_ProjectsContainer.Add(linkedProjectVE);

            foreach (var valueTuple in listOfProjects.Where(x => !x.source))
            {
                linkedProjectVE = new LinkedProjectVE
                {
                    projectName = valueTuple.name,
                    isSourceProject = false,
                    style =
                    {
                        marginLeft = new Length(4, LengthUnit.Pixel),
                        marginRight = new Length(4, LengthUnit.Pixel)
                    }
                };

                linkedProjectVE.SetColorFromProjectId(valueTuple.id);
                m_ProjectsContainer.Add(linkedProjectVE);
            }
        }

        protected void AssignTags(List<string> tags)
        {
            m_TagsContainer.Clear();
            for (var i = 0; i < tags.Count; i++)
            {
                var newTag = new Text(tags[i])
                {
                    style =
                    {
                        fontSize = new StyleLength(12),
                        color = new StyleColor(new Color(1, 1, 1, 0.82f))
                    }
                };
                newTag.AddToClassList("AssetInfoTag");
                m_TagsContainer.Add(newTag);
            }
        }
        
        protected void OnTextureDownloaded(Texture2D obj)
        {
            if (obj != null)
            {
                m_Icon.style.backgroundImage = obj;
                return;
            }
            AssetsUIBaseController.AssetIconLoadFailed.Invoke(m_Icon, SharedUIManager.SelectedAsset.Value.Properties.Value.Type);
        }

        protected void UpdateAssetUpdateLabel(DateTime dateTime)
        {
            m_AssetUpdateLabel.text = ReturnDateTimeFormat(dateTime);
        }

        protected void UpdateAssetCreationLabel(DateTime dateTime)
        {
            m_AssetCreateLabel.text = ReturnDateTimeFormat(dateTime);
        }

        private string ReturnDateTimeFormat(DateTime dateTime)
        {
            // "zzz" yields e.g. "+05:30"; take the sign and whole-hour part separately. Parsing the
            // raw string (after stripping ":00") threw for non-whole-hour offsets like +05:30 / -03:30.
            string timeZone = dateTime.ToString("zzz");
            string timeZoneSign = timeZone[0].ToString();
            int timeZoneInInt = int.Parse(timeZone.Substring(1).Split(':')[0]);
            return $"{dateTime:MMMM dd, yyyy - hh:mm:ss tt} GMT{timeZoneSign}{timeZoneInInt:0}";
        }
        
        protected DropdownItem StatusMakeItem()
        {
            var dropdownItem = new DropdownItem();
            return dropdownItem;
        }
        
        protected void StatusBindItem(DropdownItem arg1, IEnumerable<int> arg2)
        {
            if(arg2 == null || !arg2.Any()) return;
            StatusBindItem(arg1, arg2.First());
        }

        protected abstract void StatusBindItem(DropdownItem item, int index);
        
        protected void UpdateStatusBinding(AssetStatus currentAssetStatus, DropdownItem item, int index)
        {
            item.AddToClassList("AssetStatusDropdownItem");
            var icon = item.Query<Icon>().ToList().FirstOrDefault(x => x.ClassListContains("unity-hidden"));
            var assetStatus = (AssetStatus)m_AssetStatusDropdown.sourceItems[index];
            
            if (currentAssetStatus == AssetStatus.Draft || currentAssetStatus == AssetStatus.Inreview || assetStatus == AssetStatus.Withdrawn)
            {
                var currentAssetStatusIndex = Array.IndexOf(AssetStatusExtensions.GetAssetStatuses(), currentAssetStatus);
                item.SetEnabled(index >= currentAssetStatusIndex);
            } else if(currentAssetStatus == AssetStatus.Approved)
            {
                item.SetEnabled(assetStatus is AssetStatus.Approved or AssetStatus.Published);
            } else if (currentAssetStatus == AssetStatus.Rejected)
            {
                item.SetEnabled(assetStatus == AssetStatus.Rejected);
            }
            else if(currentAssetStatus == AssetStatus.Published)
            {
                item.SetEnabled(assetStatus == AssetStatus.Published);
            } else if (currentAssetStatus == AssetStatus.Withdrawn)
            {
                item.SetEnabled(assetStatus is AssetStatus.Published or AssetStatus.Withdrawn);
            }
            
            if (icon != null)
            {
                icon.style.display = DisplayStyle.Flex;
                var radius = new Length(10, LengthUnit.Pixel);
                icon.style.borderBottomLeftRadius = radius;
                icon.style.borderTopLeftRadius = radius;
                icon.style.borderBottomRightRadius = radius;
                icon.style.borderTopRightRadius = radius;
                icon.style.width = new Length(13f, LengthUnit.Pixel);
                icon.style.height = icon.style.width;
                icon.style.backgroundColor = assetStatus.ReturnStatusColor();
            }

            item.name = assetStatus.GetValueAsString(false);
            
            item.label = assetStatus.GetValueAsString(false);
        }

        public void SwitchCloseButtonBehaviour(bool clearUIOnly)
        {
            if (clearUIOnly)
            {
                CloseButton.clicked -= CloseInfoPanel;
                CloseButton.clicked -= ClearUI;
                CloseButton.clicked += ClearUI;
            }
            else
            {
                CloseButton.clicked -= ClearUI;
                CloseButton.clicked -= CloseInfoPanel;
                CloseButton.clicked += CloseInfoPanel;
            }
        }
        
        protected virtual void CloseInfoPanel()
        {
            ClearUI();
        }
        
        public virtual void ClearUI()
        {
            m_Tabs.value = 0;
            m_Tabs.SetValueWithoutNotify(0);
            m_AssetIDLabel.text = string.Empty;
            m_AssetFilesLabel.text = string.Empty;
            m_AssetFilesSizeLabel.text = string.Empty;
            m_AssetInfoPanelRoot.style.display = DisplayStyle.None;
            m_AssetTypeLabel.text = string.Empty;
            
            m_ProjectsContainer?.Clear();
            m_TagsContainer?.Clear();
            
            if (m_Icon != null)
            {
                m_Icon.parent.DisplayOn();
                m_Icon.style.backgroundImage = null;
            }

            if (m_AssetStatusDropdown != null)
            {
                m_AssetStatusDropdown.SetValueWithoutNotify(null);
                m_AssetStatusDropdown.sourceItems = null;
            }
        }
        
        public bool IsVisible()
        {
            return m_AssetInfoPanelRoot.resolvedStyle.display == DisplayStyle.Flex;
        }

        protected abstract void AssetVersionDropdownBindItem(DropdownItem arg1, int arg2);

        private void OnLocaleChanged(Locale obj)
        {
            m_AssetVersionDropdown.Refresh();
        }
        
        private void OnTabsValueChanged(ChangeEvent<int> evt)
        {
            //m_AssetInfoScrollView.style.display = evt.newValue is 0 or 2 ? DisplayStyle.Flex : DisplayStyle.None;
            m_Icon.parent.style.display = evt.newValue is 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_infoContainer.style.display = evt.newValue == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_commentContainer.style.display = evt.newValue == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            m_filesContainer.style.display = evt.newValue == 2 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public abstract void DisposeUI();

        public abstract void RegisterCallbacks();
        
        public abstract void UnregisterCallbacks();
    }
}
