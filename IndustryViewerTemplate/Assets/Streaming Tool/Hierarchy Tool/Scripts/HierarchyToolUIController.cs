using System.Collections.Generic;
using System.Linq;
using RuntimeGizmos;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Cloud.Common;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.AppUI.UI;
using Unity.Cloud.DataStreaming.Metadata;
using Unity.Mathematics;
using Unity.Cloud.HighPrecision.Runtime;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Unity.Industry.Viewer.Assets;
using Vector3Field = Unity.AppUI.UI.Vector3Field;
using Button = Unity.AppUI.UI.Button;
using System.Collections;
using Unity.Industry.Viewer.Shared;
#if ENABLE_MULTIPLAY
using Unity.Collections;
#endif

namespace Unity.Industry.Viewer.Streaming.Hierarchy
{
    public class HierarchyToolUIController : StreamToolUIBase
    {
        private const string k_HierarchyTreeViewName = "HierarchyTreeView";
        private const string k_HierarchyItemToggleLabelName = "ToggleLabel";
        private const string k_CubeIconName = "cube";
        private const string k_HiddenCubeIconName = "hidden-cube";
        private const string k_BinButtonName = "BinButton";
        private const string k_VisibilityButtonName = "VisibilityButton";
        private const string k_PositionFieldName = "Position";
        private const string k_RotationFieldName = "Rotation";
        private const string k_SnapValueFieldName = "SnapValue";
        private const string k_ResetPositionButtonName = "ResetPositionButton";
        private const string k_LoadingPanelName = "Loading";
        private const string k_PositionModeButton = "PositionModeButton";
        private const string k_RotationModeButton = "RotationModeButton";
        private const string k_TransformInspectorName = "TransformInspectorElement";
        private const string k_StreamingPanelName = "StreamingContainer";
        private const string k_ResetAllVisibilityButtonName = "ResetAllVisibilityButton";

        private TreeView m_HierarchyTreeView;

        private int m_LastInstanceId = 0;
        
        private Queue<InstanceId> m_Queue = new();

        private InstanceId m_TargetInstanceID = InstanceId.None;

        private ModelStreamId m_TargetModelStreamId;
        
        [SerializeField]
        private StyleSheet m_HierarchyToolStyleSheet;
        
        private HierarchyToolController m_HierarchyController => m_Controller as HierarchyToolController;

        [SerializeField] private VisualTreeAsset m_TransformInspector;
        
        private VisualElement m_TransformInspectorElement;
        private VisualElement m_LoadingPanel;
        private VisualElement m_StreamingContainer;
        private Vector3Field m_PositionField;
        private Vector3Field m_RotationField;
        private TouchSliderFloat m_SnapValueField;
        private Button m_ResetPositionButton;
        private IconButton m_PositionModeButton;
        private IconButton m_RotationModeButton;
        private ActionButton m_ResetAllVisibilityButton;

        private float m_LastTapTime = 0f;
        private const float k_DoubleTapThreshold = 0.5f; // 300 ms
        
        public bool Initialized { get; private set; }
        
        private GridViewManager m_GridViewManager => m_HierarchyController.GridViewManager;
        
#if ENABLE_MULTIPLAY
        private HierarchyToolNetworkObject m_NetworkObject;
#endif
        
        private void Start()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            HierarchyToolController.UpdateToggleUI += OnUpdateToggleUI;
            TransformController.TransformChanged += OnTransformChanged;
            HierarchyToolController.QueryStarted += OnQueryStarted;
            HierarchyToolController.QueryAbort += OnQueryAbort;

            TransformController.ModelAdded += OnModelAdded;
            TransformController.ModelRemoved += OnModelRemoved;
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            HierarchyToolController.UpdateToggleUI -= OnUpdateToggleUI;
            HierarchyToolController.QueryStarted -= OnQueryStarted;
            HierarchyToolController.QueryAbort -= OnQueryAbort;
            TransformController.TransformChanged -= OnTransformChanged;
            TransformController.ModelAdded -= OnModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
            if (m_PanelDocument != null)
            {
                m_PanelDocument.rootVisualElement.RemoveStyleSheetIfPresent(m_HierarchyToolStyleSheet);
            }
            
            UninitializeUI();
            if (m_Controller != null)
            {
                m_Controller.ToolOpened -= OnToolOpened;
                m_Controller.ToolClosed -= OnToolClosed;
            }
            HierarchyToolController.TreeViewItemsUpdated -= OnTreeViewItemsUpdated;
            HierarchyToolController.InstanceSelectedOnModel -= OnInstanceSelectedOnModel;

#if ENABLE_MULTIPLAY
            UnlockModel();
#endif
        }
        
        private void OnQueryAbort()
        {
            m_HierarchyTreeView.ClearSelection();
            SetLoadingPanel(false);
        }
        
        private void OnQueryStarted(int arg1, InstanceData arg2)
        {
            SetLoadingPanel(true);
        }
        
        private void OnModelAdded(GameObject arg1, ITransformValuesAccessor arg2)
        {
            RefreshPanel();
        }
        
        private void OnModelRemoved(StreamingModel obj)
        {
#if ENABLE_MULTIPLAY
            if (m_TransformInspectorElement.userData != null)
            {
                var selected = (m_TransformInspectorElement.userData as StreamingModel);
                if (selected != null && selected == obj)
                {
                    HierarchyToolController.LockModel?.Invoke(selected.name, false);
                }
            }
#endif
            RefreshPanel();
        }

        private void SetLoadingPanel(bool active)
        {
            m_LoadingPanel.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            m_HierarchyTreeView.SetEnabled(!active);
        }

        private void RefreshPanel()
        {
            EnableTransformInspector(false, null);
            m_HierarchyTreeView.ClearSelection();
            m_HierarchyTreeView.Clear();
            _ = m_HierarchyController.UpdateTreeViewItems();
        }

        private void OnTransformChanged(Transform obj)
        {
            if(m_TransformInspectorElement.userData == null) return;
            var streamingModel = m_TransformInspectorElement.userData as StreamingModel;
            if(streamingModel == null) return;
            
            if(obj != streamingModel.transform) return;
            m_PositionField.SetValueWithoutNotify(obj.localPosition);
            m_RotationField.SetValueWithoutNotify(obj.localEulerAngles);
        }

        private void OnLocaleChanged(Locale obj)
        {
            m_HierarchyTreeView?.RefreshItems();
        }

        private void OnUpdateToggleUI(InstanceData instanceData, bool visible)
        {
            if(m_HierarchyTreeView == null) return;
            var allItemId = m_HierarchyTreeView.viewController.GetAllItemIds();
            foreach (var i in allItemId)
            {
                var itemData = m_HierarchyTreeView.GetItemDataForId<InstanceData>(i);

                if (itemData == null || itemData.StreamingModel != instanceData.StreamingModel)
                {
                    continue;
                }

                var ve = m_HierarchyTreeView.GetRootElementForId(i);
                if (ve == null)
                {
                    continue; // ve can be null if the item is not visible in the tree view
                }

                if (instanceData.Instance == null && itemData.Instance?.Name == "Root")
                {
                    ve.Q<IconButton>(k_VisibilityButtonName).icon = visible ? k_CubeIconName : k_HiddenCubeIconName;
                    break;
                }

                if (itemData.Instance != null && instanceData.Instance != null
                    && itemData.Instance.Id != instanceData.Instance.Id) continue;

                ve.Q<IconButton>(k_VisibilityButtonName).icon = visible ? k_CubeIconName : k_HiddenCubeIconName;
                break;
            }
        }

        public override void InitializeUI(UIDocument uiDocument, VisualElement parent, GameObject controller)
        {
            m_PanelDocument = uiDocument;
            
            m_PanelDocument.rootVisualElement.AddStyleSheetIfMissing(m_HierarchyToolStyleSheet);
            
            if (controller.TryGetComponent(out m_Controller))
            {
                m_Controller.ToolOpened += OnToolOpened;
                m_Controller.ToolClosed += OnToolClosed;
            }
            m_LastInstanceId = 0;
            HierarchyToolController.TreeViewItemsUpdated += OnTreeViewItemsUpdated;
            HierarchyToolController.InstanceSelectedOnModel += OnInstanceSelectedOnModel;
            
            m_StreamingContainer =
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_StreamingPanelName);
            
            m_HierarchyTreeView = parent.Q<TreeView>(k_HierarchyTreeViewName);
            
            m_HierarchyTreeView.bindItem = HierarchyBindItem;
            m_HierarchyTreeView.unbindItem = HierarchyUnbindItem;
            m_HierarchyTreeView.selectedIndicesChanged += OnSelectedIndicesChanged;
            m_HierarchyTreeView.itemExpandedChanged += HierarchyItemExpanded;
            
            m_LoadingPanel = parent.Q<VisualElement>(k_LoadingPanelName);
            SetLoadingPanel(false);
            
            m_TransformInspectorElement = m_TransformInspector.Instantiate().Children().First();
            m_TransformInspectorElement.name = k_TransformInspectorName;
            
            
            m_StreamingContainer.Add(m_TransformInspectorElement);
            m_TransformInspectorElement.style.position = Position.Absolute;
            m_TransformInspectorElement.style.top = new Length(85f, LengthUnit.Pixel);
            m_TransformInspectorElement.style.left = new Length(20f, LengthUnit.Pixel);
            
            m_TransformInspectorElement.SetEnabled(false);
            
            m_PositionField = m_TransformInspectorElement.Q<Vector3Field>(k_PositionFieldName);
            m_PositionField.SetValueWithoutNotify(Vector3.zero);
            m_PositionField.RegisterValueChangedCallback(OnPositionFieldValueChanged);
            m_PositionField.RegisterValueChangingCallback(OnPositionFieldValueChanging);
            
            m_RotationField = m_TransformInspectorElement.Q<Vector3Field>(k_RotationFieldName);
            m_RotationField.SetValueWithoutNotify(Vector3.zero);
            m_RotationField.RegisterValueChangedCallback(OnRotationFieldValueChanged);
            m_RotationField.RegisterValueChangingCallback(OnRotationFieldValueChanging);
            
            m_ResetPositionButton = m_TransformInspectorElement.Q<Button>(k_ResetPositionButtonName);
            m_ResetPositionButton.clicked += OnResetPositionButtonClicked;
            
            m_SnapValueField = m_TransformInspectorElement.Q<TouchSliderFloat>(k_SnapValueFieldName);
            m_SnapValueField.RegisterValueChangingCallback(OnSnapValueChanging);
            m_SnapValueField.RegisterValueChangedCallback(OnSnapValueChanged);
            
            m_PositionModeButton = m_TransformInspectorElement.Q<IconButton>(k_PositionModeButton);
            m_PositionModeButton.clicked += OnGizmoPositionModeButtonClicked;
            
            m_RotationModeButton = m_TransformInspectorElement.Q<IconButton>(k_RotationModeButton);
            m_RotationModeButton.clicked += OnGizmoRotationModeButtonClicked;

            m_ResetAllVisibilityButton = parent.Q<ActionButton>(k_ResetAllVisibilityButtonName);
            m_ResetAllVisibilityButton.clicked += OnResetAllVisibilityButtonClicked;

            StartCoroutine(WaitForGridViewManager());
            return;
            
            IEnumerator WaitForGridViewManager()
            {
                while (m_GridViewManager == null)
                {
                    yield return null;
                }
                m_SnapValueField.SetValueWithoutNotify(m_GridViewManager.GetGridUnit());
                Initialized = true;
            }
        }

        public void RefreshTreeViewItems()
        {
            m_HierarchyTreeView?.RefreshItems();
        }

        public void RebuildTreeViewItems()
        {
            m_HierarchyTreeView?.Rebuild();
        }

        private void OnGizmoRotationModeButtonClicked()
        {
            m_PositionModeButton.primary = false;
            m_RotationModeButton.primary = true;
            m_HierarchyController.SwitchGizmoMode(TransformType.Rotate);
        }

        private void OnGizmoPositionModeButtonClicked()
        {
            m_PositionModeButton.primary = true;
            m_RotationModeButton.primary = false;
            m_HierarchyController.SwitchGizmoMode(TransformType.Move);
        }

        private void OnSnapValueChanged(ChangeEvent<float> evt)
        {
            float value = Mathf.Round(evt.newValue * 10) / 10;
            m_GridViewManager?.SetGridUnit(value);
            TransformGizmo.Instance.movementSnap = value;
            TransformGizmo.Instance.rotationSnap = value;
        }

        private void OnSnapValueChanging(ChangingEvent<float> evt)
        {
            float value = Mathf.Round(evt.newValue * 10) / 10;
            m_GridViewManager?.SetGridUnit(value);
            TransformGizmo.Instance.movementSnap = value;
            TransformGizmo.Instance.rotationSnap = value;
        }

        private void OnResetPositionButtonClicked()
        {
            Transform selectedTransform = (m_TransformInspectorElement.userData as StreamingModel).transform;
            if (selectedTransform == null) return;
            selectedTransform.localPosition = Vector3.zero;
            selectedTransform.localRotation = Quaternion.identity;
            m_PositionField.SetValueWithoutNotify(Vector3.zero);
            m_RotationField.SetValueWithoutNotify(Vector3.zero);
        }

        private void OnResetAllVisibilityButtonClicked()
        {
            HierarchyToolController.ResetVisibility(false);
        }

        private void OnRotationFieldValueChanging(ChangingEvent<Vector3> evt)
        {
            Transform selectedTransform = (m_TransformInspectorElement.userData as StreamingModel).transform;
            if (selectedTransform == null) return;
            selectedTransform.localRotation = Quaternion.Euler(evt.newValue);
        }

        private void OnRotationFieldValueChanged(ChangeEvent<Vector3> evt)
        {
            Transform selectedTransform = (m_TransformInspectorElement.userData as StreamingModel).transform;
            if (selectedTransform == null) return;
            selectedTransform.localRotation = Quaternion.Euler(evt.newValue);
        }

        private void OnPositionFieldValueChanging(ChangingEvent<Vector3> evt)
        {
            Transform selectedTransform = (m_TransformInspectorElement.userData as StreamingModel).transform;
            if (selectedTransform == null) return;
            selectedTransform.localPosition = evt.newValue;
            m_HierarchyController.UpdateTransformHandlePosition();
        }

        private void OnPositionFieldValueChanged(ChangeEvent<Vector3> evt)
        {
            Transform selectedTransform =  (m_TransformInspectorElement.userData as StreamingModel).transform;
            if (selectedTransform == null) return;
            selectedTransform.localPosition = evt.newValue;
            m_HierarchyController.UpdateTransformHandlePosition();
        }

        private void OnInstanceSelectedOnModel(ModelStreamId modelStreamId, MetadataInstance instance, Dictionary<InstanceId, List<InstanceData>> children)
        {
            if (children.Count == 0)
            {
                return;
            }
            
            SetLoadingPanel(true);
            m_HierarchyTreeView.ClearSelection();
            var roots = m_HierarchyTreeView.viewController.GetRootItemIds();
            m_TargetInstanceID = instance.Id;
            m_TargetModelStreamId = modelStreamId;

            foreach (var root in roots)
            {
                var item = m_HierarchyTreeView.GetItemDataForId<InstanceData>(root);
                if (item == null) continue;
                if(modelStreamId != item.StreamModel.Id) continue;
                if (item.Instance.Id == InstanceId.None) continue;
                QueryHierarchy(root, item.Instance.Id);
            }
            return;

            void QueryHierarchy(int parentId, InstanceId parentInstanceId)
            {
                if (!children.ContainsKey(parentInstanceId))
                {
                    // Wrong root
                    return;
                }
                var childrenTreeItemId = m_HierarchyTreeView.viewController.GetChildrenIds(parentId).ToList();
                if (childrenTreeItemId.Count == 0) return;

                var firstItemID = childrenTreeItemId[0];
                var item = m_HierarchyTreeView.GetItemDataForId<InstanceData>(firstItemID);
                KeyValuePair<InstanceId, List<InstanceData>> nextSet = default;
                if (!item.IsPlaceholder)
                {
                    children.Remove(parentInstanceId);
                    
                    if (children.Count == 0)
                    {
                        // Build a quick lookup from InstanceId -> Tree item id for this level
                        var levelMap = new Dictionary<InstanceId, int>(childrenTreeItemId.Count);
                        foreach (var childId in childrenTreeItemId)
                        {
                            var childItem = m_HierarchyTreeView.GetItemDataForId<InstanceData>(childId);
                            if (childItem == null || childItem.Instance.Id == InstanceId.None) continue;
                            levelMap[childItem.Instance.Id] = childId;
                        }
                        if (levelMap.TryGetValue(instance.Id, out var targetId))
                        {
                            m_HierarchyTreeView.SetSelectionById(new int[] {targetId});
                            m_HierarchyTreeView.ScrollToItemById(targetId);
                            m_HierarchyTreeView.Focus();
                            SetLoadingPanel(false);
                        }
                        return;
                    }
                    
                    nextSet = children.First();
                    // Build a quick lookup from InstanceId -> Tree item id for this level
                    var nextMap = new Dictionary<InstanceId, int>(childrenTreeItemId.Count);
                    foreach (var childId in childrenTreeItemId)
                    {
                        var childItem = m_HierarchyTreeView.GetItemDataForId<InstanceData>(childId);
                        if (childItem == null || childItem.Instance.Id == InstanceId.None) continue;
                        nextMap[childItem.Instance.Id] = childId;
                    }
                    if (nextMap.TryGetValue(nextSet.Key, out var nextId))
                    {
                        var nextItem = m_HierarchyTreeView.GetItemDataForId<InstanceData>(nextId);
                        QueryHierarchy(nextId, nextItem.Instance.Id);
                    }
                    return;
                }
                if (!children.Remove(parentInstanceId, out var metadataValue))
                {
                    Debug.LogError($"[HierarchyUI] Metadata value not found for parentInstanceId={parentInstanceId}");
                    return;
                }

                if (children.Count > 0)
                {
                    nextSet = children.First();
                }
                m_HierarchyTreeView.TryRemoveItem(firstItemID, false);
                var nextQueryId = 0;
                var nextQueryInstanceId = InstanceId.None;
                var focusId = 0;
                foreach (var metadataInstance in metadataValue)
                {
                    var childrenList = new List<TreeViewItemData<InstanceData>>();
                    if (metadataInstance.Instance.HasChildren)
                    {
                        m_LastInstanceId += 1;
                        childrenList.Add(new(m_LastInstanceId, InstanceData.Placeholder));
                    }
                    m_LastInstanceId += 1;
                    var newItem = new TreeViewItemData<InstanceData>(m_LastInstanceId, new InstanceData(metadataInstance.Instance, metadataInstance.StreamingModel, metadataInstance.Repository), childrenList);
                    
                    if(instance.Id == metadataInstance.Instance.Id)
                    {
                        focusId = m_LastInstanceId;
                    }
                    
                    m_HierarchyTreeView.AddItem(newItem, parentId, -1, false);
                    if (children.Count == 0  || metadataInstance.Instance.Id != nextSet.Key)
                    {
                        continue;
                    }
                    nextQueryId = m_LastInstanceId;
                    nextQueryInstanceId = metadataInstance.Instance.Id;
                }
                
                if (children.Count == 0)
                {
                    m_HierarchyTreeView.Rebuild();
                    StartCoroutine(WaitForRebuild(focusId));
                    return;
                }
                
                if (nextQueryInstanceId == InstanceId.None) return;
                QueryHierarchy(nextQueryId, nextQueryInstanceId);
            }

            IEnumerator WaitForRebuild(int ids)
            {
                // Wait for the end of the frame to ensure the tree view is fully built
                yield return new WaitForEndOfFrame();
                m_HierarchyTreeView.SetSelectionById(new int[] {ids});
                m_HierarchyTreeView.ScrollToItemById(ids);
                m_HierarchyTreeView.Focus();
                SetLoadingPanel(false);
            }
        }

        private void OnSelectedIndicesChanged(IEnumerable<int> selectedIndex)
        {
            if (selectedIndex.Count() == 0)
            {
                EnableTransformInspector(false, null);
                HierarchyToolController.InstanceSelectedFromPanel?.Invoke(null);
                return;
            }
            
            var index = selectedIndex.First();
            var item = m_HierarchyTreeView.GetItemDataForIndex<InstanceData>(index);
            if (item == null)
            {
                return;
            }
            HierarchyToolController.InstanceSelectedFromPanel?.Invoke(item);
            
#if ENABLE_MULTIPLAY
            if (!NetworkDetector.RequestedOfflineMode)
            {
                m_NetworkObject ??= FindAnyObjectByType<HierarchyToolNetworkObject>();
                if (m_NetworkObject == null)
                {
                    EnableTransformInspector(item.Instance == null || item.Instance.AncestorIds.Count == 0, item.StreamingModel);
                    return;
                }
            
                if (m_TransformInspectorElement.userData != null)
                {
                    Transform selected = (m_TransformInspectorElement.userData as StreamingModel).transform;
                    if (selected != null)
                    {
                        HierarchyToolController.LockModel?.Invoke(selected.name, false);
                    }
                }

                var isLocked = m_NetworkObject.LockList.Contains(new FixedString64Bytes(item.StreamingModel.transform.name));
                if (isLocked)
                {
                    m_TransformInspectorElement.SetEnabled(false);
                    m_PositionField.SetValueWithoutNotify(Vector3.zero);
                    m_RotationField.SetValueWithoutNotify(Vector3.zero);
                    m_TransformInspectorElement.userData = null;
                    return;
                }
            }
#endif
            EnableTransformInspector(item.Instance == null || item.Instance.AncestorIds.Count == 0, item.StreamingModel);
        }
        
        private void EnableTransformInspector(bool enable, StreamingModel streamingModel)
        {
            if (streamingModel == null)
            {
#if ENABLE_MULTIPLAY
                UnlockModel();
#endif
                if (m_HierarchyController != null)
                {
                    m_HierarchyController.HierarchyToolSceneListener.ResetHighlight();
                    //m_HierarchyController.HighlightModifier?.Reset();
                    m_HierarchyController.DestroyTransformHandle();
                }

                if (m_TransformInspectorElement != null)
                {
                    m_TransformInspectorElement.userData = null;
                    m_TransformInspectorElement.SetEnabled(false);
                }
                return;
            }
            m_TransformInspectorElement?.SetEnabled(enable);
            if (enable)
            {
                if(m_TransformInspectorElement == null) return;
                m_PositionField?.SetValueWithoutNotify(streamingModel.transform.localPosition);
                m_RotationField?.SetValueWithoutNotify(streamingModel.transform.localEulerAngles);
                m_TransformInspectorElement.userData = streamingModel;
                m_PositionModeButton.primary = true;
                m_RotationModeButton.primary = false;
                m_HierarchyController?.CreateTransformHandle(streamingModel.transform, TransformType.Move);
                //RuntimeTransformHandle.Instance.snap = m_SnapValueField.value;
#if ENABLE_MULTIPLAY
                HierarchyToolController.LockModel?.Invoke(streamingModel.transform.name, true);
#endif
            }
            else
            {
#if ENABLE_MULTIPLAY
                UnlockModel();
#endif
                m_HierarchyController?.DestroyTransformHandle();
                m_PositionField?.SetValueWithoutNotify(Vector3.zero);
                m_RotationField?.SetValueWithoutNotify(Vector3.zero);
                if (m_TransformInspectorElement != null)
                {
                    m_TransformInspectorElement.userData = null;
                }
            }
        }
        
#if ENABLE_MULTIPLAY
        private void UnlockModel()
        {
            if (m_TransformInspectorElement.userData != null)
            {
                var selected = (m_TransformInspectorElement.userData as StreamingModel);
                if (selected != null)
                {
                    HierarchyToolController.LockModel?.Invoke(selected.transform.name, false);
                }
            }
        }
#endif

        private void HierarchyItemExpanded(TreeViewExpansionChangedArgs args)
        {
            if(!args.isExpanded)
            {
                return;
            }
            var id = args.id;
            var item = m_HierarchyTreeView.GetItemDataForId<InstanceData>(id);
            if (!m_HierarchyTreeView.viewController.HasChildren(id)) return;
            var allChildren = m_HierarchyTreeView.viewController.GetChildrenIds(id);
            if(allChildren.Count() == 1)
            {
                var firstChild = allChildren.First();
                if (m_HierarchyTreeView.GetItemDataForId<InstanceData>(firstChild).IsPlaceholder)
                {
                    m_HierarchyTreeView.TryRemoveItem(firstChild);
                    HierarchyToolController.QueryStarted?.Invoke(id, item);
                }
            }
        }
        
        private void HierarchyUnbindItem(VisualElement element, int arg2)
        {
            var visibilityButton = element.Q<IconButton>(k_VisibilityButtonName);
            var binButton = element.Q<IconButton>(k_BinButtonName);
            
            element.UnregisterCallback<PointerDownEvent>(OnFocusToInstance);
            
            visibilityButton.UnregisterCallback<ClickEvent>(OnVisibilityButtonClicked);
            if(binButton.style.display == DisplayStyle.None || !binButton.enabledSelf) return;
            binButton.UnregisterCallback<ClickEvent>(OnBinButtonClicked);
        }

        private void HierarchyBindItem(VisualElement element, int index)
        {
            var item = m_HierarchyTreeView.GetItemDataForIndex<InstanceData>(index);
            if (item == null || element == null || item.StreamingModel == null)
            {
                return;
            }
            
            element.RegisterCallback<PointerDownEvent>(OnFocusToInstance);
            
            var visibilityButton = element.Q<IconButton>(k_VisibilityButtonName);
            var binButton = element.Q<IconButton>(k_BinButtonName);
            
            visibilityButton.UnregisterCallback<ClickEvent>(OnVisibilityButtonClicked);
            binButton.UnregisterCallback<ClickEvent>(OnBinButtonClicked);
            element.name = "Data";
            element.userData = item;
            
            var text = element.Q<Text>(k_HierarchyItemToggleLabelName);
            text.text = string.Empty;
            
            if (item.Instance != null)
            {
                if (item.Instance.AncestorIds.Count == 0)
                {
                    binButton.style.display = DisplayStyle.Flex;
                    binButton.SetEnabled(item.StreamingModel.Asset != StreamingModelController.StreamingAsset.Value.Asset);

                    if (binButton.enabledSelf)
                    {
                        binButton.RegisterCallback<ClickEvent>(OnBinButtonClicked);
                    }

                    text.text = GetAssetInstanceName(item.StreamingModel);
                }
                else
                {
                    binButton.DisplayOff();
                    text.text = item.Name;
                }
                
                if (m_HierarchyController != null)
                {
                    if (item.Instance.AncestorIds.Count == 0)
                    {
                        visibilityButton.icon = item.StreamingModel.gameObject.activeSelf? k_CubeIconName : k_HiddenCubeIconName;
                    }
                    else
                    {
                        bool isCurrentlyInvisible =
                            m_HierarchyController.HierarchyToolSceneListener.IsCurrentlyHidden(
                                item.StreamingModel.ModelStream.Id, item.Instance.Id);
                        visibilityButton.icon = !isCurrentlyInvisible ? k_CubeIconName : k_HiddenCubeIconName;
                    }
                }
            }
            else
            {
                binButton.DisplayOn();
                binButton.SetEnabled(item.StreamingModel.Asset != StreamingModelController.StreamingAsset.Value.Asset);

                if (binButton.enabledSelf)
                {
                    binButton.RegisterCallback<ClickEvent>(OnBinButtonClicked);
                }

                text.text = GetAssetInstanceName(item.StreamingModel);
                visibilityButton.icon = item.StreamingModel.gameObject.activeSelf ? k_CubeIconName : k_HiddenCubeIconName;
            }

            visibilityButton.RegisterCallback<ClickEvent>(OnVisibilityButtonClicked);
        }

        private string GetAssetInstanceName(StreamingModel streamingModel)
        {
            var modelIndex = streamingModel.InstanceNumber;
            var indexString = modelIndex > 1 ? $" ({modelIndex})" : string.Empty;
            return $"{streamingModel.AssetName}{indexString} v.{streamingModel.Version}";
        }

        private void OnFocusToInstance(PointerDownEvent evt)
        {
            float currentTime = Time.unscaledTime;
            bool isDoubleTap = false;
            if (evt.pointerType == UnityEngine.UIElements.PointerType.touch || evt.pointerType == UnityEngine.UIElements.PointerType.tracked)
            {
                if (currentTime - m_LastTapTime < k_DoubleTapThreshold)
                {
                    isDoubleTap = true;
                }
                m_LastTapTime = currentTime;
            }
            else if (evt.clickCount == 2)
            {
                isDoubleTap = true;
            }

            if (isDoubleTap)
            {
                var clickedElement = evt.target as VisualElement;
                var dataElement = ReturnDataVisualElement(clickedElement);
                if (dataElement?.userData is InstanceData instanceData)
                {
                    if (instanceData.Instance != null && instanceData.Instance.Geometry.HasValue && instanceData.Instance.Geometry.Value.BoundingBox.HasValue)
                    {
                        double3 boundCenter = instanceData.Instance.Geometry.Value.BoundingBox.Value.Center;
                        double3 modelPosition = new double3(instanceData.StreamingModel.transform.position.x, instanceData.StreamingModel.transform.position.y, instanceData.StreamingModel.transform.position.z);
                        var newBounds = new DoubleBounds(modelPosition + boundCenter, instanceData.Instance.Geometry.Value.BoundingBox.Value.Size);
                        NavigationController.FocusToPoint?.Invoke(newBounds);
                    }
                }
            }
        }

        private void OnBinButtonClicked(ClickEvent evt)
        {
            var clickedElement = (IconButton)evt.target;
            var dataElement = ReturnDataVisualElement(clickedElement);
            var instanceData = dataElement.userData as InstanceData;
            if (instanceData == null)
            {
                return;
            }
            SetLoadingPanel(true);
            clickedElement.SetEnabled(false);
            StreamingModelController.RemoveStreamModel(instanceData.StreamingModel);
        }

        private static VisualElement ReturnDataVisualElement(VisualElement visualElement)
        {
            var currentElement = visualElement;
            while (currentElement != null && currentElement.name != "Data")
            {
                currentElement = currentElement.parent;
            }
            return currentElement;
        }
        

        private void OnVisibilityButtonClicked(ClickEvent evt)
        {
            var clickedElement = (IconButton)evt.target;
            var dataElement = ReturnDataVisualElement(clickedElement);
            var instanceData = dataElement.userData as InstanceData;
            if (instanceData == null)
            {
                return;
            }

            if (instanceData.Instance == null || instanceData.Instance.AncestorIds.Count == 0)
            {
                var currentActive = instanceData.StreamingModel.gameObject.activeSelf;
                instanceData.StreamingModel.gameObject.SetActive(!instanceData.StreamingModel.gameObject.activeSelf);
                clickedElement.icon = !currentActive ? k_CubeIconName : k_HiddenCubeIconName;
                HierarchyToolController.InstanceVisibilityChanged?.Invoke(instanceData, !currentActive);
            }
            else
            {
                bool isCurrentlyInvisible =
                    m_HierarchyController.HierarchyToolSceneListener.IsCurrentlyHidden(
                        instanceData.StreamingModel.ModelStream.Id, instanceData.Instance.Id);
                clickedElement.icon = isCurrentlyInvisible ? k_CubeIconName : k_HiddenCubeIconName;
                HierarchyToolController.InstanceVisibilityChanged?.Invoke(instanceData, isCurrentlyInvisible);
            }
        }

        private void OnTreeViewItemsUpdated(int id, List<List<InstanceData>> list)
        {
            if (id == -1)
            {
                List<TreeViewItemData<InstanceData>> result = new();
                foreach (var instanceDataList in list)
                {
                    foreach (var instanceData in instanceDataList)
                    {
                        var children = new List<TreeViewItemData<InstanceData>>();
                        if (instanceData.Instance != null && instanceData.Instance.HasChildren)
                        {
                            m_LastInstanceId += 1;
                            children.Add(new(m_LastInstanceId, InstanceData.Placeholder));
                        }
                        m_LastInstanceId += 1;
                        var item = new TreeViewItemData<InstanceData>(m_LastInstanceId, instanceData, children);
                        result.Add(item);
                    }
                }
                m_HierarchyTreeView.userData = result;
                m_HierarchyTreeView.SetRootItems(result);
            }
            else
            {
                foreach (var instanceDataList in list)
                {
                    foreach (var instanceData in instanceDataList)
                    {
                        var children = new List<TreeViewItemData<InstanceData>>();
                        if (instanceData.Instance.HasChildren)
                        {
                            m_LastInstanceId += 1;
                            children.Add(new(m_LastInstanceId, InstanceData.Placeholder));
                        }
                        m_LastInstanceId += 1;
                        var item = new TreeViewItemData<InstanceData>(m_LastInstanceId, instanceData, children);
                        m_HierarchyTreeView.AddItem(item, id, -1, false);
                    }
                }
            }
            m_HierarchyTreeView.Rebuild();
            SetLoadingPanel(false);
            m_HierarchyTreeView.SetEnabled(true);
            if (m_Queue.Any())
            {
                if (!m_HierarchyTreeView.viewController.HasChildren(id)) return;
                var children = m_HierarchyTreeView.viewController.GetChildrenIds(id);
                
                foreach (var childId in children)
                {
                    var childItem = m_HierarchyTreeView.GetItemDataForId<InstanceData>(childId);
                    if (childItem == null) continue;
                    if(childItem.StreamModel.Id != m_TargetModelStreamId) continue;
                    if (childItem.IsPlaceholder)
                    {
                        continue;
                    }
                    if (childItem.Instance.Id == InstanceId.None) continue;
                    if (m_Queue.Peek() != childItem.Instance.Id) continue;
                    m_Queue.Dequeue();
                    m_HierarchyTreeView.TryRemoveItem(m_HierarchyTreeView.viewController.GetChildrenIds(childId).First());
                    HierarchyToolController.QueryStarted?.Invoke(childId, childItem);
                }
            } else if (!m_Queue.Any() && m_TargetInstanceID != InstanceId.None)
            {
                var children = m_HierarchyTreeView.viewController.GetChildrenIds(id);
                LookForTargetInstanceId(children);
            }
        }

        private void LookForTargetInstanceId(IEnumerable<int> children)
        {
            foreach (var child in children)
            {
                var childItem = m_HierarchyTreeView.GetItemDataForId<InstanceData>(child);
                if (childItem == null) return;
                if(childItem.StreamModel.Id != m_TargetModelStreamId) continue;
                if (childItem.Instance.Id == InstanceId.None) continue;
                if (childItem.Instance.Id != m_TargetInstanceID) continue;
                m_HierarchyTreeView.SetSelectionById(new int[] {child});
                m_HierarchyTreeView.ScrollToItemById(child);
                m_HierarchyTreeView.Focus();
                m_TargetInstanceID = InstanceId.None;
            }
        }

        public void ClearTransformInspector()
        {
            EnableTransformInspector(false, null);
            m_HierarchyTreeView.ClearSelection();
            m_HierarchyController.DestroyTransformHandle();
        }

        private void OnToolClosed()
        {
            UninitializeUI();
        }

        private void OnToolOpened()
        {
            
        }

        public override void UninitializeUI()
        {
            if (m_HierarchyTreeView != null)
            {
                m_HierarchyTreeView.selectedIndicesChanged -= OnSelectedIndicesChanged;
                m_HierarchyTreeView.itemExpandedChanged -= HierarchyItemExpanded;
            }
            
            EnableTransformInspector(false, null);
            
            m_PositionField?.UnregisterValueChangedCallback(OnPositionFieldValueChanged);
            m_PositionField?.UnregisterValueChangingCallback(OnPositionFieldValueChanging);
            m_RotationField?.UnregisterValueChangedCallback(OnRotationFieldValueChanged);
            m_RotationField?.UnregisterValueChangingCallback(OnRotationFieldValueChanging);
            m_SnapValueField?.UnregisterValueChangingCallback(OnSnapValueChanging);
            m_SnapValueField?.UnregisterValueChangedCallback(OnSnapValueChanged);

            if (m_ResetPositionButton != null)
            {
                m_ResetPositionButton.clicked -= OnResetPositionButtonClicked;
            }
            
            if (m_TransformInspectorElement == null)
            {
                // If the transform inspector element is null, we try to find it in the streaming container to make sure it gets removed
                var streamingContainer =
                    SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>("StreamingContainer");
                m_TransformInspectorElement = streamingContainer.Q<VisualElement>(k_TransformInspectorName);
            }
            
            m_TransformInspectorElement?.RemoveFromHierarchy();
            
            if (m_PositionField != null)
            {
                m_PositionModeButton.clicked -= OnGizmoPositionModeButtonClicked;
            }

            if (m_RotationField != null)
            {
                m_RotationModeButton.clicked -= OnGizmoRotationModeButtonClicked;
            }
        }
    }
}