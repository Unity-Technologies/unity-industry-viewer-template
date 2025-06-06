using System;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Cloud.Assets;
using UnityEngine;
using Unity.Cloud.Common;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using IAsset = Unity.Cloud.Assets.IAsset;

namespace Unity.Industry.Viewer.Streaming
{
    // This script manages the downloading of streaming data assets in a Unity project.
    // It handles the initialization, downloading, and processing of JSON and GLB files from a dataset.
    // The script supports asynchronous file downloads and updates the download progress.
    // It integrates with Unity's file system to store downloaded assets and provides event handlers for download completion.
    public class DownloadStreamingDataController
    {
        private enum AssetType
        {
            Streamable,
            GLB,
            Layout
        }

        public static Action<string, int, int, Action<bool>> KeepExistingAssets;
        
        public Action<IAsset, float> DownloadProgress;
        private float Progress => Mathf.Min((float)m_TotalDownloaded / m_TotalDownload, 0.9f);
        private AssetInfo m_SelectedAsset;
        private IDataset m_DataSet;
        private AssetType m_AssetType;
        private string m_StoragePath;
        private Queue<string> m_JsonFiles;
        private Queue<string> m_GLBFiles;
        private string m_DestinationPath;
        private int m_PendingDownloads = 0;
        private TaskCompletionSource<bool> m_AllDownloadsCompleted;
        private int m_TotalDownload = 0;
        private int m_TotalDownloaded = 0;
        private bool m_IsReferencedAsset = false;
        private Dictionary<IAsset, DownloadStreamingDataController> m_DownloadStreamingDataControllers;
        public bool PauseLooping => m_PauseLooping;
        private bool m_PauseLooping = false;
        private LayoutJson m_LayoutJson;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        

        public DownloadStreamingDataController(AssetInfo assetInfo, IDataset dataSet, bool referenced = false)
        {
            m_SelectedAsset = assetInfo;
            
            m_IsReferencedAsset = referenced;
            var folderName = $"{assetInfo.Asset.Descriptor.AssetId.ToString()}{assetInfo.Asset.Descriptor.ProjectId.ToString()}{assetInfo.Asset.Descriptor.OrganizationId.ToString()}";
            
            m_DestinationPath = Path.Combine(Application.persistentDataPath, "StreamingAssets", StreamingUtils.HashString(folderName) + "_" + assetInfo.Properties.Value.FrozenSequenceNumber + "_temp");
            
            m_DataSet = dataSet;

            _ = CheckDownload();

            //StreamAssetUIController.DownloadCacheFinished.Invoke(m_SelectedAsset);
        }

        private async Task CheckDownload()
        {
            var datasetProperties = await m_DataSet.GetPropertiesAsync(_cancellationTokenSource.Token);
            
            if (m_SelectedAsset.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag) || datasetProperties.SystemTags.Contains(StreamingUtils.StreamableTag))
            {
                m_AssetType = m_SelectedAsset.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag)?  AssetType.Layout : AssetType.Streamable;
                _ = GetJsonFileFromDataset();
                return;
            }

            m_AssetType = AssetType.GLB;
            _ = GetGLBFile();
        }

        private async Task GetGLBFile()
        {
            var files = m_DataSet.ListFilesAsync(Range.All, default);
            IFile fileToDownload = null;
            await foreach (var file in files)
            {
                if(_cancellationTokenSource.IsCancellationRequested) return;
                if(file.Descriptor.Path.EndsWith(".glb", StringComparison.CurrentCultureIgnoreCase) || file.Descriptor.Path.EndsWith(".gltf", StringComparison.CurrentCultureIgnoreCase))
                {
                    fileToDownload = file;
                    break;
                }
            }
            if(fileToDownload == null) return;
            if (!Directory.Exists(m_DestinationPath))
            {
                Directory.CreateDirectory(m_DestinationPath);
            }
            var finalPath = Path.Combine(m_DestinationPath, Path.GetFileName(fileToDownload.Descriptor.Path));
            var progress = new Progress<HttpProgress>(DownloadProcess);
            await using var fileStream = File.OpenWrite(finalPath);
            await fileToDownload.DownloadAsync(fileStream, progress, _cancellationTokenSource.Token);

            Debug.Log(StreamingUtils.LocalStreamingAssetPath);
            
            _ = WriteJSONFile();
            
            void DownloadProcess(HttpProgress progress)
            {
                float downloadProgress = progress.DownloadProgress ?? 0f;
                DownloadProgress?.Invoke(m_SelectedAsset.Asset, Mathf.Min(downloadProgress, 0.9f));
            }
        }

        private async Task GetJsonFileFromDataset()
        {
            var allFiles = m_DataSet.ListFilesAsync(Range.All, _cancellationTokenSource.Token);
            var jsonFileName = m_AssetType == AssetType.Streamable? StreamingUtils.TilesetJson : StreamingUtils.LayoutJson;
            await foreach(var file in allFiles)
            {
                if(_cancellationTokenSource.IsCancellationRequested) return;
                if(!string.Equals(file.Descriptor.Path, jsonFileName)) continue;
                var jsonFilePath = m_DataSet.GetFileUrl(file.Descriptor.Path);
                
                var path = jsonFilePath.ToString();
                var lastSlashIndex = path.LastIndexOf('/');
                m_StoragePath = path[..(lastSlashIndex + 1)];
                
                _ = DownloadFile(jsonFilePath);
                break;
            }
        }

        private async Task DownloadFile(Uri path)
        {
            m_PendingDownloads++;
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.RequestUri = path;
            requestMessage.Method = HttpMethod.Get;
            
            var httpClient = IdentityController.GuestMode? PlatformServices.ServiceAccountServiceHttpClient : PlatformServices.ServiceHttpClient;
            
            try
            {
                HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage, _cancellationTokenSource.Token);
                responseMessage.EnsureSuccessStatusCode();
                var jsonString = await responseMessage.Content.ReadAsStringAsync();

                var fileName = path.Segments[^1];
                
                await using Stream responseStream = await responseMessage.Content.ReadAsStreamAsync();
                if (!Directory.Exists(m_DestinationPath))
                {
                    Directory.CreateDirectory(m_DestinationPath);
                }
                
                var finalPath = Path.Combine(m_DestinationPath, fileName);
                
                await using FileStream fileStream =
                    new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                await responseStream.CopyToAsync(fileStream, _cancellationTokenSource.Token);

                if (string.Equals(Path.GetExtension(fileName), ".json"))
                {
                    if (m_AssetType == AssetType.Streamable)
                    {
                        m_JsonFiles ??= new Queue<string>();
                        if (m_JsonFiles.Count > 0)
                        {
                            QueryNextFile(m_JsonFiles);
                        }
                        ParseJsonFile(jsonString);
                    } else if (m_AssetType == AssetType.Layout)
                    {
                        ParseLayoutJson(jsonString);
                    }
                }
                else if (string.Equals(Path.GetExtension(fileName), ".glb"))
                {
                    m_GLBFiles ??= new Queue<string>();
                    if (m_GLBFiles.Count > 0)
                    {
                        QueryNextFile(m_GLBFiles);
                    }
                }
            }
            catch (HttpRequestException e)
            {
                Debug.Log("Error downloading json file: " + e.Message);
            }
            finally
            {
                if (m_AssetType == AssetType.Streamable)
                {
                    m_PendingDownloads--;
                    m_TotalDownloaded++;
                    DownloadProgress?.Invoke(m_SelectedAsset.Asset, Progress);
                    m_JsonFiles ??= new Queue<string>();
                    m_GLBFiles ??= new Queue<string>();
                    
                    if (m_JsonFiles.Count == 0 && m_GLBFiles.Count == 0 && m_PendingDownloads == 0)
                    {
                        m_AllDownloadsCompleted ??= new TaskCompletionSource<bool>();
                        m_AllDownloadsCompleted.SetResult(true);
                        Debug.Log(StreamingUtils.LocalStreamingAssetPath);
                        await WriteJSONFile();
                        DownloadProgress?.Invoke(m_SelectedAsset.Asset, 1f);
                        StreamAssetUIController.DownloadCacheFinished.Invoke(m_SelectedAsset);
                    }
                }
            }
        }

        private async Task WriteJSONFile()
        {
            OfflineAssetInfo offlineAssetInfo = new OfflineAssetInfo(m_SelectedAsset, m_DestinationPath);
            await offlineAssetInfo.m_Completed.Task;
            var json = JsonConvert.SerializeObject(offlineAssetInfo, Formatting.None);
            var finalPath = Path.Combine(m_DestinationPath, StreamingUtils.OfflineAssetJsonFileName);
            await File.WriteAllTextAsync(finalPath, json);
        }

        private void ParseLayoutJson(string jsonContent)
        {
            m_LayoutJson = JsonConvert.DeserializeObject<LayoutJson>(jsonContent);

            _ = PrepareDownload();
            return;

            async Task PrepareDownload()
            {
                var organizationRepository = PlatformServices.OrganizationRepository;
                var assetRepository = PlatformServices.AssetRepository;
                var orgList = new List<IOrganization>();
                var organizationsAsyncEnumerable = organizationRepository.ListOrganizationsAsync(Range.All, _cancellationTokenSource.Token);
                await foreach (var org in organizationsAsyncEnumerable)
                {
                    if (_cancellationTokenSource.IsCancellationRequested) return;
                    orgList.Add(org);
                }
                
                var queue = new Queue<LayoutModelEntity>(m_LayoutJson.LayoutModels.GroupBy(model => new { model.orgID, model.assetID, model.projectID, model.versionID }).Select(g => g.First()));
                
                m_TotalDownload = queue.Count;
                while (queue.Count > 0)
                {
                    var layoutModelEntity = queue.Dequeue();
                    if (string.IsNullOrEmpty(layoutModelEntity.orgID))
                    {
                        m_LayoutJson.LayoutModels.Remove(layoutModelEntity);
                        m_TotalDownload--;
                        continue;
                    }
                    
                    IAsset asset = null;
                    var projectDescriptor = new ProjectDescriptor(new OrganizationId(layoutModelEntity.orgID), new ProjectId(layoutModelEntity.projectID));
                    if (string.IsNullOrEmpty(layoutModelEntity.versionID))
                    {
                        try
                        {
                            var assetProject =
                                await assetRepository.GetAssetProjectAsync(projectDescriptor,
                                    _cancellationTokenSource.Token);
                            var assetsVersion = assetProject.QueryAssetVersions(new AssetId(layoutModelEntity.assetID))
                                .OrderBy("versionNumber", SortingOrder.Descending)
                                .WithCacheConfiguration(new AssetCacheConfiguration()
                                {
                                    CacheProperties = true
                                }).ExecuteAsync(_cancellationTokenSource.Token);
                            await foreach (var assetVersion in assetsVersion)
                            {
                                if (_cancellationTokenSource.IsCancellationRequested) return;
                                var properties = await assetVersion.GetPropertiesAsync(_cancellationTokenSource.Token);
                                if (properties.FrozenSequenceNumber != layoutModelEntity.version) continue;
                                asset = assetVersion;
                            }
                        }
                        catch (Exception e)
                        {
                            m_LayoutJson.LayoutModels.Remove(layoutModelEntity);
                            m_TotalDownload--;
                            continue;
                        }
                    }
                    else
                    {
                        try
                        {
                            asset = await assetRepository.GetAssetAsync(new AssetDescriptor(projectDescriptor,
                                new AssetId(layoutModelEntity.assetID), new AssetVersion(layoutModelEntity.versionID)), _cancellationTokenSource.Token);
                        }
                        catch (Exception e)
                        {
                            m_LayoutJson.LayoutModels.Remove(layoutModelEntity);
                            m_TotalDownload--;
                            continue;
                        }
                    }
                    
                    if (asset == null)
                    {
                        Debug.LogWarning("Asset not found");
                        m_LayoutJson.LayoutModels.Remove(layoutModelEntity);
                        m_TotalDownload--;
                        continue;
                    }

                    var property = await asset.GetPropertiesAsync(_cancellationTokenSource.Token);
                    
                    if (StreamingUtils.CheckHasLocalAsset(asset, out var ver))
                    {
                        var offlineAssetInfo = StreamingUtils.ReturnOfflineAssetInfo(asset);
                        if (!string.Equals(offlineAssetInfo.OfflineAssetInfo.assetVersionId, asset.Descriptor.AssetVersion.ToString()))
                        {
                            //Delete the old asset and download the new one
                            m_PauseLooping = true;
                            var toKeep = false;
                            KeepExistingAssets?.Invoke(property.Name, property.FrozenSequenceNumber, offlineAssetInfo.OfflineAssetInfo.assetVersion, (keepAsset) =>
                            {
                                toKeep = keepAsset;
                                m_PauseLooping = false;
                            });
                            while (m_PauseLooping)
                            {
                                float elapsed = 0f;
                                while (elapsed < 0.5f)
                                {
                                    await Task.Yield();
                                    elapsed += Time.deltaTime;
                                }
                            }
                            if (toKeep)
                            {
                                m_LayoutJson.LayoutModels.Remove(layoutModelEntity);
                                m_TotalDownload--;
                                continue;
                            }
                            StreamingUtils.RemoveCache(offlineAssetInfo, null);
                            await StartDownload(new AssetInfo()
                            {
                                Asset = asset,
                                Properties = property
                            });
                            continue;
                        }

                        //Asset already exists in the local storage
                        m_TotalDownload--;
                    }
                    else
                    {
                        //Download the asset
                        await StartDownload(new AssetInfo()
                        {
                            Asset = asset,
                            Properties = property
                        });
                    }
                }
                
                if (m_TotalDownload == 0)
                {
                    DownloadProgress?.Invoke(m_SelectedAsset.Asset, 0.9f);
                    m_AllDownloadsCompleted ??= new TaskCompletionSource<bool>();
                    m_AllDownloadsCompleted.SetResult(true);
                    await WriteJSONFile();
                    DownloadProgress?.Invoke(m_SelectedAsset.Asset, 1f);
                    StreamAssetUIController.DownloadCacheFinished.Invoke(m_SelectedAsset);
                }
            }

            async Task StartDownload(AssetInfo assetInfo)
            {
                IDataset streamableDataset = null;
                var datasets = assetInfo.Asset.ListDatasetsAsync(Range.All, _cancellationTokenSource.Token);
                await foreach (var dataset in datasets)
                {
                    if (_cancellationTokenSource.IsCancellationRequested) return;
                    var datasetProperties = await dataset.GetPropertiesAsync(_cancellationTokenSource.Token);
                    if (!datasetProperties.SystemTags.Contains(StreamingUtils.StreamableTag)) continue;
                    streamableDataset = dataset;
                    break;
                }
                if(streamableDataset == null) return;
                StreamingUtils.RemoveCache(assetInfo.Asset, null);
                var downloadController = new DownloadStreamingDataController(assetInfo, streamableDataset, true);
                m_DownloadStreamingDataControllers ??= new Dictionary<IAsset, DownloadStreamingDataController>();
                m_DownloadStreamingDataControllers.Add(assetInfo.Asset, downloadController);
                downloadController.DownloadProgress += OnDownloadProgress;
            }
        }

        private void OnDownloadProgress(IAsset asset, float progress)
        {
            var currentProgress = (float)m_TotalDownloaded;
            if (m_DownloadStreamingDataControllers != null)
            {
                foreach (var controller in m_DownloadStreamingDataControllers.Values)
                {
                    currentProgress += controller.Progress;
                }
            }
            DownloadProgress?.Invoke(m_SelectedAsset.Asset, Mathf.Min(currentProgress / m_TotalDownload, 0.9f));
            if (progress >= 1f)
            {
                m_TotalDownloaded++;
                StreamingUtils.MakeTempFolderComplete(asset);
                if(TryGetDownloadController(asset, out var downloadStreamingDataController))
                {
                    downloadStreamingDataController.DownloadProgress -= OnDownloadProgress;
                    m_DownloadStreamingDataControllers.Remove(asset);
                }

                if (m_TotalDownload != m_TotalDownloaded)
                {
                    return;
                }
                DownloadProgress?.Invoke(m_SelectedAsset.Asset, 0.9f);
                m_AllDownloadsCompleted ??= new TaskCompletionSource<bool>();
                m_AllDownloadsCompleted.SetResult(true);
                
                _ = FinaliseDownload();
            }
            
            return;

            async Task FinaliseDownload()
            {
                await WriteJSONFile();
                DownloadProgress?.Invoke(m_SelectedAsset.Asset, 1f);
                StreamAssetUIController.DownloadCacheFinished.Invoke(m_SelectedAsset);
            }
        }
        
        private bool TryGetDownloadController(IAsset asset, out DownloadStreamingDataController downloadStreamingDataController)
        {
            if (m_DownloadStreamingDataControllers == null)
            {
                downloadStreamingDataController = null;
                return false;
            }
            foreach (var key in m_DownloadStreamingDataControllers.Keys)
            {
                if (!string.Equals(key.Descriptor.AssetId.ToString(), asset.Descriptor.AssetId.ToString()) ||
                    !string.Equals(key.Descriptor.ProjectId.ToString(), asset.Descriptor.ProjectId.ToString()) ||
                    !string.Equals(key.Descriptor.OrganizationId.ToString(),
                        asset.Descriptor.OrganizationId.ToString())) continue;
                downloadStreamingDataController = m_DownloadStreamingDataControllers[key];
                return true;
            }
            downloadStreamingDataController = null;
            return false;
        }

        private void ParseJsonFile(string jsonContent)
        {
            TileJson tileJson = JsonConvert.DeserializeObject<TileJson>(jsonContent);
            ProcessGeometryData(tileJson.root);
        }

        private void ProcessGeometryData(TileJson.Geometry geometry)
        {
            if (string.Equals(Path.GetExtension(geometry.content.uri), ".json"))
            {
                m_TotalDownload++;
                m_JsonFiles ??= new Queue<string>();
                m_JsonFiles.Enqueue(geometry.content.uri);
                if(m_JsonFiles.Count == 1)
                {
                    QueryNextFile(m_JsonFiles);
                }
            }
            else if(string.Equals(Path.GetExtension(geometry.content.uri), ".glb"))
            {
                m_TotalDownload++;
                m_GLBFiles ??= new Queue<string>();
                m_GLBFiles.Enqueue(geometry.content.uri);
                if(m_GLBFiles.Count == 1)
                {
                    QueryNextFile(m_GLBFiles);
                }
            }

            if (geometry.children == null || geometry.children.Count == 0)
            {
                return;
            }

            foreach (var children in geometry.children)
            {
                ProcessGeometryData(children);
            }
        }

        private void QueryNextFile(Queue<string> queue)
        {
            var name = queue.Dequeue();
            _ = DownloadFile(ReturnFullStoragePath(name));
        }
        
        private Uri ReturnFullStoragePath(string fileName)
        {
            return new Uri(m_StoragePath + fileName);
        }

        public void CancelTask()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
}
