using System.Collections.Generic;
using System.Linq;
//using IndustryCSE.RuntimeTransformHandle;
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
#if ENABLE_MULTIPLAY
using Unity.Collections;
using Unity.Industry.Viewer.Shared;
#endif

namespace Unity.Industry.Viewer.Streaming.Hierarchy
{
    public class HierarchyToolUIController : StreamToolUIBase
    {
        private const string k_HierarchyTreeViewName = "HierarchyTreeView";
        private const string k_HierarchyItemToggleLabelName = "ToggleLabel";
        private const string k_EyeIconName = "eye";
        private const string k_EyeSlashIconName = "eye-slash";
        private const string k_FocusButtonName = "FocusButton";
        private const string k_BinButtonName = "BinButton";
        private const string k_VisibilityButtonName = "VisibilityButton";
        private const string k_PositionFieldName = "Position";
        private const string k_RotationFieldName = "Rotation";
        private const string k_SnapValueFieldName = "SnapValue";
        private const string k_ResetPositionButtonName = "ResetPositionButton";
        private const string k_LoadingPanelName = "Loading";
        private const string k_PositionModeButton = "PositionModeButton";
        private const string k_RotationModeButton = "RotationModeButton";
        
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
        private Vector3Field m_PositionField;
        private Vector3Field m_RotationField;
        private TouchSliderFloat m_SnapValueField;
        private Button m_ResetPositionButton;
        private IconButton m_PositionModeButton;
        private IconButton m_RotationModeButton;
        
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
            
            var UIDocument = SharedUIManager.Instance.AssetsUIDocument;
            if (UIDocument == null) return;
            if (!UIDocument.rootVisualElement.styleSheets.Contains(m_HierarchyToolStyleSheet))
            {
                UIDocument.rootVisualElement.styleSheets.Add(m_HierarchyToolStyleSheet);
            }

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
            var UIDocument = SharedUIManager.Instance.AssetsUIDocument;
            if (UIDocument != null)
            {
                if (UIDocument.rootVisualElement.styleSheets.Contains(m_HierarchyToolStyleSheet))
                {
                    UIDocument.rootVisualElement.styleSheets.Remove(m_HierarchyToolStyleSheet);
                }
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

        private void OnUpdateToggleUI(InstanceData arg1, bool arg2)
        {
            if(m_HierarchyTreeView == null) return;
            var allItemId = m_HierarchyTreeView.viewController.GetAllItemIds();
            foreach (var i in allItemId)
            {
                var itemData = m_HierarchyTreeView.GetItemDataForId<InstanceData>(i);
                if (itemData == null) continue;

                if (itemData.StreamingModel != arg1.StreamingModel || ((itemData.Instance != null && arg1.Instance != null) &&
                    itemData.Instance.Id != arg1.Instance.Id)) continue;
                var ve = m_HierarchyTreeView.GetRootElementForId(i);
                ve.Q<IconButton>(k_VisibilityButtonName).icon = arg2? k_EyeIconName : k_EyeSlashIconName;
            }
        }

        public override void InitializeUI(VisualElement parent, GameObject controller)
        {
            if (controller.TryGetComponent(out m_Controller))
            {
                m_Controller.ToolOpened += OnToolOpened;
                m_Controller.ToolClosed += OnToolClosed;
            }
            m_LastInstanceId = 0;
            
            HierarchyToolController.TreeViewItemsUpdated += OnTreeViewItemsUpdated;
            HierarchyToolController.InstanceSelectedOnModel += OnInstanceSelectedOnModel;
            
            m_HierarchyTreeView = parent.Q<TreeView>(k_HierarchyTreeViewName);
            
            m_HierarchyTreeView.bindItem = HierarchyBindItem;
            m_HierarchyTreeView.unbindItem = HierarchyUnbindItem;
            m_HierarchyTreeView.selectedIndicesChanged += OnSelectedIndicesChanged;
            m_HierarchyTreeView.itemExpandedChanged += HierarchyItemExpanded;
            
            m_LoadingPanel = parent.Q<VisualElement>(k_LoadingPanelName);
            SetLoadingPanel(false);

            m_TransformInspectorElement = m_TransformInspector.Instantiate().Children().First();
            var streamingContainer =
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>("StreamingContainer");
            streamingContainer.Add(m_TransformInspectorElement);
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
            m_GridViewManager?.SetGridUnit(m_SnapValueField.value);
            
            m_PositionModeButton = m_TransformInspectorElement.Q<IconButton>(k_PositionModeButton);
            m_PositionModeButton.clicked += OnGizmoPositionModeButtonClicked;
            
            m_RotationModeButton = m_TransformInspectorElement.Q<IconButton>(k_RotationModeButton);
            m_RotationModeButton.clicked += OnGizmoRotationModeButtonClicked;
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
            if(children.Count == 0)
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
                var childrenTreeItemId = m_HierarchyTreeView.viewController.GetChildrenIds(parentId);

                var firsItemID = childrenTreeItemId.First();
                var item = m_HierarchyTreeView.GetItemDataForId<InstanceData>(firsItemID);
                KeyValuePair<InstanceId, List<InstanceData>> nextSet = default;
                if (!item.IsPlaceholder)
                {
                    children.Remove(parentInstanceId);
                    
                    if (children.Count == 0)
                    {
                        foreach (var childId in childrenTreeItemId)
                        {
                            item = m_HierarchyTreeView.GetItemDataForId<InstanceData>(childId);
                            if (item.Instance.Id != instance.Id) continue;
                            m_HierarchyTreeView.SetSelectionByIdWithoutNotify(new int[] {childId});
                            m_HierarchyTreeView.ScrollToItemById(childId);
                            m_HierarchyTreeView.Focus();
                            SetLoadingPanel(false);
                            return;
                        }
                        return;
                    }
                    
                    nextSet = children.First();
                    foreach (var childId in childrenTreeItemId)
                    {
                        item = m_HierarchyTreeView.GetItemDataForId<InstanceData>(childId);
                        if (item.Instance.Id == nextSet.Key)
                        {
                            QueryHierarchy(childId, item.Instance.Id);
                        }
                    }
                    return;
                }
                var metadataValue = children[parentInstanceId];
                children.Remove(parentInstanceId);

                if (children.Count > 0)
                {
                    nextSet = children.First();
                }
                
                m_HierarchyTreeView.TryRemoveItem(firsItemID, false);
                int nextQueryId = 0;
                InstanceId nextQueryInstanceId;
                int focusId = 0;
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
                
                if(nextQueryInstanceId == InstanceId.None) return;
                QueryHierarchy(nextQueryId, nextQueryInstanceId);
            }

            IEnumerator WaitForRebuild(int ids)
            {
                // Wait for the end of the frame to ensure the tree view is fully built
                yield return new WaitForEndOfFrame();
                m_HierarchyTreeView.SetSelectionByIdWithoutNotify(new int[] {ids});
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
                m_HierarchyController.HighlightModifier?.Reset();
                m_TransformInspectorElement.userData = null;
                m_TransformInspectorElement.SetEnabled(false);
                return;
            }
            m_TransformInspectorElement.SetEnabled(enable);
            if (enable)
            {
                m_PositionField.SetValueWithoutNotify(streamingModel.transform.localPosition);
                m_RotationField.SetValueWithoutNotify(streamingModel.transform.localEulerAngles);
                m_TransformInspectorElement.userData = streamingModel;
                m_PositionModeButton.primary = true;
                m_RotationModeButton.primary = false;
                m_HierarchyController.CreateTransformHandle(streamingModel.transform, TransformType.Move);
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
                m_HierarchyController.DestroyTransformHandle();
                m_PositionField.SetValueWithoutNotify(Vector3.zero);
                m_RotationField.SetValueWithoutNotify(Vector3.zero);
                m_TransformInspectorElement.userData = null;
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
            var focusButton = element.Q<IconButton>(k_FocusButtonName);
            
            visibilityButton.UnregisterCallback<ClickEvent>(OnVisibilityButtonClicked);
            focusButton.UnregisterCallback<ClickEvent>(OnFocusButtonClicked);
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
            
            //var toggle = element.Q<Toggle>();
            var visibilityButton = element.Q<IconButton>(k_VisibilityButtonName);
            var binButton = element.Q<IconButton>(k_BinButtonName);
            var focusButton = element.Q<IconButton>(k_FocusButtonName);
            
            visibilityButton.UnregisterCallback<ClickEvent>(OnVisibilityButtonClicked);
            focusButton.UnregisterCallback<ClickEvent>(OnFocusButtonClicked);
            binButton.UnregisterCallback<ClickEvent>(OnBinButtonClicked);
            
            element.userData = item;
            
            var text = element.Q<Text>(k_HierarchyItemToggleLabelName);
            text.ClearBinding("text");
            
            if (item.Instance != null)
            {
                if (item.Instance.AncestorIds.Count == 0)
                {
                    //_ = GetTopLevelName(item, toggle);
                    binButton.style.display = DisplayStyle.Flex;
                    binButton.SetEnabled(item.StreamingModel.Asset != StreamingModelController.StreamingAsset.Value.Asset);
                    var modelIndex = int.Parse(item.StreamingModel.gameObject.name.Split("@")[1]);

                    if (binButton.enabledSelf)
                    {
                        binButton.RegisterCallback<ClickEvent>(OnBinButtonClicked);
                    }
                
                    text.text = modelIndex > 1 ? $"{item.StreamingModel.AssetName} ({modelIndex})" : item.StreamingModel.AssetName;
                }
                else
                {
                    binButton.style.display = DisplayStyle.None;
                    text.text = item.Name;
                }
                
                if (m_HierarchyController != null)
                {
                    if (item.Instance.AncestorIds.Count == 0)
                    {
                        visibilityButton.icon = item.StreamingModel.gameObject.activeSelf? k_EyeIconName : k_EyeSlashIconName;
                    }
                    else
                    {
                        if (m_HierarchyController.VisibilityModifier.HiddenInstances == null)
                        {
                            visibilityButton.icon = k_EyeIconName;
                        }
                        else
                        {
                            var isCurrentlyInvisible = m_HierarchyController.VisibilityModifier.HiddenInstances.Any(x =>
                                x.StreamingModel.ModelStream.Id == item.StreamModel.Id &&
                                x.Instance.Id == item.Instance.Id);
                            visibilityButton.icon = isCurrentlyInvisible ? k_EyeSlashIconName : k_EyeIconName;
                        }
                    }
                }
            }
            else
            {
                binButton.style.display = DisplayStyle.Flex;
                binButton.SetEnabled(item.StreamingModel.Asset != StreamingModelController.StreamingAsset.Value.Asset);
                var modelIndex = int.Parse(item.StreamingModel.gameObject.name.Split("@")[1]);

                if (binButton.enabledSelf)
                {
                    binButton.RegisterCallback<ClickEvent>(OnBinButtonClicked);
                }
                
                text.text = modelIndex > 1 ? $"{item.StreamingModel.AssetName} ({modelIndex})" : item.StreamingModel.AssetName;
                
                visibilityButton.icon = item.StreamingModel.gameObject.activeSelf? k_EyeIconName : k_EyeSlashIconName;
            }

            visibilityButton.RegisterCallback<ClickEvent>(OnVisibilityButtonClicked);
            focusButton.RegisterCallback<ClickEvent>(OnFocusButtonClicked);
        }

        private void OnBinButtonClicked(ClickEvent evt)
        {
            var clickedElement = (IconButton)evt.target;
            var instanceData = clickedElement.parent.parent.parent.userData as InstanceData;
            if (instanceData == null)
            {
                return;
            }
            SetLoadingPanel(true);
            clickedElement.SetEnabled(false);
            StreamingModelController.RemoveStreamModel(instanceData.StreamingModel);
        }

        private void OnFocusButtonClicked(ClickEvent evt)
        {
            var clickedElement = (IconButton)evt.target;
            var instanceData = clickedElement.parent.parent.parent.userData as InstanceData;
            if (instanceData == null)
            {
                return;
            }

            if (instanceData.Instance != null && instanceData.Instance.Geometry.HasValue && instanceData.Instance.Geometry.Value.BoundingBox.HasValue)
            {
                double3 boundCenter = instanceData.Instance.Geometry.Value.BoundingBox.Value.Center;
                double3 modelPosition = new double3(instanceData.StreamingModel.transform.position.x, instanceData.StreamingModel.transform.position.y, instanceData.StreamingModel.transform.position.z);
                var newBounds = new DoubleBounds(modelPosition + boundCenter,
                    instanceData.Instance.Geometry.Value.BoundingBox.Value.Size);
                NavigationController.FocusToPoint?.Invoke(newBounds);
            }
        }

        private void OnVisibilityButtonClicked(ClickEvent evt)
        {
            var clickedElement = (IconButton)evt.target;
            var instanceData = clickedElement.parent.parent.parent.userData as InstanceData;
            if (instanceData == null)
            {
                return;
            }

            if (instanceData.Instance == null || instanceData.Instance.AncestorIds.Count == 0)
            {
                var currentActive = instanceData.StreamingModel.gameObject.activeSelf;
                instanceData.StreamingModel.gameObject.SetActive(!instanceData.StreamingModel.gameObject.activeSelf);
                clickedElement.icon = !currentActive ? k_EyeIconName : k_EyeSlashIconName;
                HierarchyToolController.InstanceVisibilityChanged?.Invoke(instanceData, !currentActive);
            }
            else
            {
                bool isCurrentlyInvisible = false;
                if (m_HierarchyController.VisibilityModifier.HiddenInstances != null)
                {
                    isCurrentlyInvisible = m_HierarchyController.VisibilityModifier.HiddenInstances.Any(x =>
                        x.StreamingModel.ModelStream.Id == instanceData.StreamModel.Id &&
                        x.Instance.Id == instanceData.Instance.Id);
                }
                clickedElement.icon = isCurrentlyInvisible ? k_EyeIconName : k_EyeSlashIconName;
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
                m_HierarchyTreeView.SetSelectionByIdWithoutNotify(new int[] {child});
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
            
        }

        private void OnToolOpened()
        {
            
        }

        public override void UninitializeUI()
        {
            m_HierarchyTreeView.selectedIndicesChanged -= OnSelectedIndicesChanged;
            m_HierarchyTreeView.itemExpandedChanged -= HierarchyItemExpanded;
            
            EnableTransformInspector(false, null);
            m_PositionField.UnregisterValueChangedCallback(OnPositionFieldValueChanged);
            m_PositionField.UnregisterValueChangingCallback(OnPositionFieldValueChanging);
            m_RotationField.UnregisterValueChangedCallback(OnRotationFieldValueChanged);
            m_RotationField.UnregisterValueChangingCallback(OnRotationFieldValueChanging);
            m_SnapValueField.UnregisterValueChangingCallback(OnSnapValueChanging);
            m_SnapValueField.UnregisterValueChangedCallback(OnSnapValueChanged);
            m_ResetPositionButton.clicked -= OnResetPositionButtonClicked;
            m_TransformInspectorElement.RemoveFromHierarchy();
            
            m_PositionModeButton.clicked -= OnGizmoPositionModeButtonClicked;
            m_RotationModeButton.clicked -= OnGizmoRotationModeButtonClicked;
        }
    }
}
