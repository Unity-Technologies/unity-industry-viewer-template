using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Unity.Cloud.Assets;
using UnityEngine;
using System.Threading.Tasks;
using System.Text;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Identity;
using Unity.Cloud.Common;

namespace Unity.Industry.Viewer.Assets
{
    // This script provides functionality for downloading textures in Unity.
    // It includes methods for downloading texture thumbnails from URLs and caching them.
    // The script uses UnityWebRequest for downloading textures asynchronously.
    // It manages a cache of downloaded textures to avoid redundant downloads.
    // The script also handles invoking callbacks once the texture download is complete.
    public static class TextureDownload
    {
        private class TextureDownloadEntry
        {
            public bool IsDownloading;
            public Texture2D Texture2D;
            public string versionId;
            public string Url;
            public CancellationTokenSource CancellationTokenSource;
            public readonly List<Action<Texture2D>> Listeners = new();
            
            public void CancelToken()
            {
                if (CancellationTokenSource == null) return;
                CancellationTokenSource.Cancel();
                CancellationTokenSource.Dispose();
                CancellationTokenSource = null;
            }
            
            public bool IsCancelled()
            {
                return CancellationTokenSource == null || CancellationTokenSource.IsCancellationRequested;
            }
        }

        private static Dictionary<int, TextureDownloadEntry> s_TextureCache = new();

        public static void ClearCache()
        {
            var textureCacheCopy = s_TextureCache.Values.ToArray();
            s_TextureCache.Clear();
            foreach (var entry in textureCacheCopy)
            {
                entry.CancelToken();
            }            
        }

        public static async Task DownloadThumbnail(IAsset asset, Action<Texture2D> actionCallBack)
        {
            var downloadKey = asset.Descriptor.AssetId.GetHashCode();
            var versionId = asset.Descriptor.AssetVersion.ToString();
            Uri url = null;
            bool updateUrl = false;
            if (s_TextureCache.TryGetValue(downloadKey, out var entry))
            {
                if (entry.versionId != versionId)
                {
                    // Cancel existing download
                    entry.CancelToken();
                    url = await asset.GetPreviewUrlAsync(CancellationToken.None);
                    updateUrl = true;
                }
                else
                {
                    url = new Uri(entry.Url);
                }
            }
            else
            {
                url = await asset.GetPreviewUrlAsync(CancellationToken.None);
                
                if (url == null)
                {
                    actionCallBack?.Invoke(null);
                    return;
                }
            }

            if (updateUrl)
            {
                s_TextureCache.Remove(downloadKey);
            }

            _ = Download(downloadKey, versionId, url.ToString(), actionCallBack);
        }

        static async Task Download(int key, string versionId, string url, Action<Texture2D> actionCallBack)
        {
            Debug.Log(url);
            if(!s_TextureCache.TryGetValue(key, out var entry))
            {
                entry = new TextureDownloadEntry
                {
                    IsDownloading = true,
                    Url = url,
                    versionId = versionId,
                    CancellationTokenSource = new CancellationTokenSource()
                };
                
                lock (entry.Listeners)
                {
                    entry.Listeners.Add(actionCallBack);
                }
                
                s_TextureCache.Add(key, entry);

                try
                {
                    var taskTexture = DownloadTexture(url.ToString(), entry.CancellationTokenSource.Token);
                    if (await Task.WhenAny(taskTexture) == taskTexture)
                    {
                        if (!entry.IsCancelled())
                        {
                            entry.Texture2D = taskTexture.Result;
                            entry.IsDownloading = false;
                        }
                    }
                    else
                    {
                        //Get Preset
                        entry.IsDownloading = false;
                        if (!entry.IsCancelled())
                        {
                            actionCallBack?.Invoke(null);
                        }
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Download was cancelled, don't invoke callback
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    if (!entry.IsCancelled())
                    {
                        OnTextureDownloaded(entry);
                        actionCallBack?.Invoke(null);
                    }
                }
                
            } else if (entry.IsDownloading)
            {
                lock (entry.Listeners)
                {
                    entry.Listeners.Add(actionCallBack);
                }
                return;
            }
            OnTextureDownloaded(entry);
            actionCallBack?.Invoke(entry.Texture2D);
        }
        
        static async Task<Texture2D> DownloadTexture(string url, CancellationToken cancellationToken = default)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            var httpClient = IdentityController.GuestMode? PlatformServices.ServiceAccountServiceHttpClient : PlatformServices.ServiceHttpClient;

            try
            {
                HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage, cancellationToken);
                responseMessage.EnsureSuccessStatusCode();
                var bytes = await responseMessage.Content.ReadAsByteArrayAsync();
                var texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                return texture;
            }
            catch (HttpRequestException e)
            {
                Debug.Log($"Failed to download texture from {url}: {e.Message}");
            }
            return null;
        }
        
        static void OnTextureDownloaded(TextureDownloadEntry entry)
        {
            entry.IsDownloading = false;

            lock (entry.Listeners)
            {
                foreach (var listener in entry.Listeners)
                {
                    if (!entry.IsCancelled())
                    {
                        listener?.Invoke(entry.Texture2D);
                    }
                }
                entry.Listeners.Clear();
            }
        }

        public static void CancelDownload(int assetId)
        {
            var key = assetId;
            if (s_TextureCache.TryGetValue(key, out var entry))
            {
                entry.CancelToken();
                s_TextureCache.Remove(key);
            }
        }

        public static Task DownloadThumbnail(int assetId, string versionId, string url, Action<Texture2D> actionCallBack)
        {
            var key = assetId;
            var sb = new StringBuilder(url);
            #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_IOS
            if (!url.StartsWith("file://"))
            {
                sb.Insert(0, "file://");
            }
            #else
            if (!url.StartsWith("file:///"))
            {
                sb.Insert(0, "file:///");
            }
            #endif
            url = sb.ToString();
            
            _ = Download(key, versionId, url, actionCallBack);
            return Task.CompletedTask;
        }
    }
}
