using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;

namespace Unity.Industry.Viewer.Streaming
{
    public class OfflineCollection : IAssetCollection
    {
        public CollectionDescriptor Descriptor { get; }
        public string Name { get; }
        public string Description { get; }
        public CollectionPath ParentPath => Descriptor.Path.GetParentPath();

        public OfflineCollection(string path, string organizationId, string projectId)
        {
            Name = path.Split("/")[^1];
            Description = string.Empty;
            var collectionPath = new CollectionPath(path);
            var projectDescriptor = new ProjectDescriptor(new OrganizationId(organizationId), new ProjectId(projectId));
            Descriptor = new CollectionDescriptor(projectDescriptor, collectionPath);
        }
        
        public string GetFullCollectionPath()
        {
            return Descriptor.Path;
        }

        public Task RefreshAsync(CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public Task UpdateAsync(IAssetCollectionUpdate assetCollectionUpdate, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public Task LinkAssetsAsync(IEnumerable<IAsset> assets, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public Task UnlinkAssetsAsync(IEnumerable<IAsset> assets, CancellationToken cancellationToken) => throw new System.NotImplementedException();

        public Task MoveToNewPathAsync(CollectionPath newCollectionPath, CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }
}
