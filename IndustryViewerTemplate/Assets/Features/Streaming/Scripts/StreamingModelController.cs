// The StreamingModelController class manages the streaming of 3D models in a Unity application.
// It handles adding, removing, and updating streaming models, as well as managing camera observers and model transformations.
// The class uses Unity's Data Streaming Runtime to stream models from various sources, including local files and remote datasets.
// It also provides events for model loading, bounds updating, and observer management.

using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.DataStreaming.Runtime.AssetManager;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Unity.Cloud.HighPrecision.Runtime;
using System.IO;
using System.Threading;
using Unity.Cloud.Common;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Networking;
using Newtonsoft.Json;
#if ENABLE_MULTIPLAY
using Unity.Services.Multiplayer;
#endif
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(-100)]
    public class StreamingModelController : MonoBehaviour
    {
        public static AssetInfo? StreamingAsset;
        public static bool PauseAddingModel;
        public static Action FinishedAddingModel;
        public static Action<bool> LoadingGLBModel;
        
        public static Action<DoubleBounds, bool> BoundsUpdated;
        public static Action RequestBoundsUpdate;
        public static Action<Camera> AddObserver;
        public static Action<AssetInfo, string, int?> AddStreamModel;
        public static Action<StreamingModel> RemoveStreamModel;
        public static Action<bool> PauseWireframeMode;

        public static bool IsLayoutAsset
        {
            get
            {
                if (StreamingAsset.Value.Asset is OfflineAsset offlineAsset)
                {
                    return offlineAsset.OfflineAssetInfo.layout;
                }
                if (StreamingAsset.Value.Properties.HasValue)
                {
                    return StreamingAsset.Value.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag);
                }

                return false;
            }
        }
        
        public static int StreamingAssetVersion
        {
            get
            {
                if (StreamingAsset.Value.Asset is OfflineAsset offlineAsset)
                {
                    return offlineAsset.OfflineAssetInfo.assetVersion;
                }
                if (StreamingAsset.Value.Properties.HasValue)
                {
                    return StreamingAsset.Value.Properties.Value.FrozenSequenceNumber;
                }

                return 0;
            }
        }

        /// <summary>
        /// Returns a <see cref="IDataStreamer"/>.
        /// </summary>
        IDataStreamer m_DataStreamer => PlatformServices.DataStreamer;

        public IStage Stage => m_Stage;

        /// <summary>
        /// A reference to the stage created when opening the <see cref="IDataStreamer"/>.
        /// </summary>
        IStage m_Stage;

        ICameraObserver m_CurrentObserver;

        public Camera ActiveCamera => m_ActiveCamera;
        private Camera m_ActiveCamera;
        
        [SerializeField, Tooltip("Set custom material for streaming")]
        private StreamingMaterialFactoryObject m_MaterialFactory;
        
        [SerializeField, Tooltip("Set the resource limits for the streaming model controller. If not set, no limits will be enforced.")]
        private ResourceLimitAsset m_ResourceLimitAsset;

        IServiceHttpClient m_ServiceHttpClient => IdentityController.GuestMode? PlatformServices.ServiceAccountServiceHttpClient : PlatformServices.ServiceHttpClient;
        
        IServiceHostResolver m_ServiceHostResolver => PlatformServices.ServiceHostResolver;
        
        [SerializeField, Tooltip("Turn on wireframe mode will have performance cost (double the memory) no matter what mode you put into")]
        private bool m_EnableWireframe = false;
        private bool m_WasInWireframeMode = false;

        private bool m_InitializedBounds = false;

        private bool m_LayoutCameraInitialised = false;

        public LayoutJson LayoutJson { get; private set; }
        
#if ENABLE_MULTIPLAY
        private ISession m_Session;
#endif
        
        public bool FinishedInitialLoading {get; private set;}

        // Called when the script instance is being loaded
        private void Awake()
        {
            m_Stage = null;
            AddStreamModel += OnAddStreamModel;
            RemoveStreamModel += OnRemoveStreamModel;
            AddObserver += AddCameraObserver;
            RequestBoundsUpdate += OnRequestBoundsUpdate;
        }

        // Start is called before the first frame update
        private void Start()
        {
            var builder = DataStreamerSettingsBuilder
                .CreateDefaultBuilder()
                .EnableHighlightController() // Enable highlight controller for hierarchical highlighting of models
                .EnableVisibilityController() // Enable visibility controller for hierarchical visibility control of models
                .SetWireframeSettings(m_EnableWireframe, WireframeModes.Shaded, Color.green); //Turn on wireframe mode will have performance cost (double the memory) no matter what mode you put into
            
            if (m_MaterialFactory != null)
            {
                var materialFactory = m_MaterialFactory.Instantiate();
                builder.SetMaterialFactory(materialFactory);
            }
            
            // Set the resource limits for the streaming model controller, feel free to change as it only affects WEBGL and VR
            if (m_ResourceLimitAsset != null)
            {
                bool isVRBuild = false;
#if VR_MODE
                isVRBuild = true;
#endif
                var limitSettings = m_ResourceLimitAsset.ResourceLimits.FirstOrDefault(x => x.IsVRBuild == isVRBuild && x.Platform == Application.platform);
                if (limitSettings != null)
                {
                    builder.ConfigureDefaultResourceLimiter(x => x.SetMaxTriangleCount(limitSettings.MaxResources));
                }
                else
                {
                    Debug.Log("Use default resource limit");
                }
            }
            
            var settings = builder.Build();
            m_Stage = m_DataStreamer.Open(settings);
            m_Stage.Models.ModelRemoved.Subscribe(OnModelRemoved);

            AssetsController.AssetSelected += OnAssetSelected;
            TransformController.ModelAdded += ModelAdded;
            TransformController.ModelRemoved += OnModelRemoved;
            PauseWireframeMode += OnPauseWireframeMode;
#if ENABLE_MULTIPLAY
            StartCoroutine(SubscribeToMultiplayEvent());
            return;
            
            IEnumerator SubscribeToMultiplayEvent()
            {
                while (MultiplayerService.Instance == null)
                {
                    yield return null;
                }
                MultiplayerService.Instance.SessionAdded -= SessionAdded;
                MultiplayerService.Instance.SessionRemoved -= SessionRemoved;
                MultiplayerService.Instance.SessionAdded += SessionAdded;
                MultiplayerService.Instance.SessionRemoved += SessionRemoved;
            }
#endif
        }

        // Called when the MonoBehaviour will be destroyed
        private void OnDestroy()
        {
            PauseWireframeMode -= OnPauseWireframeMode;
            AssetsController.AssetSelected -= OnAssetSelected;
            TransformController.ModelAdded -= ModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
            m_Stage.Models.ModelRemoved.Unsubscribe(OnModelRemoved);
            m_Stage.Observers.Remove(m_CurrentObserver);
            m_Stage = null;
            m_DataStreamer.Close();
            AddStreamModel -= OnAddStreamModel;
            RemoveStreamModel -= OnRemoveStreamModel;
            AddObserver -= AddCameraObserver;
            RequestBoundsUpdate -= OnRequestBoundsUpdate;
            m_CurrentObserver = null;
            m_ActiveCamera = null;
            StreamingAsset = null;
#if ENABLE_MULTIPLAY
            if (MultiplayerService.Instance != null)
            {
                MultiplayerService.Instance.SessionAdded -= SessionAdded;
                MultiplayerService.Instance.SessionRemoved -= SessionRemoved;
            }
#endif
            // Delete temporary GLB and GLTF files
            string[] glbFiles = Directory.GetFiles(Application.persistentDataPath, "*.glb");
            string[] gltfFiles = Directory.GetFiles(Application.persistentDataPath, "*.gltf");

            foreach (var file in glbFiles.Concat(gltfFiles))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to delete file {file}: {e.Message}");
                }
            }
        }



#if ENABLE_MULTIPLAY
        private void SessionAdded(ISession session)
        {
            m_Session = session;
        }
        
        private void SessionRemoved(ISession session)
        {
            if(m_Session == null) return;
            if (string.Equals(session.Name, m_Session.Name))
            {
                m_Session = null;
            }
        }
#endif
        
        private void OnPauseWireframeMode(bool pause)
        {
            if(!m_EnableWireframe) return;
            if (pause)
            {
                m_WasInWireframeMode = m_Stage.Wireframe.Enabled && m_Stage.Wireframe.Mode == WireframeModes.Wireframe;
                m_Stage.Wireframe.Mode = WireframeModes.Shaded;
            }
            else
            {
                if (m_WasInWireframeMode)
                {
                    m_Stage.Wireframe.Mode = WireframeModes.Wireframe;
                }
            }
        }

        /// <summary>
        /// This is listener for Transform Controller event
        /// </summary>
        private void OnModelRemoved(StreamingModel obj)
        {
            RequestBoundsUpdate?.Invoke();
        }

        /// <summary>
        /// This is listener for Stage Model async removing finish
        /// </summary>
        private void OnModelRemoved(IModelStream obj)
        {
            RequestBoundsUpdate?.Invoke();
        }

        private void ModelAdded(GameObject arg1, ITransformValuesAccessor arg2)
        {
            if (LayoutJson?.LayoutModels == null)
            {
                Completed();
                return;
            }
            var layoutModelEntity = LayoutJson.LayoutModels.FirstOrDefault(x => string.Equals(x.gameObjectName, arg1.name));
            if (layoutModelEntity != null)
            {
                arg1.transform.localPosition = layoutModelEntity.GetLocalPosition();
                arg1.transform.localRotation = layoutModelEntity.GetLocalRotation();
                LayoutJson.LayoutModels.Remove(layoutModelEntity);
            }

            if (LayoutJson.LayoutModels.Count == 0)
            {
                StartCoroutine(WaitForSecond());
            }

            IEnumerator WaitForSecond()
            {
                yield return new WaitForSeconds(1f);
                Completed();
            }

            void Completed()
            {
                RequestBoundsUpdate?.Invoke();
                FinishedAddingModel?.Invoke();
            }
        }

        // Called when an asset is selected
        // use for when accepting a newer version to replace the current model
        private void OnAssetSelected(AssetInfo assetInfo)
        {
            if (TransformController.Instance == null) return;
            FinishedInitialLoading = false;
            StreamingAsset = assetInfo;
            var allStreamingModels = TransformController.Instance.transform.GetComponentsInChildren<StreamingModel>(true);
#if ENABLE_MULTIPLAY
            Action reconnectionAction = null;
            if (m_Session == null)
            {
                foreach (var streamingModel in allStreamingModels)
                {
                    RemoveStreamModel?.Invoke(streamingModel);
                }
                
                if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
                {
                    TransformController.ModelAdded -= ModelAdded;
                    TransformController.ModelAdded += ModelAdded;
                    GetLayoutJson(assetInfo.Asset, assetInfo.Asset is OfflineAsset);
                    return;
                }
                AddStreamModel?.Invoke(StreamingAsset.Value, string.Empty, null);
            } else {
                //Wait for session removed
                MultiplayerService.Instance.SessionRemoved += InstanceOnSessionRemoved;
            }
#else
            foreach (var streamingModel in allStreamingModels)
            {
                RemoveStreamModel?.Invoke(streamingModel);
            }

            if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
            {
                TransformController.ModelAdded -= ModelAdded;
                TransformController.ModelAdded += ModelAdded;
                GetLayoutJson(assetInfo.Asset, assetInfo.Asset is OfflineAsset);
                return;
            }
            AddStreamModel?.Invoke(StreamingAsset.Value, string.Empty, null);
            
#endif
            
#if ENABLE_MULTIPLAY
            return;
            
            void InstanceOnSessionRemoved(ISession obj)
            {
                MultiplayerService.Instance.SessionRemoved -= InstanceOnSessionRemoved;
                MultiplayerService.Instance.SessionAdded += InstanceOnSessionAdded;
                if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
                {
                    reconnectionAction = () => GetLayoutJson(assetInfo.Asset, assetInfo.Asset is OfflineAsset);
                }
                else
                {
                    reconnectionAction = () => AddStreamModel?.Invoke(StreamingAsset.Value, string.Empty, null); 
                }
                
                StartCoroutine(WaitForNetworkDisconnectFully());
            }
            
            void InstanceOnSessionAdded(ISession obj)
            {
                MultiplayerService.Instance.SessionAdded -= InstanceOnSessionAdded;
                StartCoroutine(WaitForNetworkTransform());
            }

            IEnumerator WaitForNetworkTransform()
            {
                while (TransformController.Instance == null)
                {
                    yield return null;
                }
                reconnectionAction?.Invoke();
            }

            IEnumerator WaitForNetworkDisconnectFully()
            {
                yield return null;
                foreach (var streamingModel in allStreamingModels)
                {
                    OnRemoveStreamModel(streamingModel);
                }

                if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
                {
                    TransformController.ModelAdded -= ModelAdded;
                    TransformController.ModelAdded += ModelAdded;
                }
            }
#endif
        }

        // Adds a camera observer to the stage
        private void AddCameraObserver(Camera observeCamera)
        {
            if (observeCamera == null || m_ActiveCamera == observeCamera || m_Stage == null)
            {
                return;
            }
            if (m_CurrentObserver != null)
            {
                m_Stage.Observers.Remove(m_CurrentObserver);
                m_ActiveCamera = null;
            }
            
#if VR_MODE
            m_CurrentObserver = new ProximityAwareObserver(observeCamera);
#else
            m_CurrentObserver = StageObserverFactory.CreateCameraObserver(observeCamera);
#endif
            
            m_ActiveCamera = observeCamera;

            m_Stage.Observers.Add(m_CurrentObserver);
        }

        // Called when a streaming model is removed
        private void OnRemoveStreamModel(StreamingModel streamingModel)
        {
            m_Stage.Models.Remove(streamingModel.ModelStream);
            TransformController.ModelRemoved?.Invoke(streamingModel);
            Destroy(streamingModel.gameObject);
        }

        // Retrieves the dataset for a streaming model
        private void GetStreamModelDataset(AssetInfo assetInfo, Action<IDataset, DatasetProperties?> onComplete)
        {
            _ = StartStreaming();
            return;

            async Task StartStreaming()
            {
                IDataset targetDataset = null;
                DatasetProperties? properties = null;
                try
                {
                    targetDataset = await GetStreamingDataset(assetInfo.Asset);
                    properties = await targetDataset.GetPropertiesAsync(CancellationToken.None);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    // Only complete when the dataset and its properties were actually retrieved.
                    // On failure both stay null, and the onComplete handler dereferences
                    // datasetProperties.Value / targetDataset, so invoking it would throw.
                    if (targetDataset != null && properties.HasValue)
                    {
                        onComplete?.Invoke(targetDataset, properties);
                    }
                }
            }
        }

        //Get Layout Json from Source Dataset
        private void GetLayoutJson(IAsset asset, bool offline)
        {
            if (!offline)
            {
                _ = GetLayout();
            }
            else
            {
                var hashFolder = StreamingUtils.ReturnHashName(asset);
                var folders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolder + "*");
                if (folders.Length == 0)
                {
                    return;
                }
                var folder = folders[0];
                var layoutJsonPath = Path.Combine(folder, StreamingUtils.LayoutJson);
                if (!File.Exists(layoutJsonPath))
                {
                    return;
                }
                var json = File.ReadAllText(layoutJsonPath);
                var result = JsonConvert.DeserializeObject<LayoutJson>(json);
                _ = ProcessLayoutJson(result, this, true);
            }
            return;
            
            async Task GetLayout()
            {
                var sourceDataset = await asset.GetSourceDatasetAsync(CancellationToken.None);
                if (sourceDataset == null)
                {
                    Debug.Log("Source Dataset is null");
                    return;
                }
                var layoutFile = await sourceDataset.GetFileAsync(StreamingUtils.LayoutJson, CancellationToken.None);
                if (layoutFile == null)
                {
                    Debug.Log("Layout File is null");;
                    return;
                }
                var downloadUrl = await layoutFile.GetDownloadUrlAsync(CancellationToken.None);
                using var www = UnityWebRequest.Get(downloadUrl);
                await www.SendWebRequest();
                if (www.result is (UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError))
                {
                    return;
                }
                var json = System.Text.Encoding.UTF8.GetString(www.downloadHandler.data);
                var result = JsonConvert.DeserializeObject<LayoutJson>(json);
                _ = ProcessLayoutJson(result, this, false);
            }
        }
        

        // Called when a streaming model is added
        private void OnAddStreamModel(AssetInfo assetInfo, string targetName, int? instanceNumber)
        {
            OfflineAsset offlineAsset = null;
            if (assetInfo.Asset is OfflineAsset asset)
            {
                offlineAsset = asset;
                if (offlineAsset.OfflineAssetInfo.layout)
                {
                    GetLayoutJson(asset, true);
                    return;
                }
            }
            else
            {
                if (assetInfo is { Asset: not null, Properties: not null })
                {
                    if(assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
                    {
                        GetLayoutJson(assetInfo.Asset, false);
                        return;
                    }
                }
            }

            if (assetInfo.Asset is not OfflineAsset infoAsset)
            {
                GetStreamModelDataset(assetInfo, (targetDataset, datasetProperties) =>
                {
                    if (datasetProperties.Value.SystemTags.Contains(StreamingUtils.StreamableTag))
                    {
                        FinishedInitialLoading = true;
                        var model = m_Stage.Models.Add(x => x.FromDataset(targetDataset, m_ServiceHttpClient, m_ServiceHostResolver));
                        var modelStream = HandleNewModelStream(assetInfo.Asset.Descriptor.AssetId.ToString(), targetName);
                        modelStream.Initialize(model, assetInfo, targetDataset, true, instanceNumber);
                        TransformController.ModelAdded?.Invoke(modelStream.gameObject, model.Transform);
                    }
                    else
                    {
                        _ = DownloadAndLoadFile(assetInfo, targetDataset);
                    }
                });
            }
            else
            {
                var path = offlineAsset?.GetLocalStreamingPath();
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                offlineAsset = infoAsset;
                var model = AddModelFromLocal(path);
                var streamModel = HandleNewModelStream(offlineAsset.Descriptor.AssetId.ToString(), targetName);
                var newAssetInfo = new AssetInfo()
                {
                    Asset = offlineAsset,
                    Properties = null
                };
                FinishedInitialLoading = true;
                streamModel.Initialize(model, newAssetInfo, false, instanceNumber);
                TransformController.ModelAdded?.Invoke(streamModel.gameObject, model.Transform);
            }

            return;

            // Adds a model from a local path
            IModelStream AddModelFromLocal(string json)
            {
                return m_Stage.Models.Add(x => x.FromUri(new Uri(json)));
            }
        }

        /// <summary>
        /// Processes the provided layout JSON data and adds models to the scene based on the specified conditions.
        /// </summary>
        /// <remarks>This method handles both online and offline modes for adding models to the scene. In
        /// offline mode, it attempts to retrieve offline asset information for each model. In online mode, it queries
        /// the asset repository to fetch the required asset data. Models that fail validation or retrieval are removed
        /// from the layout. The method supports pausing and resuming the addition of models, and invokes appropriate
        /// events when models are added or the process is completed.</remarks>
        /// <param name="layout">The layout JSON data containing the models to be processed. Cannot be <see langword="null"/>.</param>
        /// <param name="offline">Optional. Specifies whether the operation should be performed in offline mode. If <see langword="null"/>,
        /// the mode is determined by the <see cref="NetworkDetector.RequestedOfflineMode"/> property.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ProcessLayoutJson(LayoutJson layout, MonoBehaviour initializer, bool? offline = null)
        {
            if(layout == null) return;

            if (LayoutJson == null)
            {
                LayoutJson = layout;
            }
            else
            {
                LayoutJson.LayoutModels.AddRange(layout.LayoutModels);
            }

            if ((!offline.HasValue && NetworkDetector.RequestedOfflineMode) || (offline.HasValue && offline.Value))
            {
                // Process all offline models in parallel
                var modelTasks = layout.LayoutModels.Select(async layoutModelEntity =>
                {
                    if (PauseAddingModel)
                    {
                        await WaitForUnpause();
                    }

                    var offlineAssetInfo = StreamingUtils.ReturnOfflineAssetInfo(layoutModelEntity);
                    if (offlineAssetInfo == null)
                    {
                        StreamSceneUIController.ShowFailToAddModelToast?.Invoke();
                        LayoutJson.LayoutModels.Remove(layoutModelEntity);
                        return;
                    }

                    var targetName = string.Empty;

                    if (string.IsNullOrEmpty(layoutModelEntity.gameObjectName))
                    {
                        LayoutJson.LayoutModels.Remove(layoutModelEntity);
                    }
                    else
                    {
                        targetName = ReturnTargetName(layoutModelEntity);
                    }
                    
                    AddStreamModel?.Invoke(new()
                        {
                            Asset = offlineAssetInfo,
                            Properties = null
                        },
                        targetName,
                        layoutModelEntity.instanceNumber);
                }).ToList();

                // Wait for all models to be processed in batches of 5
                const int batchSize = 5;
                for (int i = 0; i < modelTasks.Count; i += batchSize)
                {
                    var batch = modelTasks.Skip(i).Take(batchSize);
                    await Task.WhenAll(batch);
                }

                PauseAddingModel = false;
                FinishedAddingModel?.Invoke();
            }
            else
            {
                // Process all online models in parallel
                var modelTasks = layout.LayoutModels.Select(async layoutModelEntity =>
                {
                    if (PauseAddingModel)
                    {
                        await WaitForUnpause();
                    }

                    if (string.IsNullOrEmpty(layoutModelEntity.orgID))
                    {
                        StreamSceneUIController.ShowFailToAddModelToast?.Invoke();
                        LayoutJson.LayoutModels.Remove(layoutModelEntity);
                        return;
                    }

                    IAssetRepository assetRepository = IdentityController.GuestMode?
                        PlatformServices.ServiceAccountAssetRepository : PlatformServices.AssetRepository;

                    try
                    {
                        var assetProject = await assetRepository.GetAssetProjectAsync(
                            new ProjectDescriptor(new OrganizationId(layoutModelEntity.orgID),
                                new ProjectId(layoutModelEntity.projectID)), CancellationToken.None);

                        if (assetProject == null)
                        {
                            StreamSceneUIController.ShowFailToAddModelToast?.Invoke();
                            LayoutJson.LayoutModels.Remove(layoutModelEntity);
                            return;
                        }

                        if (string.IsNullOrEmpty(layoutModelEntity.versionID))
                        {
                            var expectingVersion = layoutModelEntity.version == 0 ? 1 : layoutModelEntity.version;
                            SortingOrder order = layoutModelEntity.version == 0 ? SortingOrder.Ascending : SortingOrder.Descending;

                            var searchFilter = new AssetSearchFilter();
                            searchFilter.Include().Id.WithValue(layoutModelEntity.assetID);
                            var assets = assetProject.QueryAssetVersions(new AssetId(layoutModelEntity.assetID))
                                .OrderBy("versionNumber", order)
                                .WithCacheConfiguration(new AssetCacheConfiguration()
                                {
                                    CacheProperties = true
                                }).ExecuteAsync(CancellationToken.None);

                            IAsset selectedAsset = null;
                            AssetProperties? selectedAssetProperties = null;
                            await foreach (var asset in assets)
                            {
                                var versionsProperties = await asset.GetPropertiesAsync(CancellationToken.None);
                                if (versionsProperties.FrozenSequenceNumber == expectingVersion)
                                {
                                    selectedAsset = asset;
                                    selectedAssetProperties = versionsProperties;
                                    break;
                                }
                            }

                            if (selectedAsset == null)
                            {
                                Debug.LogWarning("Asset not found");
                                StreamSceneUIController.ShowFailToAddModelToast?.Invoke();
                                LayoutJson.LayoutModels.Remove(layoutModelEntity);
                                return;
                            }

                            var targetName = string.Empty;

                            if (string.IsNullOrEmpty(layoutModelEntity.gameObjectName))
                            {
                                LayoutJson.LayoutModels.Remove(layoutModelEntity);
                            }
                            else
                            {
                                targetName = ReturnTargetName(layoutModelEntity);
                            }

                            await HandleAddingModel(selectedAsset, selectedAssetProperties, targetName, layoutModelEntity.instanceNumber);
                        }
                        else
                        {
                            try
                            {
                                var selectedAsset = await assetProject.GetAssetAsync(
                                    new AssetId(layoutModelEntity.assetID),
                                    new AssetVersion(layoutModelEntity.versionID), CancellationToken.None);

                                if (selectedAsset == null)
                                {
                                    StreamSceneUIController.ShowFailToAddModelToast?.Invoke();
                                    LayoutJson.LayoutModels.Remove(layoutModelEntity);
                                    return;
                                }

                                var targetName = string.Empty;

                                if (string.IsNullOrEmpty(layoutModelEntity.gameObjectName))
                                {
                                    LayoutJson.LayoutModels.Remove(layoutModelEntity);
                                }
                                else
                                {
                                    targetName = ReturnTargetName(layoutModelEntity);
                                }

                                await HandleAddingModel(selectedAsset, null, targetName, layoutModelEntity.instanceNumber);
                            }
                            catch
                            {
                                StreamSceneUIController.ShowFailToAddModelToast?.Invoke();
                                LayoutJson.LayoutModels.Remove(layoutModelEntity);
                                return;
                            }
                        }
                    }
                    catch
                    {
                        StreamSceneUIController.ShowFailToAddModelToast?.Invoke();
                        LayoutJson.LayoutModels.Remove(layoutModelEntity);
                    }
                }).ToList();

                // Wait for all models to be processed in batches of 5
                const int batchSize = 5;
                for (int i = 0; i < modelTasks.Count; i += batchSize)
                {
                    var batch = modelTasks.Skip(i).Take(batchSize);
                    await Task.WhenAll(batch);
                }
                
                if (PauseAddingModel)
                {
                    await WaitForUnpause();
                }

                if (initializer == this && !FinishedInitialLoading)
                {
                    FinishedInitialLoading = true;
                }
                
                FinishedAddingModel?.Invoke();
            }

            return;

            async Task HandleAddingModel(
                IAsset asset,
                AssetProperties? properties,
                string targetName,
                int? instanceNumber)
            {
                var offlineAssetInfo = StreamingUtils.ReturnOfflineAssetInfo(asset);

                properties ??= await asset.GetPropertiesAsync(CancellationToken.None);
                
                AssetInfo onlineAsset = new AssetInfo()
                {
                    Asset = asset,
                    Properties = properties
                };

                if (offlineAssetInfo == null)
                {
                    //Add found Asset directly using cloud data
                    AddStreamModel?.Invoke(onlineAsset, targetName, instanceNumber);
                }
                else
                {
                    PauseAddingModel = true;

                    var offlineAsset = new AssetInfo()
                    {
                        Asset = offlineAssetInfo,
                        Properties = null
                    };

                    StreamSceneUIController.ShowPickSourceDialog?.Invoke(onlineAsset, offlineAsset, targetName);
                }
            }

            async Task WaitForUnpause()
            {
                while (PauseAddingModel)
                {
                    await Task.Yield(); // WebGL-friendly alternative to Task.Delay
                }
            }

            string ReturnTargetName(LayoutModelEntity layoutModelEntity)
            {
                if (layoutModelEntity.gameObjectName.Contains("@"))
                {
                    var streamingModels = new List<StreamingModel>();
                    TransformController.Instance.GetComponentsInChildren(true, streamingModels);
                    if (streamingModels.Any(streamingModel => streamingModel.gameObject.name == layoutModelEntity.gameObjectName))
                    {
                        layoutModelEntity.gameObjectName = ReturnGameObjectName(layoutModelEntity.assetID);
                    }
                }
                else
                {
                    layoutModelEntity.gameObjectName = ReturnGameObjectName(layoutModelEntity.gameObjectName);
                }

                return layoutModelEntity.gameObjectName;
            }
        }

        // Downloads and loads a GLB file for a streaming model
        private async Task DownloadAndLoadFile(AssetInfo assetInfo, IDataset dataset, Action<StreamingModel> onCompleted = null)
        {
            var files = dataset.ListFilesAsync(Range.All, CancellationToken.None);
            IFile fileToDownload = null;
            await foreach (var file in files)
            {
                if (!StreamingUtils.IsGLBFile(file)) continue;
                fileToDownload = file;
                break;
            }
            if (fileToDownload == null) return;
            LoadingGLBModel?.Invoke(true);

            // Download the file
            var finalPath = Path.Combine(Application.persistentDataPath, Path.GetFileName(fileToDownload.Descriptor.Path));
            await using var fileStream = File.OpenWrite(finalPath);
            await fileToDownload.DownloadAsync(fileStream, null, CancellationToken.None);

            // Load the file
            var model = m_Stage.Models.Add(x => x.FromUri(new Uri(finalPath)));
            var modelStream = HandleNewModelStream(assetInfo.Asset.Descriptor.AssetId.ToString(), string.Empty);
            modelStream.Initialize(model, assetInfo, dataset, false);
            TransformController.ModelAdded?.Invoke(modelStream.gameObject, model.Transform);
            onCompleted?.Invoke(modelStream);
            LoadingGLBModel?.Invoke(false);
        }
        
        private void OnRequestBoundsUpdate()
        {
            _ = BoundsUpdate();
            return;
            
            async Task BoundsUpdate()
            {
                try
                {
                    var bounds = await m_Stage.GetWorldBoundsAsync();
                        
                    var totalStreamModel = TransformController.Instance.GetComponentsInChildren<StreamingModel>(true)
                        .Length;

                    if (totalStreamModel == 0)
                    {
                        return;
                    }
                    
                    var newBounds = new DoubleBounds(bounds.Center, bounds.Size * 1.5f);
                        
                    if (!m_InitializedBounds)
                    {
                        m_InitializedBounds = true;
                        BoundsUpdated?.Invoke(newBounds, false);
                    }
                    else
                    {
                        BoundsUpdated?.Invoke(newBounds, true);
                        
                        if (IsLayoutAsset && !m_LayoutCameraInitialised && LayoutJson.LayoutModels.Count == 0)
                        {
                            m_LayoutCameraInitialised = true;
                            NavigationController.RequestDefaultHomeView?.Invoke();
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in BoundsUpdate: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        
        //  GetWorldBounds public version
        public DoubleBounds GetWorldBounds()
        {
            var bounds = m_Stage.GetWorldBounds();
            var newBounds = new DoubleBounds(bounds.Center, bounds.Size * 1.5f);
            return newBounds;
        }

        // Retrieves the streaming dataset for an asset
        private static async Task<IDataset> GetStreamingDataset(IAsset asset)
        {
            var cacheConfiguration = asset.CacheConfiguration;
            cacheConfiguration.DatasetCacheConfiguration = new DatasetCacheConfiguration()
            {
                CacheProperties = true
            };
            asset = await asset.WithCacheConfigurationAsync(cacheConfiguration, CancellationToken.None);
            var allDataset = asset.ListDatasetsAsync(Range.All, CancellationToken.None);
            IDataset targetDataset = null;

            IDataset sourceDataset = null;
            IDataset previewDataset = null;

            await foreach (var dataset in allDataset)
            {
                var datasetProperties = await dataset.GetPropertiesAsync(CancellationToken.None);
                if (datasetProperties.SystemTags.Contains(StreamingUtils.SourceTag))
                {
                    sourceDataset = dataset;
                    continue;
                }

                if (datasetProperties.SystemTags.Contains(StreamingUtils.PreviewTag))
                {
                    previewDataset = dataset;
                    continue;
                }

                if (datasetProperties.SystemTags.Contains(StreamingUtils.StreamableTag))
                {
                    targetDataset = dataset;
                }
            }

            if (targetDataset != null) return targetDataset;

            if (sourceDataset != null)
            {
                var hasGLB = await StreamingUtils.HasGLBFile(sourceDataset);
                if (hasGLB)
                {
                    return sourceDataset;
                }
            }

            if (previewDataset != null)
            {
                var hasGLB = await StreamingUtils.HasGLBFile(previewDataset);
                if (hasGLB)
                {
                    return previewDataset;
                }
            }

            return targetDataset;
        }

        // Handles the creation of a new model stream
        private StreamingModel HandleNewModelStream(string id, string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                targetName = ReturnGameObjectName(id);
            }

            var newModelObject = new GameObject(targetName)
            {
                tag = StreamingUtils.StreamModelTag
            };

            return newModelObject.AddComponent<StreamingModel>();
        }

        // Returns a unique name for the new game object
        private string ReturnGameObjectName(string id)
        {
            string newId;

            var isFirstModelCreated = TransformController.Instance.GetComponentInChildren<StreamingModel>() != null;

            if (isFirstModelCreated)
            {
                // Use a GUID to ensure uniqueness and avoid conflicts in multi-player mode
                newId = $"{id}@{Guid.NewGuid().ToString().Replace("-", string.Empty)}";
            }
            else
            {
                // Use @1 to have same name of main asset on all clients
                newId = $"{id}@1";
            }

            return newId;
        }
    }
}