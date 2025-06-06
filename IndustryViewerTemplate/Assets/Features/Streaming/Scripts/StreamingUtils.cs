using System;
using System.Collections.Generic;
using System.IO;
using Unity.Cloud.Assets;
using System.Linq;
using Unity.Collections;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Cloud.Identity;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming
{
    public static class StreamingUtils
    {
        [ReadOnly]
        public static string StreamModelTag = "Model";
        
        [ReadOnly]
        public static string OfflineAssetJsonFileName = "assetInfo.json";

        [ReadOnly]
        public static string StreamingPanelName = "StreamingContainer";
        [ReadOnly]
        public static string BottomLeftButtonStyleName = "BottomLeftIconButton";
        [ReadOnly]
        public static string BottomLeftContainerName = "BottomLeft";
        [ReadOnly]
        public static string IconButtonIconVEName = "appui-button__leadingicon";

        public const string TilesetJson = "tileset.json";

        public const string StreamableTag = "Streamable";

        public const string SourceTag = "Source";

        public const string PreviewTag = "Preview";

        public const string LayoutTag = "Layout";
        
        public const string LayoutJson = "layout.json";
        
        public static string LocalStreamingAssetPath => Path.Combine(Application.persistentDataPath, "StreamingAssets");
        
        public static string ReturnHashName(LayoutModelEntity layoutModelEntity)
        {
            var folderName = $"{layoutModelEntity.assetID}{layoutModelEntity.projectID}{layoutModelEntity.orgID}";
            return HashString(folderName);
        }

        public static DoubleBounds ReturnBounds(DoubleBounds bounds, float scale = 10f)
        {
            return new DoubleBounds(bounds.Center, bounds.Size * scale);
        }
        
        public static string ReturnHashName(IAsset asset)
        {
            var folderName = $"{asset.Descriptor.AssetId.ToString()}{asset.Descriptor.ProjectId.ToString()}{asset.Descriptor.OrganizationId.ToString()}";
            return HashString(folderName);
        }
        
        public static bool CheckHasLocalAsset(IAsset asset, out int ver)
        {
            if (!Directory.Exists(LocalStreamingAssetPath))
            {
                ver = 0;
                return false;
            }
            var hashFolderName = ReturnHashName(asset);
            var matchingFolders = Directory.GetDirectories(LocalStreamingAssetPath, hashFolderName + "*");
            foreach (var matchingFolder in matchingFolders)
            {
                var directoryName = new DirectoryInfo(matchingFolder).Name;
                if (directoryName.Contains("_temp")) continue;
                ver = int.Parse(directoryName.Split('_')[1]);
                return true;
            }
            ver = 0;
            return false;
        }
        
        
        public static bool IsGLBFile(IFile file)
        {
            return file.Descriptor.Path.EndsWith(".glb", StringComparison.CurrentCultureIgnoreCase) || file.Descriptor.Path.EndsWith(".gltf", StringComparison.CurrentCultureIgnoreCase);
        }
        
        public static async Task<bool> HasGLBFile(IDataset dataset)
        {
            var files = dataset.ListFilesAsync(Range.All, CancellationToken.None);
            await foreach (var file in files)
            {
                if(IsGLBFile(file))
                {
                    return true;
                }
            }

            return false;
        }

        public static OfflineAsset ReturnOfflineAssetInfo(IAsset asset)
        {
#if UNITY_WEBGL
            return null;
#endif
            var hashFolderName = ReturnHashName(asset);
            return ReturnOfflineAsset(hashFolderName);
        }
        
        public static OfflineAsset ReturnOfflineAssetInfo(LayoutModelEntity asset)
        {
#if UNITY_WEBGL
            return null;
#endif
            var hashFolderName = ReturnHashName(asset);
            var offlineAssetInfo = ReturnOfflineAsset(hashFolderName);
            return offlineAssetInfo;
        }

        private static OfflineAsset ReturnOfflineAsset(string hashFolder)
        {
            if (!Directory.Exists(LocalStreamingAssetPath))
            {
                return null;
            }
            var allFolders
                = Directory.GetDirectories(LocalStreamingAssetPath, hashFolder + "*");

            if (allFolders.Length == 0)
            {
                return null;
            }

            string finalFolder = string.Empty;
            
            foreach (var folder in allFolders)
            {
                if(folder.Contains("_temp")) continue;
                finalFolder = folder;
                break;
            }

            if (string.IsNullOrEmpty(finalFolder))
            {
                return null;
            }
            
            var offlineJsonFile = Path.Combine(LocalStreamingAssetPath, finalFolder,
                OfflineAssetJsonFileName);

            if (!File.Exists(offlineJsonFile))
            {
                return null;
            }
            
            var json = File.ReadAllText(offlineJsonFile);
            var offlineAssetInfo = JsonConvert.DeserializeObject<OfflineAssetInfo>(json);
            return new OfflineAsset(offlineAssetInfo);
        }
        
        public static void FindAllOfflineAssets(ref List<AssetInfo> offlineAssets, out List<IOrganization> organizations)
        {
            offlineAssets = null;
            if (!Directory.Exists(LocalStreamingAssetPath))
            {
                organizations = null;
                return;
            }

            offlineAssets = ReturnOfflineAssets();
            if(offlineAssets == null || offlineAssets.Count == 0)
            {
                organizations = null;
                return;
            }
            
            organizations = new List<IOrganization>();
            
            foreach (var assetInfo in offlineAssets)
            {
                if(organizations.Any(x => x.Id == assetInfo.Asset.Descriptor.OrganizationId)) continue;

                organizations.Add(new OfflineOrg(assetInfo.Asset.Descriptor.OrganizationId.ToString(), ((OfflineAsset)assetInfo.Asset).OfflineAssetInfo.organizationName));
            }
        }

        public static OfflineAsset ReturnOfflineAsset(string orgId, string assetProjectId, string assetId)
        {
            var allOfflineAssets = ReturnOfflineAssets();
            if(allOfflineAssets == null || allOfflineAssets.Count == 0)
            {
                return null;
            }
            var offlineAsset = allOfflineAssets.FirstOrDefault(x =>
                x.Asset.Descriptor.OrganizationId.ToString() == orgId &&
                x.Asset.Descriptor.ProjectId.ToString() == assetProjectId &&
                x.Asset.Descriptor.AssetId.ToString() == assetId);
            return offlineAsset == null ? null : (OfflineAsset)offlineAsset.Asset;
        }

        private static List<AssetInfo> ReturnOfflineAssets()
        {
            if (!Directory.Exists(LocalStreamingAssetPath))
            {
                return null;
            }
            
            List<AssetInfo> offlineAssets = null;
            
            List<string> allOfflineAssetsFolders = new List<string>();
            
            string[] allOfflineAssets = Directory.GetDirectories(LocalStreamingAssetPath);
            foreach (var allOfflineAsset in allOfflineAssets)
            {
                if(allOfflineAsset.Contains("_temp")) continue;
                allOfflineAssetsFolders.Add(allOfflineAsset);
            }
            
            if(allOfflineAssetsFolders.Count == 0)
            {
                return null;
            }
            
            foreach (var folder in allOfflineAssetsFolders)
            {
                var offlineAssetJsonFilePath =
                    Path.Combine(folder, OfflineAssetJsonFileName);
                if (!File.Exists(offlineAssetJsonFilePath)) continue;
                var json = File.ReadAllText(offlineAssetJsonFilePath);
                var offlineAssetInfo = JsonConvert.DeserializeObject<OfflineAssetInfo>(json);
                
                if (!string.IsNullOrEmpty(offlineAssetInfo.previewPic))
                {
                    offlineAssetInfo.previewPic = Path.Combine(folder, offlineAssetInfo.previewPic);
                }
                
                var offlineAsset = new OfflineAsset(offlineAssetInfo);
                offlineAssets ??= new List<AssetInfo>();
                offlineAssets.Add(new AssetInfo()
                {
                    Asset = offlineAsset,
                    Properties = null
                });
            }

            return offlineAssets;
        }
        
        public static void RemoveCache(IAsset asset, Action callBack)
        {
            string hashFolderName = ReturnHashName(asset);
            RemoveCacheFolder(hashFolderName);
            callBack?.Invoke();
        }
        
        private static void RemoveCacheFolder(string folderName)
        {
            if (!Directory.Exists(LocalStreamingAssetPath)) return;
            var matchingFolders = Directory.GetDirectories(LocalStreamingAssetPath, folderName+"*");
            foreach (var folder in matchingFolders)
            {
                Directory.Delete(folder, true);
            }
        }
        
        public static void MakeTempFolderComplete(IAsset asset)
        {
            string hashFolderName = ReturnHashName(asset);
            
            if (!Directory.Exists(LocalStreamingAssetPath))
            {
                Directory.CreateDirectory(LocalStreamingAssetPath);
            }
            
            var matchingFolders = Directory.GetDirectories(LocalStreamingAssetPath, hashFolderName + "*");
            foreach (var matchingFolder in matchingFolders)
            {
                var directoryName = new DirectoryInfo(matchingFolder).Name;
                if (directoryName.Contains("_temp"))
                {
                    var newName = directoryName.Replace("_temp", "");
                    Directory.Move(matchingFolder, Path.Combine(LocalStreamingAssetPath, newName));
                }
            }
        }
        
#if !UNITY_WEBGL || UNITY_EDITOR
        
        public static void RemoveCache(OfflineAsset asset, Action callBack)
        {
            string hashFolderName = ReturnHashName(asset);
            RemoveCacheFolder(hashFolderName);
            callBack?.Invoke();
        }
        
        public static void RemoveCache(LayoutModelEntity asset, Action callBack)
        {
            string hashFolderName = ReturnHashName(asset);
            RemoveCacheFolder(hashFolderName);
            callBack?.Invoke();
        }
        
#endif
        
        public static string HashString(string input)
        {
            int cutDownBytes = 16;
            byte[] sha256Hash = ComputeSHA256Hash();
            
            byte[] cutDownHash = new byte[cutDownBytes];
            Array.Copy(sha256Hash, cutDownHash, cutDownBytes);
            
            string base32Encoded = Base32Encoder.ToBase32String(cutDownHash);

            return base32Encoded;
            
            byte[] ComputeSHA256Hash()
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                    return sha256.ComputeHash(inputBytes);
                }
            }
        }
        
        private static class Base32Encoder
        {
            private static readonly char[] Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();
            private static readonly byte[] Base32Lookup = new byte[256];

            static Base32Encoder()
            {
                for (int i = 0; i < Base32Lookup.Length; i++)
                {
                    Base32Lookup[i] = 0xFF;
                }
                for (int i = 0; i < Base32Chars.Length; i++)
                {
                    Base32Lookup[Base32Chars[i]] = (byte)i;
                }
            }

            public static string ToBase32String(byte[] bytes)
            {
                StringBuilder result = new StringBuilder((bytes.Length + 4) / 5 * 8);

                for (int i = 0; i < bytes.Length; i += 5)
                {
                    ulong buffer = 0;
                    int bitsLeft = 0;
                    for (int j = i; j < i + 5 && j < bytes.Length; j++)
                    {
                        buffer <<= 8;
                        buffer |= bytes[j];
                        bitsLeft += 8;
                    }

                    int pad = 8 - (bitsLeft + 4) / 5;
                    buffer <<= pad * 5;

                    for (int j = 0; j < 8 - pad; j++)
                    {
                        int index = (int)((buffer >> (35 - j * 5)) & 0x1F);
                        result.Append(Base32Chars[index]);
                    }
                }

                return result.ToString();
            }
        }
    }
}
