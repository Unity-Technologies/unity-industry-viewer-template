using System;
using Unity.Cloud.DataStreaming.Runtime;
using System.Threading;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Cloud.DataStreaming.Metadata;
using Unity.Industry.Viewer.Shared;
using Unity.Cloud.Common;
using System.Collections.Generic;
using Unity.Industry.Viewer.Identity;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Cloud.DataStreaming.Runtime.AssetManager;
using System.Linq;
using RuntimeGizmos;
using UnityEngine.InputSystem;
#if VR_MODE
using Unity.Industry.Viewer.VR;
#endif

namespace Unity.Industry.Viewer.Streaming.Interaction
{
    public struct GeoGameObjectDetails
    {
        public readonly GameObject InSceneGameObject;
        public readonly IModelStream ModelStream;
        public readonly InstanceId InstanceId;
        private Vector3 m_InitialPosition;
        private Quaternion m_InitialRotation;
        private Vector3 m_InitialScale;
        
        public GeoGameObjectDetails(GameObject inSceneGameObject, IModelStream modelStream, InstanceId instanceId)
        {
            InSceneGameObject = inSceneGameObject;
            ModelStream = modelStream;
            InstanceId = instanceId;
            m_InitialPosition = inSceneGameObject.transform.position;
            m_InitialRotation = inSceneGameObject.transform.rotation;
            m_InitialScale = inSceneGameObject.transform.localScale;
        }

        public void ResetTransform()
        {
            InSceneGameObject.transform.position = m_InitialPosition;
            InSceneGameObject.transform.rotation = m_InitialRotation;
            InSceneGameObject.transform.localScale = m_InitialScale;
        }
    }
    
    public class InteractionToolController : StreamToolControllerBase
    {
        public static Action<InstanceId?> InstanceSelected;
        public static Action<GeoGameObjectDetails?> GeoGameObjectCreated;
        public static Action OfflineAssetSelected;
        
        private static readonly int m_CullId = Shader.PropertyToID("_Cull");
        
        [SerializeField]
        private Color highlightColor = new Color(0, 200, 255, 255);
        
        [SerializeField] private InputActionProperty m_PointerClick;
        [SerializeField] private InputActionProperty m_PointerPress;
        [SerializeField] private InputActionProperty m_PointerMove;
        
        [SerializeField]
        private LayerMask m_RuntimeHandleLayerMask;
        
        private ModelStreamId? m_ModelStreamId;
        private InstanceId? m_InstanceId;
        private StreamingModelController m_StreamingModelController;
        
        private IServiceHttpClient m_ServiceHttpClient => IdentityController.GuestMode?
            PlatformServices.ServiceAccountServiceHttpClient : PlatformServices.ServiceHttpClient;
        
        private IServiceHostResolver m_ServiceHostResolver => PlatformServices.ServiceHostResolver;

        private Dictionary<ModelStreamId, IMetadataRepository> m_ModelIDRepositoriesMapping = new();

        private List<GeoGameObjectDetails> m_GeoGameObjectsDetails;
        
        private TransformGizmo m_TransformGizmo;
        
        private async void Start()
        {
            m_StreamingModelController = FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
            ToolOpened?.Invoke();
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            await CreateRepositories();
        }

        private void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            ToolClosed?.Invoke();
            DestroyTransformGizmo();
             if (m_ModelStreamId.HasValue)
            {
                m_StreamingModelController.Stage.HighlightController.ResetHighlight(m_ModelStreamId.Value);
                m_ModelStreamId = null;
            }
            m_InstanceId = null;
            ClearReplacement();
#if VR_MODE
            VRInteractionController.UnsubscribeSingleActivate(this);
#else
            InteractionController.UnsubscribeTap(this);
#endif
        }

        private void OnNetworkStatusChanged(bool isConnected)
        {
            if(isConnected) return;
            if (!m_ModelStreamId.HasValue || !m_InstanceId.HasValue) return;
            m_StreamingModelController.Stage.HighlightController.ResetHighlight(m_ModelStreamId.Value);
            InstanceSelected?.Invoke(InstanceId.None);
            m_ModelStreamId = null;
            m_InstanceId = null;
        }

        private void ClearReplacement()
        {
            if (m_GeoGameObjectsDetails != null)
            {
                foreach (var details in m_GeoGameObjectsDetails)
                {
                    Destroy(details.InSceneGameObject);
                    m_StreamingModelController.Stage.HighlightController.ResetHighlight(details.ModelStream.Id);
                    m_StreamingModelController.Stage.VisibilityController.ResetVisibility(details.ModelStream.Id);
                }
            }
            m_GeoGameObjectsDetails?.Clear();
        }

        private async Task CreateRepositories()
        {
            for (var i = 0; i < TransformController.Instance.transform.childCount; i++)
            {
                if (TransformController.Instance.transform.GetChild(i).TryGetComponent(out StreamingModel streamModel))
                {
                    await NewRepository(streamModel);
                }
            }
        }
        
        private Task NewRepository(StreamingModel streamModel)
        {
            var newFactory = new MetadataRepositoryFactory();
            if(streamModel.Dataset == null) return Task.CompletedTask;
            var metadataRepository = newFactory.Create(streamModel.Dataset, m_ServiceHttpClient, m_ServiceHostResolver);
            m_ModelIDRepositoriesMapping.Add(streamModel.ModelStream.Id, metadataRepository);
            return Task.CompletedTask;
        }
        
#if !VR_MODE
        private void OnSelectActionInvoked(Vector3 position)
        {
            if (m_StreamingModelController.ActiveCamera == null)
            {
                return;
            }
            Ray ray = m_StreamingModelController.ActiveCamera.ScreenPointToRay(position);
            RaycastStreamingModel(ray);
        }
#endif
        
#if VR_MODE
        private void OnSingleActivateActionInvoked(Ray ray, int controllerInstanceID)
        {
            if(m_StreamingModelController == null) return;
            RaycastStreamingModel(ray, true);
        }
#endif
        
        private void DestroyTransformGizmo()
        {
            if (m_TransformGizmo != null)
            {
                if (m_TransformGizmo.mainTargetRoot != null)
                {
                    Renderer[] allRenderers = m_TransformGizmo.mainTargetRoot.GetComponentsInChildren<Renderer>();
                    foreach (var allRenderer in allRenderers)
                    {
                        foreach (var material in allRenderer.materials)
                        {
                            material.SetFloat(m_CullId, 2);
                        }
                    }
                }
                UnsubscribeGizmoHandlers(m_TransformGizmo);
                Destroy(m_TransformGizmo);
            }
#if !VR_MODE
            m_PointerPress.action.Disable();
            m_PointerClick.action.Disable();
            m_PointerMove.action.Disable();
#else
            VRInteractionController.UnsubscribeControllerMoved(this);
            VRInteractionController.UnsubscribePressActivate(this);
#endif
            m_TransformGizmo = null;
        }
        
        private void RaycastStreamingModel(Ray ray, bool checkWorldSpaceUI = false)
        {
            if (NetworkDetector.IsOffline)
            {
                // In offline mode, we don't want to allow interaction with the streaming model, so we exit early.
                return;
            }
            if(m_StreamingModelController == null) return;
            _ = Raycast();
            return;

            async Task Raycast()
            {
                try
                {
                    var raycastResult = await m_StreamingModelController.Stage.RaycastAsync((DoubleRay) ray, m_StreamingModelController.ActiveCamera.farClipPlane, RaycastOptions.ExcludeHiddenInstances | RaycastOptions.ExcludeNormalFromResult);
                    if (raycastResult.InstanceId == InstanceId.None)
                    {
                        if (!checkWorldSpaceUI || !Physics.Raycast(ray, out _, m_StreamingModelController.ActiveCamera.farClipPlane,
                                LayerMask.GetMask("UI")))
                        {
                            ResetAll();
                            return;
                        }
                    }

                    if (NetworkDetector.RequestedOfflineMode)
                    {
                        return;
                    }

                    if (checkWorldSpaceUI && WorldSpaceUiBlocksRay(ray, m_StreamingModelController.ActiveCamera.farClipPlane,
                            raycastResult.InstanceId != InstanceId.None, raycastResult.Point.ToVector3()))
                    {
                        return;
                    }
                    ResetAll();
                    ModelStreamId modelStreamId = raycastResult.ModelId;
                    await QueryMetadata(modelStreamId, raycastResult.InstanceId);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            void ResetAll()
            {
                InstanceSelected?.Invoke(InstanceId.None);
                DestroyTransformGizmo();
                m_InstanceId = null;
                if (m_ModelStreamId.HasValue)
                {
                    m_StreamingModelController.Stage.HighlightController.ResetHighlight(m_ModelStreamId.Value);
                    m_ModelStreamId = null;
                }
            }
        }
        
        private async Task QueryMetadata(ModelStreamId modelID, InstanceId id)
        {
            var streamingModels = TransformController.Instance.GetComponentsInChildren<StreamingModel>();
            if (streamingModels.All(x => x.ModelStream.Id != modelID))
            {
                InstanceSelected?.Invoke(InstanceId.None);
                return;
            }
            
            if (!m_ModelIDRepositoriesMapping.TryGetValue(modelID, out var repository))
            {
                OfflineAssetSelected?.Invoke();
                return;
            }

            var firstQuery = await repository
                .Query()
                .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.Id))
                .WhereInstanceEquals(id)
                .GetFirstOrDefaultAsync(CancellationToken.None);

            if(firstQuery == null) return;
            
            m_ModelStreamId = modelID;
            m_InstanceId = id;
            m_StreamingModelController.Stage.HighlightController.SetHighlight(m_ModelStreamId.Value, highlightColor, new []{id});
                
            InstanceSelected?.Invoke(id);
        }

        public void OnItemSelected(Transform target, TransformType transformType, TransformSpace transformSpace)
        {
            foreach (var mGeoGameObjectsDetail in m_GeoGameObjectsDetails)
            {
                m_StreamingModelController.Stage.HighlightController.ResetHighlight(mGeoGameObjectsDetail.ModelStream.Id);
            }
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
            m_TransformGizmo.snapOverride = false;
            m_TransformGizmo.SetType(transformType);
            m_TransformGizmo.SetSpace(transformSpace);
            SubscribeGizmoHandlers(m_TransformGizmo);
            m_TransformGizmo.SetTarget(target);
            
            Renderer[] allRenderers = target.GetComponentsInChildren<Renderer>();
            foreach (var allRenderer in allRenderers)
            {
                foreach (var material in allRenderer.materials)
                {
                    material.SetFloat(m_CullId, 0);
                }
            }
#if !VR_MODE
            m_PointerPress.action.Enable();
            m_PointerClick.action.Enable();
            m_PointerMove.action.Enable();
#else
            VRInteractionController.SubscribeControllerMoved(this, OnVRControllerMoves);
            VRInteractionController.SubscribePressActivate(this, OnVRControllerPress);
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
        

        public void RemoveGeoGameObject(GeoGameObjectDetails details, out bool isCurrentSelected)
        {
            if (!m_GeoGameObjectsDetails.Any(x => x.InSceneGameObject == details.InSceneGameObject
                                                  && x.InstanceId == details.InstanceId &&
                                                  x.ModelStream.Id == details.ModelStream.Id))
            {
                isCurrentSelected = false;
                return;
            }
            m_GeoGameObjectsDetails.Remove(details);
            if(m_TransformGizmo != null && m_TransformGizmo.mainTargetRoot == details.InSceneGameObject.transform)
            {
                isCurrentSelected = true;
                DestroyTransformGizmo();
            }
            Destroy(details.InSceneGameObject);
            var currentHiddenInstances = m_GeoGameObjectsDetails.Where(x => x.ModelStream.Id == details.ModelStream.Id).Select(x => x.InstanceId).ToList();
            m_StreamingModelController.Stage.VisibilityController.SetVisibility(details.ModelStream.Id, currentHiddenInstances, Array.Empty<InstanceId>());
            isCurrentSelected = false;
        }

        public void MakeItInteractable()
        {
            if (!m_ModelStreamId.HasValue || !m_InstanceId.HasValue)
            {
                // Nothing to instantiate: clear the loading panel the UI showed on click.
                GeoGameObjectCreated?.Invoke(null);
                return;
            }
            InstantiateGameObject();

            async void InstantiateGameObject()
            {
                // Every exit must raise GeoGameObjectCreated (null on failure) because the UI only
                // hides its loading panel from that handler; a silent return leaves it stuck.
                var metadataRepository = m_ModelIDRepositoriesMapping[m_ModelStreamId.Value];
                var metadata = await metadataRepository.Query().WhereInstanceEquals(m_InstanceId.Value)
                    .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.Name | OptionalData.Fields.Geometry))
                    .GetFirstOrDefaultAsync(CancellationToken.None);
                if (metadata == null || metadata.Geometry == null)
                {
                    GeoGameObjectCreated?.Invoke(null);
                    return;
                }
                var geometryData = metadata.Geometry;
                if(!geometryData.HasValue)
                {
                    GeoGameObjectCreated?.Invoke(null);
                    return;
                }
                var geometry = geometryData.Value;
                StreamingModel[] streamingModels = TransformController.Instance.GetComponentsInChildren<StreamingModel>();
                StreamingModel streamModel = streamingModels.FirstOrDefault(x => x.ModelStream.Id == m_ModelStreamId.Value);
                if (streamModel == null)
                {
                    GeoGameObjectCreated?.Invoke(null);
                    return;
                }
                try
                {
                    var geometryObject =
                        await streamModel.ModelStream.LoadGeometryAsync(geometry.Payload.Value, CancellationToken.None);
                    var newInSceneGameObject = await geometryObject.InstantiateAsync(metadata.Geometry.Value.WorldTransform.Value, CancellationToken.None);
                    newInSceneGameObject.SetActive(true);
                    newInSceneGameObject.name = metadata.Name;
                    m_StreamingModelController.Stage.HighlightController.ResetHighlight(m_ModelStreamId.Value);
                    
                    var currentHiddenInstances = m_GeoGameObjectsDetails == null? new List<InstanceId>() : m_GeoGameObjectsDetails.Where(x => x.ModelStream.Id == m_ModelStreamId.Value).Select(x => x.InstanceId).ToList();
                    currentHiddenInstances.Add(m_InstanceId.Value);
                    m_StreamingModelController.Stage.VisibilityController.SetVisibility(m_ModelStreamId.Value, currentHiddenInstances, Array.Empty<InstanceId>());
                    m_GeoGameObjectsDetails ??= new List<GeoGameObjectDetails>();
                    var newGeoGameObjectDetails = new GeoGameObjectDetails(newInSceneGameObject, streamModel.ModelStream, m_InstanceId.Value);
                    m_GeoGameObjectsDetails.Add(newGeoGameObjectDetails);
                    GeoGameObjectCreated?.Invoke(newGeoGameObjectDetails);
                    InstanceSelected?.Invoke(InstanceId.None);
                } catch (Exception e)
                {
                    Debug.LogException(e);
                    GeoGameObjectCreated?.Invoke(null);
                }
            }
        }

        public void SetTransformSpace(TransformSpace transformSpace)
        {
            if(m_TransformGizmo == null) return;
            m_TransformGizmo.SetSpace(transformSpace);
        }
        
        public void SetGizmoMode(TransformType type)
        {
            if(m_TransformGizmo == null) return;
            m_TransformGizmo.SetType(type);
        }

        public void ResetTransform()
        {
            if(m_TransformGizmo == null) return;
            var currentTarget = m_TransformGizmo.mainTargetRoot.gameObject;
            var details = m_GeoGameObjectsDetails.FirstOrDefault(x => x.InSceneGameObject == currentTarget);
            details.ResetTransform();
        }
        
        public override void OnToolOpened()
        {
            ToolOpened?.Invoke();
#if VR_MODE
            VRInteractionController.SubscribeSingleActivate(this, OnSingleActivateActionInvoked);
#else
            InteractionController.SubscribeTap(this, OnSelectActionInvoked);
#endif
        }

        public override void OnToolClosed()
        {
            ToolClosed?.Invoke();
            if (m_ModelStreamId.HasValue)
            {
                m_StreamingModelController.Stage.HighlightController.ResetHighlight(m_ModelStreamId.Value);
                m_ModelStreamId = null;
            }

            m_InstanceId = null;
            DestroyTransformGizmo();
            ClearReplacement();
            InteractionController.UnsubscribeTap(this);
        }
    }
}