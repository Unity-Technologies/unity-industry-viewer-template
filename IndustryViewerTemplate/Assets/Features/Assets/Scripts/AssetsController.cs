using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Assets
{
    // This script is the main controller for managing assets in the Unity Cloud environment.
    // It handles various operations related to organizations, asset projects, collections, assets, datasets, and files.
    // The script uses Unity's MonoBehaviour and integrates with Unity Cloud services for asset management.
    public class AssetsController : MonoBehaviour
    {
        public class AssetCreationParameters
        {
            public IOrganization Organization;
            public IAssetProject Project;
            public IAssetCollection Collection;

            public string AssetName;
            public string AssetDescription;
            public AssetType AssetType;
            public List<string> Tags;
            public string FileName;

            public bool DoVersionFreeze;
        }

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
        public static Action<IOrganization, Action<IOrganization, List<AssetProjectInfo>>> RequestAssetProjects;
        private CancellationTokenSource m_ProjectCancellationTokenSource;
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
        public static Action<AssetCreationParameters> AssetCreation;
        public static Action<AssetCreationParameters, IAsset, float?, string, CancellationTokenSource> AssetCreationProgress;
        public static Action<AssetInfo> NewVersionAvailable;
        public static Action<SortingType, string> UpdateSortingType;
        public static Action<bool> PauseResumeVersionChecking;
        
        public static Action<AssetInfo, Action<List<(string, string, bool)>>> GetLinkedProjects;
        
        private static AssetInfo? _selectedAsset, _selectedParentAsset, _newerVersionAsset;
        public static AssetInfo? SelectedAsset => _selectedAsset;
        public static AssetInfo? SelectedParentAsset => _selectedParentAsset;
        public static AssetInfo? NewerVersionAsset => _newerVersionAsset;
        
        public static Action<List<AssetInfo>> AssetsLoaded;
        public static Action<IAssetProject, CollectionDescriptor?> AllAssetsLoaded;
        public static Action<AssetInfo> AssetSelected;
        public static Action<AssetInfo?> ParentAssetSelected;
        public static Action AssetDeselected;
        public static Action<string> AssetSearch;
        public static Action<IAsset, Action<List<AssetInfo>>> AssetVersionRequest;
        private CancellationTokenSource m_AssetRepositoryCancellationTokenSource;
        private CancellationTokenSource m_VersionQueryTokenSource;

        private const float m_VersionCheckInterval = 10f;
        private Coroutine m_VersionCheckCoroutine;
        private static CancellationTokenSource m_VersionCheckerTokenSource;
        private static bool m_IsCheckingForNewVersionEnabled = true;
        public static bool IsCheckingForNewVersionEnabled
        {
            get => m_IsCheckingForNewVersionEnabled;
            set
            {
                m_IsCheckingForNewVersionEnabled = value;
                if (!value)
                {
                    TaskUtils.CancelTokenSource(ref m_VersionCheckerTokenSource);
                }
            }
        }

        private SortingType m_SortingType;
#endregion

#region Dataset
        // Manages datasets, including loading and selecting datasets.
        // Handles dataset-related events and actions.
        private CancellationTokenSource m_DatasetTokenSource;
        public static Action<IAsset, bool, Action<TransformationProperties?, string, CancellationTokenSource>> Trigger3DDSTransformation;
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
            PauseResumeVersionChecking -= OnPauseResumeVersionChecking;
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
                
                TaskUtils.CancelTokenSource(ref m_CollectionCancellationTokenSource);
                TaskUtils.CancelTokenSource(ref m_OrganizationCancellationTokenSource);
                TaskUtils.CancelTokenSource(ref m_AssetRepositoryCancellationTokenSource);
                TaskUtils.CancelTokenSource(ref m_VersionQueryTokenSource);
                TaskUtils.CancelTokenSource(ref m_VersionCheckerTokenSource);
                TaskUtils.CancelTokenSource(ref m_DatasetTokenSource);
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
                PauseResumeVersionChecking += OnPauseResumeVersionChecking;
                
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
            if (_selectedAsset.HasValue)
            {
                CancelVersionChecking();

                m_VersionCheckCoroutine = StartCoroutine(StartVersionChecking());
            }
            
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

        private void OnPauseResumeVersionChecking(bool pause)
        {
            if (pause)
            {
                CancelVersionChecking();
            }
            else
            {
                CancelVersionChecking();
                m_VersionCheckCoroutine = StartCoroutine(StartVersionChecking());
            }
        }

        private void CancelVersionChecking()
        {
            if (m_VersionCheckCoroutine != null)
            {
                TaskUtils.CancelTokenSource(ref m_VersionCheckerTokenSource);
                StopCoroutine(m_VersionCheckCoroutine);
            }
        }

        #region IDataset

        // Handles the request to trigger a 3D data streaming transformation for an asset.
        private void Trigger3DdsTransformation(IAsset asset, bool waitForTransformationFinalState, Action<TransformationProperties?, string, CancellationTokenSource> callback)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            callback?.Invoke(null, null, cancellationTokenSource);
            _ = TriggerTransformation(cancellationTokenSource.Token);
            return;

            async Task TriggerTransformation(CancellationToken cancellationToken)
            {
                try
                {
                    var sourceDataset = await asset.GetSourceDatasetAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (sourceDataset == null)
                    {
                        callback?.Invoke(null, "Source dataset not found", null);
                        return;
                    }

                    var transformations = sourceDataset.ListTransformationsAsync(Range.All, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    await foreach (var existingTransformation in transformations)
                    {
                        var exisitingTransformationProperties = await existingTransformation.GetPropertiesAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (exisitingTransformationProperties.WorkflowType == WorkflowType.Data_Streaming
                            && (exisitingTransformationProperties.Status is TransformationStatus.Pending or TransformationStatus.Running or TransformationStatus.Queued))
                        {
                            callback?.Invoke(exisitingTransformationProperties, null, null);
                            if (waitForTransformationFinalState) await WaitForTransaction(existingTransformation);
                            return;
                        }
                    }
                    var assetProperties = await asset.GetPropertiesAsync(cancellationToken);
                    if (assetProperties.State == AssetState.Frozen)
                    {
                        Debug.Log("Asset Creation: 3DDS, creating unfrozen version...");
                        asset = await asset.CreateUnfrozenVersionAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        sourceDataset = await asset.GetSourceDatasetAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var transformationCreation = new TransformationCreation()
                    {
                        WorkflowType = WorkflowType.Data_Streaming
                    };

                    Debug.Log("Asset Creation: 3DDS, starting transformation...");
                    var transformationDescriptor = await sourceDataset.StartTransformationAsync(transformationCreation, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    var transformation = await sourceDataset.GetTransformationAsync(transformationDescriptor.Descriptor.TransformationId, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    var transformationProperties = await transformation.GetPropertiesAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    // This gives opportunity to cancel freezing from callback if needed
                    callback.Invoke(transformationProperties, null, null);
                    cancellationToken.ThrowIfCancellationRequested();

                    Debug.Log($"Asset Creation: 3DDS, transformation started with status {transformationProperties.Status}");
                    if (transformationProperties.Status is
                           TransformationStatus.Pending
                        or TransformationStatus.Running
                        or TransformationStatus.Queued
                        or TransformationStatus.Succeeded)
                    {
                        Debug.Log("Asset Creation: 3DDS, request freezing asset after transformation...");
                        var freeze = new AssetFreeze("Freeze after transformation")
                        {
                            Operation = AssetFreezeOperation.WaitOnTransformations,
                            ChangeLog = "Freeze after transformation"
                        };

                        await asset.FreezeAsync(freeze, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (waitForTransformationFinalState) await WaitForTransaction(transformation);
                }
                catch (OperationCanceledException)
                {
                    //Debug.Log($"Asset Creation: 3DDS Task for '{asset.Name}' was canceled.");
                    callback?.Invoke(null, "Operation canceled", null);
                    return;
                }
                catch (Exception exception)
                {
                    //Debug.LogError($"Asset Creation: exception in 3DDS Task for '{asset.Name}': {exception}");
                    callback?.Invoke(null, exception.Message, null);
                    return;
                }

                return;

                async Task WaitForTransaction(ITransformation transformation)
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        await transformation.RefreshAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        var transformationProperites = await transformation.GetPropertiesAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        callback?.Invoke(transformationProperites, null, null);

                        if (transformationProperites.Status is
                            TransformationStatus.Succeeded
                            or TransformationStatus.Failed
                            or TransformationStatus.Error
                            or TransformationStatus.Terminated
                            or TransformationStatus.Skipped
                            or TransformationStatus.TimedOut)
                        {
                            Debug.Log($"Asset Creation: 3DDS, transformation ended with status {transformationProperites.Status}");
                            break;
                        }
                    }
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
                
                foreach (var assetProject in assetInfo.Properties.Value.LinkedProjects.Where(x => x.ProjectId != assetInfo.Properties.Value.SourceProject.ProjectId))
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
        private void OnAssetVersionRequest(IAsset asset, Action<List<AssetInfo>> callback)
        {
            _ = GetAssetVersions();
            
            return;

            // Asynchronously retrieves all versions of the specified asset, ordered by version number in descending order.
            // Only includes versions that are in the Frozen state.
            async Task GetAssetVersions()
            {
                TaskUtils.CancelTokenSource(ref m_VersionQueryTokenSource);
                m_VersionQueryTokenSource = new CancellationTokenSource();
                var cancellationToken = m_VersionQueryTokenSource.Token;

                var assetProject = await m_AssetRepository.GetAssetProjectAsync(asset.Descriptor.ProjectDescriptor, cancellationToken);

                var searchFilter = new AssetSearchFilter();
                
                searchFilter.Include().State.WithValue(AssetState.Frozen);

                var cacheConfiguration = new AssetCacheConfiguration()
                {
                    CacheProperties = true,
                    CachePreviewUrl = true,
                };
                
                var query = assetProject.QueryAssetVersions(asset.Descriptor.AssetId)
                    .OrderBy("versionNumber", SortingOrder.Descending)
                    .SelectWhereMatchesFilter(searchFilter)
                    .WithCacheConfiguration(cacheConfiguration)
                    .ExecuteAsync(cancellationToken);

                List<AssetInfo> resultVersions = new List<AssetInfo>();
                await foreach (var version in query)
                {
                    var properties = await version.GetPropertiesAsync(cancellationToken);

                    if (cancellationToken.IsCancellationRequested) return;

                    resultVersions.Add(new AssetInfo()
                    {
                        Asset = version,
                        Properties = properties
                    });
                }

                callback?.Invoke(resultVersions);
            }
        }

        private class FileUploadingProgress : IProgress<HttpProgress>
        {
            AssetCreationParameters m_Parameters;
            IAsset m_Asset;

            public FileUploadingProgress(AssetCreationParameters parameters, IAsset asset)
            {
                m_Parameters = parameters;
                m_Asset = asset;
            }

            public void Report(HttpProgress value)
            {
                AssetCreationProgress?.Invoke(m_Parameters, m_Asset, value.UploadProgress, null, null);
            }
        }

        // Handles the creation of a new asset, including uploading associated files and freezing the asset.
        // The method first creates the asset, then uploads each file to the asset's dataset, and finally freezes the asset.
        // Progress of the asset creation is reported through the AssetCreationProgress event.
        private void OnAssetCreation(AssetCreationParameters parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var cancellationTokenSource = new CancellationTokenSource();
            AssetCreationProgress?.Invoke(parameters, null, null, null, cancellationTokenSource);
            _ = CreateAsset(cancellationTokenSource.Token);
            return;
            
            async Task CreateAsset(CancellationToken cancellationToken)
            {
                /*Debug.Log($"Asset Creation: {parameters.Organization.Name}\\{parameters.Project.Name}\\{parameters.Collection?.Descriptor.Path}\\{parameters.AssetName}" +
                    $" - {parameters.AssetType} - {parameters.AssetDescription}");*/

                IAsset newAsset = null;

                try
                {
                    var newAssetCreation = new AssetCreation(parameters.AssetName)
                    {
                        Description = parameters.AssetDescription,
                        Type = parameters.AssetType,
                        Tags = parameters.Tags
                    };

                    if (parameters.Collection != null)
                    {
                        newAssetCreation.Collections = new List<CollectionPath>() { parameters.Collection.Descriptor.Path };
                    }

                    newAsset = await parameters.Project.CreateAssetAsync(newAssetCreation, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (newAsset == null)
                    {
                        //Debug.LogError($"Asset Creation: failed to create asset '{parameters.AssetName}' in project '{parameters.Project.Name}'.");
                        AssetCreationProgress?.Invoke(parameters, null, null, "Failed to create asset", null);
                        return;
                    }

                    if (string.IsNullOrEmpty(parameters.FileName))
                    {
                        if (parameters.DoVersionFreeze)
                        {
                            await WaitToFreeze(newAsset, AssetFreezeOperation.WaitOnTransformations, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        AssetCreationProgress?.Invoke(parameters, newAsset, 1f, null, null);
                        return;
                    }

                    // this will tell tracking that asset is created and next step is file uploading
                    AssetCreationProgress?.Invoke(parameters, newAsset, null, null, null);

                    var sourceDataset = await newAsset.GetSourceDatasetAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (sourceDataset == null)
                    {
                        //Debug.LogError($"Asset Creation: source dataset not found for created asset '{newAsset.Name}'.");
                        AssetCreationProgress?.Invoke(parameters, newAsset, null, "Source dataset not found", null);
                        return;
                    }

                    var filepath = Path.GetFileName(parameters.FileName);
                    var fileCreation = new FileCreation(filepath)
                    {
                        Path = filepath,
                        Description = "",
                        Tags = new List<string>() { parameters.AssetType.GetValueAsString() }
                    };

                    await using (var fileStream = File.OpenRead(parameters.FileName))
                    {
                        await sourceDataset.UploadFileAsync(fileCreation, fileStream, new FileUploadingProgress(parameters, newAsset), cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (parameters.DoVersionFreeze)
                    {
                        await WaitToFreeze(newAsset, AssetFreezeOperation.WaitOnTransformations, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.Log($"Asset Creation: asset creation Task for '{parameters.AssetName}' was canceled.");
                    AssetCreationProgress?.Invoke(parameters, newAsset, null, "Operation canceled", null);
                    return;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Asset Creation: exception in asset '{parameters.AssetName}' creation Task: {exception}");
                    AssetCreationProgress?.Invoke(parameters, newAsset, null, exception.Message, null);
                    return;
                }

                AssetCreationProgress?.Invoke(parameters, newAsset, 1f, null, null);
            }

            async Task WaitToFreeze(IAsset newAsset, AssetFreezeOperation operation, CancellationToken cancellationToken)
            {
                IAssetFreeze newAssetFreeze = new AssetFreeze("Initial Version")
                {
                    Operation = operation
                };

                Debug.Log("Asset Creation: freezing asset...");
                await newAsset.FreezeAsync(newAssetFreeze, cancellationToken);
            }
        }

        private void OnAssetDeselected()
        {
            _selectedAsset = null;
            _selectedParentAsset = null;
            _newerVersionAsset = null;

            CancelVersionChecking();
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
            if (_selectedParentAsset.HasValue)
            {
                
                if (_selectedParentAsset.Value.Asset.Descriptor.OrganizationId != asset.Asset.Descriptor.OrganizationId ||
                    _selectedParentAsset.Value.Asset.Descriptor.ProjectId != asset.Asset.Descriptor.ProjectId ||
                    _selectedParentAsset.Value.Asset.Descriptor.AssetId != asset.Asset.Descriptor.AssetId)
                {
                    _selectedParentAsset = null;
                }
            } else
            {
                _selectedParentAsset = null;
            }
            _newerVersionAsset = null;
            CancelVersionChecking();
            m_VersionCheckCoroutine = StartCoroutine(StartVersionChecking());
        }

        private IEnumerator StartVersionChecking()
        {
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
            TaskUtils.CancelTokenSource(ref m_VersionCheckerTokenSource);

            var assetNullable = _selectedAsset;
            if (assetNullable == null || !IsCheckingForNewVersionEnabled) return;
            var selectedAsset = assetNullable.Value;

            var newSource = new CancellationTokenSource();
            var cancellationToken = newSource.Token;
            m_VersionCheckerTokenSource = newSource;

            var currentAssetVersion = selectedAsset.Properties.Value.FrozenSequenceNumber;
            
            var assetProject = await m_AssetRepository.GetAssetProjectAsync(
                selectedAsset.Asset.Descriptor.ProjectDescriptor,
                cancellationToken);

            if(cancellationToken.IsCancellationRequested || !IsCheckingForNewVersionEnabled) return;

            var latestVersion = await assetProject.GetAssetWithLatestVersionAsync(
                selectedAsset.Asset.Descriptor.AssetId,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested || !IsCheckingForNewVersionEnabled) return;

            var latestVersionAssetProperty = await latestVersion.GetPropertiesAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested || !IsCheckingForNewVersionEnabled) return;

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
            var localAssetRepositoryCancellationTokenSource = m_AssetRepositoryCancellationTokenSource;

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
            IAssetProject assetProject = null;
            CollectionDescriptor? assetCollectionDescriptor = null;
            switch (allAssets)
            {
                case true:
                {
                    if (SceneManager.GetActiveScene() == gameObject.scene)
                    {
                        SelectedCollection = null;
                        SelectedAssetProject = null;
                    }

                    m_AllAssetProjects = await GetAssetProjects(SharedUIManager.Organization, localAssetRepositoryCancellationTokenSource.Token);

                    assets = m_AssetRepository.QueryAssets(m_AllAssetProjects.Select(p => p.AssetProject.Descriptor))
                        .SelectWhereMatchesFilter(searchFilter).OrderBy(m_SortingType.GetPropertyName(), sortingOrder)
                        .WithCacheConfiguration(cacheConfiguration)
                        .ExecuteAsync(localAssetRepositoryCancellationTokenSource.Token);
                    break;
                }
                case false when SharedUIManager.AssetProjectInfo.HasValue && SharedUIManager.AssetCollection == null:
                {
                    assetProject = SharedUIManager.AssetProjectInfo.Value.AssetProject;
                    assets = assetProject.QueryAssets().SelectWhereMatchesFilter(searchFilter)
                        .OrderBy(m_SortingType.GetPropertyName(), sortingOrder)
                        .WithCacheConfiguration(cacheConfiguration)
                        .ExecuteAsync(localAssetRepositoryCancellationTokenSource.Token);
                    break;
                }
                case false when SharedUIManager.AssetProjectInfo.HasValue && SharedUIManager.AssetCollection != null:
                {
                    assetProject = SharedUIManager.AssetProjectInfo.Value.AssetProject;
                    assetCollectionDescriptor = SharedUIManager.AssetCollection.Descriptor;
                    searchFilter.Collections.WhereContains(assetCollectionDescriptor.Value.Path);
                    assets = assetProject.QueryAssets().SelectWhereMatchesFilter(searchFilter)
                        .OrderBy(m_SortingType.GetPropertyName(), sortingOrder)
                        .WithCacheConfiguration(cacheConfiguration)
                        .ExecuteAsync(localAssetRepositoryCancellationTokenSource.Token);
                    break;
                }
            }

            if (localAssetRepositoryCancellationTokenSource.IsCancellationRequested) return;

            List<AssetInfo> assetsToBatchProcess = new List<AssetInfo>();
            int count = 0;

            if (assets != null)
            {
                await foreach (var asset in assets)
                {
                    if (localAssetRepositoryCancellationTokenSource.IsCancellationRequested) return;
                    count++;
                    var assetProperty = await asset.GetPropertiesAsync(localAssetRepositoryCancellationTokenSource.Token);
                    assetsToBatchProcess.Add(new AssetInfo()
                    {
                        Asset = asset,
                        Properties = assetProperty
                    });
                    if (localAssetRepositoryCancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }
                    if (count % 98 == 0) AssetsLoaded?.Invoke(assetsToBatchProcess);
                    }
                if (localAssetRepositoryCancellationTokenSource.IsCancellationRequested) return;
                AssetsLoaded?.Invoke(assetsToBatchProcess.Count == 0 ? null : assetsToBatchProcess);
            }

            AllAssetsLoaded?.Invoke(assetProject, assetCollectionDescriptor);
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

        private void OnRequestAssetProjects(IOrganization organization, Action<IOrganization, List<AssetProjectInfo>> callback)
        {
            _ = GetProjects();
            return;

            async Task GetProjects()
            {
                m_ProjectCancellationTokenSource?.Cancel();
                m_ProjectCancellationTokenSource = new CancellationTokenSource();
                var localProjectCancellationTokenSource = m_ProjectCancellationTokenSource;

                var results = await GetAssetProjects(organization, localProjectCancellationTokenSource.Token);

                localProjectCancellationTokenSource.Token.ThrowIfCancellationRequested();

                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    m_AllAssetProjects = results;
                }

                callback?.Invoke(organization, results);
            }
        }

        /// <summary>
        /// Retrieves a list of asset projects for the specified organization.
        /// </summary>
        /// <param name="selectedOrg">The organization for which to retrieve asset projects.</param>
        /// <param name="cancelationToken">Cancellation token to cancel the operation if needed.</param>
        /// <returns>A list of asset projects associated with the specified organization.</returns>
        private async Task<List<AssetProjectInfo>> GetAssetProjects(IOrganization selectedOrg, CancellationToken cancelationToken)
        {
            var tempAssetProjects = new List<AssetProjectInfo>();

            var orgID = selectedOrg.Id;
            
            AssetProjectCacheConfiguration cacheConfiguration = new AssetProjectCacheConfiguration()
            {
                CacheProperties = true,
            };

            var assetProjectsAsyncEnumerable = m_AssetRepository
                .QueryAssetProjects(orgID)
                .WithCacheConfiguration(cacheConfiguration)
                .ExecuteAsync(cancelationToken);

            await foreach (var assetProject in assetProjectsAsyncEnumerable)
            {
                var assetProjectProperties = await assetProject.GetPropertiesAsync(cancelationToken);
                tempAssetProjects.Add(new AssetProjectInfo()
                {
                    AssetProject = assetProject,
                    Properties = assetProjectProperties
                });

                cancelationToken.ThrowIfCancellationRequested();
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
                var localCollectionCancellationTokenSource = m_CollectionCancellationTokenSource;

                var collectionsEnumerable = assetProject.AssetProject.QueryCollections()
                    .ExecuteAsync(localCollectionCancellationTokenSource.Token);
                
                await foreach (var collection in collectionsEnumerable)
                {
                    localCollectionCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    collections.Add(collection);
                }

                localCollectionCancellationTokenSource.Token.ThrowIfCancellationRequested();
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
                    if(m_AllOrganizations.Any(x => x.Id == organization.Id)) continue;
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
                
                TaskUtils.CancelTokenSource(ref m_AssetRepositoryCancellationTokenSource);
                TaskUtils.CancelTokenSource(ref m_VersionQueryTokenSource);
                TaskUtils.CancelTokenSource(ref m_VersionCheckerTokenSource);
                TaskUtils.CancelTokenSource(ref m_DatasetTokenSource);
                TaskUtils.CancelTokenSource(ref m_OrganizationCancellationTokenSource);
                TaskUtils.CancelTokenSource(ref m_CollectionCancellationTokenSource);
            }
        }
    }
}
