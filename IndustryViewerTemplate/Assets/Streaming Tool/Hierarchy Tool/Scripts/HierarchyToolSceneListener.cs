using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Cloud.Common;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.DataStreaming.Metadata;
using Unity.Cloud.DataStreaming.Runtime.AssetManager;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
#if ENABLE_MULTIPLAY
using Unity.Netcode;
#endif

namespace Unity.Industry.Viewer.Streaming.Hierarchy
{
    // This script listens for and manages hierarchy-related events in a Unity project.
    // It handles the visibility and highlighting of streaming models based on user interactions and metadata queries.
    // The script supports asynchronous operations to fetch and update instance data from metadata repositories.
    // It integrates with Unity's MonoBehaviour for lifecycle management and supports both VR and non-VR modes.
    // The script provides user feedback through various events and updates the UI accordingly.
    public class HierarchyToolSceneListener : MonoBehaviour
    {
        [SerializeField] private GameObject hierarchyMultiplaySyncPrefab;
        
        public StreamingModelController StreamingModelController => m_StreamingModelController;
        
        private StreamingModelController m_StreamingModelController;
        
        private IServiceHttpClient m_ServiceHttpClient => IdentityController.GuestMode? 
            PlatformServices.ServiceAccountServiceHttpClient : PlatformServices.ServiceHttpClient;
        
        private IServiceHostResolver m_ServiceHostResolver => PlatformServices.ServiceHostResolver;
        
        public Dictionary<StreamingModel, IMetadataRepository> StreamModelRepositoriesMapping => m_StreamModelRepositoriesMapping;
        
        private Dictionary<StreamingModel, IMetadataRepository> m_StreamModelRepositoriesMapping = new();
        
        [SerializeField]
        private Color highlightColor = new Color(0, 200, 255, 127);
        
        public VisibilityModifier VisibilityModifier => m_VisibilityModifier;
        private VisibilityModifier m_VisibilityModifier;
        
        public HighlightModifier HighlightModifier => m_HighlightModifier;
        private HighlightModifier m_HighlightModifier;
        
#if ENABLE_MULTIPLAY
        NetworkObject m_hierarchyNetworkObject;
#endif

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            TransformController.ModelAdded += OnNewModelAdded;
            TransformController.ModelRemoved += OnModelRemoved;
            
            HierarchyToolController.InstanceVisibilityChanged += OnInstanceVisibilityChanged;
            
            m_StreamingModelController = FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
            
            m_VisibilityModifier = new VisibilityModifier();
            m_HighlightModifier = new HighlightModifier(highlightColor);
            
            m_StreamingModelController.Stage.InstanceModifiers.Add(m_VisibilityModifier);
            m_StreamingModelController.Stage.InstanceModifiers.Add(m_HighlightModifier);
            
            _ = CreateRepositories();
            
#if ENABLE_MULTIPLAY
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientStarted;
            }
#endif
        }

        private void OnDestroy()
        {
            TransformController.ModelAdded -= OnNewModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
            HierarchyToolController.InstanceVisibilityChanged -= OnInstanceVisibilityChanged;
            
            m_VisibilityModifier.Reset();
            m_HighlightModifier.Reset();
            
            if (m_StreamingModelController != null)
            {
                m_StreamingModelController.Stage.InstanceModifiers.Remove(m_VisibilityModifier);
                m_StreamingModelController.Stage.InstanceModifiers.Remove(m_HighlightModifier);
            }
            
            m_VisibilityModifier.Dispose();
            
#if ENABLE_MULTIPLAY
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientStarted;
            }
#endif
        }


#if ENABLE_MULTIPLAY
        private void OnClientStarted(ulong obj)
        {
            if (!NetworkManager.Singleton.LocalClient.IsSessionOwner || m_hierarchyNetworkObject != null) return;
            var addModelSyncObject = Instantiate(hierarchyMultiplaySyncPrefab);
            if(addModelSyncObject.TryGetComponent(out m_hierarchyNetworkObject))
            {
                m_hierarchyNetworkObject.Spawn(true);
            }
        }
#endif
        
        // This function handles the visibility changes of instances in the hierarchy.
        // It updates the visibility of the instance and its children based on the provided visibility flag.
        // If the instance has no ancestors, it directly updates the visibility and the UI toggle.
        // If the instance has children, it queries the metadata repository to update the visibility of the children.
        // The function also manages the HidingList to keep track of hidden instances.
        private void OnInstanceVisibilityChanged(InstanceData data, bool visible)
        {
            if (data.Instance == null || data.Instance.AncestorIds.Count == 0)
            {
                HierarchyToolController.UpdateToggleUI?.Invoke(data, visible);
                data.StreamingModel.gameObject.SetActive(visible);
            }
            else
            {
                HierarchyToolController.UpdateToggleUI?.Invoke(data, visible);
                m_VisibilityModifier.UpdateVisibility(data, visible);
            }
        }
        
        private void OnNewModelAdded(GameObject newGameObject, ITransformValuesAccessor newTransform)
        {
            if(newGameObject.TryGetComponent(out StreamingModel model))
            {
                _ = NewRepository(model);
            }
        }
        
        private void OnModelRemoved(StreamingModel obj)
        {
            if (!m_StreamModelRepositoriesMapping.ContainsKey(obj)) return;
            m_StreamModelRepositoriesMapping.Remove(obj);
        }
        
        private async Task CreateRepositories()
        {
            while (TransformController.Instance.transform.childCount == 0)
            {
                await Task.Yield();
            }
            for(var i = 0; i < TransformController.Instance.transform.childCount; i++)
            {
                if (!TransformController.Instance.transform.GetChild(i).TryGetComponent(out StreamingModel model))
                {
                    continue;
                }
                await NewRepository(model);
            }
        }
        
        public async Task<List<InstanceData>> QueryHierarchyData(InstanceId instanceId, IMetadataRepository repository)
        {
            return await GetChildItems().ToListAsync(CancellationToken.None);

            async IAsyncEnumerable<InstanceData> GetChildItems()
            {
                var query = repository
                    .Query()
                    .Select(MetadataPathCollection.All)
                    .WhereHasAncestor(instanceId, 0)
                    .WithCancellation(CancellationToken.None);
                
                StreamingModel streamingModel = null;

                foreach (var streamModelKeyPairValue in m_StreamModelRepositoriesMapping)
                {
                    if (streamModelKeyPairValue.Value == repository)
                    {
                        streamingModel = streamModelKeyPairValue.Key;
                        break;
                    }
                }

                if (streamingModel == null) yield break;
                
                await foreach (var each in query)
                {
                    yield return new InstanceData(each, streamingModel, repository);
                }
            }
        }
        
        private async Task NewRepository(StreamingModel model)
        {
            await Task.Yield();
            var newFactory = new MetadataRepositoryFactory();
            if (model.Dataset == null)
            {
                m_StreamModelRepositoriesMapping.Add(model, null);
                return;
            }
            var metadataRepository = newFactory.Create(model.Dataset, m_ServiceHttpClient, m_ServiceHostResolver);
            m_StreamModelRepositoriesMapping.Add(model, metadataRepository);
        }

        public void UpdateVisibility(StreamingModel model, bool root, InstanceId instanceId, bool visibility)
        {
            if (root)
            {
                HierarchyToolController.UpdateToggleUI?.Invoke(new InstanceData(null, model, null), visibility);
                model.gameObject.SetActive(visibility);
                return;
            }
            
            if (!visibility)
            {
                if (m_VisibilityModifier.HiddenInstances != null && m_VisibilityModifier.HiddenInstances.Any(x => 
                        x.StreamingModel.ModelStream.Id == model.ModelStream.Id 
                        && x.Instance.Id == instanceId))
                {
                    return;
                }
            }
            
            if (!m_StreamModelRepositoriesMapping.TryGetValue(model, out var repository))
            {
                return;
            }
            
            _ = QueryData();
            
            return;

            async Task QueryData()
            {
                var query = await repository
                    .Query()
                    .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.Id | OptionalData.Fields.AncestorIds | OptionalData.Fields.HasChildren))
                    .WhereInstanceEquals(instanceId)
                    .GetFirstOrDefaultAsync(CancellationToken.None);
                
                if (query == null)
                {
                    return;
                }
                
                var newInstanceData = new InstanceData(query, model, repository);
                OnInstanceVisibilityChanged(newInstanceData, visibility);
            }
        }
    }
}
