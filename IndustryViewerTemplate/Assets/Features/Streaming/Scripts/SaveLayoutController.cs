using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Industry.Viewer.Assets;
using Unity.Cloud.Assets;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading.Tasks;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using System.Linq;
using System.IO;

namespace Unity.Industry.Viewer.Streaming
{
    public static class SaveLayoutController
    {
        private static IAssetProject m_project;
        private static IAssetCollection m_assetCollection;
        private static string m_layoutName;
        private static Texture2D m_texture;
        private static string m_LayoutJSON;
        private static Action<AssetInfo?, string> m_saveCompleteCallback;
        
        public static void SaveLayout(string layoutName, IAssetProject project,
            IAssetCollection assetCollection, Texture2D screenshot, Action<AssetInfo?, string> saveCompleteCallback)
        {
            m_project = project;
            m_assetCollection = assetCollection;
            m_layoutName = layoutName;
            m_texture = screenshot;
            m_saveCompleteCallback = saveCompleteCallback;
            
            SharedUIManager.ShowLoadingModal(() =>
            {
                List<LayoutModelEntity> layoutModels = new List<LayoutModelEntity>();
                var streamModels = TransformController.Instance.GetComponentsInChildren<StreamingModel>();
                foreach (var streamingModel in streamModels)
                {
                    layoutModels.Add(new LayoutModelEntity(streamingModel));
                }
                var layoutJson = new LayoutJson(layoutModels);
                m_LayoutJSON = JsonConvert.SerializeObject(layoutJson);
                WriteAndSave();
            });
        }

        private static void Clear()
        {
            m_project = null;
            m_assetCollection = null;
            m_layoutName = string.Empty;
            if (m_texture != null)
            {
                UnityEngine.Object.Destroy(m_texture);
                m_texture = null;
            }
            m_saveCompleteCallback = null;
        }

        private static void WriteAndSave()
        {
            _ = WriteAndSaveAsync();
            
            return;
            async Task WriteAndSaveAsync()
            {
                string previewPath = "layout.png";
                
                var jsonFileCreation = new FileCreation(StreamingUtils.LayoutJson)
                {
                    Path = StreamingUtils.LayoutJson,
                    Description = string.Empty,
                    Tags = new List<string>() { StreamingUtils.LayoutTag }
                };
                
                var previewFileCreation = new FileCreation(previewPath)
                {
                    Path = previewPath,
                    Description = string.Empty,
                    Tags = new List<string>() { StreamingUtils.LayoutTag }
                };
                
                IAsset unFrozenAsset = null;
                IDataset sourceDataset = null;
                IDataset previewDataset = null;
                string changeLog = string.Empty;
                
                bool updating = StreamingModelController.StreamingAsset.Value.Properties.Value.Tags.Contains(StreamingUtils
                    .LayoutTag);
                int currentVersion = updating
                    ? StreamingModelController.StreamingAsset.Value.Properties.Value.FrozenSequenceNumber
                    : 0;
                
                if (!updating)
                {
                    //New layout asset
                    //changeLog = "Initial Layout";
                    
                    var newAssetCreation = new AssetCreation(m_layoutName)
                    {
                        Tags = new List<string>() { StreamingUtils.LayoutTag },
                        Type = AssetType.Other,
                    };
                    
                    if (m_assetCollection != null)
                    {
                        newAssetCreation.Collections = new List<CollectionPath>() { m_assetCollection.Descriptor.Path };
                    }

                    try
                    {
                        unFrozenAsset = await m_project.CreateAssetAsync(newAssetCreation, CancellationToken.None);
                        if (unFrozenAsset == null)
                        {
                            m_saveCompleteCallback?.Invoke(null, "unknown");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        m_saveCompleteCallback?.Invoke(null, e.Message);
                        Clear();
                        Debug.Log(e.Message);
                    }
                }
                else
                {
                    changeLog = "Updated Layout";
                    try
                    {
                        unFrozenAsset =
                            await StreamingModelController.StreamingAsset.Value.Asset.CreateUnfrozenVersionAsync(
                                CancellationToken.None);
                        if (unFrozenAsset == null)
                        {
                            m_saveCompleteCallback?.Invoke(null, "unknown");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        m_saveCompleteCallback?.Invoke(null, e.Message);
                        Clear();
                        Debug.Log(e.Message);
                    }
                }
                
                sourceDataset = await unFrozenAsset.GetSourceDatasetAsync(CancellationToken.None);
                previewDataset = await unFrozenAsset.GetPreviewDatasetAsync(CancellationToken.None);

                if (updating)
                {
                    IFile file = null;
                    file = await sourceDataset.GetFileAsync(StreamingUtils.LayoutJson, CancellationToken.None);
                    if (file != null)
                    {
                        await sourceDataset.RemoveFileAsync(StreamingUtils.LayoutJson, CancellationToken.None);
                    }

                    if (previewDataset != null)
                    {
                        file = await previewDataset.GetFileAsync(previewPath, CancellationToken.None);
                        if (file != null)
                        {
                            await previewDataset.RemoveFileAsync(previewPath, CancellationToken.None);
                        }
                    }
                }
                
                await sourceDataset.UploadFileAsync(jsonFileCreation, ReturnLayoutMemoryStream(), null, CancellationToken.None);
                
                await previewDataset.UploadFileAsync(previewFileCreation, ReturnPictureMemoryStream(), null, CancellationToken.None);
                IAssetFreeze newFreeze = new AssetFreeze(changeLog)
                {
                    Operation = AssetFreezeOperation.WaitOnTransformations
                };
                await unFrozenAsset.FreezeAsync(newFreeze, CancellationToken.None);
                while (true)
                {
                    await Task.Yield(); // WebGL-friendly alternative to Task.Delay
                    var latestVersion = await unFrozenAsset.WithLatestVersionAsync(CancellationToken.None);
                    var properties = await latestVersion.GetPropertiesAsync(CancellationToken.None);
                    if (properties.FrozenSequenceNumber > currentVersion && properties.ParentFrozenSequenceNumber == currentVersion)
                    {
                        var assetInfo = new AssetInfo()
                        {
                            Asset = latestVersion,
                            Properties = properties
                        };
                        m_saveCompleteCallback?.Invoke(assetInfo, string.Empty);
                       Clear();
                        return;
                    }
                }
            }

            MemoryStream ReturnLayoutMemoryStream()
            {
                var jsonMemoryStream = new MemoryStream();
                var writer = new StreamWriter(jsonMemoryStream);
                writer.WriteAsync(m_LayoutJSON);
                writer.FlushAsync();
                jsonMemoryStream.Position = 0;

                return jsonMemoryStream;
            }

            MemoryStream ReturnPictureMemoryStream()
            {
                byte[] bytes = m_texture.EncodeToPNG();
                var previewMemoryStream = new MemoryStream(bytes);
                return previewMemoryStream;
            }
        }
    }
}
