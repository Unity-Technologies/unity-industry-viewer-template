using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Cloud.DataStreaming.Metadata;
using Unity.Cloud.DataStreaming.Runtime.AssetManager;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine.EventSystems;
#if VR_MODE
using Unity.Industry.Viewer.Navigation.VR;
#endif

namespace Unity.Industry.Viewer.Streaming.Metadata
{
    // This script manages the metadata tool in a Unity project.
    // It handles the querying and highlighting of metadata instances for streaming models.
    // The script supports asynchronous operations to fetch metadata from repositories and update the UI accordingly.
    // It integrates with Unity's MonoBehaviour for lifecycle management and supports both VR and non-VR modes.
    // The script provides user feedback through various events and manages the interaction with streaming models.
    public class MetadataToolController : StreamToolControllerBase
    {
        public static Action<List<MetadataInstance>> MetadataFound;
        public static Action<ModelStreamId, InstanceId> InstanceSelected;
        public static Action OfflineAssetSelected;
        
        [SerializeField]
        private Color highlightColor = new Color(0, 200, 255, 255);
        
        private StreamingModelController m_StreamingModelController;
        
        private IServiceHttpClient m_ServiceHttpClient => IdentityController.GuestMode?
            PlatformServices.ServiceAccountServiceHttpClient : PlatformServices.ServiceHttpClient;
        
        private IServiceHostResolver m_ServiceHostResolver => PlatformServices.ServiceHostResolver;

        private Dictionary<ModelStreamId, IMetadataRepository> m_ModelIDRepositoriesMapping = new();
        
        private HighlightModifier m_HighlightModifier;
        
        private async void Start()
        {
            m_StreamingModelController = FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
            ToolOpened?.Invoke();
            
            m_HighlightModifier ??= new HighlightModifier(highlightColor);

            m_StreamingModelController.Stage.InstanceModifiers.Add(m_HighlightModifier);
            
            await CreateRepositories();

            if(NetworkDetector.RequestedOfflineMode) return;
            
            FindSelectedInstance();
        }

        private void OnDestroy()
        {
            m_StreamingModelController?.Stage.InstanceModifiers.Remove(m_HighlightModifier);
            #if VR_MODE
            VRInteractionController.UnsubscribeSingleActivate(this);
            #else
            InteractionController.UnsubscribeTap(this);
            #endif
        }

        private void FindSelectedInstance()
        {
            var sceneListeners = FindAnyObjectByType<MetadataSceneListener>();
            if(sceneListeners == null) return;
            if (sceneListeners.SelectedInstanceID == InstanceId.None || sceneListeners.SelectedModelID == default)
            {
                return;
            }
            _ = QueryMetadata(sceneListeners.SelectedModelID, sceneListeners.SelectedInstanceID);
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
        
        #if VR_MODE
        private void OnSingleActivateActionInvoked(Ray ray, int controllerInstanceID)
        {
            if(m_StreamingModelController == null) return;
            RaycastStreamingModel(ray);
        }
        #endif
        
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

        // This function performs a raycast to detect streaming models in the scene and queries metadata for the detected instance.
        // It consists of two asynchronous tasks: Raycast and QueryMetadata.
        //
        // - RaycastStreamingModel: Initiates the raycast process.
        //   - Raycast: Resets the highlight modifier, performs the raycast, and if an instance is detected, it queries the metadata.
        //   - QueryMetadata: Fetches metadata for the detected instance and its ancestors, updates the highlight modifier, and invokes the MetadataFound event with the retrieved metadata.
        private void RaycastStreamingModel(Ray ray)
        {
            if(m_StreamingModelController == null) return;
            if(IsPointerOverUI()) return;
            
            _ = Raycast();
            
            return;

            async Task Raycast()
            {
                MetadataFound?.Invoke(null);
                InstanceSelected?.Invoke(default, InstanceId.None);
                m_HighlightModifier?.Reset();
                try
                {
                    var raycastResult = await m_StreamingModelController.Stage.RaycastAsync((DoubleRay) ray, m_StreamingModelController.ActiveCamera.farClipPlane, RaycastOptions.ExcludeHidden);
                    if (raycastResult.InstanceId == InstanceId.None)
                    {
                        return;
                    }
                    
                    if (NetworkDetector.RequestedOfflineMode)
                    {
                        return;
                    }
                    
                    ModelStreamId modelStreamId = raycastResult.ModelId;
                    await QueryMetadata(modelStreamId, raycastResult.InstanceId);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
        
        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
        }
        
        private async Task QueryMetadata(ModelStreamId modelID, InstanceId id)
        {
            var streamingModels = TransformController.Instance.GetComponentsInChildren<StreamingModel>();
            if (streamingModels.All(x => x.ModelStream.Id != modelID))
            {
                InstanceSelected?.Invoke(default, InstanceId.None);
                return;
            }
            
            if (!m_ModelIDRepositoriesMapping.TryGetValue(modelID, out var repository))
            {
                OfflineAssetSelected?.Invoke();
                return;
            }
                
            var firstQuery = await repository
                .Query()
                .Select(MetadataPathCollection.All)
                .WhereInstanceEquals(id)
                .WhereHasAncestor(InstanceId.None, int.MaxValue)      
                .GetFirstOrDefaultAsync(CancellationToken.None);

            if(firstQuery == null) return;
            
            var found = new List<MetadataInstance>();
            
            if (firstQuery.AncestorIds is {Count: > 0})
            {
                var ancestors = new Dictionary<InstanceId, MetadataInstance>();
                
                var ancestorEnumerator = repository
                    .Query()
                    .Select(MetadataPathCollection.All)
                    .WhereInstanceEquals(firstQuery.AncestorIds);
                
                await foreach (var ancestor in ancestorEnumerator.WithCancellation(CancellationToken.None))
                    ancestors.Add(ancestor.Id, ancestor);
                    
                foreach (var ancestorId in firstQuery.AncestorIds)
                {
                    if (ancestors.TryGetValue(ancestorId, out var ancestor))
                        found.Add(ancestor);
                }
            }
            found.Add(firstQuery);
                
            m_HighlightModifier.Update(modelID, id);
                
            InstanceSelected?.Invoke(modelID, id);
                
            MetadataFound?.Invoke(found);
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
            m_HighlightModifier?.Reset();
            m_HighlightModifier?.Dispose();
            InteractionController.UnsubscribeTap(this);
        }
    }
}
