using PostSharp.Patterns.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VeeamApi
{
    public interface IVeeamClient
    {
        [Required] [StrictlyPositive] int Port { get; set; }
        [Required] string Host { get; set; }
        [Required] string UsernamePasswordHash { get; set; }
        string Token { get; }

        Task<CloudTenantEntityType> CreateTenant(string name, string password, string backupServerUid, string description = null, bool enabled = true);
        Task<CloudTenantEntityType> CreateTenant(CreateCloudTenantSpecType tenant);
        Task<CloudTenantEntityType> CreateTenant(CreateCloudTenantSpecType tenant, IList<CreateCloudTenantResourceSpecType> backups = null, IList<CloudTenantComputeResourceCreateSpecType> replicas = null);
        Task<CloudTenantResourceType> CreateTenantBackupResource(string uid, CreateCloudTenantResourceSpecType resource);
        Task<CloudTenantComputeResourceType> CreateTenantReplicaResource(string uid, CloudTenantComputeResourceCreateSpecType resource);
        Task<bool> DeleteTenantBackupResource(string uid, string resourceId);
        Task<bool> DeleteTenantReplicaResource(string uid, string resourceId);
        Task<bool> EnableTenantById(string uid, string password, bool enable = true);
        Task<bool> EnableTenantByName(string name, string password, bool enable = true);
        Task<EntityReferenceListType> GetBackupServers();
        Task<EntityReferenceListType> GetCloudHardwarePlans();
        Task<CloudHardwarePlanEntityType> GetCloudHardwarePlanByName(string hardwarePlanName);
        Task<EntityReferenceListType> GetCloudReplicaResources();
        Task<LogonSessionListType> GetLogonSessions();
        Task<EntityReferenceListType> GetRepositories();
        Task<CloudTenantResourceType> GetTenantBackupResource(string uid, string resourceId);
        Task<CloudTenantResourceListType> GetTenantBackupResources(string uid);
        Task<CloudTenantEntityType> GetTenantById(string uid);
        Task<CloudTenantEntityType> GetTenantByName(string name);
        Task<CloudTenantComputeResourceType> GetTenantReplicaResource(string uid, string resourceId);
        Task<CloudTenantComputeResourceListType> GetTenantReplicaResources(string uid);
        Task<EntityReferenceListType> GetTenants();
        Task<EntityReferenceListType> GetVlans();
        Task Logon();
        Task Logout();
        Task<CloudTenantResourceType> UpdateTenantBackupResource(string uid, CloudTenantResourceType resource);
        Task<CloudTenantComputeResourceType> UpdateTenantReplicaResource(string uid, CloudTenantComputeResourceType resource);
    }
}