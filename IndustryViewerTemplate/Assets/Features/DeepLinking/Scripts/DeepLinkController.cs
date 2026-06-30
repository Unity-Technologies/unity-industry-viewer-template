using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using Unity.Cloud.DeepLinking;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using UnityEngine;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.DeepLinking
{
    /// <summary>
    /// This controller handles deep linking not UI logic.
    /// </summary>
    public class DeepLinkController : MonoBehaviour
    {
        public static Action AccessErrorAction;
        public static Action CreationErrorAction;
        public static Action CreatedLinkAction;
        public static Action NotSupportedAction;
        public static Action ShowSelectionUIAction;
        public static Action ShowOrganizationUIAction;
        
        public static Uri DeepLinkUri { get; private set; }
        public static DeepLinkInfo DeepLinkInfo { get; private set; }
        public static IAssetProject AssetProject { get; private set; }
        public static AssetInfo? AssetInfo { get; private set; }
        public static CollectionDescriptor? AssetCollectionDescriptor { get; private set; }

        public static DeepLinkController Instance { get; private set; }

        public static bool IsDeepLinkCreationEnabled =>
            !IdentityController.GuestMode
            && !NetworkDetector.RequestedOfflineMode
            && !NetworkDetector.IsOffline
            && PlatformServices.IsUserLoggedIn;

        private async Task<DeepLinkInfo> GetDeepLinkInfoAsync()
        {
            if (DeepLinkInfo == null && DeepLinkUri != null)
            {
                try
                {
                    /* Possible exceptions:
                    /// <exception cref="UnauthorizedException"></exception>
                    /// <exception cref="ConnectionException"></exception>
                    /// <exception cref="ForbiddenException">Thrown if current user has no permission over the expected returned `deepLinkInfo.ResourceId`.</exception> */
                    DeepLinkInfo = await PlatformServices.DeepLinkProvider.GetDeepLinkInfoAsync(DeepLinkUri);
                    Debug.Log($"Deep linking: link '{DeepLinkUri}' decoded: resId: {DeepLinkInfo?.ResourceId}, resType: {DeepLinkInfo?.ResourceType}");
                }
                catch (ForbiddenException forbiddenException)
                {
                    Debug.LogError($"Deep linking: failed to get deep link '{DeepLinkUri}'. Exception: {forbiddenException}");
                    AccessErrorAction?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Deep linking: failed to get deep link '{DeepLinkUri}'. Exception: {exception}");
                }
            }

            return DeepLinkInfo;
        }

        private static AssetDescriptor GetAssetDescriptor(ResourceId resourceId)
        {
            var splitId = resourceId.ToString().Split(',');
            if (splitId.Length < 4)
            {
                throw new System.FormatException($"Unexpected deep link ResourceId format (expected 4 comma-separated parts): {resourceId}");
            }
            var orgId = splitId[0];
            var projectId = splitId[1];
            var assetId = splitId[2];
            var assetVersion = splitId[3];

            return new AssetDescriptor(
                new ProjectDescriptor(
                    new OrganizationId(orgId),
                    new ProjectId(projectId)),
                new AssetId(assetId),
                new AssetVersion(assetVersion)
            );
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            ClearCurrentLink();
            PlatformServices.UrlRedirectionInterceptor.DeepLinkForwarded += OnDeepLinkForwardedAsync;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            ClearCurrentLink();
            PlatformServices.UrlRedirectionInterceptor.DeepLinkForwarded -= OnDeepLinkForwardedAsync;
            IdentityController.UserInfoUpdatedEvent -= OnUserInfoUpdated;
            Instance = null;
        }

        public static void ClearCurrentLink()
        {
            DeepLinkUri = null;
            DeepLinkInfo = null;
            AssetProject = null;
            AssetInfo = null;
            AssetCollectionDescriptor = null;
        }

        public async Task CreateDeepLinkAndCopyToClipboardAsync(AssetInfo assetInfo)
        {
            string deepLink;

            try
            {
                /* Possible exceptions:
                /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the request fails to complete. See the returned StatusCode for more details.</exception>
                /// <exception cref="UnauthorizedException"></exception>
                /// <exception cref="ConnectionException"></exception>
                /// <exception cref="UriFormatException"></exception>
                /// <exception cref="ForbiddenException">Thrown if current user has no permission to create a link for the provided `deepLinkInfo.ResourceId`.</exception> */
                var uri = await PlatformServices.DeepLinkProvider.CreateDeepLinkAsync(assetInfo.Asset.Descriptor);
                deepLink = uri.ToString();
                Debug.Log($"Deep linking: link created: '{deepLink}', asset: '{assetInfo.Properties?.Name}' ({assetInfo.Asset.Descriptor.AssetId})");
            }
            catch (ForbiddenException forbiddenException)
            {
                Debug.LogError($"Deep linking: failed to create link for asset '{assetInfo.Properties?.Name}' ({assetInfo.Asset.Descriptor.AssetId}). Exception: '{forbiddenException}'.");
                AccessErrorAction?.Invoke();
                return;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Deep linking: failed to create link for asset '{assetInfo.Properties?.Name}' ({assetInfo.Asset.Descriptor.AssetId}). Exception: '{exception}'.");
                CreationErrorAction?.Invoke();
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(deepLink);
#else
            GUIUtility.systemCopyBuffer = deepLink;
#endif
            CreatedLinkAction?.Invoke();
        }

        private async void OnDeepLinkForwardedAsync(Uri uri)
        {
            Debug.Log($"Deep linking: activated URL: '{uri}'.");

            ClearCurrentLink();

            if (IdentityController.GuestMode)
            {
                // We do not support deep linking in Guest mode.
                Debug.LogWarning("Deep linking: Guest mode is enabled. Deep linking is not supported in Guest mode.");
                return;
            }

            DeepLinkUri = uri;

            // Handle case when user is not logged in
            if (IdentityController.UserInfo == null)
            {
                Debug.Log("Deep linking: user is not logged in. Waiting for login...");
                IdentityController.UserInfoUpdatedEvent -= OnUserInfoUpdated;
                IdentityController.UserInfoUpdatedEvent += OnUserInfoUpdated;
                return;
            }

            var deepLinkInfo = await GetDeepLinkInfoAsync();
            if (deepLinkInfo == null) return;

            _ = ProcessDeepLinkAsync();
        }

        private void OnUserInfoUpdated(IUserInfo info)
        {
            if (info != null)
            {
                IdentityController.UserInfoUpdatedEvent -= OnUserInfoUpdated;
                Debug.Log("Deep linking: user is logged in. Continue with link activation...");
                _ = ProcessDeepLinkAsync();
            }
        }

        private async Task ProcessDeepLinkAsync()
        {
            var deepLinkInfo = await GetDeepLinkInfoAsync();
            if (deepLinkInfo == null) return;

            // For offline mode , we show a message dialog and do not open the asset in viewer.
            if (NetworkDetector.RequestedOfflineMode || NetworkDetector.IsOffline)
            {
                Debug.Log("Deep linking: offline mode. Show message for user and break the flow.");
                ClearCurrentLink();
                NotSupportedAction?.Invoke();
                return;
            }

            _ = ShowSelectionUIAndSelectAssetAsync();
        }

        private async Task ShowSelectionUIAndSelectAssetAsync()
        {
            var deepLinkInfo = DeepLinkInfo;
            if (deepLinkInfo == null)
            {
                Debug.LogError("DeepLinkInfo is null. Cannot open asset in viewer.");
                return;
            }

            if (deepLinkInfo.ResourceType != DeepLinkResourceType.Asset)
            {
                Debug.LogError($"Resource type '{deepLinkInfo.ResourceType}' is not supported for opening in the viewer.");
                return;
            }

            if (await FetchAssetManagerServiceData() == false) return;

            // Open the Asset selection UI.
            // Do nothing if it's not opened, user can be on landing page without selected organization.
            // Next step will select organization, which automatically will open the Asset selection UI.
            ShowSelectionUIAction?.Invoke();

            // Select Organization in Asset selection UI, then Project will be selected.
            ShowOrganizationUIAction?.Invoke();
        }

        private async Task<bool> FetchAssetManagerServiceData()
        {
            var assetDescriptor = GetAssetDescriptor(DeepLinkInfo.ResourceId);

            var repository = IdentityController.GuestMode ?
                PlatformServices.ServiceAccountAssetRepository :
                PlatformServices.AssetRepository;

            // Fetch the asset project
            try
            {
                AssetProject = await repository.GetAssetProjectAsync(assetDescriptor.ProjectDescriptor, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Deep linking: can't get asset project '{assetDescriptor.ProjectDescriptor.ProjectId}' via API. Exception: {exception}.");
                AccessErrorAction?.Invoke();
                ClearCurrentLink();
                return false;
            }

            // Fetch the asset
            try
            {
                var asset = await AssetProject.GetAssetAsync(assetDescriptor.AssetId, assetDescriptor.AssetVersion, CancellationToken.None);
                AssetInfo = new AssetInfo
                {
                    Asset = asset,
                    Properties = await asset.GetPropertiesAsync(CancellationToken.None)
                };
            }
            catch (Exception exception)
            {
                Debug.LogError($"Deep linking: can't get asset '{assetDescriptor.AssetId}' version '{assetDescriptor.AssetVersion}' via API. Exception: {exception}.");
                AccessErrorAction?.Invoke();
                ClearCurrentLink();
                return false;
            }

            // Fetch the asset collection and find the first one that matches the project descriptor
            try
            {
                var asyncCollectionDescriptors = AssetInfo.Value.Asset.ListLinkedAssetCollectionsAsync(Range.All, CancellationToken.None);
                var projectDescriptor = AssetProject.Descriptor;
                await foreach (var collectionDescriptor in asyncCollectionDescriptors)
                {
                    if (collectionDescriptor.ProjectDescriptor == projectDescriptor)
                    {
                        AssetCollectionDescriptor = collectionDescriptor;
                        //Debug.Log($"Deep linking: found first collection '{collectionDescriptor.Path}' for asset '{AssetInfo.Value.Asset.Name}'.");
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Deep linking: can't get asset collections '{assetDescriptor.AssetId}' version '{assetDescriptor.AssetVersion}' via API. Exception: {exception}.");
                AccessErrorAction?.Invoke();
                ClearCurrentLink();
                return false;
            }

            //Debug.Log($"Deep linking: no linked asset collections found for asset '{AssetInfo.Value.Asset.Name}' in project '{AssetProject.Name}'.");
            return true; // we don't fail here because asset can be located in root of project.
        }
    }
}