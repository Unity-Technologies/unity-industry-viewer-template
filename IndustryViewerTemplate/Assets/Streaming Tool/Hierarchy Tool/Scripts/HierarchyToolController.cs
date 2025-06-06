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
using Unity.Industry.Viewer.Streaming.Metadata;
using UnityEngine.EventSystems;
using RuntimeGizmos;
using Unity.Industry.Viewer.Shared;
using UnityEngine.InputSystem;
using TransformType = RuntimeGizmos.TransformType;
#if VR_MODE
using Unity.Industry.Viewer.Navigation.VR;
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
    public class HierarchyToolController : StreamToolControllerBase
    {
        public static Action<int, List<List<InstanceData>>> TreeViewItemsUpdated;
        public static Action<int, InstanceData> QueryStarted;
        public static Action QueryAbort;
        public static Action<InstanceData, bool> InstanceVisibilityChanged;
        public static Action<InstanceData> InstanceSelectedFromPanel;
        public static Action<ModelStreamId, MetadataInstance, Dictionary<InstanceId, List<InstanceData>>> InstanceSelectedOnModel;
        public static Action<InstanceData, bool> UpdateToggleUI;
        
#if ENABLE_MULTIPLAY
        public static Action<string, bool> LockModel;
#endif

        private int m_LastInstanceId;
        
        private HierarchyToolSceneListener m_HierarchyToolSceneListener;
        
        private Dictionary<StreamingModel, IMetadataRepository> m_StreamModelRepositoriesMapping => m_HierarchyToolSceneListener.StreamModelRepositoriesMapping;

        private StreamingModelController m_StreamingModelController => m_HierarchyToolSceneListener.StreamingModelController;
        
        public VisibilityModifier VisibilityModifier => m_HierarchyToolSceneListener.VisibilityModifier;
        public HighlightModifier HighlightModifier => m_HierarchyToolSceneListener.HighlightModifier;
        
        [SerializeField] private InputActionProperty m_PointerClick;
        [SerializeField] private InputActionProperty m_PointerPress;
        [SerializeField] private InputActionProperty m_PointerMove;

        [SerializeField] private LayerMask m_RuntimeHandleLayerMask;
        
        private HierarchyToolUIController m_HierarchyToolUIController;
        
        public GridViewManager GridViewManager { get; private set;}
        
        private TransformGizmo m_TransformGizmo;
        
        private CancellationTokenSource m_QueryTokenSource;
        
        private async void Start()
        {
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            StreamingModelController.RemoveStreamModel += RemoveStreamModel;
            StreamingModelController.AddObserver += AddObserver;
            m_HierarchyToolSceneListener = FindAnyObjectByType<HierarchyToolSceneListener>();
            QueryStarted += OnQueryStarted;
            InstanceSelectedFromPanel += OnInstanceSelectedFromPanel;
            
            GridViewManager = FindFirstObjectByType<GridViewManager>();
            
            ToolOpened?.Invoke(); 
            await UpdateTreeViewItems();
            if(NetworkDetector.RequestedOfflineMode) return;
            FindSelectedInstance();
        }

        private void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            StreamingModelController.AddObserver -= AddObserver;
            HighlightModifier?.Reset();
            VisibilityModifier?.Reset();
            StreamingModelController.RemoveStreamModel -= RemoveStreamModel;
            DestroyTransformHandle();
            QueryStarted -= OnQueryStarted;
            InstanceSelectedFromPanel -= OnInstanceSelectedFromPanel;
            if (m_TransformGizmo != null)
            {
                m_TransformGizmo.OnHandlerSelected -= OnCheckForSelectedAxis;
                m_TransformGizmo.OnHandlerReleased -= OnCheckForReleaseAxis;
                Destroy(m_TransformGizmo);
            }
#if !VR_MODE
            InteractionController.UnsubscribeTap(this);
#endif
        }
        
        private void OnNetworkStatusChanged(bool obj)
        {
            if(!obj && !NetworkDetector.RequestedOfflineMode) return;
            VisibilityModifier?.Reset();
            HighlightModifier?.Reset();
            foreach (Transform child in TransformController.Instance.transform)
            {
                child.gameObject.SetActive(true);
            }
            _ = UpdateTreeViewItems();
        }
        
        private void FindSelectedInstance()
        {
            var sceneListeners = FindAnyObjectByType<MetadataSceneListener>();
            if(sceneListeners == null) return;
            if (sceneListeners.SelectedInstanceID == InstanceId.None || sceneListeners.SelectedModelID == default)
            {
                return;
            }
            _ = QueryData(sceneListeners.SelectedModelID, sceneListeners.SelectedInstanceID);
        }

        // This function handles the selection of an instance from the panel.
        // It resets the highlight modifier and checks if the selected instance has children.
        // If the instance has no children, it updates the highlight modifier with the instance's ID.
        // If the instance has children, it queries the mesh data asynchronously and updates the highlight modifier with the IDs of the child instances.
        private void OnInstanceSelectedFromPanel(InstanceData data)
        {
            if (data == null || data.Instance == null)
            {
                return;
            }
            HighlightModifier.Reset();
            
            m_QueryTokenSource?.Cancel();
            m_QueryTokenSource?.Dispose();
            m_QueryTokenSource = null;
            
            if (!data.Instance.HasChildren)
            {
                HighlightModifier.Update(data.StreamModel.Id, data.Instance.Id);
                MetadataToolController.InstanceSelected?.Invoke(data.StreamModel.Id, data.Instance.Id);
                return;
            }
            DestroyTransformHandle();
            _ = QueryMeshData();
            return;

            async Task QueryMeshData()
            {
                var repository = data.Repository;
                var model = data.StreamModel;
                var instanceId = data.Instance.Id;
                
                var query = repository
                    .Query()
                    .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.Id | OptionalData.Fields.HasChildren | OptionalData.Fields.Name | OptionalData.Fields.AncestorIds))
                    .WhereHasAncestor(instanceId, int.MaxValue)
                    .WithCancellation(CancellationToken.None);
                
                HashSet<InstanceId> childrenIds = new();
                
                await foreach (var each in query)
                {
                    if(each.HasChildren) continue;
                    childrenIds.Add(each.Id);
                }
                
                HighlightModifier.UpdateList(model.Id, childrenIds, true);
            }
        }

        private void OnQueryStarted(int id, InstanceData data)
        {
            if(id < -1 || data == null) return;
            
            var repository = data.Repository;
            var instance = data.Instance.Id;
            
            _ = GetChildData(instance, repository);
            
            async Task GetChildData(InstanceId instanceId, IMetadataRepository repository)
            {
                var children = await m_HierarchyToolSceneListener.QueryHierarchyData(instanceId, repository);
                TreeViewItemsUpdated?.Invoke(id, new List<List<InstanceData>>() { children });
            }
        }

        public async Task UpdateTreeViewItems()
        {
            List<List<InstanceData>> eachRepository = new();
            
            foreach (var key in m_StreamModelRepositoriesMapping.Keys)
            {
                var repository = m_StreamModelRepositoriesMapping[key];
                List<InstanceData> data = null;
                if (NetworkDetector.RequestedOfflineMode)
                {
                    data = new List<InstanceData>() { new InstanceData(null, key, null) };
                }
                else
                {
                    if (repository != null)
                    {
                        data = await m_HierarchyToolSceneListener.QueryHierarchyData(InstanceId.None, repository);
                    }
                    else
                    {
                        data = new List<InstanceData>() { new InstanceData(null, key, null) };
                    }
                }


                if (data == null)
                {
                    continue;
                }
                
                eachRepository.Add(data);
            }
            TreeViewItemsUpdated?.Invoke(-1, eachRepository);
        }
        
        #if !VR_MODE
        private void OnSelectActionInvoked(Vector3 position)
        {
            HighlightModifier.Reset();
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
            HighlightModifier.Reset();
            if (m_StreamingModelController == null) return;
            RaycastStreamingModel(ray);
        }
        #endif
        
        // This function performs a raycast on the streaming model to detect instances.
        // It uses asynchronous tasks to handle the raycast and query data from the metadata repository.
        // If an instance is detected, it queries additional data and updates the highlight modifier.
        private void RaycastStreamingModel(Ray ray)
        {
            if(m_StreamingModelController == null) return;
            if(IsPointerOverUI()) return;
            _ = Raycast();
            return;

            async Task Raycast()
            {
                MetadataToolController.InstanceSelected?.Invoke(default, InstanceId.None);
                
                m_QueryTokenSource?.Cancel();
                m_QueryTokenSource?.Dispose();
                m_QueryTokenSource = null;
                
                var raycastResult = await m_StreamingModelController.Stage.RaycastAsync((DoubleRay) ray, m_StreamingModelController.ActiveCamera.farClipPlane, RaycastOptions.ExcludeHiddenInstances);
                if (raycastResult.InstanceId == InstanceId.None)
                {
                    if (Physics.Raycast(ray, m_StreamingModelController.ActiveCamera.farClipPlane,
                            m_RuntimeHandleLayerMask)) return;
                    m_HierarchyToolUIController ??= gameObject.GetComponent<HierarchyToolUIController>();
                    m_HierarchyToolUIController?.ClearTransformInspector();
                    return;
                }
                HighlightModifier.Reset();
                if (NetworkDetector.RequestedOfflineMode)
                {
                    HighlightModifier.Update(raycastResult.ModelId, raycastResult.InstanceId);
                    QueryAbort?.Invoke();
                    return;
                }
                QueryStarted.Invoke(-2, null);
                DestroyTransformHandle();
                await QueryData(raycastResult.ModelId, raycastResult.InstanceId);
            }
        }
        
        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
        }
        
        private async Task QueryData(ModelStreamId modelId, InstanceId id)
        {
            if(m_StreamModelRepositoriesMapping.Keys.All(x => x.ModelStream.Id != modelId))
            {
                return;
            }
            
            var repository = m_StreamModelRepositoriesMapping.FirstOrDefault(x => x.Key.ModelStream.Id == modelId).Value;

            if (repository == null)
            {
                HighlightModifier.Update(modelId, id);
                QueryAbort?.Invoke();
                return;
            }
            
            m_QueryTokenSource?.Cancel();
            m_QueryTokenSource?.Dispose();
            m_QueryTokenSource = null;
            
            m_QueryTokenSource = new CancellationTokenSource();
            
            var firstQuery = await repository
                .Query()
                .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.AncestorIds | OptionalData.Fields.Name | OptionalData.Fields.Id | OptionalData.Fields.HasChildren))
                .WhereInstanceEquals(id)
                //.WhereHasAncestor(InstanceId.None, int.MaxValue)
                .GetFirstOrDefaultAsync(m_QueryTokenSource.Token);
            
            if(firstQuery == null) return;
            HighlightModifier.Update(modelId, id);
            MetadataToolController.InstanceSelected?.Invoke(modelId, id);
            
            Dictionary<InstanceId, List<InstanceData>> children = new();
            
            //Debug.Log($"Select {firstQuery.Id}, Name: {firstQuery.Name}");
            
            foreach (var ancestorId in firstQuery.AncestorIds)
            {
                if(m_QueryTokenSource.IsCancellationRequested) return;
                //Debug.Log("Ancestor: " + ancestorId);
                var moreQuery = repository
                    .Query()
                    .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.Id | OptionalData.Fields.Name | OptionalData.Fields.HasChildren | OptionalData.Fields.AncestorIds))
                    .WhereHasAncestor(ancestorId, 0)
                    .WithCancellation(m_QueryTokenSource.Token);

                var streamingModel = m_StreamModelRepositoriesMapping.Keys.First(x => x.ModelStream.Id == modelId);
                
                await foreach (var each in moreQuery)
                {
                    //Debug.Log($"Child: {each.Id}, Name: {each.Name}, has children {each.HasChildren} child of {ancestorId}");
                    var newInstanceData = new InstanceData(each, streamingModel, repository);
                    if (children.ContainsKey(ancestorId))
                    {
                        children[ancestorId].Add(newInstanceData);
                    }
                    else
                    {
                        children.Add(ancestorId, new List<InstanceData> { newInstanceData });
                    }
                }
            }
           
            InstanceSelectedOnModel?.Invoke(modelId, firstQuery, children);
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
            m_TransformGizmo.OnHandlerSelected -= OnCheckForSelectedAxis;
            m_TransformGizmo.OnHandlerReleased -= OnCheckForReleaseAxis;
            
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
            m_TransformGizmo.OnHandlerSelected -= OnCheckForSelectedAxis;
            m_TransformGizmo.OnHandlerReleased -= OnCheckForReleaseAxis;
            
            m_TransformGizmo.OnHandlerSelected += OnCheckForSelectedAxis;
            m_TransformGizmo.OnHandlerReleased += OnCheckForReleaseAxis;
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

        private void OnVRControllerPress(Ray ray, bool press, int rayID)
        {
            m_TransformGizmo?.SelectAction?.Invoke(press, ray, rayID);
        }

#if VR_MODE
        private void OnVRControllerMoves(Ray ray, int rayID)
        {
            m_TransformGizmo?.InputMoveAction?.Invoke(ray, rayID);
        }
#endif

        private void OnCheckForReleaseAxis()
        {
            NavigationController.PauseCameraControl?.Invoke(false);
        }

        private void OnCheckForSelectedAxis()
        {
            NavigationController.PauseCameraControl?.Invoke(true);
        }

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
            VRInteractionController.SubscribeSingleActivate(this, OnSingleActivateInvoked);
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
