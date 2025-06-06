using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Cloud.Identity;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Assets
{
    // This script is the main controller for managing assets in the Unity Cloud environment.
    // It handles various operations related to organizations, asset projects, collections, assets, datasets, and files.
    // The script uses Unity's MonoBehaviour and integrates with Unity Cloud services for asset management.
    
    public class AssetsController : MonoBehaviour
    {
        private AuthenticationState m_AuthenticationState;
        
#region Organizations
        // Manages organizations, including loading and selecting organizations.
        // Handles organization-related events and actions.
        List<IOrganization> m_AllOrganizations;
        public static IOrganization SelectedOrganization;
        public static Action<Action<List<IOrganization>>> RequestOrganizations;
        public static Action<List<IOrganization>> OrganizationsLoaded;
        private CancellationTokenSource m_OrganizationCancellationTokenSource;
#endregion
        
#region AssetProject
        // Manages asset projects, including loading and selecting asset projects.
        // Handles asset project-related events and actions.
        List<AssetProjectInfo> m_AllAssetProjects = new();
        public static AssetProjectInfo? SelectedAssetProject;
        public static Action<IOrganization, Action<List<AssetProjectInfo>>> RequestAssetProjects;
#endregion
        

        // Manages asset collections, including loading and selecting asset collections.
        // Handles asset collection-related events and actions.
#region Collection
        public static IAssetCollection SelectedCollection;
        public static Action<AssetProjectInfo, Action<List<IAssetCollection>>> GetAssetCollectionsForProject;
        private CancellationTokenSource m_CollectionCancellationTokenSource;
#endregion

#region Asset
        // Manages assets, including loading and selecting assets.
        // Handles asset-related events and actions.
        public static Action<bool, string> RequestAssets;
        public static Action<ProjectDescriptor, Action<bool>> CheckHaveWriteAccess;
        public static Action<string, string, AssetType, IOrganization, IAssetProject, IAssetCollection, List<string>> AssetCreation;
        public static Action<float> AssetCreationProgress;
        public static Action<AssetInfo> NewVersionAvailable;
        public static Action<SortingType, string> UpdateSortingType;
        
        public static Action<AssetInfo, Action<List<(string, string, bool)>>> GetLinkedProjects;
        
        private static AssetInfo? _selectedAsset, _selectedParentAsset, _newerVersionAsset;
        public static AssetInfo? SelectedAsset => _selectedAsset;
        public static AssetInfo? SelectedParentAsset => _selectedParentAsset;
        public static AssetInfo? NewerVersionAsset => _newerVersionAsset;
        
        public static Action<List<AssetInfo>> AssetsLoaded;
        public static Action<AssetInfo> AssetSelected;
        public static Action<AssetInfo?> ParentAssetSelected;
        public static Action AssetDeselected;
        public static Action<string> AssetSearch;
        public static Action<IAsset> AssetVersionRequest;
        public static Action<List<AssetInfo>> AssetVersionsLoaded;
        private CancellationTokenSource m_AssetRepositoryCancellationTokenSource;
        
        private readonly float m_VersionCheckInterval = 10f;
        private Coroutine m_VersionCheckCoroutine;
        private WaitForSeconds m_WaitForSeconds;
        private CancellationTokenSource m_VersionQueryTokenSource;
        private CancellationTokenSource m_VersionCheckerTokenSource;
        private SortingType m_SortingType;
        private readonly Permission m_AssetManagerCreatorPermission = new Permission("amc.assets.create");
#endregion

#region Dataset
        // Manages datasets, including loading and selecting datasets.
        // Handles dataset-related events and actions.
        private CancellationTokenSource m_DatasetTokenSource;
        public static Action<IAsset, Action<bool>> Trigger3DDSTransformation;
#endregion
        
        IOrganizationRepository m_OrganizationRepository => PlatformServices.OrganizationRepository;
        
        IAssetRepository m_AssetRepository => IdentityController.GuestMode? PlatformServices.ServiceAccountAssetRepository : PlatformServices.AssetRepository;
        
#region Service Account
        // Manages Unity Cloud service account organization.
        // Handles service account organization-related events and actions.
        public ServiceAccountOrganization ServiceAccountOrganization { get; private set; }
#endregion

        private bool m_Initialized;

        #region Unity Messages
        
        private void Awake()
        {
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
        }
        
        // Initializes the controller and subscribes to events.
        private void Start()
        {
            IdentityController.AuthenticationStateChangedEvent += OnAuthStateChanged;
        }

        // Unsubscribes from events when the controller is destroyed.
        private void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            IdentityController.AuthenticationStateChangedEvent -= OnAuthStateChanged;
            UnregisterActions();
        }

        #endregion

        private void UnregisterActions()
        {
            SharedUIManager.OrganizationSelected -= OnOrganizationSelected;
            SharedUIManager.AssetProjectSelected -= OnAssetProjectSelected;
            SharedUIManager.AssetCollectionSelected -= OnCollectionSelected;
            RequestOrganizations -= OnRequestOrganizations;
            RequestAssetProjects -= OnRequestAssetProjects;
            RequestAssets -= OnRequestAssets;
            
            GetAssetCollectionsForProject -= OnGetAssetCollectionsForProject;
            
            AssetCreation -= OnAssetCreation;
            AssetSelected -= OnAssetSelected;
            AssetDeselected -= OnAssetDeselected;
            UpdateSortingType -= OnUpdateSortingType;
            AssetSearch -= OnAssetSearch;
            
            AssetVersionRequest -= OnAssetVersionRequest;
            ParentAssetSelected -= OnParentVersionSelected;
            
            CheckHaveWriteAccess -= OnCheckAssetProjectHasWriteAccess;
            Trigger3DDSTransformation -= Trigger3DdsTransformation;
            
            GetLinkedProjects -= OnGetLinkedProjects;
        }
        
        private void OnNetworkStatusChanged(bool connected)
        {
            if (!connected)
            {
                if (m_Initialized)
                {
                    m_Initialized = false;
                    UnregisterActions();
                }
                
                m_CollectionCancellationTokenSource?.Cancel();
                m_CollectionCancellationTokenSource?.Dispose();
                m_CollectionCancellationTokenSource = null;
                
                m_OrganizationCancellationTokenSource?.Cancel();
                m_OrganizationCancellationTokenSource?.Dispose();
                m_OrganizationCancellationTokenSource = null;
                
                m_AssetRepositoryCancellationTokenSource?.Cancel();
                m_AssetRepositoryCancellationTokenSource?.Dispose();
                m_AssetRepositoryCancellationTokenSource = null;
                
                m_VersionQueryTokenSource?.Cancel();
                m_VersionQueryTokenSource?.Dispose();
                m_VersionQueryTokenSource = null;
                
                m_VersionCheckerTokenSource?.Cancel();
                m_VersionCheckerTokenSource?.Dispose();
                m_VersionCheckerTokenSource = null;
                
                m_DatasetTokenSource?.Cancel();
                m_DatasetTokenSource?.Dispose();
                m_DatasetTokenSource = null;
                return;
            }

            if (!m_Initialized)
            {
                m_Initialized = true;
                SharedUIManager.OrganizationSelected += OnOrganizationSelected;
                SharedUIManager.AssetProjectSelected += OnAssetProjectSelected;
                SharedUIManager.AssetCollectionSelected += OnCollectionSelected;
                RequestOrganizations += OnRequestOrganizations;
                RequestAssetProjects += OnRequestAssetProjects;
                RequestAssets += OnRequestAssets;
                
                GetAssetCollectionsForProject += OnGetAssetCollectionsForProject;
            
                AssetCreation += OnAssetCreation;
                AssetSelected += OnAssetSelected;
                AssetDeselected += OnAssetDeselected;
                UpdateSortingType += OnUpdateSortingType;
                AssetSearch += OnAssetSearch;
            
                AssetVersionRequest += OnAssetVersionRequest;
                ParentAssetSelected += OnParentVersionSelected;
            
                CheckHaveWriteAccess += OnCheckAssetProjectHasWriteAccess;
                Trigger3DDSTransformation += Trigger3DdsTransformation;
            
                GetLinkedProjects += OnGetLinkedProjects;
            }

            m_SortingType = SortingType.Name;
            
            if (m_AuthenticationState == AuthenticationState.LoggedIn)
            {
                if (m_AllOrganizations == null)
                {
                    RequestOrganizations?.Invoke((result) => OrganizationsLoaded?.Invoke(result));
                }
                else
                {
                    OrganizationsLoaded?.Invoke(m_AllOrganizations);
                }
            }
        }

        #region IDataset

        // Handles the request to trigger a 3D data streaming transformation for an asset.
        private void Trigger3DdsTransformation(IAsset asset, Action<bool> callback)
        {
            _ = TriggerTransformation();
            return;

            async Task TriggerTransformation()
            {
                var listOfDatasets = asset.ListDatasetsAsync(Range.All, CancellationToken.None);
                IDataset sourceDataset = null;
                await foreach (var dataset in listOfDatasets)
                {
                    if (dataset.SystemTags.Contains("Source"))
                    {
                        sourceDataset = dataset;
                        break;
                    }
                }
                
                if (sourceDataset == null)
                {
                    callback?.Invoke(false);
                    return;
                }
                
                IAsyncEnumerable<ITransformation> transformations = sourceDataset.ListTransformationsAsync(Range.All, CancellationToken.None);
                await foreach(var transformation in transformations)
                {
                    if (transformation.Status is TransformationStatus.Pending or TransformationStatus.Running && 
                        transformation.WorkflowType == WorkflowType.Data_Streaming)
                    {
                        callback?.Invoke(false);
                        return;
                    }
                }
                
                IAsset targetAsset = asset;
                if (targetAsset.State == AssetState.Frozen)
                {
                    targetAsset = await asset.CreateUnfrozenVersionAsync(CancellationToken.None);
                    float elapsed = 0f;
                    while (elapsed < 1f)
                    {
                        await Task.Yield();
                        elapsed += Time.deltaTime;
                    }
                }
                
                sourceDataset = null;
                listOfDatasets = targetAsset.ListDatasetsAsync(Range.All, CancellationToken.None);
                await foreach (var dataset in listOfDatasets)
                {
                    if (dataset.SystemTags.Contains("Source"))
                    {
                        sourceDataset = dataset;
                        break;
                    }
                }
                var transformationCreation = new TransformationCreation()
                {
                    WorkflowType = WorkflowType.Data_Streaming
                };
                var transformationDescriptor = await sourceDataset.StartTransformationAsync(transformationCreation, CancellationToken.None);
                if (transformationDescriptor.Status == TransformationStatus.Pending)
                {
                    IAssetFreeze freeze = new AssetFreeze("Freeze after transformation")
                    {
                        Operation = AssetFreezeOperation.WaitOnTransformations,
                        ChangeLog = "Freeze after transformation"
                    };
                    await targetAsset.FreezeAsync(freeze, CancellationToken.None);
                    callback.Invoke(true);
                }
            }
        }
        
#endregion

#region IAsset

        private void OnGetLinkedProjects(AssetInfo assetInfo, Action<List<(string name, string id, bool source)>> callback)
        {
            if(m_AllAssetProjects == null || m_AllAssetProjects.Count == 0)
            {
                callback?.Invoke(null);
                return;
            }

            _ = GetProjects();
            return;

            async Task GetProjects()
            {
                List<(string name, string id, bool source)> linkedProjects = new List<(string name, string id, bool source)>();
                AssetProjectInfo? sourceProject =
                    m_AllAssetProjects.FirstOrDefault(x => x.AssetProject.Descriptor == assetInfo.Properties.Value.SourceProject);
                var assetRepository = IdentityController.GuestMode? PlatformServices.ServiceAccountAssetRepository : PlatformServices.AssetRepository;
                if (sourceProject.HasValue)
                {
                    var assetProject =
                        await assetRepository.GetAssetProjectAsync(sourceProject.Value.AssetProject.Descriptor, CancellationToken.None);
                    if (assetProject != null)
                    {
                        var property = await assetProject.GetPropertiesAsync(CancellationToken.None);
                        linkedProjects.Add((property.Name, assetProject.Descriptor.ProjectId.ToString(), true));
                    }
                }
                
                foreach (var assetProject in assetInfo.Properties.Value.LinkedProjects.SkipWhile(x => x.ProjectId == assetInfo.Properties.Value.SourceProject.ProjectId))
                {
                    if (m_AllAssetProjects.All(x => x.AssetProject.Descriptor != assetProject)) continue;
                    var project = m_AllAssetProjects.FirstOrDefault(x => x.AssetProject.Descriptor == assetProject);
                    
                    var properties = await project.AssetProject.GetPropertiesAsync(CancellationToken.None);
                    
                    linkedProjects.Add((properties.Name, project.AssetProject.Descriptor.ProjectId.ToString(), false));
                }
            
                callback?.Invoke(linkedProjects);
            }
        }

        /// <summary>
        /// Checks if the specified asset project has write access for the given organization.
        /// </summary>
        /// <param name="org">The organization to check roles and permissions for.</param>
        /// <param name="assetProject">The asset project to check permissions against.</param>
        /// <param name="callback">The callback to invoke with the result of the permission check.</param>
        private void OnCheckAssetProjectHasWriteAccess(ProjectDescriptor projectDescriptor, Action<bool> callback)
        {
            // Write permission will always be true until we have a better way to check
            callback?.Invoke(true);
        }
        
        private void OnUpdateSortingType(SortingType type, string searchText)
        {
            if(m_SortingType == type) return;
            m_SortingType = type;
            OnRequestAssets(!SharedUIManager.AssetProjectInfo.HasValue, searchText);
        }
        
        private void OnParentVersionSelected(AssetInfo? parentAsset)
        {
            _selectedParentAsset = parentAsset;
        }
        
        /// <summary>
        /// Handles the request to retrieve all versions of a specified asset.
        /// </summary>
        /// <param name="asset">The asset for which to retrieve versions.</param>
        private void OnAssetVersionRequest(IAsset asset)
        {
            _ = GetAssetVersions();
            
            return;

            // Asynchronously retrieves all versions of the specified asset, ordered by version number in descending order.
            // Only includes versions that are in the Frozen state.
            async Task GetAssetVersions()
            {
                m_VersionQueryTokenSource?.Cancel();
                m_VersionQueryTokenSource = new CancellationTokenSource();
                
                var assetProject = await m_AssetRepository.GetAssetProjectAsync(asset.Descriptor.ProjectDescriptor, m_VersionQueryTokenSource.Token);

                var searchFilter = new AssetSearchFilter();
                
                searchFilter.Include().State.WithValue(AssetState.Frozen);

                var cacheConfiguration = new AssetCacheConfiguration()
                {
                    CacheProperties = true,
                    CachePreviewUrl = true,
                };
                
                var query = assetProject.QueryAssetVersions(asset.Descriptor.AssetId).
                    OrderBy("versionNumber", SortingOrder.Descending).SelectWhereMatchesFilter(searchFilter).
                    WithCacheConfiguration(cacheConfiguration).ExecuteAsync(m_VersionQueryTokenSource.Token);
                
                List<AssetInfo> resultVersions = new List<AssetInfo>();
                await foreach (var version in query)
                {
                    var properties = await version.GetPropertiesAsync(m_VersionQueryTokenSource.Token);
                    resultVersions.Add(new AssetInfo()
                    {
                        Asset = version,
                        Properties = properties
                    });
                }
                AssetVersionsLoaded?.Invoke(resultVersions);
            }
        }
        
        // Handles the creation of a new asset, including uploading associated files and freezing the asset.
        // The method first creates the asset, then uploads each file to the asset's dataset, and finally freezes the asset.
        // Progress of the asset creation is reported through the AssetCreationProgress event.
        private void OnAssetCreation(string assetName, string description, AssetType type, IOrganization org,
            IAssetProject project, IAssetCollection collection, List<string> files)
        {
            _ = CreateAsset();
            return;
            
            async Task CreateAsset()
            {
                Debug.Log($"{org.Name} - {project.Name} - {assetName} - {description} - {type}");
                
                var newAssetCreation = new AssetCreation(assetName)
                {
                    Description = description,
                    Type = type,
                };

                if (collection != null)
                {
                    newAssetCreation.Collections = new List<CollectionPath>() {collection.Descriptor.Path};
                }
                
                m_AssetRepositoryCancellationTokenSource?.Cancel();
                m_AssetRepositoryCancellationTokenSource = new CancellationTokenSource();
                
                var newAsset = await project.CreateAssetAsync(newAssetCreation, m_AssetRepositoryCancellationTokenSource.Token);
                if (newAsset == null || files == null || files.Count == 0)
                {
                    if (newAsset != null)
                    {
                        await WaitToFreeze(newAsset, AssetFreezeOperation.CancelTransformations);
                    }
                    AssetCreationProgress?.Invoke(1f);
                    return;
                }

                var progress = 1f / ((float)files.Count + 1f);
                AssetCreationProgress?.Invoke(progress);
                
                m_DatasetTokenSource?.Cancel();
                m_DatasetTokenSource = new CancellationTokenSource();

                var datasets = newAsset.ListDatasetsAsync(Range.All, m_DatasetTokenSource.Token);

                var dataSetsList = new List<IDataset>();
                
                await foreach (var dataset in datasets)
                {
                    dataSetsList.Add(dataset);
                }
                
                IDataset sourceDataset = dataSetsList.FirstOrDefault();

                if (sourceDataset == null)
                {
                    Debug.LogError($"No datasets found for created asset {newAsset.Name}.");
                    AssetCreationProgress?.Invoke(1f);
                    return;
                }
                
                for (var i = 0; i < files.Count; i++)
                {
                    try
                    {
                        var filepath = Path.GetFileName(files[i]);
                        var fileCreation = new FileCreation(filepath)
                        {
                            Path = filepath,
                            Description = "",
                            Tags = new List<string>(){ newAsset.Type.GetValueAsString() }
                        };
                        //Debug.Log($"File Path: {filepath}, File:{files[i]}");
                        
                        m_DatasetTokenSource?.Cancel();
                        m_DatasetTokenSource = new CancellationTokenSource();
                        
                        await using (var fileStream = File.OpenRead(files[i]))
                        {
                            await sourceDataset.UploadFileAsync(fileCreation, fileStream, null, m_DatasetTokenSource.Token);
                        }
                        var progressUpdate = 1 + i / ((float)files.Count + 1);
                        if (progressUpdate < 1f)
                        {
                            AssetCreationProgress?.Invoke(progressUpdate);
                        }
                    } catch(UploadFailedException e)
                    {
                        Debug.LogError(e);
                    }
                    catch(Exception e)
                    {
                        Debug.LogError(e);
                    }
                }
                
                await WaitToFreeze(newAsset, AssetFreezeOperation.WaitOnTransformations);
                
                AssetCreationProgress?.Invoke(1f);
            }

            async Task WaitToFreeze(IAsset newAsset, AssetFreezeOperation operation)
            {
                IAssetFreeze newAssetFreeze = new AssetFreeze("Initial Version")
                {
                    Operation = operation
                };
                m_AssetRepositoryCancellationTokenSource?.Cancel();
                m_AssetRepositoryCancellationTokenSource = new CancellationTokenSource();
                await newAsset.FreezeAsync(newAssetFreeze, m_AssetRepositoryCancellationTokenSource.Token);
            }
        }

        private void OnAssetDeselected()
        {
            _selectedAsset = null;
            _selectedParentAsset = null;
            _newerVersionAsset = null;
            if (m_VersionCheckCoroutine != null)
            {
                m_VersionCheckerTokenSource?.Cancel();
                m_VersionCheckerTokenSource = null;
                StopCoroutine(m_VersionCheckCoroutine);
            }
        }

        /// <summary>
        /// Handles the search for assets based on the provided asset name.
        /// Cancels any ongoing asset repository operations and initiates a new search.
        /// The search results are filtered by the asset name and the selected project and collection.
        /// </summary>
        /// <param name="assetName">The name of the asset to search for.</param>
        private void OnAssetSearch(string assetName)
        {
            _ = RequestAssetsAsync(!SharedUIManager.AssetProjectInfo.HasValue, assetName);
        }

        private void OnAssetSelected(AssetInfo asset)
        {
            _selectedAsset = asset;
            _selectedParentAsset = null;
            _newerVersionAsset = null;
            
            if (m_VersionCheckCoroutine != null)
            {
                m_VersionCheckerTokenSource?.Cancel();
                m_VersionCheckerTokenSource = null;
                StopCoroutine(m_VersionCheckCoroutine);
            }

            m_VersionCheckCoroutine = StartCoroutine(StartVersionChecking());
        }
        
        private IEnumerator StartVersionChecking()
        {
            m_WaitForSeconds ??= new WaitForSeconds(m_VersionCheckInterval);
            while (true)
            {
                if (_selectedAsset == null)
                {
                    yield break;
                }
                yield return new WaitForSeconds(m_VersionCheckInterval);
                var versionCheckTask = CheckAssetVersionsAsync();
                yield return new WaitUntil(() => versionCheckTask.IsCompleted);
                if (versionCheckTask.Exception != null)
                {
                    Debug.LogError(versionCheckTask.Exception);
                }
            }
        }

        private async Task CheckAssetVersionsAsync()
        {
            m_VersionCheckerTokenSource?.Cancel();
            m_VersionCheckerTokenSource = new CancellationTokenSource();

            var currentAssetVersion = _selectedAsset.Value.Properties.Value.FrozenSequenceNumber;
            
            var assetProject = await 
                m_AssetRepository.GetAssetProjectAsync(_selectedAsset.Value.Asset.Descriptor.ProjectDescriptor,
                    CancellationToken.None);
            if(m_VersionCheckerTokenSource.IsCancellationRequested) return;
            var latestVersion = await 
                assetProject.GetAssetWithLatestVersionAsync(_selectedAsset.Value.Asset.Descriptor.AssetId,
                    CancellationToken.None);
            if(m_VersionCheckerTokenSource.IsCancellationRequested) return;
            var latestVersionAssetProperty = await latestVersion.GetPropertiesAsync(CancellationToken.None);
            if(m_VersionCheckerTokenSource.IsCancellationRequested) return;
            if (latestVersionAssetProperty.FrozenSequenceNumber > currentAssetVersion)
            {
                _newerVersionAsset = new AssetInfo()
                {
                    Asset = latestVersion,
                    Properties = latestVersionAssetProperty
                };
                NewVersionAvailable?.Invoke(_newerVersionAsset.Value);
            }
        }
        
        /// <summary>
        /// Handles the request to load assets based on the specified criteria.
        /// Cancels any ongoing asset repository operations and initiates a new request.
        /// The assets are filtered and ordered based on the provided parameters.
        /// </summary>
        /// <param name="allAssets">If true, loads all assets; otherwise, loads assets based on the selected project and collection.</param>
        private void OnRequestAssets(bool allAssets, string searchText = "")
        {
            _ = RequestAssetsAsync(allAssets, searchText);
        }
        
        private async Task RequestAssetsAsync(bool allAssets, string assetName)
        {
            m_AssetRepositoryCancellationTokenSource?.Cancel();
            IAsyncEnumerable<IAsset> assets = null;
            
            m_AssetRepositoryCancellationTokenSource = new CancellationTokenSource();
            
            var searchFilter = new AssetSearchFilter();
            
            if (!string.IsNullOrEmpty(assetName))
            {
                searchFilter.Include().Name.WithValue(new StringPredicate(assetName, StringSearchOption.Wildcard));
            }
            
            searchFilter.Any().Datasets.SystemTags.WithValue("Streamable");
            searchFilter.Any().Tags.WithValue("Layout");

            var cacheConfiguration = new AssetCacheConfiguration()
            {
                CacheProperties = true,
                CachePreviewUrl = true
            };
            SortingOrder sortingOrder = m_SortingType == SortingType.Upload_date || m_SortingType == SortingType.Last_modified? SortingOrder.Descending : SortingOrder.Ascending;
            switch (allAssets)
            {
                case true:
                {
                    if (SceneManager.GetActiveScene() == gameObject.scene)
                    {
                        SelectedCollection = null;
                        SelectedAssetProject = null;
                    }

                    m_AllAssetProjects = await GetAssetProjects(SharedUIManager.Organization);
                    
                    assets = m_AssetRepository.QueryAssets(m_AllAssetProjects.Select(p => p.AssetProject.Descriptor))
                        .SelectWhereMatchesFilter(searchFilter).OrderBy(m_SortingType.GetPropertyName(), sortingOrder)
                        .WithCacheConfiguration(cacheConfiguration)
                        .ExecuteAsync(m_AssetRepositoryCancellationTokenSource.Token);
                    break;
                }
                case false when SharedUIManager.AssetProjectInfo.HasValue && SharedUIManager.AssetCollection == null:
                {
                    assets = SharedUIManager.AssetProjectInfo.Value.AssetProject.QueryAssets().SelectWhereMatchesFilter(searchFilter)
                        .OrderBy(m_SortingType.GetPropertyName(), sortingOrder)
                        .WithCacheConfiguration(cacheConfiguration)
                        .ExecuteAsync(m_AssetRepositoryCancellationTokenSource.Token);
                    break;
                }

                case false when SharedUIManager.AssetProjectInfo.HasValue && SharedUIManager.AssetCollection != null:
                {
                    searchFilter.Collections.WhereContains(SharedUIManager.AssetCollection.Descriptor.Path);
                    assets = SharedUIManager.AssetProjectInfo.Value.AssetProject.QueryAssets().SelectWhereMatchesFilter(searchFilter)
                        .OrderBy(m_SortingType.GetPropertyName())
                        .WithCacheConfiguration(cacheConfiguration)
                        .ExecuteAsync(m_AssetRepositoryCancellationTokenSource.Token);
                    break;
                }
            }
            
            if (m_AssetRepositoryCancellationTokenSource.IsCancellationRequested) return;

            List<AssetInfo> assetsToBatchProcess = new List<AssetInfo>();
            int count = 0;

            if (assets != null)
            {
                await foreach (var asset in assets)
                {
                    if (m_AssetRepositoryCancellationTokenSource.IsCancellationRequested) return;
                    count++;
                    var assetProperty = await asset.GetPropertiesAsync(m_AssetRepositoryCancellationTokenSource.Token);
                    assetsToBatchProcess.Add(new AssetInfo()
                    {
                        Asset = asset,
                        Properties = assetProperty
                    });
                    if (m_AssetRepositoryCancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }
                    if (count % 98 == 0) AssetsLoaded?.Invoke(assetsToBatchProcess);
                }
                if (m_AssetRepositoryCancellationTokenSource.IsCancellationRequested) return;
                AssetsLoaded?.Invoke(assetsToBatchProcess.Count == 0 ? null : assetsToBatchProcess);
            }
        }

#endregion
        
#region ICollection
        private void OnCollectionSelected(IAssetCollection assetCollection)
        {
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SelectedCollection = assetCollection;
            }
            OnRequestAssets(false);
        }
#endregion

#region IAssetProject

        private void OnRequestAssetProjects(IOrganization organization, Action<List<AssetProjectInfo>> callback)
        {
            _ = GetProjects();
            return;

            async Task GetProjects()
            {
                var results = await GetAssetProjects(organization);
                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    m_AllAssetProjects = results;
                }
                callback?.Invoke(results);
            }
        }
        
        /// <summary>
        /// Retrieves a list of asset projects for the specified organization.
        /// </summary>
        /// <param name="selectedOrg">The organization for which to retrieve asset projects.</param>
        /// <returns>A list of asset projects associated with the specified organization.</returns>
        private async Task<List<AssetProjectInfo>> GetAssetProjects(IOrganization selectedOrg)
        {
            var tempAssetProjects = new List<AssetProjectInfo>();
            
            m_AssetRepositoryCancellationTokenSource?.Cancel();
            m_AssetRepositoryCancellationTokenSource = new CancellationTokenSource();
            
            var orgID = selectedOrg.Id;
            
            AssetProjectCacheConfiguration cacheConfiguration = new AssetProjectCacheConfiguration()
            {
                CacheProperties = true,
            };
            var assetProjectsAsyncEnumerable = m_AssetRepository.QueryAssetProjects(orgID)
                .WithCacheConfiguration(cacheConfiguration).ExecuteAsync(CancellationToken.None);
            
            await foreach (var assetProject in assetProjectsAsyncEnumerable)
            {
                var assetProjectProperties =
                    await assetProject.GetPropertiesAsync(m_AssetRepositoryCancellationTokenSource.Token);
                tempAssetProjects.Add(new AssetProjectInfo()
                {
                    AssetProject = assetProject,
                    Properties = assetProjectProperties
                });
            }

            return tempAssetProjects;
        }

        /// <summary>
        /// Handles the request to retrieve all collections for a specified asset project.
        /// Cancels any ongoing collection operations and initiates a new request.
        /// The collections are loaded asynchronously and the provided callback is invoked with the list of collections.
        /// </summary>
        /// <param name="assetProject">The asset project for which to retrieve collections.</param>
        /// <param name="collectionsLoaded">The callback to invoke with the list of collections.</param>
        private void OnGetAssetCollectionsForProject(AssetProjectInfo assetProject,
            Action<List<IAssetCollection>> collectionsLoaded)
        {
            if (collectionsLoaded != null)
            {
                _ = ListAllCollections();
            }
            
            OnRequestAssets(false);
            return;
            
            async Task ListAllCollections()
            {
                List<IAssetCollection> collections = new List<IAssetCollection>();
                
                m_CollectionCancellationTokenSource?.Cancel();
                m_CollectionCancellationTokenSource = new CancellationTokenSource();

                var collectionsEnumerable = assetProject.AssetProject.QueryCollections()
                    .ExecuteAsync(m_CollectionCancellationTokenSource.Token);
                
                await foreach (var collection in collectionsEnumerable)
                {
                    collections.Add(collection);
                }
                collectionsLoaded?.Invoke(collections);
            }
        }
        
        private void OnAssetProjectSelected(AssetProjectInfo? assetProject)
        {
            if (SceneManager.GetActiveScene() != gameObject.scene) return;
            SelectedAssetProject = assetProject;
            SelectedCollection = null;
        }
        
#endregion
        
#region IOrganization

        private void OnOrganizationSelected(IOrganization organization)
        {
            if (gameObject.scene == SceneManager.GetActiveScene())
            {
                SelectedOrganization = organization;
                AssetDeselected.Invoke();
            }
        }

        private void OnRequestOrganizations(Action<List<IOrganization>> callback)
        {
            _ = GetOrganizations(callback);
        }
        
        /// <summary>
        /// Retrieves all organizations and invokes the OrganizationsLoaded event with the list of organizations.
        /// If not in guest mode, it fetches the organizations from the repository asynchronously.
        /// If in guest mode, it adds the service account organization to the list.
        /// </summary>
        private async Task GetOrganizations(Action<List<IOrganization>> callback)
        {
            m_AllOrganizations ??= new List<IOrganization>();
            m_AllOrganizations?.Clear();

            if (!IdentityController.GuestMode)
            {
                m_OrganizationCancellationTokenSource?.Cancel();
                m_OrganizationCancellationTokenSource = new CancellationTokenSource();
            
                var organizationsAsyncEnumerable = m_OrganizationRepository.ListOrganizationsAsync(Range.All, m_OrganizationCancellationTokenSource.Token);
                await foreach (var organization in organizationsAsyncEnumerable)
                {
                    m_AllOrganizations.Add(organization);
                }
            }
            else
            {
                m_AllOrganizations.Add(ServiceAccountOrganization);
            }
            callback?.Invoke(m_AllOrganizations);
        }

#endregion
        
        private void OnAuthStateChanged(AuthenticationState state)
        {
            m_AuthenticationState = state;
            if (state == AuthenticationState.LoggedIn)
            {
                _ = GetOrganizations((result) => OrganizationsLoaded?.Invoke(result));
            } else if (state is AuthenticationState.AwaitingLogout or AuthenticationState.LoggedOut)
            {
                if (PlatformServices.ServiceAccountCredentials != null)
                {
                    ServiceAccountOrganization = new ServiceAccountOrganization(PlatformServices.ServiceAccountCredentials.OrganizationId,
                        PlatformServices.ServiceAccountCredentials.OrganizationName);
                }
                
                m_AllOrganizations?.Clear();
                m_AllAssetProjects?.Clear();
                SelectedOrganization = null;
                SelectedAssetProject = null;
                SelectedCollection = null;
                _selectedAsset = null;
                AssetDeselected?.Invoke();
                
                m_AssetRepositoryCancellationTokenSource?.Cancel();
                m_AssetRepositoryCancellationTokenSource?.Dispose();
                m_AssetRepositoryCancellationTokenSource = null;
                
                m_VersionQueryTokenSource?.Cancel();
                m_VersionQueryTokenSource?.Dispose();
                m_VersionQueryTokenSource = null;
                
                m_VersionCheckerTokenSource?.Cancel();
                m_VersionCheckerTokenSource?.Dispose();
                m_VersionCheckerTokenSource = null;
                
                m_DatasetTokenSource?.Cancel();
                m_DatasetTokenSource?.Dispose();
                m_DatasetTokenSource = null;
                
                m_OrganizationCancellationTokenSource?.Cancel();
                m_OrganizationCancellationTokenSource?.Dispose();
                m_OrganizationCancellationTokenSource = null;
                
                m_CollectionCancellationTokenSource?.Cancel();
                m_CollectionCancellationTokenSource?.Dispose();
                m_CollectionCancellationTokenSource = null;
            }
        }
    }
}
