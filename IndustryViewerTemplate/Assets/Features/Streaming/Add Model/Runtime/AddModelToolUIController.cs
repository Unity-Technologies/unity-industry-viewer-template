using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming.AddModel
{
    [DefaultExecutionOrder(-100)]
    public class AddModelToolUIController : MonoBehaviour
    {
        private const string k_AssetTopBarName = "AssetTopBar";
        private const string k_AssetInfoPanelRootName = "AssetInfoContainer";
        private const string k_OffloadAssetButtonName = "OffloadAssetButton";
        private const string k_AddToSelectionClassName = "AddModelToSelectionButton";
        private const string k_RemoveFromSelectionClassName = "RemoveModelFromSelectionButton";
        private const string k_TopLeftBarName = "TopLeftBar";
        private const string k_NewAssetButtonName = "NewAssetButton";

        private IconButton m_FolderButton;
        private ActionButton m_AddToSelectionButton;
        private ActionButton m_AddToSceneButton;
        private ActionButton m_NewAssetButton;
        private VisualElement m_OriginalOrganizationContainer;
        private StreamAssetUIController m_StreamAssetUIController;
        private AssetsUIBaseController m_AssetsUIBaseController;
        private AssetInfoUIBaseController m_AssetInfoUIBaseController;
        private Panel m_Panel;
        private VisualElement m_AssetTopBar;
        private AssetsUIBaseController m_CurrentActiveAssetsUIBaseController = null;
        private bool? m_CurrentVersionHasStreamableDataset;

        [SerializeField]
        private StyleSheet m_StyleSheet;

        private AddModelToolController m_AddModelToolController;

        [SerializeField] private LocalizedString m_SelectLocalizedString;
        [SerializeField] private LocalizedString m_SelectedLocalizedString;
        [SerializeField] private LocalizedString m_AddToSceneLocalizedString;
        [SerializeField] private LocalizedString m_UserNotLoggedInLocalizedString;
        [SerializeField] private LocalizedString m_AskToLoginLocalizedString;
        [SerializeField] private LocalizedString m_OK;

        private void Awake()
        {
            m_AddModelToolController = GetComponent<AddModelToolController>();
        }

        private void Start()
        {
            AddModelToolController.OnSelectedAssetChanged += OnSelectedAssetChanged;
            ToolPanelUIController.OpenToolPanel += OnOpenToolPanel;
            StreamingModelController.FinishedAddingModel += OnFinishedAddingModel;
            NavigationController.OnNavigationOptionChanged += NavigationOptionChanged;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            NavigationController.RequestDefaultHomeView += CloseUI;
            SharedUIManager.Instance.AssetsContainer.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.AddStyleSheetIfMissing(m_StyleSheet);

            InitializeToolButton();
            StreamSceneController.ExitSceneConfirmed += CloseUI;
            OfflineModeAssetsController.AssetOffloaded += OnFinishedOffloadModel;
            AssetsController.AssetSelected += OnAssetSelected;

        }

        private void OnDestroy()
        {
            AddModelToolController.OnSelectedAssetChanged -= OnSelectedAssetChanged;
            StopAllCoroutines();
            if (SharedUIManager.Instance?.AssetsUIDocument?.rootVisualElement.styleSheets.Contains(m_StyleSheet) == true)
            {
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Remove(m_StyleSheet);
            }
            SharedUIManager.Instance?.AssetsContainer?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            ToolPanelUIController.OpenToolPanel -= OnOpenToolPanel;
            RemoveToolButton();
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            StreamingModelController.FinishedAddingModel -= OnFinishedAddingModel;
            StreamSceneController.ExitSceneConfirmed -= CloseUI;
            NavigationController.OnNavigationOptionChanged -= NavigationOptionChanged;
            NavigationController.RequestDefaultHomeView -= CloseUI;
            OfflineModeAssetsController.AssetOffloaded -= OnFinishedOffloadModel;
            AssetsController.AssetSelected -= OnAssetSelected;
            if (m_AssetsUIBaseController?.AssetInfoUIController != null)
            {
                m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown?.UnregisterValueChangedCallback(OnVersionDropdownValueChanged);
                m_AssetsUIBaseController.AssetInfoUIController.AssetVersionsLoaded -= OnAssetVersionsLoaded;
            }
        }

        protected virtual void RemoveToolButton()
        {
            m_FolderButton.clicked -= FolderButtonOnClicked;
            m_FolderButton?.RemoveFromHierarchy();
        }

        protected virtual void InitializeToolButton()
        {
            var topLeftBar = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_TopLeftBarName);
            m_FolderButton = new IconButton
            {
                icon = "folder",
                name = "AddModelIconButton",
                primary = false,
                size = Size.L
            };
            topLeftBar.Insert(0, m_FolderButton);
            m_FolderButton.clicked += FolderButtonOnClicked;
        }
        
        private void OnSelectedAssetChanged(HashSet<AssetInfo> obj)
        {
            m_AddToSceneButton?.SetEnabled(obj.Count > 0);
        }
        
        private void OnAssetSelected(AssetInfo obj)
        {
            CloseUI();
        }
        
        private void OnFinishedOffloadModel(AssetInfo assetInfo)
        {
            if (m_AddModelToolController.SelectedAssetsContainExactVersion(assetInfo))
            {
                m_AddModelToolController.RemoveExactVersionOfSelectedAsset(assetInfo);
            }
        }

        protected virtual void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if ((evt.target as VisualElement).style.display == DisplayStyle.None)
            {
                m_AddToSceneButton?.RemoveFromHierarchy();
                m_AddToSelectionButton?.RemoveFromHierarchy();
                UninitializeUI();
            }
        }

        private void OnOpenToolPanel(LocalizedString arg1, VisualElement arg2, bool arg3)
        {
            CloseUI();
        }
        
        private void NavigationOptionChanged(NavigationOption obj)
        {
            CloseUI();
        }

        private void CloseUI()
        {
            if (!SharedUIManager.Instance.AssetsContainer.IsDisplayOn()) return;
            StopAllCoroutines();
            UninitializeUI();
        }

        protected async void FolderButtonOnClicked()
        {
            if (!NetworkDetector.IsOffline && !PlatformServices.IsUserLoggedIn)
            {
                var alertDialog = new AlertDialog()
                {
                    title = await m_UserNotLoggedInLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                    description = await m_AskToLoginLocalizedString.GetTitleLocalizedStringForAppUIAsync()
                };
                alertDialog.SetCancelAction(0, await m_OK.GetTitleLocalizedStringForAppUIAsync());
                var modal = Modal.Build(m_FolderButton, alertDialog);
                modal.Show();
                return;
            }
            
            if (IsVisible())
            {
                UninitializeUI();
            }
            else
            {
                Show();
            }
        }
        
        public bool IsVisible()
        {
            return SharedUIManager.Instance.AssetsContainer.style.display == DisplayStyle.Flex;
        }

        public void Show()
        {
            if (IsVisible()) return;

            StreamToolsController.DisableAllTools?.Invoke(true);
            ToolPanelUIController.CloseToolPanel?.Invoke();
            NavigationController.PauseCameraControl?.Invoke(true);
            InitializeUI();
        }

        private void OnNetworkStatusChanged(bool obj)
        {
            if (!obj && NetworkDetector.RequestedOfflineMode && SharedUIManager.Instance.AssetsContainer.style.display == DisplayStyle.Flex)
            {
                SharedUIManager.Instance.AssetProjectScrollList.Clear();
                SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;
                m_AssetTopBar?.SetEnabled(false);
                SharedUIManager.OrganizationSelected += OrganizationSelected;
            }
            
            DefineUIController();
            
            var checkBoxes = SharedUIManager.Instance.AssetGridView.Query<Checkbox>().ToList();
            foreach (var checkBox in checkBoxes)
            {
                checkBox.SetValueWithoutNotify(CheckboxState.Unchecked);
            }
            m_AddModelToolController.ClearSelectedAssets();

            if (SharedUIManager.Instance.AssetsContainer.style.display == DisplayStyle.Flex && m_AssetInfoUIBaseController.IsVisible())
            {
                UpdateSelectedButton(false);
            }
            
            m_AddToSceneButton?.SetEnabled(m_AddModelToolController.GetSelectedAssetCount() > 0);
        }
        
        private void OnFinishedAddingModel()
        {
            StartCoroutine(WaitForUIUpdate());
            return;

            IEnumerator WaitForUIUpdate()
            {
                SharedUIManager.Instance.AssetGridView.SetEnabled(false);
                m_AssetInfoUIBaseController?.ClearUI();
                SharedUIManager.Instance.AssetGridView.ClearSelectionWithoutNotify();
                SharedUIManager.ClearSelectionOnGrid();
                var allGridAssets = SharedUIManager.Instance.AssetGridView
                    .Query<Checkbox>().ToList();
                foreach (var checkbox in allGridAssets)
                {
                    checkbox.SetEnabled(true);
                    checkbox.SetValueWithoutNotify(CheckboxState.Unchecked);
                }
                m_AddModelToolController.ClearSelectedAssets();
                m_AddToSelectionButton?.SetEnabled(true);
                m_AddToSceneButton?.SetEnabled(false);
                yield return new WaitForEndOfFrame();
                LoadingUIPanel.HideLoadingPanel?.Invoke(() =>
                {
                    SharedUIManager.Instance.AssetGridView.SetEnabled(true);
                });
            }
            
        }

        protected virtual async void InitializeUI()
        {
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.AddStyleSheetIfMissing(m_StyleSheet);

            if (m_FolderButton != null)
            {
                m_FolderButton.primary = true;
            }
            
            m_Panel = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<Panel>();
            
            m_AssetTopBar = SharedUIManager.Instance.AssetsContainer.Q<VisualElement>(k_AssetTopBarName);
            
            SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.Flex;

            m_OriginalOrganizationContainer ??= SharedUIManager.Instance.OrganizationButton.parent;
            
            SharedUIManager.Instance.AssetProjectScrollList.parent.Insert(0, SharedUIManager.Instance.OrganizationButton);
            
            SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;

            m_NewAssetButton = m_AssetTopBar.Q<ActionButton>(k_NewAssetButtonName);
            m_NewAssetButton?.DisplayOff();
            m_StreamAssetUIController = FindFirstObjectByType<StreamAssetUIController>();
            m_StreamAssetUIController.IsStreamFunctionalityActive = false;
            m_StreamAssetUIController.IsDownOffloadFunctionalityActive = false;

            if (NetworkDetector.RequestedOfflineMode)
            {
                SharedUIManager.Organization = null;
                SharedUIManager.Instance.OrganizationButton.label = await 
                    SharedUIManager.Instance.OrganizationPlaceholder.GetTitleLocalizedStringForAppUIAsync();
                SharedUIManager.OrganizationSelected += OrganizationSelected;
            }

            SharedUIManager.Instance.SetAssetGridColumn(7,4);

            var assetsUIBase =
                FindObjectsByType<AssetsUIBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var assetsUIBaseController in assetsUIBase)
            {
                assetsUIBaseController?.AssetInfoUIController?.SwitchCloseButtonBehaviour(true);
                assetsUIBaseController?.AssetInfoUIController?.ClearUI();
            }
            
            m_CurrentActiveAssetsUIBaseController = NetworkDetector.RequestedOfflineMode ? assetsUIBase.First(x => x is OfflineModeAssetsUIController) : assetsUIBase.First(x => x is AssetsUIToolkitController);
            
            m_CurrentActiveAssetsUIBaseController?.SetPathText(null, SharedUIManager.AssetProjectInfo, SharedUIManager.AssetCollection);

            StartCoroutine(WaitForRefresh());

            DefineUIController();

            m_AssetInfoUIBaseController.CloseButton.clicked -= OnCloseInfoButtonPress;
            m_AssetInfoUIBaseController.CloseButton.clicked += OnCloseInfoButtonPress;

            var assetInfoPanelRoot = SharedUIManager.Instance.AssetsContainer.Q<VisualElement>(k_AssetInfoPanelRootName);

            m_AddToSelectionButton = new ActionButton()
            {
                icon = "plus-circle",
            };
            
            m_AddToSelectionButton.AddToClassList("SelectionButton");
            m_AddToSelectionButton.label = await m_SelectLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            m_AddToSelectionButton.clicked += AddToSelectionButtonOnClicked;
            var m_OffloadButton = assetInfoPanelRoot.Q<ActionButton>(k_OffloadAssetButtonName);
            m_OffloadButton.parent.Insert(0, m_AddToSelectionButton);

            m_AddToSceneButton = new ActionButton
            {
                accent = true,
                selected = true,
                icon = "broadcast",
                name = "AddModelToSceneButton",
                label = await m_AddToSceneLocalizedString.GetTitleLocalizedStringForAppUIAsync()
            };

            m_AssetTopBar.Add(m_AddToSceneButton);
            m_AddToSceneButton.SetEnabled(false);
            m_AddToSceneButton.clicked += AddToSceneButtonOnClicked;

            IEnumerator WaitForRefresh()
            {
                yield return new WaitForEndOfFrame();
                SharedUIManager.Instance.AssetGridView.ClearSelectionWithoutNotify();
                SharedUIManager.ClearSelectionOnGrid();

                if (NetworkDetector.RequestedOfflineMode)
                {
                    SharedUIManager.Instance.ClearGridView();
                    SharedUIManager.Instance.AssetProjectScrollList.Clear();
                }
                
                SharedUIManager.Instance.AssetGridView.selectionChanged -= OnSelectedAsset;
                SharedUIManager.Instance.AssetGridView.selectionChanged += OnSelectedAsset;
                SharedUIManager.Instance.AssetGridView.bindItem -= AssetGridBindItem;
                SharedUIManager.Instance.AssetGridView.bindItem += AssetGridBindItem;

                SharedUIManager.Instance.AssetGridView.unbindItem -= AssetGridUnbindItem;
                SharedUIManager.Instance.AssetGridView.unbindItem += AssetGridUnbindItem;
                
                var allGridAssets = SharedUIManager.Instance.AssetGridView
                    .Query<VisualElement>(className: SharedUIManager.k_GridAssetNonSelectedClass).ToList();
                foreach (var gridAsset in allGridAssets)
                {
                    CheckAndCreateCheckBox(gridAsset);
                }
            }
        }

        private void OrganizationSelected(IOrganization obj)
        {
            SharedUIManager.OrganizationSelected -= OrganizationSelected;
            m_AssetTopBar?.SetEnabled(true);
        }

        private void DefineUIController()
        {
            if (NetworkDetector.RequestedOfflineMode)
            {
                m_AssetsUIBaseController = FindFirstObjectByType<OfflineModeAssetsUIController>();
                m_AssetInfoUIBaseController = m_AssetsUIBaseController.AssetInfoUIController;
                return;
            }

            m_AssetsUIBaseController = FindFirstObjectByType<AssetsUIToolkitController>();
            m_AssetInfoUIBaseController = m_AssetsUIBaseController.AssetInfoUIController;
            m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown?.RegisterValueChangedCallback(OnVersionDropdownValueChanged);
            m_AssetsUIBaseController.AssetInfoUIController.AssetVersionsLoaded += OnAssetVersionsLoaded;
        }

        private void AddToSceneButtonOnClicked()
        {
            m_AddToSceneButton.SetEnabled(false);
            m_AddToSelectionButton?.SetEnabled(false);
            SetEnabledAllCheckBoxes(false);

            StartCoroutine(WaitForUpdate());

            IEnumerator WaitForUpdate()
            {
                var wait = new WaitForSecondsRealtime(0.25f);
                //Wait for a short time to make sure the list is updated
                yield return wait;
                //yield return wait;
                
                if (m_Panel.popupContainer.childCount > 0)
                {
                    SetEnabledAllCheckBoxes(true);
                    m_AddToSelectionButton?.SetEnabled(true);
                    yield break;
                }

                if (m_AddModelToolController.GetSelectedAssetCount() == 0)
                {
                    SetEnabledAllCheckBoxes(true);
                    m_AddToSelectionButton?.SetEnabled(true);
                    yield break;
                }
                LoadingUIPanel.ShowLoadingPanel?.Invoke(() =>
                {
                    m_AddModelToolController.AddToScene();
                });
            }
        }

        private static void SetEnabledAllCheckBoxes(bool enable)
        {
            var allGridAssets = SharedUIManager.Instance.AssetGridView.Query<Checkbox>().ToList();
            foreach (var checkbox in allGridAssets)
            {
                checkbox.SetDisplay(true);
                checkbox.SetEnabled(enable);
            }
        }

        private void AssetGridBindItem(VisualElement arg1, int arg2)
        {
            var checkBox = CheckAndCreateCheckBox(arg1);
            if (checkBox == null) return;
            checkBox.SetEnabled(true);
            
            var item = SharedUIManager.Instance.AssetGridView.itemsSource[arg2] as AssetInfo?;
            if (!item.HasValue) return;
            checkBox.name = item.Value.Asset.Descriptor.AssetId.ToString();
            if (!m_CurrentVersionHasStreamableDataset.HasValue || (m_CurrentVersionHasStreamableDataset.HasValue && m_CurrentVersionHasStreamableDataset.Value))
            {
                var contain = m_AddModelToolController.SelectedAssetsContainAnyVersion(item.Value);
                checkBox.SetValueWithoutNotify(contain ? CheckboxState.Checked : CheckboxState.Unchecked);
                checkBox.DisplayOn();
            }
            else
            {
                checkBox.DisplayOff();
            }
            
        }
        
        private void AssetGridUnbindItem(VisualElement element, int index)
        {
            var checkBox = element.Q<Checkbox>();
            if(checkBox == null) return;
            checkBox.SetValueWithoutNotify(CheckboxState.Unchecked);
            checkBox.UnregisterValueChangedCallback(OnCheckBoxValueChanged);
        }

        private Checkbox CheckAndCreateCheckBox(VisualElement element)
        {
            var checkBox = element.Q<Checkbox>();
            if (checkBox != null)
            {
                checkBox.UnregisterValueChangedCallback(OnCheckBoxValueChanged);
                checkBox.RegisterValueChangedCallback(OnCheckBoxValueChanged);
                return checkBox;
            }
            checkBox = new Checkbox()
            {
                emphasized = true,
                style =
                {
                    position = Position.Absolute,
                    left = new Length(15f, LengthUnit.Pixel),
                    top = new Length(15f, LengthUnit.Pixel)
                }
            };
            element.Add(checkBox);
            checkBox.UnregisterValueChangedCallback(OnCheckBoxValueChanged);
            checkBox.RegisterValueChangedCallback(OnCheckBoxValueChanged);
            return checkBox;
        }
        
        private void OnCheckBoxValueChanged(ChangeEvent<CheckboxState> evt)
        {
            var elementName = ((VisualElement)evt.currentTarget).parent.name;
            var index = int.Parse(elementName.Remove(0, SharedUIManager.k_GridItemName.Length));
            var gridAssetInfo = SharedUIManager.Instance.AssetGridView.itemsSource[index] as AssetInfo?;
            
            if(!gridAssetInfo.HasValue) return;

            var assetInfoToProcess = gridAssetInfo.Value;
            var isCurrentGridItem = false;

            // User clicks on currently selected item in the grid, so we need to apply version selected in dropdown
            if (SharedUIManager.SelectedAsset.HasValue && SharedUIManager.SelectedAsset.Value.Asset.Descriptor.AssetId == gridAssetInfo.Value.Asset.Descriptor.AssetId)
            {
                assetInfoToProcess = SharedUIManager.SelectedAsset.Value;
                isCurrentGridItem = true;
            }

            var added = m_AddModelToolController.ManageSelectedAssets(assetInfoToProcess, evt.newValue == CheckboxState.Checked);
            m_AddToSceneButton.SetEnabled(m_AddModelToolController.GetSelectedAssetCount() > 0);

            if (isCurrentGridItem)
            {
                UpdateSelectedButton(added);
                SetSelectionControlsAvailability();
            }
        }

        private void AddToSelectionButtonOnClicked()
        {
            var added = m_AddModelToolController.ManageSelectedAssets(
                SharedUIManager.SelectedAsset.Value,
                !m_AddModelToolController.SelectedAssetsContainAnyVersion(SharedUIManager.SelectedAsset.Value));

            UpdateSelectedButton(added);
            m_AddToSceneButton.SetEnabled(m_AddModelToolController.GetSelectedAssetCount() > 0);
            
            var checkBox = FindSelectionCheckbox(SharedUIManager.SelectedAsset.Value);
            if (checkBox != null)
            {
                checkBox.SetValueWithoutNotify(added ? CheckboxState.Checked : CheckboxState.Unchecked);
            }

            SetSelectionControlsAvailability();
        }

        private Checkbox FindSelectionCheckbox(AssetInfo assetInfo)
        {
            var itemSource = SharedUIManager.Instance.AssetGridView.itemsSource as List<AssetInfo>;
            if (itemSource == null) return null;
            var index = itemSource.FindIndex(sourceAssetInfo => sourceAssetInfo.Asset.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId);
            var item = SharedUIManager.Instance.AssetGridView.Q<VisualElement>(SharedUIManager.ItemNameFromIndex(index));
            return item?.Q<Checkbox>();
        }

        protected virtual async void UninitializeUI()
        {
            if (m_FolderButton != null)
            {
                m_FolderButton.primary = false;
            }

            m_CurrentVersionHasStreamableDataset = null;
            NavigationController.PauseCameraControl?.Invoke(false);
            
            m_AddModelToolController?.ClearSelectedAssets();
            m_NewAssetButton?.DisplayOn();
            SharedUIManager.Instance.AssetGridView.ClearSelection();
            m_CurrentActiveAssetsUIBaseController?.SetPathText(null, SharedUIManager.AssetProjectInfo, SharedUIManager.AssetCollection);
            
            SharedUIManager.Instance.ResetAssetGridColumn();
            SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
            
            SharedUIManager.Instance.AssetGridView.selectionChanged -= OnSelectedAsset;

            if (m_AssetInfoUIBaseController != null)
            {
                m_AssetInfoUIBaseController.CloseButton.clicked -= OnCloseInfoButtonPress;
            }
            
            m_AssetInfoUIBaseController?.PanelTabs.SetEnabled(true);
            
            m_AddToSelectionButton?.RemoveFromHierarchy();
            
            m_AddToSceneButton?.RemoveFromHierarchy();

            m_AddToSelectionButton = null;

            if (m_StreamAssetUIController != null)
            {
                m_StreamAssetUIController.IsStreamFunctionalityActive = true;
                m_StreamAssetUIController.IsDownOffloadFunctionalityActive = true;
            }

            SharedUIManager.Instance.AssetGridView.bindItem -= AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem -= AssetGridUnbindItem;
            
            SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.None;
            m_OriginalOrganizationContainer?.Add(SharedUIManager.Instance.OrganizationButton);

            if (NetworkDetector.RequestedOfflineMode)
            {
                SharedUIManager.Instance.OrganizationButton.label = await 
                    SharedUIManager.Instance.OrganizationPlaceholder.GetTitleLocalizedStringForAppUIAsync();
                SharedUIManager.Instance.ClearGridView();
                SharedUIManager.Instance.AssetProjectScrollList.Clear();
            }
            else
            {
                SharedUIManager.Instance.OrganizationButton.label = SharedUIManager.Organization.Name;
            }
        }

        private void OnCloseInfoButtonPress()
        {
            m_CurrentVersionHasStreamableDataset = null;
            SharedUIManager.Instance.AssetGridView.ClearSelectionWithoutNotify();
            SharedUIManager.SelectedAsset = null;
            SharedUIManager.ClearSelectionOnGrid();
            SharedUIManager.Instance.PathText.text = string.Empty;
        }

        private async void UpdateSelectedButton(bool isSelected)
        {
            m_AddToSelectionButton.label = string.Empty;
            
            if (isSelected)
            {
                m_AddToSelectionButton.icon = "circle-selected";
                if (m_AddToSelectionButton.ClassListContains(k_AddToSelectionClassName))
                {
                    m_AddToSelectionButton.RemoveFromClassList(k_AddToSelectionClassName);
                }

                if (!m_AddToSelectionButton.ClassListContains(k_RemoveFromSelectionClassName))
                {
                    m_AddToSelectionButton.AddToClassList(k_RemoveFromSelectionClassName);
                }

                m_AddToSelectionButton.label = await m_SelectedLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            }
            else
            {
                m_AddToSelectionButton.icon = "circle-nonselected";
                if (m_AddToSelectionButton.ClassListContains(k_RemoveFromSelectionClassName))
                {
                    m_AddToSelectionButton.RemoveFromClassList(k_RemoveFromSelectionClassName);
                }
                
                if (!m_AddToSelectionButton.ClassListContains(k_AddToSelectionClassName))
                {
                    m_AddToSelectionButton.AddToClassList(k_AddToSelectionClassName);
                }

                m_AddToSelectionButton.label = await m_SelectLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            }
        }

        private void OnSelectedAsset(IEnumerable<object> obj)
        {
            if (obj == null || !obj.Any())
            {
                Debug.Log("No asset selected");
                m_CurrentVersionHasStreamableDataset = null;
                return;
            }

            var selectedAssetNullable = obj.First() as AssetInfo?;
            if (!selectedAssetNullable.HasValue)
            {
                return;
            }

            var selectedAsset = selectedAssetNullable.Value;

            // Clear version behavior
            m_CurrentVersionHasStreamableDataset = true; // assuming latest version supports streaming
            AssetsController.ParentAssetSelected?.Invoke(null);
            SetEnabledAllCheckBoxes(true);

            m_AssetInfoUIBaseController.AssetSelected(selectedAsset);
            if (selectedAsset.Asset is OfflineAsset)
            {
                AssetsController.ParentAssetSelected?.Invoke(selectedAsset);
            }

            m_AssetInfoUIBaseController.PanelTabs.value = 0;
            m_AssetInfoUIBaseController.PanelTabs.SetEnabled(false);
            m_AssetInfoUIBaseController.AssetStatusDropdown.SetEnabled(false);

            var assetIsChecked = m_AddModelToolController.SelectedAssetsContainAnyVersion(selectedAsset);
            UpdateSelectedButton(assetIsChecked);
            m_AddToSelectionButton.SetEnabled(true);

            if (selectedAsset.Asset is OfflineAsset offlineAsset)
            {
                m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.sourceItems = new List<int>()
                    { offlineAsset.OfflineAssetInfo.assetVersion };
                m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.SetValueWithoutNotify(new[] { 0 });
                m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.SetEnabled(false);
            }
#if !UNITY_WEBGL || UNITY_EDITOR
            m_StreamAssetUIController.ShowStreamingAssetDownload(selectedAsset);
#endif
        }

        private async void OnVersionDropdownValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            if (evt.newValue == null || !evt.newValue.Any()) return;
            var assets = m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.sourceItems as List<AssetInfo>;
            if (assets == null) return;
            var index = evt.newValue.First();
            var asset = assets[index];
            // Prevent selection before we know if the version has 3DDS
            m_CurrentVersionHasStreamableDataset = false;
            SetSelectionControlsAvailability();

            if (index == 0)
            {
                AssetsController.ParentAssetSelected?.Invoke(null);
            }
            else
            {
                AssetsController.ParentAssetSelected?.Invoke(asset);
            }

            SharedUIManager.SelectedAsset = asset;
            m_AssetInfoUIBaseController.AssetSelected(asset);
            m_AssetInfoUIBaseController.AssetStatusDropdown.SetEnabled(false);

            if (asset.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
            {
                m_CurrentVersionHasStreamableDataset = true;
            }
            else
            {
                m_CurrentVersionHasStreamableDataset = await StreamAssetUIController.HasStreamableDataset(asset);
            }
            var selected = m_AddModelToolController.SelectedAssetsContainAnyVersion(asset);

            if (m_CurrentVersionHasStreamableDataset.Value && selected)
            {
                m_AddModelToolController.ManageSelectedAssets(asset, true);
            } else if (!m_CurrentVersionHasStreamableDataset.Value && selected)
            {
                m_AddModelToolController.ManageSelectedAssets(asset, false);
                var checkBox = SharedUIManager.Instance.AssetGridView.Q<Checkbox>(asset.Asset.Descriptor.AssetId.ToString());
                checkBox?.SetValueWithoutNotify(CheckboxState.Unchecked);
                checkBox?.SetEnabled(false);
                m_AddToSceneButton?.SetEnabled(m_AddModelToolController.GetSelectedAssetCount() > 0);
                UpdateSelectedButton(false);
            }

            SetSelectionControlsAvailability();
        }

        private void OnAssetVersionsLoaded(List<AssetInfo> assets)
        {
            if (!SharedUIManager.SelectedAsset.HasValue)
            {
                Debug.LogWarning("OnAssetVersionsLoaded: No selected asset to match version to.");
                return;
            }

            if (assets == null || assets.Count == 0)
            {
                Debug.LogWarning("OnAssetVersionsLoaded: No versions loaded.");
                return;
            }

            var selectedAssetVersion = m_AddModelToolController.GetSelectedAssetVersion(SharedUIManager.SelectedAsset.Value);
            if (selectedAssetVersion == null)
            {
                Debug.Log("OnAssetVersionsLoaded: No selected asset version found in selected assets.");
                return;
            }

            // Can't use Descriptor equality because for linked assets the ProjectId is different even if the AssetId is the same
            var index = assets.FindIndex(asset =>
                asset.Asset.Descriptor.AssetId == selectedAssetVersion.Value.Asset.Descriptor.AssetId
                && asset.Asset.Descriptor.AssetVersion == selectedAssetVersion.Value.Asset.Descriptor.AssetVersion);

            if (index < 0)
            {
                Debug.LogWarning($"OnAssetVersionsLoaded: Selected asset version not found in versions list. Selected asset version: {selectedAssetVersion.Value.Asset.Descriptor.AssetVersion}, versions found: {string.Join(',', assets.Select(a => a.Asset.Descriptor.AssetVersion.ToString()))}");
                return;
            }

            m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.value = new[] { index };
        }

        private void SetSelectionControlsAvailability()
        {
            if (!SharedUIManager.SelectedAsset.HasValue) return;

            var asset = SharedUIManager.SelectedAsset.Value;
            
            var selected = m_AddModelToolController.SelectedAssetsContainAnyVersion(asset);

            // hasStreamableDataset | SelectedAssetsContainAnyVersion | Button
            //      true               false                            enabled, Select
            //      true               true                             enabled, Deselect
            //      false              false                            disabled, Select
            //      false              true                             enabled, Deselect
            var addToSelectionEnabled = (m_CurrentVersionHasStreamableDataset.HasValue && m_CurrentVersionHasStreamableDataset.Value) || selected;
            m_AddToSceneButton.SetEnabled(m_AddModelToolController.GetSelectedAssetCount() > 0);
            m_AddToSelectionButton.SetEnabled(addToSelectionEnabled);
            Checkbox checkBox = FindSelectionCheckbox(asset);
            checkBox?.SetEnabled(addToSelectionEnabled);
            checkBox?.SetDisplay(addToSelectionEnabled);
        }
    }
}