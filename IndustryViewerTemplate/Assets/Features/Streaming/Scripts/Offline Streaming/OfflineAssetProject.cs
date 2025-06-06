using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;

namespace Unity.Industry.Viewer.Streaming
{
    public class OfflineAssetProject : IAssetProject
    {
        public ProjectDescriptor Descriptor { get; }
        [Obsolete]
        public string Name { get; set; }

        public string OfflineAssetProjectName { get; private set; }
        public IDeserializable Metadata { get; set; }
        
        public OfflineAssetProject(string assetProjectOfflineAssetProjectName, string assetProjectId, string organizationId)
        {
            OfflineAssetProjectName = assetProjectOfflineAssetProjectName;
            Descriptor = new ProjectDescriptor(new OrganizationId(organizationId), new ProjectId(assetProjectId));
        }
        
        public Task<IAsset> GetAssetAsync(AssetId assetId, AssetVersion assetVersion, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public Task<IAsset> CreateAssetAsync(IAssetCreation assetCreation, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public AssetQueryBuilder QueryAssets() => throw new System.NotImplementedException();

        public GroupAndCountAssetsQueryBuilder GroupAndCountAssets() => throw new System.NotImplementedException();

        public CollectionQueryBuilder QueryCollections() => throw new System.NotImplementedException();

        public Task<IAssetCollection> GetCollectionAsync(CollectionPath collectionPath, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public Task<IAssetCollection> CreateCollectionAsync(IAssetCollectionCreation assetCollectionCreation, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public Task DeleteCollectionAsync(CollectionPath collectionPath, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public TransformationQueryBuilder QueryTransformations() => throw new System.NotImplementedException();
    }
}
