using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using Unity.Cloud.Identity;

namespace Unity.Industry.Viewer.Assets
{
    public class ServiceAccountOrganization : IOrganization
    {
        /// <summary>
        /// Gets the organization ID.
        /// </summary>
        public OrganizationId Id { get; }
        /// <summary>
        /// Gets the name of the organization.
        /// </summary>
        public string Name => organizationName;

        private string organizationName;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceAccountOrganization"/> class with the specified organization ID.
        /// </summary>
        /// <param name="organizationId">The ID of the organization.</param>
        public ServiceAccountOrganization(string organizationId, string organizationName)
        {
            Id = new OrganizationId(organizationId);
            this.organizationName = organizationName;
        }

        #region Not Implemented

        /// <summary>
        /// Throws a <see cref="NotImplementedException"/> as listing roles is not supported for the service account organization.
        /// </summary>
        /// <returns>Throws a <see cref="NotImplementedException"/>.</returns>
        public Task<IEnumerable<Role>> ListRolesAsync() => throw new NotImplementedException();
        /// <summary>
        /// Throws a <see cref="NotImplementedException"/> as listing permissions is not supported for the service account organization.
        /// </summary>
        /// <returns>Throws a <see cref="NotImplementedException"/>.</returns>
        public Task<IEnumerable<Permission>> ListPermissionsAsync() => throw new NotImplementedException();
        /// <summary>
        /// Throws a <see cref="NotImplementedException"/> as listing members is not supported for the service account organization.
        /// </summary>
        /// <param name="range">The range of members to list.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>Throws a <see cref="NotImplementedException"/>.</returns>
        public IAsyncEnumerable<IMemberInfo> ListMembersAsync(Range range, CancellationToken cancellationToken = new())
            => throw new NotImplementedException();
        /// <summary>
        /// Throws a <see cref="NotImplementedException"/> as listing projects is not supported for the service account organization.
        /// </summary>
        /// <param name="range">The range of projects to list.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>Throws a <see cref="NotImplementedException"/>.</returns>
        public IAsyncEnumerable<IProject> ListProjectsAsync(Range range, CancellationToken cancellationToken = new())
            => throw new NotImplementedException();

        /// <summary>
        /// Throws a <see cref="NotImplementedException"/> as role information is not available for the service account organization.
        /// </summary>
        public Role Role => throw new NotImplementedException();

        #endregion
    }
}
