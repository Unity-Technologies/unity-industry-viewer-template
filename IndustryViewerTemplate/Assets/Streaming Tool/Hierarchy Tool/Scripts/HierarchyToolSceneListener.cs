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
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Streaming;
#if ENABLE_MULTIPLAY
using Unity.Netcode;
#endif

namespace Unity.Industry.Viewer.Streaming.Hierarchy
{
    public class InstanceState
    {
        public readonly Dictionary<Color, List<InstanceId>> Highlighted = new();
        public readonly List<InstanceData> Hidden = new();
        public InstanceId Isolated = InstanceId.None;
    }
    
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
        
        // Cache for early-loaded hierarchy data (loaded before geometry)
        private Dictionary<StreamingModel, List<InstanceData>> m_CachedHierarchyData = new();
        
        // Queue for batching early hierarchy loads
        private List<(StreamingModel model, IMetadataRepository repository)> m_PendingHierarchyLoads = new();
        private bool m_IsLoadingHierarchies = false;
        private Task m_CurrentBatchLoadTask = null;
        
        // Snapshot of original layout order (before models are removed from LayoutJson)
        private List<LayoutModelEntity> m_OriginalLayoutOrder = new();
        
        public Dictionary<ModelStreamId, InstanceState> InstanceStates => m_InstanceStates;
        private Dictionary<ModelStreamId, InstanceState> m_InstanceStates = new();

        // Last selected instance, persisted across tool open/close for restore-on-open.
        // Replaces the former MetadataSceneListener.
        public ModelStreamId SelectedModelID { get; private set; }
        public InstanceId SelectedInstanceID { get; private set; } = InstanceId.None;

        [SerializeField]
        private Color highlightColor = new Color(0, 200, 255, 127);
        
        public CancellationTokenSource QueryTokenSource;
        
#if ENABLE_MULTIPLAY
        NetworkObject m_hierarchyNetworkObject;
#endif

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            TransformController.ModelAdded += OnNewModelAdded;
            TransformController.ModelRemoved += OnModelRemoved;
            HierarchyToolController.InstanceVisibilityChanged += OnInstanceVisibilityChanged;
            HierarchyToolController.VisibilityReset += OnVisibilityReset;
            HierarchyToolController.InstanceSelected += OnInstanceSelected;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;

            m_StreamingModelController = FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
            
            // Start coroutine to capture layout order snapshot when LayoutJson becomes available
            StartCoroutine(CaptureLayoutOrderWhenAvailable());
            
            _ = CreateRepositories();
            
#if ENABLE_MULTIPLAY
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientStarted;
            }
#endif
        }

        private void OnInstanceSelected(ModelStreamId modelID, InstanceId instanceID)
        {
            SelectedModelID = modelID;
            SelectedInstanceID = instanceID;
        }

        private void OnDestroy()
        {
            TransformController.ModelAdded -= OnNewModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
            HierarchyToolController.InstanceVisibilityChanged -= OnInstanceVisibilityChanged;
            HierarchyToolController.VisibilityReset -= OnVisibilityReset;
            HierarchyToolController.InstanceSelected -= OnInstanceSelected;
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            
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
            if (addModelSyncObject.TryGetComponent(out m_hierarchyNetworkObject))
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
            UpdateVisibility(data.StreamingModel, data.Instance == null || data.Instance.AncestorIds.Count == 0, data, visible);
        }
        
        private void OnNewModelAdded(GameObject newGameObject, ITransformValuesAccessor newTransform)
        {
            if(newGameObject.TryGetComponent(out StreamingModel model))
            {
                // Try to capture layout order snapshot if not already captured (fallback)
                CaptureLayoutOrderSnapshot();
                
                _ = NewRepository(model);
            }
        }
        
        private void OnModelRemoved(StreamingModel obj)
        {
            lock (m_StreamModelRepositoriesMapping)
            {
                if (!m_StreamModelRepositoriesMapping.ContainsKey(obj))
                {
                    return;
                }
                m_StreamModelRepositoriesMapping.Remove(obj);
            }
            lock (m_CachedHierarchyData)
            {
                m_CachedHierarchyData.Remove(obj);
            }
            if(m_InstanceStates == null || m_InstanceStates.Count == 0) return;
            lock (m_InstanceStates)
            {
                if(!m_InstanceStates.ContainsKey(obj.ModelStreamId))
                {
                    return;
                }
                m_InstanceStates.Remove(obj.ModelStreamId);
            }
        }
        
        private async Task CreateRepositories()
        {
            while (TransformController.Instance.transform.childCount == 0)
            {
                await Task.Yield();
            }
            
            // Collect all models first
            var models = new List<StreamingModel>();
            for(var i = 0; i < TransformController.Instance.transform.childCount; i++)
            {
                if (TransformController.Instance.transform.GetChild(i).TryGetComponent(out StreamingModel model))
                {
                    models.Add(model);
                }
            }
            
            // Create all repositories in parallel using Task.WhenAll
            var repositoryTasks = models.Select(model => NewRepository(model)).ToList();
            await Task.WhenAll(repositoryTasks);
        }
        
        public async Task<List<InstanceData>> QueryHierarchyData(InstanceId instanceId, IMetadataRepository repository, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
            {
                if (QueryTokenSource == null)
                {
                    QueryTokenSource = new CancellationTokenSource();
                }
                cancellationToken = QueryTokenSource.Token;
            }
            
            return await GetChildItems().ToListAsync(cancellationToken);

            async IAsyncEnumerable<InstanceData> GetChildItems()
            {
                var query = repository
                    .Query()
                    .Select(MetadataPathCollection.All)
                    .WhereHasAncestor(instanceId, 0)
                    .WithCancellation(cancellationToken);
                
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
                    if (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    yield return new InstanceData(each, streamingModel, repository);
                }
            }
        }

        public void ResetToken()
        {
            QueryTokenSource?.Cancel();
            QueryTokenSource?.Dispose();
            QueryTokenSource = new CancellationTokenSource();
        }
        
        private async Task NewRepository(StreamingModel model)
        {
            await Task.Yield();
            var newFactory = new MetadataRepositoryFactory();
            if (model.Dataset == null)
            {
                lock (m_StreamModelRepositoriesMapping)
                {
                    m_StreamModelRepositoriesMapping.Add(model, null);
                }
                return;
            }
            var metadataRepository = newFactory.Create(model.Dataset, m_ServiceHttpClient, m_ServiceHostResolver);
            lock (m_StreamModelRepositoriesMapping)
            {
                m_StreamModelRepositoriesMapping.Add(model, metadataRepository);
            }
            
            // Queue for batched parallel loading
            lock (m_PendingHierarchyLoads)
            {
                m_PendingHierarchyLoads.Add((model, metadataRepository));
            }
            // Start batched loading if not already in progress
            if (!m_IsLoadingHierarchies)
            {
                m_CurrentBatchLoadTask = BatchLoadHierarchiesAsync();
            }
        }
        
        private async Task BatchLoadHierarchiesAsync()
        {
            m_IsLoadingHierarchies = true;

            await Task.Yield();
            
            List<(StreamingModel model, IMetadataRepository repository)> pendingLoads;
            lock (m_PendingHierarchyLoads)
            {
                if (m_PendingHierarchyLoads.Count == 0)
                {
                    m_IsLoadingHierarchies = false;
                    return;
                }
                pendingLoads = new List<(StreamingModel, IMetadataRepository)>(m_PendingHierarchyLoads);
                m_PendingHierarchyLoads.Clear();
            }
            
            try
            {
                // Load all hierarchies in parallel
                var loadTasks = pendingLoads.Select(async load =>
                {
                    try
                    {
                        var loadTokenSource = new CancellationTokenSource();
                        var hierarchyData = await QueryHierarchyData(InstanceId.None, load.repository, loadTokenSource.Token);
                        
                        lock (m_CachedHierarchyData)
                        {
                            m_CachedHierarchyData[load.model] = hierarchyData;
                        }
                        
                        return (load.model, hierarchyData);
                    }
                    catch (OperationCanceledException)
                    {
                        return (load.model, (List<InstanceData>)null);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        return (load.model, (List<InstanceData>)null);
                    }
                }).ToList();
                
                // Wait for all hierarchies to load in parallel
                await Task.WhenAll(loadTasks);
                
                // Refresh tree view once after all hierarchies are loaded
                HierarchyToolController.RequestTreeViewRefresh?.Invoke();
            }
            finally
            {
                // Check if there are more pending loads
                lock (m_PendingHierarchyLoads)
                {
                    if (m_PendingHierarchyLoads.Count > 0)
                    {
                        // More repositories were added while loading, start another batch
                        m_CurrentBatchLoadTask = BatchLoadHierarchiesAsync();
                    }
                    else
                    {
                        m_IsLoadingHierarchies = false;
                        m_CurrentBatchLoadTask = null;
                    }
                }
            }
        }
        
        public bool TryGetCachedHierarchyData(StreamingModel model, out List<InstanceData> hierarchyData)
        {
            lock (m_CachedHierarchyData)
            {
                return m_CachedHierarchyData.TryGetValue(model, out hierarchyData);
            }
        }
        
        public async Task WaitForEarlyLoadingToComplete()
        {
            // Wait for any in-progress early loading to complete
            if (m_CurrentBatchLoadTask != null)
            {
                try
                {
                    await m_CurrentBatchLoadTask;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
        
        private System.Collections.IEnumerator CaptureLayoutOrderWhenAvailable()
        {
            // Wait for LayoutJson to be set (it's set asynchronously in ProcessLayoutJson)
            while (m_StreamingModelController?.LayoutJson?.LayoutModels == null || 
                   m_StreamingModelController.LayoutJson.LayoutModels.Count == 0)
            {
                yield return null;
            }
            
            // Capture snapshot immediately when LayoutJson becomes available
            // This happens before models start being removed in ModelAdded
            CaptureLayoutOrderSnapshot();
        }
        
        private void CaptureLayoutOrderSnapshot()
        {
            // Capture a snapshot of the original layout order before models start being removed
            if (m_StreamingModelController?.LayoutJson?.LayoutModels != null && 
                m_StreamingModelController.LayoutJson.LayoutModels.Count > 0 &&
                m_OriginalLayoutOrder.Count == 0) // Only capture if not already captured
            {
                m_OriginalLayoutOrder = new List<LayoutModelEntity>(m_StreamingModelController.LayoutJson.LayoutModels);
            }
        }
        
        public List<LayoutModelEntity> GetOriginalLayoutOrder()
        {
            // Try to capture if not yet captured (fallback)
            if (m_OriginalLayoutOrder.Count == 0)
            {
                CaptureLayoutOrderSnapshot();
            }
            return m_OriginalLayoutOrder;
        }
        
        public void HighlightInstance(ModelStreamId modelId, InstanceId instanceId)
        {
            foreach (var state in m_InstanceStates.Keys)
            {
                m_InstanceStates[state].Highlighted.Clear();
                m_StreamingModelController.Stage.HighlightController.ResetHighlight(state);
            }
            
            if (!m_InstanceStates.TryGetValue(modelId, out var instanceState))
            {
                instanceState = new InstanceState();
                m_InstanceStates.Add(modelId, instanceState);
            }
            
            if (!instanceState.Highlighted.TryGetValue(highlightColor, out var ids))
            {
                ids = new List<InstanceId>();
                instanceState.Highlighted.Add(highlightColor, ids);
            }
            
            ids.Add(instanceId);
            foreach (var modelStreamId in m_InstanceStates.Keys)
            {
                foreach (var highlightedKey in instanceState.Highlighted.Keys)
                {
                    m_StreamingModelController.Stage.HighlightController.SetHighlight(modelStreamId, highlightedKey, instanceState.Highlighted[highlightedKey]);
                }
            }
        }

        public void ResetHierarchyModifiers(bool resetHighlighted, bool resetHidden)
        {
            foreach (var modelStreamId in m_InstanceStates.Keys)
            {
                if (resetHighlighted)
                {
                    m_InstanceStates[modelStreamId].Highlighted.Clear();
                    m_StreamingModelController.Stage.HighlightController.ResetHighlight(modelStreamId);
                }

                if (resetHidden)
                {
                    m_InstanceStates[modelStreamId].Hidden.Clear();
                    m_StreamingModelController.Stage.VisibilityController.ResetVisibility(modelStreamId);
                }
            }
        }

        public void ResetHighlight()
        {
            foreach (var modelStreamId in m_InstanceStates.Keys)
            {
                m_InstanceStates[modelStreamId].Highlighted.Clear();
                m_StreamingModelController.Stage.HighlightController.ResetHighlight(modelStreamId);
            }
        }

        public bool IsCurrentlyHidden(ModelStreamId modelStreamId, InstanceId instanceId)
        {
            if (!m_InstanceStates.TryGetValue(modelStreamId, out var instanceState)) return false;
            return instanceState.Hidden != null && instanceState.Hidden.Any(x => x.Instance.Id == instanceId);
        }

        private void OnNetworkStatusChanged(bool connected)
        {
            if (!connected && !NetworkDetector.RequestedOfflineMode) return;

            HierarchyToolController.ResetVisibility(true);
        }

        private void OnVisibilityReset(bool resetHighlighted)
        {
#if ENABLE_MULTIPLAY
            // Assuming MP code handles this.
            // Anyway need to reset locally if user:
            // - is not logged in
            // - is or goes Offline
            // - in Guest mode
            if (IdentityController.IsLoogedIn
                && !NetworkDetector.IsOffline
                && !NetworkDetector.RequestedOfflineMode
                && !IdentityController.GuestMode)
            {
                return;
            }
#endif

            foreach (var streamingModel in TransformController.Instance.GetComponentsInChildren<StreamingModel>(true))
            {
                streamingModel.gameObject.SetActive(true);
            }

            ResetHierarchyModifiers(resetHighlighted, true);
        }

        public async Task UpdateVisibility(StreamingModel model, bool root, InstanceId instanceId, bool visible)
        {
            if (root)
            {
                UpdateVisibility(model, true, InstanceData.Placeholder, visible);
                return;
            }
            
            var repository = m_StreamModelRepositoriesMapping[model];
            if (repository == null)
            {
                Debug.LogWarning($"No metadata repository found for model {model.name}");
                return;
            }

            var query = await repository
                .Query()
                .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.Id))
                .WhereInstanceEquals(instanceId)
                .GetFirstOrDefaultAsync(CancellationToken.None);
            
            if (query == null) return;

            var instanceData = new InstanceData(query, model, repository);
            UpdateVisibility(model, false, instanceData, visible);
        }

        private void UpdateVisibility(StreamingModel model, bool root, InstanceData instanceData, bool visible)
        {
            if (root)
            {
                HierarchyToolController.UpdateToggleUI?.Invoke(new InstanceData(null, model, null), visible);
                model.gameObject.SetActive(visible);
                return;
            }

            HierarchyToolController.UpdateToggleUI?.Invoke(instanceData, visible);

            if (!m_InstanceStates.TryGetValue(instanceData.StreamModel.Id, out var instanceState))
            {
                instanceState = new InstanceState();
                m_InstanceStates.Add(instanceData.StreamModel.Id, instanceState);
            }

            if (visible)
            {
                var index = instanceState.Hidden.FindIndex(x => x.Instance.Id == instanceData.Instance.Id);
                
                if (index >= 0)
                {
                    instanceState.Hidden.RemoveAt(index);
                }
            }
            else
            {
                if (instanceState.Hidden.All(x => x.Instance.Id != instanceData.Instance.Id))
                {
                    instanceState.Hidden.Add(instanceData);
                }
            }
            m_StreamingModelController.Stage.VisibilityController.SetVisibility(model.ModelStreamId,
                instanceState.Hidden.Select(x => x.Instance.Id), Array.Empty<InstanceId>());
        }
    }
}
