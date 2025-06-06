using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using UnityEngine.Networking;
using Unity.Cloud.Common;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using Object = UnityEngine.Object;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Threading;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming
{
    // This class represents offline asset information in a Unity project.
    // It includes details such as asset ID, name, type, organization, project, and modification dates.
    // The class supports serialization and deserialization of asset information.
    // It provides methods to fetch additional asset details asynchronously, including preview images and linked collections.
    // The class integrates with Unity's file system to store and retrieve asset data.
    [JsonObject(MemberSerialization.OptIn)]
    public class OfflineAssetInfo
    {
        [JsonProperty]
        public string assetId { get; set; }
    
        [JsonProperty]
        public string assetName { get; set; }

        [JsonIgnore]
        public bool layout => tags != null && tags.Contains(StreamingUtils.LayoutTag);
        
        [JsonProperty]
        public List<string> tags { get; set; }

        [JsonProperty]
        public string assetTypeString { get; set; }

        [JsonProperty]
        public string previewPic { get; set; }

        [JsonProperty]
        public string organizationName { get; set; }

        [JsonProperty]
        public string organizationId { get; set; }

        [JsonProperty]
        public string projectName { get; set; }

        [JsonProperty]
        public string projectId { get; set; }

        [JsonProperty]
        public string projectStatus { get; set; }

        [JsonProperty]
        public List<string> collectionPaths { get; set; }

        [JsonProperty]
        public string lastModifiedDateString { get; set; }

        [JsonProperty]
        public string lastModifiedBy { get; set; }

        [JsonProperty]
        public string createdDateString { get; set; }

        [JsonProperty]
        public string createdBy { get; set; }

        [JsonProperty]
        public int assetVersion { get; set; }
        
        [JsonProperty]
        public string assetVersionId { get; set; }

        [JsonIgnore]
        public DateTime lastModified { get; set; }

        [JsonIgnore]
        public DateTime created { get; set; }

        [JsonIgnore]
        public AssetType assetType { get; set; }

        [JsonIgnore]
        public TaskCompletionSource<bool> m_Completed { get; set; } = new TaskCompletionSource<bool>();

        public OfflineAssetInfo() { }
        
        public OfflineAssetInfo(AssetInfo assetInfo, string storedPath)
        {
            if (assetInfo.Asset == null) throw new ArgumentNullException(nameof(assetInfo));
            assetId = assetInfo.Asset.Descriptor.AssetId.ToString();
            assetName = assetInfo.Properties.Value.Name;
            assetType = assetInfo.Properties.Value.Type;
            tags = assetInfo.Properties.Value.Tags.ToList();
            organizationId = assetInfo.Asset.Descriptor.OrganizationId.ToString();
            projectId = assetInfo.Asset.Descriptor.ProjectId.ToString();
            assetVersion = assetInfo.Properties.Value.FrozenSequenceNumber;
            projectStatus = assetInfo.Properties.Value.StatusName;
            lastModified = assetInfo.Properties.Value.AuthoringInfo.Updated;
            created = assetInfo.Properties.Value.AuthoringInfo.Created;
            
            _ = GetInfo(assetInfo, storedPath);
            
        }

        private async Task GetInfo(AssetInfo assetInfo, string storedPath)
        {
            try
            {
                var previewImage = await assetInfo.Asset.GetPreviewUrlAsync(CancellationToken.None);
                previewPic = previewImage.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            if (!string.IsNullOrEmpty(previewPic))
            {
                try
                {
                    using var uwr = new UnityWebRequest(previewPic, UnityWebRequest.kHttpVerbGET);
                    uwr.downloadHandler = new DownloadHandlerTexture();

                    var operation = uwr.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }
                    var texture2d = DownloadHandlerTexture.GetContent(uwr);
                    var finalPath = Path.Combine(storedPath, "previewPic.png");
                    await File.WriteAllBytesAsync(finalPath, texture2d.EncodeToPNG());
                    previewPic = "previewPic.png";
                }
                catch (Exception e)
                {
                    previewPic = string.Empty;
                }
            }
            assetVersionId = assetInfo.Asset.Descriptor.AssetVersion.ToString();
            OrganizationId orgId;
            
            if (!IdentityController.GuestMode)
            {
                var assetOrg = await PlatformServices.OrganizationRepository.GetOrganizationAsync(
                    new OrganizationId(assetInfo.Asset.Descriptor.OrganizationId.ToString()));

                if (assetOrg == null)
                {
                    return;
                }
                if (long.TryParse(assetInfo.Properties.Value.AuthoringInfo.CreatedBy.ToString(), out long createId))
                {
                    var memberInfo = await assetOrg.GetMemberAsync(assetInfo.Properties.Value.AuthoringInfo.CreatedBy);
                    createdBy = memberInfo.Name;
                }
                else
                {
                    createdBy = "Uknown";
                }
                if (long.TryParse(assetInfo.Properties.Value.AuthoringInfo.UpdatedBy.ToString(), out long number))
                {
                    var memberInfo = await assetOrg.GetMemberAsync(assetInfo.Properties.Value.AuthoringInfo.UpdatedBy);
                    lastModifiedBy = memberInfo.Name;
                }
                else
                {
                    lastModifiedBy = "Uknown";
                }
                
                organizationName = assetOrg.Name;
            }
            else
            {
                var assetsController = Object.FindFirstObjectByType<AssetsController>();
                if (assetsController != null)
                {
                    organizationName = assetsController.ServiceAccountOrganization.Name;
                }
            }
            var assetRepository = IdentityController.GuestMode? PlatformServices.ServiceAccountAssetRepository : PlatformServices.AssetRepository; 
            
            IAssetProject assetProject =
                await assetRepository.GetAssetProjectAsync(assetInfo.Asset.Descriptor.ProjectDescriptor,
                    CancellationToken.None);
            
            if(assetProject == null) return;
            
            var assetProjectProperties = await assetProject.GetPropertiesAsync(CancellationToken.None);
            
            projectName = assetProjectProperties.Name;
            var path = assetInfo.Asset.ListLinkedAssetCollectionsAsync(Range.All, CancellationToken.None);
            collectionPaths = new List<string>();
            await foreach(var collection in path)
            {
                collectionPaths.Add(collection.Path);
            }
            m_Completed.SetResult(true);
        }

        [OnSerializing]
        internal void OnBeforeSerialize(StreamingContext context)
        {
            lastModifiedDateString = lastModified.ToString("o");
            createdDateString = created.ToString("o");
            assetTypeString = assetType.ToString();
        }

        [OnDeserialized]
        internal void OnAfterDeserialize(StreamingContext context)
        {
            lastModified = DateTime.Parse(lastModifiedDateString, null, DateTimeStyles.RoundtripKind);
            created = DateTime.Parse(createdDateString, null, DateTimeStyles.RoundtripKind);
            assetType = (AssetType)Enum.Parse(typeof(AssetType), assetTypeString);
        }
    }
}
