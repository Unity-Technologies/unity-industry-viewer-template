using System.Threading.Tasks;
using Unity.Cloud.Identity;
using Unity.Cloud.Identity.Runtime;
using Unity.Cloud.Common;
using Unity.Cloud.Common.Runtime;
using Unity.Cloud.AppLinking.Runtime;
using Unity.Cloud.Assets;
using Unity.Cloud.DataStreaming.Runtime;
using System.Reflection;

namespace Unity.Industry.Viewer.Shared
{
    // This static class provides various platform services for Unity Cloud.
    // It includes authentication, asset management, data streaming, and service account handling.
    // The class initializes and manages service clients and repositories for these functionalities.
    // It supports asynchronous initialization and proper shutdown of services.
    public static class PlatformServices
    {
        static CompositeAuthenticator _sCompositeAuthenticator;
        
        public static ICompositeAuthenticator CompositeAuthenticator => _sCompositeAuthenticator;
        
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
        public static IServiceHttpClient ServiceHttpClient { get; private set; }
        
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
        public static IServiceHostResolver ServiceHostResolver { get; private set; }
        
        #endregion

        #region Serviec Account
        public static IServiceHttpClient ServiceAccountServiceHttpClient { get; private set; }
        private static IServiceAuthorizer ServiceAccountServiceAuthorizer { get; set; }
        public static IAssetRepository ServiceAccountAssetRepository { get; private set; }
        public static ServiceAccountCredentials ServiceAccountCredentials { get; private set; }
        #endregion
        
        public static void Create(ServiceAccountCredentials serviceAccountCredentials)
        {
            HttpClient = new UnityHttpClient();
            ServiceAccountCredentials = serviceAccountCredentials;
            var playerSettings = UnityCloudPlayerSettings.Instance;
            var platformSupport = PlatformSupportFactory.GetAuthenticationPlatformSupport();
            ServiceHostResolver = UnityRuntimeServiceHostResolverFactory.Create();

            var compositeAuthenticatorSettings = new CompositeAuthenticatorSettingsBuilder(HttpClient, platformSupport, ServiceHostResolver, playerSettings)
                .AddDefaultBrowserAuthenticatedAccessTokenProvider(playerSettings)
                .AddDefaultPkceAuthenticator(playerSettings)
                .Build();

            _sCompositeAuthenticator = new CompositeAuthenticator(compositeAuthenticatorSettings);

            #region Assets

            ServiceHttpClient = new ServiceHttpClient(HttpClient, _sCompositeAuthenticator, playerSettings)
                .WithApiSourceHeadersFromAssembly(Assembly.GetExecutingAssembly());

            AssetRepository = AssetRepositoryFactory.Create(ServiceHttpClient, ServiceHostResolver);

            #endregion
            
            #region Service Account
            if(serviceAccountCredentials == null || string.IsNullOrEmpty(serviceAccountCredentials.Credentials)) return;
            ServiceAccountServiceAuthorizer = new ServiceAccountAuthorizer(serviceAccountCredentials);

            ServiceAccountServiceHttpClient = new ServiceHttpClient(HttpClient, ServiceAccountServiceAuthorizer, playerSettings)
                .WithApiSourceHeadersFromAssembly(Assembly.GetExecutingAssembly());
            ServiceAccountAssetRepository = AssetRepositoryFactory.Create(ServiceAccountServiceHttpClient, ServiceHostResolver);
            #endregion
        }

        public static async Task InitializeAsync()
        {
            await _sCompositeAuthenticator.InitializeAsync();
        }

        public static void Shutdown()
        {
            _sCompositeAuthenticator?.Dispose();
            _sCompositeAuthenticator = null;
            ServiceHttpClient = null;
            AssetRepository = null;
            HttpClient = null;
            ServiceHostResolver = null;
            m_DataStreamer = null;
            ServiceAccountServiceAuthorizer = null;
            ServiceAccountServiceHttpClient = null;
            ServiceAccountAssetRepository = null;
        }
    }
}
