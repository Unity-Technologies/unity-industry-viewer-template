using System.Net.Http.Headers;
using System.Threading.Tasks;
using Unity.Cloud.Common;

namespace Unity.Industry.Viewer.Shared
{
    public class ServiceAccountAuthorizer : IServiceAuthorizer
    {
        private readonly string _accountCredentials;
        private readonly string _organizationId;
        
        /// <summary>
        /// Returns an <see cref="IServiceAuthorizer"/> implementation that expects service account credentials.
        /// </summary>
        /// <remarks>
        /// The Unity service account must be created for the correct organization and have the correct permissions to access Unity Cloud APIs.
        /// </remarks>
        /// <param name="serviceAccountCredentials"><see cref="ServiceAccountCredentials"/> scriptable object that provides encoded credentials to authorize with.</param>
        public ServiceAccountAuthorizer(ServiceAccountCredentials serviceAccountCredentials)
        {
            _accountCredentials = serviceAccountCredentials.Credentials;
        }

        /// <inheritdoc cref="IServiceAuthorizer.AddAuthorization"/>
        public Task AddAuthorization(HttpHeaders headers)
        {
            headers.AddAuthorization(_accountCredentials, ServiceHeaderUtils.k_BasicScheme);
            return Task.CompletedTask;
        }
    }
}
