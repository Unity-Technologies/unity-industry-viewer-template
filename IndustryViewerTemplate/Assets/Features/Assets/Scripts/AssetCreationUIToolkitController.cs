using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using UnityEngine.UIElements;
using Button = Unity.AppUI.UI.Button;
using TextField = Unity.AppUI.UI.TextField;
using Unity.AppUI.Core;
using Unity.Industry.Viewer.Identity;
using UnityEngine.Localization;

namespace Unity.Industry.Viewer.Assets
{
    // This script is a controller for the Asset Creation UI in Unity using the UIToolkit.
    // It handles the UI interactions for creating new assets, including selecting organizations, projects, collections, and uploading files.
    
    public class AssetCreationUIToolkitController : IDisposable
    {
        // Constants for UI element names
        private const string k_NewAssetContainerName = "NewAssetContainer";
        private const string k_OrganizationDropDownName = "OrgDropDown";
        private const string k_ProjectDropDownName = "ProjectDropDown";
        private const string k_AssetNameFieldName = "AssetNameField";
        private const string k_AssetTypeDropDownName = "AssetTypeDropDown";
        private const string k_CollectionDropDownName = "CollectionDropDown";
        private const string k_AssetDescriptionFieldName = "AssetDescriptionField";
        
        private const string k_CloseButtonName = "CloseButton";
        private const string k_FileListName = "AssetCreationFileList";
        private const string k_PickFileButtonName = "PickFileButton";
        private const string k_RemoveFileButtonName = "RemoveFileButton";
        private const string k_AddNewAssetButtonName = "AddNewAssetButton";
        private const string k_CreationProgressName = "CreationProgress";
        private const string k_ProgressLabelName = "ProgressLabel";

        #region Localisation

        private const string k_AssetTableKey = "Assets";
        private const string k_UploadAssetKey = "Upload Asset";
        private const string k_UploadingAssetKey = "Uploading Asset";
        private const string k_AssetCreatedKey = "Asset Created";
        private const string k_SelectFileKey = "Select Files";

        #endregion
        
        // UI elements
        private VisualElement m_NewAssetContainerRoot;
        private Dropdown m_OrganizationDropDown;
        private Dropdown m_ProjectDropDown;
        private Dropdown m_CollectionDropDown;
        private TextField m_AssetNameField;
        private Dropdown m_AssetTypeDropDown;
        private TextArea m_AssetDescriptionField;
        private Button m_CloseButton;
        private ListView m_FileList;
        private ActionButton m_PickFileButton;
        private ActionButton m_RemoveFileButton;
        private ActionButton m_AddNewAssetButton;
        private CircularProgress m_CreationProgress;
        private Text m_ProgressLabel;
        
        public bool IsUploading { get; private set; }

        private Modal m_LoadingModal;
        private Toast m_Toast;

        #region Localisation

        private LocalizedString m_UploadAssetString = new LocalizedString(k_AssetTableKey, k_UploadAssetKey);
        private LocalizedString m_UploadingAssetString = new LocalizedString(k_AssetTableKey, k_UploadingAssetKey);
        private LocalizedString m_AssetCreatedString = new LocalizedString(k_AssetTableKey, k_AssetCreatedKey);
        private LocalizedString m_SelectFileString = new LocalizedString(k_AssetTableKey, k_SelectFileKey);

        private Dictionary<LocalizedString, string> m_Translations = new Dictionary<LocalizedString, string>();
        
        #endregion

        // Constructor to initialize the controller and register event handlers
        public AssetCreationUIToolkitController(VisualElement root)
        {
            AssetsController.OrganizationsLoaded += OnAllOrganizationsLoaded;
            AssetsController.AssetCreationProgress += OnAssetCreationProgress;
            //AssetsController.OrganizationSelected += OrganizationSelected;
            m_SelectFileString.StringChanged += SelectFileStringOnStringChanged;
            
            m_NewAssetContainerRoot = root.Q<VisualElement>(k_NewAssetContainerName);
            
            m_CloseButton = m_NewAssetContainerRoot.Q<Button>(k_CloseButtonName);

            m_CloseButton.clicked += OnCloseButtonPressed;
            
            m_OrganizationDropDown = m_NewAssetContainerRoot.Q<Dropdown>(k_OrganizationDropDownName);
            m_OrganizationDropDown.bindItem = OrganizationDropDownBindItem;
            m_OrganizationDropDown.RegisterValueChangedCallback(OnOrganizationDropDownChanged);
            
            m_ProjectDropDown = m_NewAssetContainerRoot.Q<Dropdown>(k_ProjectDropDownName);
            m_ProjectDropDown.bindItem = ProjectDropDownBindItem;
            m_ProjectDropDown.RegisterValueChangedCallback(OnProjectDropDownChanged);
            
            m_CollectionDropDown = m_NewAssetContainerRoot.Q<Dropdown>(k_CollectionDropDownName);
            m_CollectionDropDown.bindItem = OnCollectionBindItem;
            
            m_AssetNameField = m_NewAssetContainerRoot.Q<TextField>(k_AssetNameFieldName);

            m_AssetNameField.RegisterValueChangingCallback(OnAssetNameChanging);
            m_AssetNameField.RegisterValueChangedCallback(OnAssetNameChanged);
            
            m_AssetTypeDropDown = m_NewAssetContainerRoot.Q<Dropdown>(k_AssetTypeDropDownName);
            m_AssetTypeDropDown.bindItem = AssetTypeDropDownBindItem;
            m_AssetTypeDropDown.RegisterValueChangedCallback(OnAssetTypeDropDownChanged);
            m_AssetTypeDropDown.sourceItems = CustomAssetTypeExtension.AssetTypeList();
            m_AssetTypeDropDown.SetValueWithoutNotify(null);
            
            m_AssetDescriptionField = m_NewAssetContainerRoot.Q<TextArea>(k_AssetDescriptionFieldName);
            m_FileList = m_NewAssetContainerRoot.Q<ListView>(k_FileListName);

            m_FileList.makeItem = FileListMakeItem;
            m_FileList.bindItem = FileListBindItem;
            m_FileList.selectionChanged += OnSelectionChanged;
            
            m_PickFileButton = m_NewAssetContainerRoot.Q<ActionButton>(k_PickFileButtonName);

            m_PickFileButton.clicked += OnPickFileButtonPressed;
            
            m_RemoveFileButton = m_NewAssetContainerRoot.Q<ActionButton>(k_RemoveFileButtonName);
            m_RemoveFileButton.clicked += OnRemoveButtonPressed;
            
            m_AddNewAssetButton = m_NewAssetContainerRoot.Q<ActionButton>(k_AddNewAssetButtonName);
            m_AddNewAssetButton.clicked += AddNewAssetButtonOnClicked;

            m_ProgressLabel = m_NewAssetContainerRoot.Q<Text>(k_ProgressLabelName);
            
            m_CreationProgress = m_AddNewAssetButton.parent.Q<CircularProgress>(k_CreationProgressName);
            
            m_NewAssetContainerRoot.style.display = DisplayStyle.None;
            
            IdentityController.AuthenticationStateChangedEvent += OnAuthenticationStateChanged;
        }

        private void SelectFileStringOnStringChanged(string value)
        {
            if (m_Translations.TryGetValue(m_SelectFileString, out var _))
            {
                m_Translations[m_SelectFileString] = value;
            } 
            else
            {
                m_Translations.Add(m_SelectFileString, value);
            }
        }

        // Dispose method to unregister event handlers
        public void Dispose()
        {
            m_CloseButton.clicked -= OnCloseButtonPressed;
            m_SelectFileString.StringChanged -= SelectFileStringOnStringChanged;
            m_AssetNameField.UnregisterValueChangingCallback(OnAssetNameChanging);
            m_AssetNameField.UnregisterValueChangedCallback(OnAssetNameChanged);
            m_OrganizationDropDown.UnregisterValueChangedCallback(OnOrganizationDropDownChanged);
            m_ProjectDropDown.UnregisterValueChangedCallback(OnProjectDropDownChanged);
            m_AssetTypeDropDown.UnregisterValueChangedCallback(OnAssetTypeDropDownChanged);
            AssetsController.OrganizationsLoaded -= OnAllOrganizationsLoaded;
            //AssetsController.OrganizationSelected -= OrganizationSelected;
            m_FileList.selectionChanged -= OnSelectionChanged;
            m_PickFileButton.clicked -= OnPickFileButtonPressed;
            m_RemoveFileButton.clicked -= OnRemoveButtonPressed;
            m_AddNewAssetButton.clicked -= AddNewAssetButtonOnClicked;
            AssetsController.AssetCreationProgress -= OnAssetCreationProgress;
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
        }

        // Dispose method to unregister event handlers
        private void OnAssetTypeDropDownChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            CheckAddNewAssetButton();
        }

        // Bind item for collection dropdown
        private void OnCollectionBindItem(DropdownItem arg1, int arg2)
        {
            if(m_CollectionDropDown.sourceItems == null) return;
            var collections = m_CollectionDropDown.sourceItems as List<IAssetCollection>;
            if (collections == null)
            {
                return;
            }
            arg1.label = collections[arg2].Name;
        }

        // Event handler for project dropdown value change
        private void OnProjectDropDownChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            if(m_ProjectDropDown.sourceItems == null) return;
            var selectedProject = m_ProjectDropDown.sourceItems[evt.newValue.First()] as AssetProjectInfo?;
            if(selectedProject == null) return;
            m_CollectionDropDown.SetValueWithoutNotify(null);
            m_CollectionDropDown.sourceItems = null;
            CheckAddNewAssetButton();
            AssetsController.GetAssetCollectionsForProject?.Invoke(selectedProject.Value, OnAllCollectionsLoaded);
        }

        // Callback for when all collections are loaded
        private void OnAllCollectionsLoaded(List<IAssetCollection> obj)
        {
            m_CollectionDropDown.sourceItems = obj;
            m_CollectionDropDown.SetValueWithoutNotify(null);
        }

        // Event handler for organization dropdown value change
        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            if (state is AuthenticationState.AwaitingLogout or AuthenticationState.LoggedOut)
            {
                OnCloseButtonPressed();
            }
        }

        // Event handler for organization dropdown value change
        private void OnOrganizationDropDownChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            if(m_OrganizationDropDown.sourceItems == null) return;
            var selectedOrg = m_OrganizationDropDown.sourceItems[evt.newValue.First()] as IOrganization;
            if(selectedOrg == null) return;
            
            m_ProjectDropDown.value = null;
            m_ProjectDropDown.sourceItems = null;
            
            m_CollectionDropDown.value = null;
            m_CollectionDropDown.sourceItems = null;
            
            CheckAddNewAssetButton();
            //AssetsController.GetAssetProjectsForOrganization?.Invoke(selectedOrg, OnAllAssetsProjectsLoaded, false);
        }

        // Callback for when all asset projects are loaded
        private void OnAllAssetsProjectsLoaded(List<AssetProjectInfo> obj)
        {
            m_ProjectDropDown.sourceItems = obj;
            
            if (AssetsController.SelectedAssetProject != null)
            {
                var currentSelectedOrg =
                    m_OrganizationDropDown.sourceItems[m_OrganizationDropDown.selectedIndex] as IOrganization;
                if (currentSelectedOrg == null)
                {
                    m_ProjectDropDown.SetValueWithoutNotify(null);
                    return;
                }

                if (string.Equals(currentSelectedOrg.Id.ToString(), AssetsController.SelectedAssetProject.Value.AssetProject.Descriptor.OrganizationId.ToString()))
                {
                    var index = obj.FindIndex(x => string.Equals(x.AssetProject.Descriptor.ProjectId.ToString(),
                        AssetsController.SelectedAssetProject.Value.AssetProject.Descriptor.ProjectId.ToString()));
                    m_ProjectDropDown.SetValueWithoutNotify(new [] {index});
                    AssetsController.GetAssetCollectionsForProject?.Invoke(obj[index], OnAllCollectionsLoaded);
                }
                else
                {
                    m_ProjectDropDown.SetValueWithoutNotify(null);
                }
            }
            else
            {
                m_ProjectDropDown.SetValueWithoutNotify(null);
            }
        }

        // Event handler for asset creation progress
        private void OnAssetCreationProgress(float progress)
        {
            if (progress >= 1f)
            {
                IsUploading = false;
                
                m_OrganizationDropDown.SetEnabled(true);
                m_ProjectDropDown.SetEnabled(true);
                m_CollectionDropDown.SetEnabled(true);
                m_AssetNameField.SetEnabled(true);
                m_AssetTypeDropDown.SetEnabled(true);
                m_AssetDescriptionField.SetEnabled(true);
                
                m_CloseButton.SetEnabled(true);
                m_PickFileButton.SetEnabled(true);
                m_RemoveFileButton.SetEnabled(false);
                m_AddNewAssetButton.SetEnabled(false);
                m_AddNewAssetButton.SetBinding("label", m_UploadAssetString);
                m_FileList.ClearSelection();
                m_FileList.itemsSource = null;
                m_AssetNameField.value = string.Empty;
                m_AssetDescriptionField.value = string.Empty;
                m_CreationProgress.value = 0f;
                m_CreationProgress.style.display = DisplayStyle.None;
                m_ProgressLabel.style.display = DisplayStyle.None;
                
                m_LoadingModal?.Dismiss();
                
                m_Toast?.Dismiss();
                m_Toast = Toast.Build(m_CreationProgress, "", NotificationDuration.Long).SetStyle(NotificationStyle.Positive);
                m_Toast.shown += M_ToastOnshown;
                m_Toast.Show();
                return;
            }
            IsUploading = true;
            m_AddNewAssetButton.SetBinding("label", m_UploadingAssetString);
            m_CreationProgress.style.display = DisplayStyle.Flex;
            m_CreationProgress.value = progress;
            m_ProgressLabel.style.display = DisplayStyle.Flex;
            m_ProgressLabel.text = $"{progress * 100f:0}%";
        }

        private void M_ToastOnshown(Toast obj)
        {
            m_Toast.shown -= M_ToastOnshown;
            var text = m_Toast.view.Q<LocalizedTextElement>("appui-toast__message");
            text.SetBinding("text", m_AssetCreatedString);
        }

        // Event handler for add new asset button click
        private void AddNewAssetButtonOnClicked()
        {
            if(m_OrganizationDropDown.selectedIndex == -1 || m_ProjectDropDown.selectedIndex == -1 || m_AssetTypeDropDown.selectedIndex == -1) return;

            var selectedAssetType = (m_AssetTypeDropDown.sourceItems as AssetType[])[m_AssetTypeDropDown.selectedIndex];
            
            IOrganization selectedOrg = m_OrganizationDropDown.sourceItems[m_OrganizationDropDown.selectedIndex] as IOrganization;
            IAssetProject selectedProject = m_ProjectDropDown.sourceItems[m_ProjectDropDown.selectedIndex] as IAssetProject;
            IAssetCollection selectedCollection = m_CollectionDropDown.selectedIndex == -1 ? null : m_CollectionDropDown.sourceItems[m_CollectionDropDown.selectedIndex] as IAssetCollection;
            var allFiles = m_FileList.itemsSource as List<string>;
            
            if(selectedOrg == null || selectedProject == null) return;
            
            m_OrganizationDropDown.SetEnabled(false);
            m_ProjectDropDown.SetEnabled(false);
            m_CollectionDropDown.SetEnabled(false);
            m_AssetNameField.SetEnabled(false);
            m_AssetTypeDropDown.SetEnabled(false);
            m_AssetDescriptionField.SetEnabled(false);
            m_AddNewAssetButton.SetBinding("label", m_UploadingAssetString);
            m_CloseButton.SetEnabled(false);
            m_PickFileButton.SetEnabled(false);
            m_RemoveFileButton.SetEnabled(false);
            m_AddNewAssetButton.SetEnabled(false);
            m_FileList.SetEnabled(false);
            m_CreationProgress.style.display = DisplayStyle.Flex;
            AssetsController.AssetCreation.Invoke(m_AssetNameField.value, m_AssetDescriptionField.value, selectedAssetType, selectedOrg, selectedProject, selectedCollection, allFiles);

            ShowLoadingModal();
        }

        // Show loading modal
        private void ShowLoadingModal()
        {
            m_LoadingModal?.Dismiss();
            var process = new CircularProgress
            {
                size = Size.L
            };
            m_LoadingModal = Modal.Build(m_AddNewAssetButton, process);
            m_LoadingModal.shown += (modal) =>
            {
                //To remove the shadow from the background.
                modal.contentView.parent.RemoveFromClassList("appui-modal__content");
            };
            m_LoadingModal.Show();
        }

        // Event handler for remove button click
        private void OnRemoveButtonPressed()
        {
            var selectedItems = m_FileList.selectedItems;
            var files = m_FileList.itemsSource as List<string>;
            if(files == null) return;
            foreach(var item in selectedItems)
            {
                files.Remove(item as string);
            }

            m_FileList.itemsSource = null;
            m_FileList.itemsSource = new List<string>(files);
            m_FileList.ClearSelection();
        }

        // Bind item for asset type dropdown
        private void AssetTypeDropDownBindItem(DropdownItem arg1, int arg2)
        {
            var assetTypes = m_AssetTypeDropDown.sourceItems as AssetType[];
            if (assetTypes == null)
            {
                return;
            }
            var assetType = assetTypes[arg2];
            
            var localizedString = assetType.GetAssetTypeAsString();
            if(localizedString == null) return;

            var text = arg1.Q<LocalizedTextElement>();
            text.SetBinding("text", localizedString);
        }

        // Bind item for project dropdown
        private void ProjectDropDownBindItem(DropdownItem arg1, int arg2)
        {
            var projects = m_ProjectDropDown.sourceItems as List<IAssetProject>;
            if (projects == null)
            {
                return;
            }
            arg1.label = projects[arg2].Name;
        }

        // Bind item for organization dropdown
        private void OrganizationDropDownBindItem(DropdownItem arg1, int arg2)
        {
            var org = m_OrganizationDropDown.sourceItems as List<IOrganization>;
            if (org == null)
            {
                return;
            }
            arg1.label = org[arg2].Name;
        }

        // Event handler for pick file button click
        private void OnPickFileButtonPressed()
        {
            // Open file picker to select files
            var paths = new List<string>();
            
            if(paths.Count == 0) return;
            var files = m_FileList.itemsSource as List<string>;
            files ??= new List<string>();
            foreach(var path in paths)
            {
                if(files.Contains(path)) continue;
                files.Add(path);
            }

            m_FileList.itemsSource = null;
            m_FileList.itemsSource = new List<string>(files);
        }

        // Create a new visual element for the file list
        private static VisualElement FileListMakeItem()
        {
            var item = new VisualElement();

            var label = new Label
            {
                name = "FileLabel",
            };
            item.Add(label);
            return item;
        }
        
        // Bind item for the file list
        private void FileListBindItem(VisualElement arg1, int arg2)
        {
            var label = arg1.Q<Label>("FileLabel");
            var file = m_FileList.itemsSource[arg2] as string;
            label.text = file;
        }

        // Event handler for file list selection change
        private void OnSelectionChanged(IEnumerable<object> obj)
        {
            m_RemoveFileButton.SetEnabled(obj.Any());
        }

        // Callback for when all organizations are loaded
        private void OnAllOrganizationsLoaded(List<IOrganization> obj)
        {
            if(m_OrganizationDropDown == null) return;
            m_OrganizationDropDown.sourceItems = obj;
            if(m_NewAssetContainerRoot.style.display == DisplayStyle.None) return;
            m_OrganizationDropDown.selectedIndex = 0;
        }

        // Show the asset creation UI
        public void Show()
        {
            m_FileList.ClearSelection();
            m_FileList.itemsSource?.Clear();
            m_NewAssetContainerRoot.style.display = DisplayStyle.Flex;
            m_CreationProgress.style.display = DisplayStyle.None;
            m_ProgressLabel.style.display = DisplayStyle.None;
            
            m_AddNewAssetButton.SetBinding("label", m_UploadAssetString);
            
            m_AddNewAssetButton.SetEnabled(false);
            
            if(Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                m_PickFileButton.SetEnabled(false);
            }
            else
            {
                m_PickFileButton.SetEnabled(true);
            }
            
            m_RemoveFileButton.SetEnabled(false);
            m_AssetNameField.value = string.Empty;
            m_AssetDescriptionField.value = string.Empty;
            m_AssetTypeDropDown.SetValueWithoutNotify(null);
            
            if (AssetsController.SelectedOrganization != null)
            {
                var index = (m_OrganizationDropDown.sourceItems as List<IOrganization>).FindIndex(x =>
                    x.Id == AssetsController.SelectedOrganization.Id);
                m_OrganizationDropDown.SetValueWithoutNotify(new []{index});
                //AssetsController.GetAssetProjectsForOrganization?.Invoke(m_OrganizationDropDown.sourceItems[m_OrganizationDropDown.selectedIndex] as IOrganization, OnAllAssetsProjectsLoaded, false);
            }
            else
            {
                m_OrganizationDropDown.SetValueWithoutNotify(null);
            }
            
            m_ProjectDropDown.SetValueWithoutNotify(null);
            m_ProjectDropDown.sourceItems = null;
            m_CollectionDropDown.SetValueWithoutNotify(null);
            m_CollectionDropDown.sourceItems = null;
        }

        // Close the asset creation UI
        public void Close()
        {
            OnCloseButtonPressed();
        }

        // Event handler for asset name changing
        private void OnAssetNameChanging(ChangingEvent<string> evt)
        {
            CheckAddNewAssetButton();
        }

        // Event handler for asset name changed
        private void OnAssetNameChanged(ChangeEvent<string> evt)
        {
            CheckAddNewAssetButton();
        }

        // This method checks if the "Add New Asset" button should be enabled.
        // The button is enabled if the asset name field is not empty,
        // and an organization, project, and asset type are selected.
        private void CheckAddNewAssetButton()
        {
            bool enable = !string.IsNullOrEmpty(m_AssetNameField.value) &&
                           m_OrganizationDropDown.selectedIndex != -1 &&
                           m_ProjectDropDown.selectedIndex != -1 &&
                           m_AssetTypeDropDown.selectedIndex != -1;
            
            m_AddNewAssetButton.SetEnabled(enable);
        }

        // This method handles the close button press event.
        // It clears the file list selection and hides the new asset container UI.
        private void OnCloseButtonPressed()
        {
            m_FileList.ClearSelection();
            m_FileList.Clear();
            m_NewAssetContainerRoot.style.display = DisplayStyle.None;
        }

        
        // This method checks if the new asset container UI is visible.
        public bool IsVisible()
        {
            return m_NewAssetContainerRoot.resolvedStyle.display == DisplayStyle.Flex;
        }
    }
}
