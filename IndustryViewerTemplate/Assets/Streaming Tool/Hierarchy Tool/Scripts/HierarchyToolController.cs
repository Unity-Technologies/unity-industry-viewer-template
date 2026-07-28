using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cloud.Common;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Cloud.DataStreaming.Metadata;
using UnityEngine.EventSystems;
using RuntimeGizmos;
using Unity.Industry.Viewer.Shared;
using UnityEngine.InputSystem;
using TransformType = RuntimeGizmos.TransformType;
#if VR_MODE
using Unity.Industry.Viewer.VR;
#endif
#if ENABLE_MULTIPLAY
using Unity.Industry.Viewer.Multiplay;
#endif

namespace Unity.Industry.Viewer.Streaming.Hierarchy
{
    // This script manages the hierarchy tool in a Unity project.
    // It handles the querying and updating of hierarchical data for streaming models.
    // The script supports asynchronous operations to fetch and update instance data from metadata repositories.
    // It integrates with Unity's MonoBehaviour for lifecycle management and supports both VR and non-VR modes.
    // The script provides user feedback through various events and updates the UI accordingly.
    public class HierarchyToolController: StreamToolControllerBase
    {
        public static Action<int, List<List<InstanceData>>> TreeViewItemsUpdated;
        public static Action<int, InstanceData> QueryStarted;
        public static Action QueryAbort;
        public static Action<InstanceData, bool> InstanceVisibilityChanged;
        public static Action<InstanceData> InstanceSelectedFromPanel;
        public static Action<ModelStreamId, MetadataInstance, Dictionary<InstanceId, List<InstanceData>>> InstanceSelectedOnModel;
        public static Action<InstanceData, bool> UpdateToggleUI;
        public static Action RequestTreeViewRefresh;

        // Full metadata for the currently-selected instance, consumed by the Hierarchy tool's Metadata tab.
        public static Action<List<MetadataInstance>> MetadataForSelection;

        // Fires when an instance is selected (model + instance id); persisted by HierarchyToolSceneListener
        // for restore-on-open. Formerly MetadataToolController.InstanceSelected.
        public static Action<ModelStreamId, InstanceId> InstanceSelected;

        public static event Action<bool> VisibilityReset;

#if ENABLE_MULTIPLAY
        public static Action<string, bool> LockModel;
#endif

        private int m_LastInstanceId;
        
        public HierarchyToolSceneListener HierarchyToolSceneListener => m_HierarchyToolSceneListener;
        private HierarchyToolSceneListener m_HierarchyToolSceneListener;
        
        private Dictionary<StreamingModel, IMetadataRepository> m_StreamModelRepositoriesMapping => 
            m_HierarchyToolSceneListener == null? null : m_HierarchyToolSceneListener.StreamModelRepositoriesMapping;

        private StreamingModelController m_StreamingModelController => m_HierarchyToolSceneListener == null? null :
            m_HierarchyToolSceneListener.StreamingModelController;
        
        [SerializeField] private InputActionProperty m_PointerClick;
        [SerializeField] private InputActionProperty m_PointerPress;
        [SerializeField] private InputActionProperty m_PointerMove;

        [SerializeField] private LayerMask m_RuntimeHandleLayerMask;
        
        private HierarchyToolUIController m_HierarchyToolUIController;
        private static HierarchyToolController m_Instance;
        
        public GridViewManager GridViewManager { get; private set;}
        
        private TransformGizmo m_TransformGizmo;
        
        private async void Start()
        {
            m_Instance = this;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            StreamingModelController.RemoveStreamModel += RemoveStreamModel;
            StreamingModelController.AddObserver += AddObserver;
            m_HierarchyToolSceneListener = FindAnyObjectByType<HierarchyToolSceneListener>();
            m_HierarchyToolUIController ??= gameObject.GetComponent<HierarchyToolUIController>();
            QueryStarted += OnQueryStarted;
            InstanceSelectedFromPanel += OnInstanceSelectedFromPanel;
            RequestTreeViewRefresh += OnRequestTreeViewRefresh;
            
            GridViewManager = FindFirstObjectByType<GridViewManager>();
            
            ToolOpened?.Invoke(); 
            await UpdateTreeViewItems();
            if(NetworkDetector.RequestedOfflineMode) return;
            FindSelectedInstance();
        }

        private void OnDestroy()
        {
            m_Instance = null;
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            StreamingModelController.AddObserver -= AddObserver;
            m_HierarchyToolSceneListener?.ResetHierarchyModifiers(true, false);

            StreamingModelController.RemoveStreamModel -= RemoveStreamModel;
            DestroyTransformHandle();
            QueryStarted -= OnQueryStarted;
            InstanceSelectedFromPanel -= OnInstanceSelectedFromPanel;
            RequestTreeViewRefresh -= OnRequestTreeViewRefresh;
            
            m_RefreshDebounceTokenSource?.Cancel();
            m_RefreshDebounceTokenSource?.Dispose();
            m_RefreshDebounceTokenSource = null;
            if (m_TransformGizmo != null)
            {
                UnsubscribeGizmoHandlers(m_TransformGizmo);
                Destroy(m_TransformGizmo);
            }
#if !VR_MODE
            InteractionController.UnsubscribeTap(this);
#endif
        }

        public static void ResetVisibility(bool resetHighlighted)
        {
            VisibilityReset?.Invoke(resetHighlighted);
            m_Instance?.m_HierarchyToolUIController?.RefreshTreeViewItems();
        }

        private void OnNetworkStatusChanged(bool connected)
        {
            if (!connected && !NetworkDetector.RequestedOfflineMode) return;
            _ = UpdateTreeViewItems();
        }
        
        private CancellationTokenSource m_RefreshDebounceTokenSource;
        
        private void OnRequestTreeViewRefresh()
        {
            // Debounce refresh requests - cancel previous pending refresh and start a new one
            m_RefreshDebounceTokenSource?.Cancel();
            m_RefreshDebounceTokenSource?.Dispose();
            m_RefreshDebounceTokenSource = new CancellationTokenSource();
            
            // Wait a bit to batch multiple refresh requests, then refresh
            _ = DebouncedRefreshAsync(m_RefreshDebounceTokenSource.Token);
        }
        
        private async Task DebouncedRefreshAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Wait a bit to batch multiple refresh requests together
                await Task.Delay(100, cancellationToken);
                
                // Refresh tree view when new hierarchy data is available
                await UpdateTreeViewItems();
            }
            catch (OperationCanceledException)
            {
                // Refresh was canceled (new refresh requested), ignore
            }
        }

        private void FindSelectedInstance()
        {
            if (m_HierarchyToolSceneListener == null) return;
            if (m_HierarchyToolSceneListener.SelectedInstanceID == InstanceId.None || m_HierarchyToolSceneListener.SelectedModelID == default)
            {
                return;
            }
            QueryData(m_HierarchyToolSceneListener.SelectedModelID, m_HierarchyToolSceneListener.SelectedInstanceID, true);
        }

        // This function handles the selection of an instance from the panel.
        // It resets the highlight modifier and checks if the selected instance has children.
        // If the instance has no children, it updates the highlight modifier with the instance's ID.
        // If the instance has children, it queries the mesh data asynchronously and updates the highlight modifier with the IDs of the child instances.
        private void OnInstanceSelectedFromPanel(InstanceData data)
        {
            m_HierarchyToolSceneListener.ResetHighlight();
            
            if (data == null || data.Instance == null)
            {
                MetadataForSelection?.Invoke(null);
                return;
            }

            QueryData(data.StreamModel.Id, data.Instance.Id, false);
            
            if (!data.Instance.HasChildren)
            {
                InstanceSelected?.Invoke(data.StreamModel.Id, data.Instance.Id);
                return;
            }
            DestroyTransformHandle();
        }

        private void OnQueryStarted(int id, InstanceData data)
        {
            if(id < -1 || data == null) return;
            
            var repository = data.Repository;
            var instance = data.Instance.Id;
            
            _ = GetChildData(instance, repository);
            
            async Task GetChildData(InstanceId instanceId, IMetadataRepository repository)
            {
                try
                {
                    var queryTokenSource = new CancellationTokenSource();
                    var children = await m_HierarchyToolSceneListener.QueryHierarchyData(instanceId, repository, queryTokenSource.Token);
                    TreeViewItemsUpdated?.Invoke(id, new List<List<InstanceData>>() { children });
                }
                catch (OperationCanceledException)
                {
                    // Query was canceled, ignore
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public async Task UpdateTreeViewItems()
        {
            List<List<InstanceData>> eachRepository = new();

            while (!m_HierarchyToolUIController.Initialized)
            {
                await Task.Yield();
            }
            
            // Wait for repositories to be created if models are still loading
            var maxWaitTime = 30f;
            var elapsed = 0f;
            var streamingModels = TransformController.Instance.GetComponentsInChildren<StreamingModel>(true);
            
            while (elapsed < maxWaitTime)
            {
                var allRepositoriesReady = true;
                foreach (var model in streamingModels)
                {
                    if (!m_StreamModelRepositoriesMapping.ContainsKey(model))
                    {
                        allRepositoriesReady = false;
                        break;
                    }
                }
                
                if (allRepositoriesReady && streamingModels.Length == m_StreamModelRepositoriesMapping.Count)
                {
                    break;
                }
                
                await Task.Yield();
                elapsed += Time.deltaTime;
            }

            // Wait for early loading to complete (if in progress)
            // This ensures all hierarchies are loaded in parallel before refreshing
            await m_HierarchyToolSceneListener.WaitForEarlyLoadingToComplete();
            
            // Use a separate cancellation token for the initial load
            var loadTokenSource = new CancellationTokenSource();
            var loadToken = loadTokenSource.Token;
            
            // Create a snapshot of keys to avoid collection modification during enumeration
            StreamingModel[] keysSnapshot;
            lock (m_StreamModelRepositoriesMapping)
            {
                keysSnapshot = m_StreamModelRepositoriesMapping.Keys.ToArray();
            }
            
            // Load all hierarchies in parallel using Task.WhenAll
            var loadTasks = new List<Task<(StreamingModel model, List<InstanceData> data)>>();
            
            foreach (var key in keysSnapshot)
            {
                // Check if key still exists (might have been removed)
                if (!m_StreamModelRepositoriesMapping.TryGetValue(key, out var repository))
                {
                    continue;
                }
                
                // Create a task for each model's hierarchy loading
                var loadTask = LoadHierarchyForModel(key, repository, loadToken);
                loadTasks.Add(loadTask);
            }
            
            // Wait for all hierarchy loads to complete in parallel
            var results = await Task.WhenAll(loadTasks);
            
            await Task.Yield();
            
            // Sort results by original layout order from LayoutJson snapshot
            // Use the snapshot because LayoutJson.LayoutModels gets modified (models removed) as they're added
            var originalLayoutOrder = m_HierarchyToolSceneListener.GetOriginalLayoutOrder();
            
            var sortedResults = results
                .Where(r => r.data != null)
                .OrderBy(r => 
                {
                    // Get the layout order from the original layout snapshot if available
                    if (originalLayoutOrder is { Count: > 0 } && r.model != null)
                    {
                        // Find the model's position in the original layout by matching assetID and instanceNumber
                        var layoutIndex = originalLayoutOrder
                            .FindIndex(layoutModel => 
                                string.Equals(layoutModel.assetID, r.model.AssetId, StringComparison.OrdinalIgnoreCase) &&
                                layoutModel.instanceNumber == r.model.InstanceNumber);
                        
                        if (layoutIndex >= 0)
                        {
                            return layoutIndex;
                        }
                        
                        // Fallback: try matching by gameObjectName if assetID/instanceNumber match fails
                        layoutIndex = originalLayoutOrder
                            .FindIndex(layoutModel => 
                                string.Equals(layoutModel.gameObjectName, r.model.gameObject.name, StringComparison.OrdinalIgnoreCase));
                        
                        if (layoutIndex >= 0)
                        {
                            return layoutIndex;
                        }
                    }
                    
                    // Fallback to sibling index if layout order not available
                    if (r.model != null && r.model.transform != null && r.model.transform.parent != null)
                    {
                        return r.model.transform.GetSiblingIndex() - 10000; // Offset to put before layout-ordered items
                    }
                    
                    return int.MaxValue; // Put models without parent at the end
                })
                .ToList();
            
            // Process results in sorted order
            foreach (var (model, data) in sortedResults)
            {
                if (data != null)
                {
                    eachRepository.Add(data);
                }
            }
            
            TreeViewItemsUpdated?.Invoke(-1, eachRepository);
        }
        
        private async Task<(StreamingModel model, List<InstanceData> data)> LoadHierarchyForModel(StreamingModel model, IMetadataRepository repository, CancellationToken cancellationToken)
        {
            try
            {
                if (NetworkDetector.RequestedOfflineMode)
                {
                    return (model, new List<InstanceData>() { new InstanceData(null, model, null) });
                }
                else
                {
                    // Check if hierarchy data was already loaded early (before geometry)
                    if (m_HierarchyToolSceneListener.TryGetCachedHierarchyData(model, out var cachedData))
                    {
                        return (model, cachedData);
                    }
                    else if (repository != null)
                    {
                        var data = await m_HierarchyToolSceneListener.QueryHierarchyData(InstanceId.None, repository, cancellationToken);
                        return (model, data);
                    }
                    else
                    {
                        return (model, new List<InstanceData>() { new InstanceData(null, model, null) });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return (model, null);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return (model, null);
            }
        }
        
        #if !VR_MODE
        private void OnSelectActionInvoked(Vector3 position)
        {
            m_HierarchyToolSceneListener.ResetHighlight();
            if (m_StreamingModelController.ActiveCamera == null)
            {
                return;
            }
            Ray ray = m_StreamingModelController.ActiveCamera.ScreenPointToRay(position);
            RaycastStreamingModel(ray);
        }
        #endif
        
        #if VR_MODE
        private void OnSingleActivateInvoked(Ray ray, int controllerInstanceID)
        {
            if (m_StreamingModelController == null) return;
            RaycastStreamingModel(ray, true);
        }
        #endif
        
        // This function performs a raycast on the streaming model to detect instances.
        // It uses asynchronous tasks to handle the raycast and query data from the metadata repository.
        // If an instance is detected, it queries additional data and updates the highlight modifier.
        private void RaycastStreamingModel(Ray ray, bool checkUIWorldSpace = false)
        {
            if(m_StreamingModelController == null) return;
           _ = Raycast();
            return;

            async Task Raycast()
            {
                InstanceSelected?.Invoke(default, InstanceId.None);
                var raycastResult = await m_StreamingModelController.Stage.RaycastAsync((DoubleRay) ray, m_StreamingModelController.ActiveCamera.farClipPlane, RaycastOptions.ExcludeHiddenInstances | RaycastOptions.ExcludeNormalFromResult);
                if (checkUIWorldSpace && WorldSpaceUiBlocksRay(ray, m_StreamingModelController.ActiveCamera.farClipPlane,
                        raycastResult.InstanceId != InstanceId.None, raycastResult.Point.ToVector3()))
                {
                    return;
                }
                if (raycastResult.InstanceId == InstanceId.None)
                {
                    m_HierarchyToolSceneListener.ResetHighlight();
                    if (Physics.Raycast(ray, m_StreamingModelController.ActiveCamera.farClipPlane,
                            m_RuntimeHandleLayerMask)) return;
                    m_HierarchyToolUIController?.ClearTransformInspector();
                    DestroyTransformHandle();
                    return;
                }

                m_HierarchyToolSceneListener.ResetHighlight();
                if (NetworkDetector.RequestedOfflineMode)
                {
                    m_HierarchyToolSceneListener.HighlightInstance(raycastResult.ModelId, raycastResult.InstanceId);
                    QueryAbort?.Invoke();
                    return;
                }
                QueryStarted.Invoke(-2, null);
                DestroyTransformHandle();
                QueryData(raycastResult.ModelId, raycastResult.InstanceId, true);
            }
        }
        
        private async void QueryData(ModelStreamId modelId, InstanceId id, bool invokeEvent)
        {
            if(m_StreamModelRepositoriesMapping.Keys.All(x => x.ModelStream.Id != modelId))
            {
                return;
            }
            
            var repository = m_StreamModelRepositoriesMapping.FirstOrDefault(x => x.Key.ModelStream.Id == modelId).Value;

            if (repository == null)
            {
                MetadataForSelection?.Invoke(null);
                m_HierarchyToolSceneListener.HighlightInstance(modelId, id);
                QueryAbort?.Invoke();
                return;
            }
            
            var queryTokenSource = new CancellationTokenSource();
            var token = queryTokenSource.Token;
            
            try
            {
                var firstQuery = await repository
                    .Query()
                    .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.AncestorIds | OptionalData.Fields.Name | OptionalData.Fields.Id | OptionalData.Fields.HasChildren))
                    .WhereInstanceEquals(id)
                    .GetFirstOrDefaultAsync(token);
                if(firstQuery == null) return;

                m_HierarchyToolSceneListener.HighlightInstance(modelId, id);
                
                InstanceSelected?.Invoke(modelId, id);

                _ = QueryMetadataForSelection(repository, id, token);

                if (!invokeEvent) return;
                {
                    Dictionary<InstanceId, List<InstanceData>> children = new();
                
                    var streamingModel = m_StreamModelRepositoriesMapping.Keys.First(x => x.ModelStream.Id == modelId);

                    var ancestorIds = firstQuery.AncestorIds.ToList();
                    var tasks = ancestorIds.Select(async ancestorId =>
                    {
                        if (token.IsCancellationRequested) return (ancestorId, new List<InstanceData>());
                        try
                        {
                            var query = repository
                                .Query()
                                .Select(MetadataPathCollection.All)
                                .WhereHasAncestor(ancestorId, 0)
                                .WithCancellation(token);

                            var list = new List<InstanceData>();
                            await foreach (var each in query)
                            {
                                list.Add(new InstanceData(each, streamingModel, repository));
                            }
                            return (ancestorId, list);
                        }
                        catch (OperationCanceledException)
                        {
                            return (ancestorId, new List<InstanceData>());
                        }
                    }).ToList();

                    var results = await Task.WhenAll(tasks);
                    foreach (var result in results)
                    {
                        var ancestorId = result.Item1;
                        var list = result.Item2;
                        if (!children.TryAdd(ancestorId, list))
                        {
                            children[ancestorId].AddRange(list);
                        }
                    }
                    
                    InstanceSelectedOnModel?.Invoke(modelId, firstQuery, children);
                }
            }
            catch (OperationCanceledException)
            {
                // Query was canceled, ignore
            }
            catch (InvalidOperationException ex)
            {
                if (!ex.Message.Contains("Hierarchy was modified."))
                {
                    Debug.LogException(ex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // Fetches full metadata for the selected instance and pushes it to the Metadata tab.
        // Ported from the standalone Metadata tool so the tab works while the Hierarchy tool is open.
        private async Task QueryMetadataForSelection(IMetadataRepository repository, InstanceId id, CancellationToken token)
        {
            if (repository == null)
            {
                MetadataForSelection?.Invoke(null);
                return;
            }

            try
            {
                var firstQuery = await repository
                    .Query()
                    .Select(MetadataPathCollection.All)
                    .WhereInstanceEquals(id)
                    .WhereHasAncestor(InstanceId.None, int.MaxValue)
                    .GetFirstOrDefaultAsync(token);

                if (firstQuery == null)
                {
                    MetadataForSelection?.Invoke(null);
                    return;
                }

                var found = new List<MetadataInstance>();

                if (firstQuery.Properties.Count == 0 && firstQuery.AncestorIds is { Count: > 0 })
                {
                    // Selected instance has no properties of its own - fall back to its parent.
                    var parentId = firstQuery.AncestorIds[^1];
                    var secondQuery = await repository
                        .Query()
                        .Select(MetadataPathCollection.All)
                        .WhereInstanceEquals(parentId)
                        .GetFirstOrDefaultAsync(token);
                    if (secondQuery != null)
                    {
                        found.Add(secondQuery);
                    }
                }
                else
                {
                    found.Add(firstQuery);
                }

                MetadataForSelection?.Invoke(found);
            }
            catch (OperationCanceledException)
            {
                // Query was canceled, ignore.
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void RemoveStreamModel(StreamingModel obj)
        {
            RemoveTransformHandle();
        }
        
        private void RemoveTransformHandle()
        {
            if (m_TransformGizmo == null) return;
            m_TransformGizmo.SetTarget(null);
#if ENABLE_MULTIPLAY
            SyncModelTransform.RuntimeTransformHandleCreated?.Invoke(false);
#endif
        }
        
#if !VR_MODE
        private void OnSelectedHandle(bool selected)
        {
            NavigationController.PauseCameraControl?.Invoke(selected);
        }
#endif
        
        private void AddObserver(Camera obj)
        {
            if (m_TransformGizmo == null) return;
            UnsubscribeGizmoHandlers(m_TransformGizmo);

            var target = m_TransformGizmo.mainTargetRoot;
            var type = m_TransformGizmo.transformType;
            Destroy(m_TransformGizmo);
            m_TransformGizmo = null;
            StartCoroutine(WaitForEndOfFrameToRefresh());

            IEnumerator WaitForEndOfFrameToRefresh()
            {
                yield return new WaitForEndOfFrame();
                CreateTransformHandle(target, type);
            }
        }

        public void UpdateTransformHandlePosition()
        {
            m_TransformGizmo?.SetPivotPoint();
        }
        
        public void CreateTransformHandle(Transform target, TransformType type)
        {
            m_TransformGizmo?.SetTarget(null);
            if (m_TransformGizmo == null)
            {
                var camera = FindFirstObjectByType<StreamingModelController>().ActiveCamera;
                m_TransformGizmo ??= TransformGizmo.CreateHandle(camera, target)
                    .WithActions(m_PointerMove.action, m_PointerPress.action)
                    .WithLayer(m_RuntimeHandleLayerMask);
            }
            else
            {
                m_TransformGizmo.SetTarget(target);
            }
            m_TransformGizmo.snapOverride = true;
            m_TransformGizmo.SetType(type);
            m_TransformGizmo.movementSnap = GridViewManager.GetGridUnit();
            m_TransformGizmo.rotationSnap = GridViewManager.GetGridUnit();
            SubscribeGizmoHandlers(m_TransformGizmo);
            m_TransformGizmo.SetTarget(target);
#if !VR_MODE
            m_PointerPress.action.Enable();
            m_PointerClick.action.Enable();
            m_PointerMove.action.Enable();
#else
            VRInteractionController.SubscribeControllerMoved(this, OnVRControllerMoves);
            VRInteractionController.SubscribePressActivate(this, OnVRControllerPress);
#endif
#if ENABLE_MULTIPLAY
            SyncModelTransform.RuntimeTransformHandleCreated?.Invoke(true);
#endif
        }

#if VR_MODE
        private void OnVRControllerPress(Ray ray, bool press, int rayID)
        {
            m_TransformGizmo?.SelectAction?.Invoke(press, ray, rayID);
        }
        
        private void OnVRControllerMoves(Ray ray, int rayID)
        {
            m_TransformGizmo?.InputMoveAction?.Invoke(ray, rayID);
        }
#endif


        public void SwitchGizmoMode(TransformType type)
        {
            if(m_TransformGizmo == null) return;
            var target = m_TransformGizmo.mainTargetRoot;
            CreateTransformHandle(target, type);
        }

        public void DestroyTransformHandle()
        {
            RemoveTransformHandle();
#if !VR_MODE
            m_PointerPress.action.Disable();
            m_PointerClick.action.Disable();
            m_PointerMove.action.Disable();
#else
            VRInteractionController.UnsubscribeControllerMoved(this);
            VRInteractionController.UnsubscribePressActivate(this);
#endif
        }

        public override void OnToolOpened()
        {
            ToolOpened?.Invoke();
            #if VR_MODE
            StartCoroutine(WaitForSetup());
            return;

            IEnumerator WaitForSetup()
            {
                yield return new WaitForSeconds(1);
                VRInteractionController.SubscribeSingleActivate(this, OnSingleActivateInvoked);
            }
            
            #else
            InteractionController.SubscribeTap(this, OnSelectActionInvoked);
            #endif
        }

        public override void OnToolClosed()
        {
            ToolClosed?.Invoke();
            DestroyTransformHandle();
            #if VR_MODE
            VRInteractionController.UnsubscribeSingleActivate(this);
            #else
            InteractionController.UnsubscribeTap(this);
            #endif
        }
    }
}
