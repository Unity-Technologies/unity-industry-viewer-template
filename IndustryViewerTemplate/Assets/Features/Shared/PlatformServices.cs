using System;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Cloud.AppLinking;
using Unity.Cloud.AppLinking.Runtime;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using Unity.Cloud.Common.Runtime;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.DeepLinking;
using Unity.Cloud.Identity;
using Unity.Cloud.Identity.Runtime;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading;
using Unity.Cloud.Collaboration;

namespace Unity.Industry.Viewer.Shared
{
    // This static class provides various platform services for Unity Cloud.
    // It includes authentication, asset management, data streaming, and service account handling.
    // The class initializes and manages service clients and repositories for these functionalities.
    // It supports asynchronous initialization and proper shutdown of services.
    public static class PlatformServices
    {
        private const string k_ServiceAccountEnvVar = "UNITY_SERVICE_ACCOUNT_CREDENTIALS";

        private static ICertificateValidationPolicy _sCertificateValidationPolicy;
        
        static ICompositeAuthenticator _sCompositeAuthenticator;
        
        public static ICompositeAuthenticator CompositeAuthenticator => _sCompositeAuthenticator;
        
        public static bool IsUserLoggedIn => _sCompositeAuthenticator != null &&
                                             (_sCompositeAuthenticator.AuthenticationState == AuthenticationState.LoggedIn ||
                                              (ServiceAccountServiceAuthenticator != null &&
                                               ServiceAccountServiceAuthenticator.AuthenticationState == AuthenticationState.LoggedIn));
        
        public static ServiceConnector ServiceConnector { get; private set; }
        
        #region Assets
        
        /// <summary>
        /// Returns an <see cref="IOrganizationRepository"/>.
        /// </summary>
        public static IOrganizationRepository OrganizationRepository => _sCompositeAuthenticator;
        
        /// <summary>
        /// Returns an <see cref="IAssetRepository"/>.
        /// </summary>
        public static IAssetRepository AssetRepository { get; private set; }
        
        /// <summary>
        /// Returns a <see cref="UnityHttpClient"/>
        /// </summary>
        public static IHttpClient HttpClient { get; private set; }
        
        #endregion
        
        #region Streaming
        
        /// <summary>
        /// Returns a <see cref="IServiceHttpClient"/>.
        /// </summary>
        public static IServiceHttpClient ServiceHttpClient => ServiceConnector.ServiceHttpClient
            .WithApiSourceHeadersFromAssembly(Assembly.GetExecutingAssembly());

        
        /// <summary>
        /// Returns a <see cref="IDataStreamer"/>.
        /// </summary>
        public static IDataStreamer DataStreamer {
            get { return m_DataStreamer ??= IDataStreamer.Create(); }
        }

        private static IDataStreamer m_DataStreamer;
        
        #endregion
        
        #region Metadata

        /// <summary>
        /// Returns a <see cref="IServiceHostResolver"/>.
        /// </summary>
        public static IServiceHostResolver ServiceHostResolver => ServiceConnector.ServiceHostResolver;
        
        #endregion

        #region Service Account
        public static IServiceHttpClient ServiceAccountServiceHttpClient { get; private set; }
        public static ServiceAccountAuthenticator ServiceAccountServiceAuthenticator {get; set;}
        public static IAssetRepository ServiceAccountAssetRepository { get; private set; }
        public static ServiceAccountCredentials ServiceAccountCredentials { get; private set; }
        #endregion

        #region Deep Linking

        public static DeepLinkProvider DeepLinkProvider { get; private set; }

        public static IUrlRedirectionInterceptor UrlRedirectionInterceptor => Unity.Cloud.AppLinking.Runtime.UrlRedirectionInterceptor.GetInstance();

        #endregion

        #region VPC

        private static VPCCredentials _mVpcCredentials;

        #endregion
        
        #region Collaboration

        public static AnnotationManagement AnnotationManagement { get; private set; }
        
        private static AnnotationManagementFactory m_aManagementFactory = new AnnotationManagementFactory();

        #endregion

        public static void Create(VPCCredentials vpcCredentials, ServiceAccountCredentials serviceAccountCredentials)
        {
            if (CertificatePinning.HasBuiltInManifest)
            {
                _sCertificateValidationPolicy = new PublicKeyPinningCertificateValidationPolicy();
            }
            
            HttpClient = new UnityHttpClient(CertificatePinning.HasBuiltInManifest? _sCertificateValidationPolicy : null);
            var playerSettings = UnityCloudPlayerSettings.Instance;
            var platformSupport = PlatformSupportFactory.GetAuthenticationPlatformSupport();

            if (vpcCredentials == null)
            {
                ServiceConnector = ServiceConnectorFactory.Create(platformSupport, HttpClient, playerSettings, playerSettings);
            }
            else
            {
                _mVpcCredentials = vpcCredentials;
                ServiceConnector = ServiceConnectorFactory.CreateForFullyQualifiedDomainName(platformSupport, HttpClient, 
                    playerSettings, playerSettings, vpcCredentials.DomainName, vpcCredentials.OpenIdConfigurationUrl, vpcCredentials.PathPrefixValue);
            }
            
            _sCompositeAuthenticator = ServiceConnector.CompositeAuthenticator;

            #region Assets

            AssetRepository = AssetRepositoryFactory.Create(ServiceHttpClient, ServiceHostResolver);

            #endregion
            
            #region Deep Linking
            DeepLinkProvider = new DeepLinkProvider(ServiceHttpClient, new QueryArgumentsProcessor(), ServiceHostResolver, new UnityRuntimeUrlProcessor(), playerSettings);
            #endregion
            
            #region Collaboration
            AnnotationManagement = m_aManagementFactory.CreateAnnotationManagement(ServiceHttpClient, ServiceHostResolver);
            #endregion

            #region Service Account
            if (serviceAccountCredentials == null || string.IsNullOrEmpty(serviceAccountCredentials.Credentials))
            {
                return;
            }
            
            ServiceAccountCredentials = serviceAccountCredentials;
            #endregion
        }

        public static void ServiceAccountCreation()
        {
            if(ServiceAccountCredentials == null) return;
            
            Environment.SetEnvironmentVariable(k_ServiceAccountEnvVar, ServiceAccountCredentials.Credentials, EnvironmentVariableTarget.Process);
            
            var platformSupport = PlatformSupportFactory.GetAuthenticationPlatformSupport();
            var playerSettings = UnityCloudPlayerSettings.Instance;
            
            var serviceAccountAuthenticatorSettingsBuilder =
                new ServiceAccountAuthenticatorSettingsBuilder(HttpClient, ServiceHostResolver, platformSupport)
                    .SetAppIdProvider(playerSettings);

            if (_mVpcCredentials != null)
            {
                var pkceConfigurationProvider = PkceConfigurationProviderFactory.CreateForFullyQualifiedDomainName(ServiceHostResolver, HttpClient, _mVpcCredentials.OpenIdConfigurationUrl, "sdk");
                serviceAccountAuthenticatorSettingsBuilder.SetServiceAccountCredentialsExchanger(
                    pkceConfigurationProvider);
            }
            
            ServiceAccountServiceAuthenticator =
                new ServiceAccountAuthenticator(serviceAccountAuthenticatorSettingsBuilder.Build());
            
            ServiceAccountServiceHttpClient = new ServiceHttpClient(HttpClient, ServiceAccountServiceAuthenticator, UnityCloudPlayerSettings.Instance)
                .WithApiSourceHeadersFromAssembly(Assembly.GetExecutingAssembly());;
            ServiceAccountAssetRepository = AssetRepositoryFactory.Create(ServiceAccountServiceHttpClient, ServiceHostResolver);
        }
        
        public static async Task InitializeServiceAccountAsync()
        {
            await ServiceAccountServiceAuthenticator.InitializeAsync();
        }

        public static async Task InitializeAsync()
        {
            await _sCompositeAuthenticator.InitializeAsync();
        }

        public static void Shutdown()
        {
            (_sCompositeAuthenticator as IDisposable)?.Dispose();
            _sCompositeAuthenticator = null;
            AssetRepository = null;
            HttpClient = null;
            m_DataStreamer = null;
            ServiceAccountLogout();
            (ServiceAccountServiceAuthenticator as IDisposable)?.Dispose();
            ServiceAccountServiceAuthenticator = null;
            (ServiceAccountServiceHttpClient as IDisposable)?.Dispose();
            ServiceAccountServiceHttpClient = null;
            ServiceAccountAssetRepository = null;
        }

        public static void ServiceAccountLogout()
        {
            var environmentVar = Environment.GetEnvironmentVariable(k_ServiceAccountEnvVar, EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(environmentVar))
            {
                // Clear the environment variable if it was set by this class, to avoid affecting other parts of the application or service account leakage.
                Environment.SetEnvironmentVariable(k_ServiceAccountEnvVar, string.Empty, EnvironmentVariableTarget.Process);
            }
        }
        
        /// <summary>
        /// Provides functionality to upload binary data to provided URL
        /// </summary>
        /// <param name="uploadUri">URL for data uploading</param>
        /// <param name="source">Array of bytes to upload</param>
        /// <param name="cancellationToken">Cancellation token for operation</param>
        /// <exception cref="InvalidUrlException">Thrown when uploading URL is not provided</exception>
        /// <exception cref="UploadFailedException">Thrown when uploading failed</exception>
        public static async Task UploadContentAsync(Uri uploadUri, string filePath, CancellationToken cancellationToken)
        {
            const string blobTypeHeaderKey = "X-Ms-Blob-Type";
            const string blobTypeHeaderValue = "BlockBlob";

            cancellationToken.ThrowIfCancellationRequested();

            if (uploadUri == null)
            {
                throw new InvalidUrlException("Upload url is null or empty");
            }

            using var httpRequestMessage = new HttpRequestMessage();
            httpRequestMessage.Method = HttpMethod.Put;
            httpRequestMessage.RequestUri = uploadUri;
            var bytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
            httpRequestMessage.Content = new ByteArrayContent(bytes);

            httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            httpRequestMessage.Headers.Add(blobTypeHeaderKey, blobTypeHeaderValue);

            using var response = await ServiceHttpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseContentRead, null, cancellationToken);
            var result = response.EnsureSuccessStatusCode();

            if (!result.IsSuccessStatusCode)
            {
                throw new UploadFailedException($"Upload of content stream for file id {uploadUri} failed.");
            }
        }
        
        /// <summary>
        /// Provides functionality to download binary data from provided URL
        /// </summary>
        /// <param name="downloadUri">URL to download data from</param>
        /// <param name="cancellationToken">Cancellation token for operation</param>
        /// <returns></returns>
        /// <exception cref="InvalidUrlException">Thrown when uploading URL is not provided</exception>
        public static async Task<byte[]> DownloadContentAsync(Uri downloadUri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (downloadUri == null)
            {
                throw new InvalidUrlException("Download url is null or empty");
            }

            using var httpRequestMessage = new HttpRequestMessage();
            httpRequestMessage.Method = HttpMethod.Get;
            httpRequestMessage.RequestUri = downloadUri;

            using var response = await ServiceHttpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseContentRead, null, cancellationToken);
            response.EnsureSuccessStatusCode();

            byte[] result = await response.Content.ReadAsByteArrayAsync();
            cancellationToken.ThrowIfCancellationRequested();

            return result;
        }
    }
}
