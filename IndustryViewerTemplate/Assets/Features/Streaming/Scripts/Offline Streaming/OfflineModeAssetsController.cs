using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using UnityEngine.SceneManagement;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming
{
    // This script manages offline assets in a Unity project.
    // It handles the selection, deselection, and organization of offline assets.
    // The script listens for various events to update the asset list and UI accordingly.
    // It supports searching, sorting, and filtering of assets based on different criteria.
    // The script integrates with Unity's MonoBehaviour for lifecycle management.
    [DefaultExecutionOrder(100)]
    public class OfflineModeAssetsController : MonoBehaviour
    {
        public static AssetProjectInfo? SelectedAssetProject;
        public static Action<List<IOrganization>> AllOrganizationFound;
        public static Action<List<AssetProjectInfo>> AssetProjectsLoaded;
        public static Action<List<AssetInfo>> AssetsLoaded;
        public static Action<AssetInfo> AssetSelected;
        public static Action AssetDeselected;
        public static Action<AssetInfo> AssetOffloaded;
        public static Action<AssetProjectInfo?, Action<List<IAssetCollection>>> RequestAssetCollections;
        public static Action<SortingType, string> SortingTypeChangedEvent;
        public static Action<string> SearchAssets;
        
        private List<AssetInfo> m_OfflineAssets;
        
        public static IAssetCollection SelectedCollection;
        private SortingType m_SortingType;
        private string m_SearchString;

        private bool m_Initialized;

        private void Awake()
        {
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
        }

        private void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            UnregisterCalls();
        }

        private void UnregisterCalls()
        {
            SharedUIManager.OrganizationSelected -= OnOrganizationChanged;
            SharedUIManager.AssetProjectSelected -= OnAssetProjectChanged;
            SharedUIManager.AssetSelected -= OnAssetSelected;
            RequestAssetCollections -= OnRequestAssetCollections;
            
            SharedUIManager.AssetCollectionSelected -= OnCollectionSelected;
            AssetDeselected -= OnAssetDeselected;
            SearchAssets -= OnSearchAssets;
            SortingTypeChangedEvent -= OnSortingTypeChanged;
            AssetOffloaded -= OnAssetRemoved;
        }

        private void OnNetworkStatusChanged(bool connected)
        {
            if (!NetworkDetector.RequestedOfflineMode)
            {
                SelectedAssetProject = null;
                SelectedCollection = null;
                AssetDeselected?.Invoke();
                if (!m_Initialized) return;
                m_Initialized = false;
                UnregisterCalls();
            }
            
            if (connected)
            {
                return;
            }
            
            if (!m_Initialized)
            {
                m_Initialized = true;
                SharedUIManager.OrganizationSelected += OnOrganizationChanged;
                SharedUIManager.AssetProjectSelected += OnAssetProjectChanged;
                SharedUIManager.AssetSelected += OnAssetSelected;
                RequestAssetCollections += OnRequestAssetCollections;
                
                SharedUIManager.AssetCollectionSelected += OnCollectionSelected;
                AssetDeselected += OnAssetDeselected;
                SearchAssets += OnSearchAssets;
                SortingTypeChangedEvent += OnSortingTypeChanged;
                AssetOffloaded += OnAssetRemoved;
            }
            
            StreamingUtils.FindAllOfflineAssets(ref m_OfflineAssets, out var organizations);
            if (m_OfflineAssets == null || m_OfflineAssets.Count == 0)
            {
                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    SelectedAssetProject = null;
                    SelectedCollection = null;
                }
            }
            
            AllOrganizationFound?.Invoke(organizations);
        }

        private void OnAssetSelected(AssetInfo assetInfo)
        {
            if(SceneManager.GetActiveScene() == gameObject.scene)
            {
                AssetSelected?.Invoke(assetInfo);
            }
        }

        private void OnAssetRemoved(AssetInfo offlineAsset)
        {
            StreamingUtils.FindAllOfflineAssets(ref m_OfflineAssets, out var organizations);
            
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                AssetDeselected?.Invoke();
            }
            
            SharedUIManager.SelectedAsset = null;
            SharedUIManager.AssetProjectInfo = null;
            SharedUIManager.AssetCollection = null;
            
            if (m_OfflineAssets == null || m_OfflineAssets.Count == 0)
            {
                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    SelectedAssetProject = null;
                    SelectedCollection = null;
                }
                
                AllOrganizationFound?.Invoke(null);
                return;
            }
            
            AllOrganizationFound?.Invoke(organizations);
            var removedAssetOrgId = offlineAsset.Asset.Descriptor.OrganizationId;
            foreach (var organization in organizations)
            {
                if (removedAssetOrgId != organization.Id) continue;
                SharedUIManager.Organization = organization;
                return;
            }

            SharedUIManager.Organization = null;
        }

        private void OnSearchAssets(string arg1)
        {
            m_SearchString = arg1;
            RequestAssetProjects(!SharedUIManager.AssetProjectInfo.HasValue, m_SearchString);
        }

        private void OnSortingTypeChanged(SortingType arg1, string searchText)
        {
            m_SortingType = arg1;
            RequestAssetProjects(!SharedUIManager.AssetProjectInfo.HasValue, searchText);
        }

        private void OnAssetDeselected()
        {
            
        }
        
        private void OnRequestAssetCollections(AssetProjectInfo? assetProject, Action<List<IAssetCollection>> arg2)
        {
            List<IAssetCollection> collections = new List<IAssetCollection>();
            var allAssets = m_OfflineAssets.FindAll(x => x.Asset.Descriptor.ProjectDescriptor == assetProject.Value.AssetProject.Descriptor);
            
            var offlineCollections = allAssets.SelectMany(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.collectionPaths).Distinct();
            
            foreach (var offlineCollection in offlineCollections)
            {
                var newOfflineCollection = new OfflineCollection(offlineCollection,
                    assetProject.Value.AssetProject.Descriptor.OrganizationId.ToString(), assetProject.Value.AssetProject.Descriptor.ProjectId.ToString());

                if (!newOfflineCollection.ParentPath.IsEmpty)
                {
                    var totalParent = newOfflineCollection.ParentPath.GetPathComponents();
                    for (var i = 0; i < totalParent.Length; i++)
                    {
                        var path = string.Join("/", totalParent.Take(i + 1));
                        if(collections.Any(x => string.Equals(x.Descriptor.Path.ToString(), path))) continue;
                        var parentCollection = new OfflineCollection(path, assetProject.Value.AssetProject.Descriptor.OrganizationId.ToString(), assetProject.Value.AssetProject.Descriptor.ProjectId.ToString());
                        collections.Add(parentCollection);
                    }
                }
                
                collections.Add(newOfflineCollection);
            }
            
            arg2?.Invoke(collections);
        }

        private void OnAssetProjectChanged(AssetProjectInfo? assetProject)
        {
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SelectedCollection = null;
            }
            
            if (!assetProject.HasValue)
            {
                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    SelectedAssetProject = null;
                }
                RequestAssetProjects(true, string.Empty);
                return;
            }

            var allMatchedAssets = m_OfflineAssets.Any(x => x.Asset.Descriptor.ProjectDescriptor == assetProject.Value.AssetProject.Descriptor);
            if(!allMatchedAssets) return;
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SelectedAssetProject = assetProject;
            }
            
            RequestAssetProjects(false, string.Empty);
        }
        
        private void OnCollectionSelected(IAssetCollection arg1)
        {
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SelectedCollection = arg1;
            }
            RequestAssetProjects(false, string.Empty);
        }

        private void RequestAssetProjects(bool allAssetProjects, string searchText)
        {
            if(m_OfflineAssets == null || m_OfflineAssets.Count == 0) return;
            
            if (allAssetProjects)
            {
                if (SceneManager.GetActiveScene() == gameObject.scene)
                {
                    SelectedAssetProject = null;
                    SelectedCollection = null;
                }

                m_SearchString = searchText;
                var selectedOrganizationAssets = m_OfflineAssets.FindAll(x => x.Asset.Descriptor.OrganizationId == SharedUIManager.Organization.Id);
                if(!string.IsNullOrEmpty(m_SearchString))
                {
                    selectedOrganizationAssets = selectedOrganizationAssets.FindAll(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.assetName.Contains(m_SearchString, StringComparison.OrdinalIgnoreCase));
                }
                AssetsLoaded?.Invoke(ReturnSorting(selectedOrganizationAssets));
            }
            else
            {
                var selectedAssetProjectAssets = m_OfflineAssets.FindAll(x => x.Asset.Descriptor.ProjectDescriptor == SharedUIManager.AssetProjectInfo.Value.AssetProject.Descriptor);
                if(SharedUIManager.AssetCollection != null)
                {
                    selectedAssetProjectAssets = selectedAssetProjectAssets.FindAll(x => 
                       string.Equals(SharedUIManager.AssetCollection.Descriptor.Path.ToString(), string.Join("/", ((OfflineAsset)x.Asset).OfflineAssetInfo.collectionPaths)));
                }
                if(!string.IsNullOrEmpty(m_SearchString))
                {
                    selectedAssetProjectAssets = selectedAssetProjectAssets.FindAll(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.assetName.Contains(m_SearchString, StringComparison.OrdinalIgnoreCase));
                }
                AssetsLoaded?.Invoke(ReturnSorting(selectedAssetProjectAssets));
            }

            return;

            List<AssetInfo> ReturnSorting(List<AssetInfo> assets)
            {
                return m_SortingType switch
                {
                    SortingType.Name => assets.OrderBy(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.assetName).ToList(),
                    SortingType.Upload_date => assets.OrderByDescending(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.created).ToList(),
                    SortingType.Last_modified => assets.OrderByDescending(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.lastModified).ToList(),
                    SortingType.Asset_type => assets.OrderBy(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.assetType).ToList(),
                    SortingType.Status => assets.OrderBy(x => ((OfflineAsset)x.Asset).OfflineAssetInfo.projectStatus).ToList(),
                    _ => assets
                };
            }
        }

        private void OnOrganizationChanged(IOrganization org)
        {
            if(m_OfflineAssets == null) return;
            var organization = m_OfflineAssets.Any(x => x.Asset.Descriptor.OrganizationId == org.Id);
            if(!organization) return;
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                SelectedAssetProject = null;
                SelectedCollection = null;
            }
            
            var selectedOrganizationAssets = m_OfflineAssets.FindAll(x => x.Asset.Descriptor.OrganizationId == org.Id);
            var assetProjects = new List<AssetProjectInfo>();
            
            foreach (var selectedOrganizationAsset in selectedOrganizationAssets)
            {
                var offlineAsset = (OfflineAsset) selectedOrganizationAsset.Asset;
                if(assetProjects.Any(x => x.AssetProject.Descriptor == offlineAsset.Descriptor.ProjectDescriptor )) continue;
                assetProjects.Add(
                    new AssetProjectInfo()
                    {
                        AssetProject = new OfflineAssetProject(offlineAsset.OfflineAssetInfo.projectName, offlineAsset.Descriptor.ProjectId.ToString(), offlineAsset.Descriptor.OrganizationId.ToString()),
                    }
                );
            }
            AssetProjectsLoaded?.Invoke(assetProjects.Distinct().ToList());
            RequestAssetProjects(true, string.Empty);
        }
    }
}
