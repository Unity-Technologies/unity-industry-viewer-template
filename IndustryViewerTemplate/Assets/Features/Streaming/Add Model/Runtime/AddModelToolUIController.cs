using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Localization;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming.AddModel
{
    [DefaultExecutionOrder(-100)]
    public class AddModelToolUIController : MonoBehaviour
    {
        private const string k_AssetTopBarName = "AssetTopBar";
        private const string k_AssetInfoPanelRootName = "AssetInfoContainer";
        private const string k_StreamAssetButtonName = "StreamAssetButton";
        private const string k_OffloadAssetButtonName = "OffloadAssetButton";
        private const string k_DownloadStreamingAssetButtonName = "DownloadStreamingAssetButton";
        private const string k_AddToSelectionClassName = "AddModelToSelectionButton";
        private const string k_RemoveFromSelectionClassName = "RemoveModelFromSelectionButton";
        private const string k_TopLeftBarName = "TopLeftBar";

        private IconButton m_FolderButton;
        private ActionButton m_OffloadButton;
        private ActionButton m_AddToSelectionButton;
        private ActionButton m_AddToSceneButton;
        private VisualElement m_OriginalOrganizationContainer;
        private ProgressActionButton m_DownloadButton;
        private StreamAssetUIController m_StreamAssetUIController;
        private AssetsUIBaseController m_AssetsUIBaseController;
        private AssetInfoUIBaseController m_AssetInfoUIBaseController;
        private Panel m_Panel;
        
        [SerializeField]
        private StyleSheet m_StyleSheet;

        private AddModelToolController m_AddModelToolController;

        [SerializeField] private LocalizedString m_SelectLocalizedString;
        [SerializeField] private LocalizedString m_SelectedLocalizedString;
        [SerializeField] private LocalizedString m_AddToSceneLocalizedString;

        private WaitForEndOfFrame m_Wait = new WaitForEndOfFrame();
        
        private void Awake()
        {
            m_AddModelToolController = GetComponent<AddModelToolController>();
        }

        private void Start()
        {
            ToolPanelUIController.OpenToolPanel += OnOpenToolPanel;
            StreamingModelController.FinishedAddingModel += OnFinishedAddingModel;
            NavigationController.OnNavigationOptionChanged += NavigationOptionChanged;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            NavigationController.RequestDefaultHomeView += CloseUI;
            SharedUIManager.Instance.AssetsRoot.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            
            if (!SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
            {
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Add(m_StyleSheet);
            }
            
            var topLeftBar = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_TopLeftBarName);
            m_FolderButton = new IconButton()
            {
                icon = "folder",
                name = "AddModelIconButton"
            };
            topLeftBar.Insert(0, m_FolderButton);
            m_FolderButton.clicked += FolderButtonOnClicked;
            StreamSceneController.ExitSceneConfirmed += CloseUI;
            OfflineModeAssetsController.AssetOffloaded += OnFinishedOffloadModel;
            AssetsController.AssetSelected += OnAssetSelected;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            if (SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
            {
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Remove(m_StyleSheet);
            }
            SharedUIManager.Instance.AssetsRoot.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            ToolPanelUIController.OpenToolPanel -= OnOpenToolPanel;
            m_FolderButton.clicked -= FolderButtonOnClicked;
            m_FolderButton?.RemoveFromHierarchy();
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            StreamingModelController.FinishedAddingModel -= OnFinishedAddingModel;
            StreamSceneController.ExitSceneConfirmed -= CloseUI;
            NavigationController.OnNavigationOptionChanged -= NavigationOptionChanged;
            NavigationController.RequestDefaultHomeView -= CloseUI;
            OfflineModeAssetsController.AssetOffloaded -= OnFinishedOffloadModel;
            AssetsController.AssetSelected -= OnAssetSelected;
        }
        
        private void OnAssetSelected(AssetInfo obj)
        {
            CloseUI();
        }
        
        private void OnFinishedOffloadModel(AssetInfo obj)
        {
            if (m_AddModelToolController.SelectedAssets.Any(x => x.Asset.Descriptor == obj.Asset.Descriptor))
            {
                m_AddModelToolController.SelectedAssets.Remove(obj);
                m_AddToSceneButton?.SetEnabled(m_AddModelToolController.SelectedAssets.Count > 0);
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            m_FolderButton.primary = (evt.target as VisualElement).style.display == DisplayStyle.Flex;
            if ((evt.target as VisualElement).style.display == DisplayStyle.None)
            {
                m_AddToSceneButton?.RemoveFromHierarchy();
                m_AddToSelectionButton?.RemoveFromHierarchy();
                UninitializeUI();
            }
        }

        private void OnOpenToolPanel(LocalizedString arg1, VisualElement arg2)
        {
            CloseUI();
        }
        
        private void NavigationOptionChanged(NavigationOption obj)
        {
            CloseUI();
        }

        private void CloseUI()
        {
            if (SharedUIManager.Instance.AssetsRoot.style.display == DisplayStyle.None) return;
            StopAllCoroutines();
            UninitializeUI();
        }

        private void FolderButtonOnClicked()
        {
            if (SharedUIManager.Instance.AssetsRoot.style.display == DisplayStyle.None)
            {
                StreamToolsController.DisableAllTools?.Invoke();
                ToolPanelUIController.CloseToolPanel?.Invoke();
                NavigationController.PauseCameraControl?.Invoke(true);
                InitializeUI();
            }
            else
            {
                UninitializeUI();
            }
        }
        
        private void OnNetworkStatusChanged(bool obj)
        {
            if (!obj && NetworkDetector.RequestedOfflineMode && SharedUIManager.Instance.AssetsRoot.style.display == DisplayStyle.Flex)
            {
                SharedUIManager.Instance.AssetProjectScrollList.Clear();
                SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;
            }
            
            DefineUIController();
            
            var checkBoxes = SharedUIManager.Instance.AssetGridView.Query<Checkbox>().ToList();
            foreach (var checkBox in checkBoxes)
            {
                checkBox.SetValueWithoutNotify(CheckboxState.Unchecked);
            }
            m_AddModelToolController.ClearSelectedAssets();

            if (m_AssetInfoUIBaseController.IsVisible())
            {
                UpdateSelectedButton(false);
            }
            
            m_AddToSceneButton?.SetEnabled(m_AddModelToolController.SelectedAssets.Count > 0);
        }
        
        private void OnFinishedAddingModel()
        {
            SharedUIManager.HideLoadingModal();
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
            m_AddToSelectionButton?.SetEnabled(true);
            m_AddToSceneButton?.SetEnabled(false);
        }

        private void InitializeUI()
        {
            if (!SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
            {
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Add(m_StyleSheet);
            }
            
            m_Panel = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<Panel>();
            
            m_FolderButton.primary = true;
            
            SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.Flex;

            m_OriginalOrganizationContainer ??= SharedUIManager.Instance.OrganizationButton.parent;
            
            SharedUIManager.Instance.AssetProjectScrollList.parent.Insert(0, SharedUIManager.Instance.OrganizationButton);
            
            SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.Flex;

            if (NetworkDetector.RequestedOfflineMode)
            {
                SharedUIManager.Instance.OrganizationButton.label = string.Empty;
                SharedUIManager.Instance.OrganizationButton.ClearBinding("label");
                SharedUIManager.Instance.OrganizationButton.SetBinding("label", SharedUIManager.Instance.SelectOrganization);
            }

            SharedUIManager.Instance.SetAssetGridColumn(7,4);
            
            SharedUIManager.Instance.PathText.text = string.Empty;

            m_StreamAssetUIController = FindFirstObjectByType<StreamAssetUIController>();
            
            var assetsUIBase =
                FindObjectsByType<AssetsUIBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var assetsUIBaseController in assetsUIBase)
            {
                assetsUIBaseController?.AssetInfoUIController?.SwitchCloseButtonBehaviour(true);
                assetsUIBaseController?.AssetInfoUIController?.ClearUI();
            }

            StartCoroutine(WaitForRefresh());

            DefineUIController();

            m_AssetInfoUIBaseController.CloseButton.clicked -= OnCloseInfoButtonPress;
            m_AssetInfoUIBaseController.CloseButton.clicked += OnCloseInfoButtonPress;

            var assetInfoPanelRoot = SharedUIManager.Instance.AssetsRoot.Q<VisualElement>(k_AssetInfoPanelRootName);
            
            var streamButton = assetInfoPanelRoot.Q<ActionButton>(k_StreamAssetButtonName);
            streamButton.style.display = DisplayStyle.None;

            m_OffloadButton = assetInfoPanelRoot.Q<ActionButton>(k_OffloadAssetButtonName);
            m_OffloadButton.style.display = DisplayStyle.None;

            m_DownloadButton = assetInfoPanelRoot.Q<ProgressActionButton>(k_DownloadStreamingAssetButtonName);
            m_DownloadButton.style.display = DisplayStyle.None;

            m_AddToSelectionButton = new ActionButton()
            {
                icon = "plus-circle",
            };
            
            m_AddToSelectionButton.SetBinding("label", m_SelectLocalizedString);
            
            m_OffloadButton.parent.Insert(0, m_AddToSelectionButton);
            m_AddToSelectionButton.clicked += AddToSelectionButtonOnclicked;

            m_AddToSceneButton = new ActionButton()
            {
                accent = true,
                selected = true,
                icon = "broadcast",
                name = "AddModelToSceneButton"
            };
            
            m_AddToSceneButton.SetBinding("label", m_AddToSceneLocalizedString);
            
            var assetTopBar = SharedUIManager.Instance.AssetsRoot.Q<VisualElement>(k_AssetTopBarName);
            assetTopBar.Add(m_AddToSceneButton);
            m_AddToSceneButton.SetEnabled(false);
            m_AddToSceneButton.clicked += AddToSceneButtonOnClicked;
            return;
            
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

        private void DefineUIController()
        {
            if (!NetworkDetector.RequestedOfflineMode)
            {
                m_AssetsUIBaseController = FindFirstObjectByType<AssetsUIToolkitController>();
                m_AssetInfoUIBaseController = m_AssetsUIBaseController.AssetInfoUIController;
            }
            else
            {
                m_AssetsUIBaseController = FindFirstObjectByType<OfflineModeAssetsUIController>();
                m_AssetInfoUIBaseController = m_AssetsUIBaseController.AssetInfoUIController;
            }
        }

        private void AddToSceneButtonOnClicked()
        {
            m_AddToSceneButton.SetEnabled(false);
            m_AddToSelectionButton?.SetEnabled(false);
            var allGridAssets = SharedUIManager.Instance.AssetGridView
                .Query<Checkbox>().ToList();
            foreach (var checkbox in allGridAssets)
            {
                checkbox?.SetEnabled(false);
            }

            StartCoroutine(WaitForUpdate());

            IEnumerator WaitForUpdate()
            {
                var wait = new WaitForSecondsRealtime(0.25f);
                //Wait for a short time to make sure the list is updated
                yield return wait;
                //yield return wait;
                
                if (m_Panel.popupContainer.childCount > 0)
                {
                    var allGridAssets = SharedUIManager.Instance.AssetGridView
                        .Query<Checkbox>().ToList();
                    foreach (var checkbox in allGridAssets)
                    {
                        checkbox?.SetEnabled(true);
                    }
                    m_AddToSelectionButton?.SetEnabled(true);
                    m_AddToSceneButton?.SetEnabled(m_AddModelToolController.SelectedAssets.Count > 0);
                    yield break;
                }
                
                if (m_AddModelToolController.SelectedAssets.Count == 0)
                {
                    var allGridAssets = SharedUIManager.Instance.AssetGridView
                        .Query<Checkbox>().ToList();
                    foreach (var checkbox in allGridAssets)
                    {
                        checkbox?.SetEnabled(true);
                    }
                    m_AddToSelectionButton?.SetEnabled(true);
                    yield break;
                }
            
                SharedUIManager.ShowLoadingModal(() =>
                {
                    m_AddModelToolController.AddToScene();
                });
            }
        }

        private void AssetGridBindItem(VisualElement arg1, int arg2)
        {
            StartCoroutine(WaitForEndFrame());
            
            var checkBox = CheckAndCreateCheckBox(arg1);
            if (checkBox == null) return;
            checkBox.SetEnabled(true);
            var item = SharedUIManager.Instance.AssetGridView.itemsSource[arg2] as AssetInfo?;
            
            if (!item.HasValue) return;
            var contain =
                m_AddModelToolController.SelectedAssets.Any(x => x.Asset.Descriptor == item.Value.Asset.Descriptor);
            checkBox.SetValueWithoutNotify(contain ? CheckboxState.Checked : CheckboxState.Unchecked);
            return;

            //Wait for end frame to make sure the button is created
            IEnumerator WaitForEndFrame()
            {
                yield return m_Wait;
                if(arg1 == null) yield break;
                var directStreamButton = arg1.Q<ActionButton>("3DDSButton");
                if (directStreamButton != null)
                {
                    directStreamButton.style.display = DisplayStyle.None;
                }
            }
        }
        
        private void AssetGridUnbindItem(VisualElement element, int index)
        {
            var directStreamButton = element.Q<ActionButton>("3DDSButton");
            directStreamButton.style.display = DisplayStyle.Flex;
            
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
            var item = SharedUIManager.Instance.AssetGridView.itemsSource[index] as AssetInfo?;
            if(!item.HasValue) return;
            var added = m_AddModelToolController.ManageSelectedAssets(item.Value, evt.newValue == CheckboxState.Checked);
            m_AddToSceneButton.SetEnabled(m_AddModelToolController.SelectedAssets.Count > 0);
            if (!SharedUIManager.SelectedAsset.HasValue) return;
            if (SharedUIManager.SelectedAsset.Value == item.Value)
            {
                UpdateSelectedButton(added);
            }
        }

        private void AddToSelectionButtonOnclicked()
        {
            var added = m_AddModelToolController.ManageSelectedAssets(SharedUIManager.SelectedAsset.Value, !m_AddModelToolController.SelectedAssets.Contains(SharedUIManager.SelectedAsset.Value));
            UpdateSelectedButton(added);
            m_AddToSceneButton.SetEnabled(m_AddModelToolController.SelectedAssets.Count > 0);
            var index = SharedUIManager.Instance.AssetGridView.itemsSource.IndexOf(SharedUIManager.SelectedAsset);
            var item = SharedUIManager.Instance.AssetGridView.Q<VisualElement>(
                SharedUIManager.ItemNameFromIndex(index));
            if (item != null)
            {
                var checkBox = item.Q<Checkbox>();
                checkBox?.SetValueWithoutNotify(added ? CheckboxState.Checked : CheckboxState.Unchecked);
            }
        }

        private void UninitializeUI()
        {
            m_FolderButton.primary = false;
            
            NavigationController.PauseCameraControl?.Invoke(false);
            
            m_AddModelToolController?.ClearSelectedAssets();
            
            SharedUIManager.Instance.AssetGridView.ClearSelection();
            SharedUIManager.Instance.PathText.text = string.Empty;
            
            SharedUIManager.Instance.ResetAssetGridColumn();
            SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.None;
            
            SharedUIManager.Instance.AssetGridView.selectionChanged -= OnSelectedAsset;

            if (m_AssetInfoUIBaseController != null)
            {
                m_AssetInfoUIBaseController.CloseButton.clicked -= OnCloseInfoButtonPress;
            }
            
            m_AssetInfoUIBaseController?.PanelTabs.SetEnabled(true);
            
            m_AddToSelectionButton?.RemoveFromHierarchy();
            
            m_AddToSceneButton?.RemoveFromHierarchy();

            m_AddToSelectionButton = null;
            
            SharedUIManager.Instance.AssetGridView.bindItem -= AssetGridBindItem;
            SharedUIManager.Instance.AssetGridView.unbindItem -= AssetGridUnbindItem;
            
            SharedUIManager.Instance.OrganizationButton.style.display = DisplayStyle.None;
            m_OriginalOrganizationContainer?.Add(SharedUIManager.Instance.OrganizationButton);

            if (NetworkDetector.RequestedOfflineMode)
            {
                SharedUIManager.Instance.OrganizationButton.ClearBinding("label");
                SharedUIManager.Instance.OrganizationButton.SetBinding("label", SharedUIManager.Instance.SelectOrganization);
                SharedUIManager.Instance.ClearGridView();
                SharedUIManager.Instance.AssetProjectScrollList.Clear();
            }
            else
            {
                SharedUIManager.Instance.OrganizationButton.ClearBinding("label");
                SharedUIManager.Instance.OrganizationButton.label = SharedUIManager.Organization.Name;
            }
        }

        private void OnCloseInfoButtonPress()
        {
            SharedUIManager.Instance.AssetGridView.ClearSelectionWithoutNotify();
            SharedUIManager.SelectedAsset = null;
            SharedUIManager.ClearSelectionOnGrid();
            SharedUIManager.Instance.PathText.text = string.Empty;
        }

        private void UpdateSelectedButton(bool isSelected)
        {
            m_AddToSelectionButton.ClearBinding("label");
            
            if (isSelected)
            {
                if (m_AddToSelectionButton.ClassListContains(k_AddToSelectionClassName))
                {
                    m_AddToSelectionButton.RemoveFromClassList(k_AddToSelectionClassName);
                }

                if (!m_AddToSelectionButton.ClassListContains(k_RemoveFromSelectionClassName))
                {
                    m_AddToSelectionButton.AddToClassList(k_RemoveFromSelectionClassName);
                }
                
                m_AddToSelectionButton.SetBinding("label", m_SelectedLocalizedString);
            }
            else
            {
                if (m_AddToSelectionButton.ClassListContains(k_RemoveFromSelectionClassName))
                {
                    m_AddToSelectionButton.RemoveFromClassList(k_RemoveFromSelectionClassName);
                }
                
                if (!m_AddToSelectionButton.ClassListContains(k_AddToSelectionClassName))
                {
                    m_AddToSelectionButton.AddToClassList(k_AddToSelectionClassName);
                }
                m_AddToSelectionButton.SetBinding("label", m_SelectLocalizedString);
            }
        }

        private void OnSelectedAsset(IEnumerable<object> obj)
        {
            if (obj == null || !obj.Any())
            {
                return;
            }
            
            var selectedAsset = (obj.First() as AssetInfo?);
            
            m_AssetInfoUIBaseController.AssetSelected(selectedAsset.Value);

            m_AssetInfoUIBaseController.PanelTabs.value = 0;
            m_AssetInfoUIBaseController.PanelTabs.SetEnabled(false);
            m_AssetInfoUIBaseController.AssetStatusDropdown.SetEnabled(false);

            m_DownloadButton.style.display = DisplayStyle.None;
            m_OffloadButton.style.display = DisplayStyle.None;

            var contain =
                m_AddModelToolController.SelectedAssets.Any(x => x.Asset.Descriptor == selectedAsset.Value.Asset.Descriptor);
            
            UpdateSelectedButton(m_AddModelToolController.SelectedAssets != null && contain);
            m_AddToSelectionButton.SetEnabled(true);
            StreamingModel[] streamingModels =
                TransformController.Instance.GetComponentsInChildren<StreamingModel>(true);
            
            m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.sourceItems = new List<AssetInfo> { selectedAsset.Value };
            m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.SetValueWithoutNotify(new []{ 0 });
            m_AssetsUIBaseController.AssetInfoUIController.AssetVersionDropdown.SetEnabled(false);
            
#if !UNITY_WEBGL || UNITY_EDITOR
            if (NetworkDetector.IsOffline && NetworkDetector.RequestedOfflineMode)
            {
                bool isCurrentlyStreaming = false;
                foreach (var streamingModel in streamingModels)
                {
                    if (streamingModel.Asset.Descriptor != selectedAsset.Value.Asset.Descriptor) continue;
                    isCurrentlyStreaming = true;
                    break;
                }
                
                //If what users opened is the same as the current streaming asset, disable the offload button
                m_StreamAssetUIController.OffloadAssetButton.style.display = isCurrentlyStreaming ? DisplayStyle.None : DisplayStyle.Flex;
                return;
            }
            
            m_StreamAssetUIController.ShowStreamingAssetDownload(selectedAsset.Value);

            foreach (var streamingModel in streamingModels)
            {
                if (streamingModel.Asset is OfflineAsset && streamingModel.Asset.Descriptor == selectedAsset.Value.Asset.Descriptor)
                {
                    m_StreamAssetUIController.OffloadAssetButton.style.display = DisplayStyle.None;
                    return;
                }
            }
            
#endif
        }
    }
}
