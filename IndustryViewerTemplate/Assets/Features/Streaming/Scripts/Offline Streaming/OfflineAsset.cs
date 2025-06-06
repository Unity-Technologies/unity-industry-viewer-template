using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using System.IO;
using System.Linq;

namespace Unity.Industry.Viewer.Streaming
{
    public class OfflineAsset : IAsset
    {
        public struct OfflineAuthoringInfo
        {
            public string CreatedBy { get; }
            public DateTime Created { get; }
            public string UpdatedBy { get; }
            public DateTime Updated { get; }

            public OfflineAuthoringInfo(string createdBy, DateTime created, string updatedBy, DateTime updated)
            {
                CreatedBy = createdBy;
                Created = created;
                UpdatedBy = updatedBy;
                Updated = updated;
            }
        }
        
        public AssetDescriptor Descriptor { get; }
        [Obsolete]
        public ProjectDescriptor SourceProject { get; }
        [Obsolete]
        public IEnumerable<ProjectDescriptor> LinkedProjects { get; }
        [Obsolete]
        public string Name { get; }
        [Obsolete]
        public string Description { get; }
        [Obsolete]
        public IEnumerable<string> Tags { get; }
        [Obsolete]
        public IEnumerable<string> SystemTags { get; }
        [Obsolete]
        public AssetType Type { get; }
        [Obsolete]
        public string PreviewFile { get; }
        [Obsolete]
        public string Status { get; }
        [Obsolete]
        public AuthoringInfo AuthoringInfo { get; }
        [Obsolete]
        public IMetadataContainer Metadata { get; }
        
        public OfflineAssetInfo OfflineAssetInfo { get; }
        
        public OfflineAsset(OfflineAssetInfo offlineAssetInfo)
        {
            var projectDescriptor = new ProjectDescriptor(new OrganizationId(offlineAssetInfo.organizationId), new ProjectId(offlineAssetInfo.projectId));
            Descriptor = new AssetDescriptor(projectDescriptor, new AssetId(offlineAssetInfo.assetId),
                new AssetVersion(offlineAssetInfo.assetVersionId));
            OfflineAssetInfo = offlineAssetInfo;
        }

        public string GetLocalStreamingPath()
        {
            if (!Directory.Exists(StreamingUtils.LocalStreamingAssetPath))
            {
                return string.Empty;
            }
            var hashFolderName = StreamingUtils.ReturnHashName(this);
            var matchingFolders = Directory.GetDirectories(StreamingUtils.LocalStreamingAssetPath, hashFolderName + "*");
            string finalFolderPath = string.Empty;
            
            foreach (var matchingFolder in matchingFolders)
            {
                var directoryName = new DirectoryInfo(matchingFolder).Name;
                if (directoryName.Contains("_temp")) continue;
                {
                    finalFolderPath = matchingFolder;
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(finalFolderPath)) return string.Empty;
            string returnFilePath = Path.Combine(finalFolderPath, StreamingUtils.TilesetJson);
            
            if (File.Exists(returnFilePath)) return returnFilePath;
            
            string[] glbFiles = Directory.GetFiles(finalFolderPath, "*.glb");
            string[] gltfFiles = Directory.GetFiles(finalFolderPath, "*.gltf");
            var allFiles = glbFiles.Concat(gltfFiles);
            returnFilePath = allFiles.Any() ? allFiles.First() : string.Empty;

            return returnFilePath;
        }
        
        public IAsset WithProject(ProjectDescriptor projectDescriptor) => throw new NotImplementedException();
        
        public IAsyncEnumerable<CollectionDescriptor> ListLinkedAssetCollectionsAsync(Range range,
            CancellationToken cancellationToken) => throw new NotImplementedException();
        
        public string SerializeIdentifiers()
        {
            return Descriptor.ToJson();
        }

        public string Serialize() => throw new NotImplementedException();
        
        public Task<Uri> GetPreviewUrlAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task RefreshAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task UpdateAsync(IAssetUpdate assetUpdate, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task UpdateStatusAsync(AssetStatusAction statusAction, CancellationToken cancellationToken) => throw new NotImplementedException();
        
        public IAsyncEnumerable<IAssetProject> GetLinkedProjectsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task LinkToProjectAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task UnlinkFromProjectAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IDictionary<string, Uri>> GetAssetDownloadUrlsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IDataset> CreateDatasetAsync(DatasetCreation datasetCreation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IDataset> GetDatasetAsync(DatasetId datasetId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public IAsyncEnumerable<IDataset> ListDatasetsAsync(Range range, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IFile> GetFileAsync(string filePath, CancellationToken cancellationToken) => throw new NotImplementedException();

        public IAsyncEnumerable<IFile> ListFilesAsync(Range range, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
