using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PostSharp.Patterns.Contracts;

namespace VeeamApi
{
    /// <summary>
    /// Cient of the Veeam Backup Enterprise Manager Restful API.
    /// This class contains operations to manage Cloud Tenants and associated resources.
    /// The account used to access the Veeam API must have one of the following roles :
    ///     Portal Administrators
    ///     Portal Users
    ///     Restore Operators
    /// </summary>
    [RethrowException]
    public class VeeamClient : IVeeamClient
    {
        #region Constants

        private const string headerTokenName = "X-RestSvcSessionId";

        #endregion

        [Required]
        [StrictlyPositive]
        public int Port { get; set; }

        [Required]
        public string Host { get; set; }

        /// <summary>
        /// Username:Password base64 encoded
        /// </summary>
        [Required]
        public string UsernamePasswordHash { get; set; }

        //Http related stuff manager
        private IHttpClientHelper httpHelper;
        private bool RunSetup = false;

        #region Constructors

        private VeeamClient()
        {
            httpHelper = new HttpClientHelper();
        }

        public VeeamClient(string host, int port, string usernamePasswordHash) : this()
        {
            this.Host = host;
            this.Port = port;
            this.UsernamePasswordHash = usernamePasswordHash;
            Task.Run(() => Logon()).Wait();
        }

        private VeeamClient(IHttpClientHelper httpClientHelper)
        {
            httpHelper = httpClientHelper;
        }

        public VeeamClient(IHttpClientHelper httpClientHelper, string host, int port, string usernamePasswordHash) : this(httpClientHelper)
        {
            this.Host = host;
            this.Port = port;
            this.UsernamePasswordHash = usernamePasswordHash;
            Task.Run(() => Logon()).Wait();
        }

        #endregion

        /// <summary>
        /// Get everything ready
        /// </summary>
        private void Setup()
        {
            var baseUrl = $"http://{Host}:{Port}";
            httpHelper.HeaderTokenName = headerTokenName;
            httpHelper.SetBaseAddress(baseUrl);
            httpHelper.OnUnauthorizeEvent += new UnauthorizedEvent.OnUnauthorized(PerformLogon);
            RunSetup = true;
        }

        #region Authentication

        public string Token { get => httpHelper.Token; }

        /// <summary>
        /// Event handler called when a request has been tagged as 401.Unauthorized 
        /// </summary>
        public async Task PerformLogon(object sender, EventArgs e) => await Logon();

        /// <summary>
        /// Authenticate the user to grant him access to the Veeam API
        /// </summary>
        public async Task Logon()
        {
            if (!RunSetup) Setup();
            var enterpriseManagerType = await GetApiAsync();
            await PostLogonAsync(
                enterpriseManagerType.Links.First(l => l.Rel == nameof(Rel.Create)).Href);
        }

        /// <summary>
        /// The client sends an unauthorized GET HTTP request to the base URL of Veeam  API to get a logon link
        /// </summary>
        private async Task<EnterpriseManagerType> GetApiAsync() =>
            await httpHelper.GetAsync<EnterpriseManagerType>("/api/");

        /// <summary>
        /// Request authentication
        /// </summary>
        private async Task<LogonSessionType> PostLogonAsync(string url)
        {
            using (var response = await httpHelper.AuthenticateBasic(UsernamePasswordHash, url))
            {
                return await httpHelper.ReadAsync<LogonSessionType>(response);
            }
        }

        #endregion

        #region Create Cloud Tenant

        /// <summary>
        /// Get the list of cloud tenants
        /// </summary>
        public async Task<EntityReferenceListType> GetTenants() =>
            await httpHelper.GetAsync<EntityReferenceListType>("/api/cloud/tenants");

        /// <summary>
        /// Create a tenant with minimal information
        /// </summary>
        public async Task<CloudTenantEntityType> CreateTenant(string name, string password, string backupServerUid, string description = null, bool enabled = true)
        {
            var tenant = new CreateCloudTenantSpecType()
            {
                Name = name,
                Password = password,
                Enabled = enabled,
                BackupServerUid = backupServerUid
            };
            if (null != description)
                tenant.Description = description;
            return await CreateTenant(tenant);
        }

        /// <summary>
        /// Create a tenant, with resources in the same object
        /// </summary>
        public async Task<CloudTenantEntityType> CreateTenant(CreateCloudTenantSpecType tenant) =>
            await httpHelper.PostAsync<CloudTenantEntityType, CreateCloudTenantSpecType>
                    ("/api/cloud/tenants", tenant);

        /// <summary>
        /// Create a tenant with resources as separate objects
        /// </summary>">
        public async Task<CloudTenantEntityType> CreateTenant(CreateCloudTenantSpecType tenant,
            IList<CreateCloudTenantResourceSpecType> backups = null,
            IList<CloudTenantComputeResourceCreateSpecType> replicas = null)
        {
            if (null != backups) //include backups
            {
                tenant.Resources = new CreateCloudTenantResourceListType()
                {
                    Items = backups.ToArray()
                };
            }
            if (null != replicas)
            {
                tenant.ComputeResources = new CloudTenantComputeResourceCreateListType()
                {
                    Items = replicas.ToArray()
                };
            }
            return await CreateTenant(tenant);
        }

        #endregion

        #region Get Tenant information

        /// <summary>
        /// Retrieve tenant information providing his id
        /// </summary>
        public async Task<CloudTenantEntityType> GetTenantById(string uid) =>
            await httpHelper.GetAsync<CloudTenantEntityType>($"/api/cloud/tenants/{uid}?format=Entity");

        /// <summary>
        /// Retrieve tenant information providing his name
        /// </summary>
        public async Task<CloudTenantEntityType> GetTenantByName(string name)
        {
            var tenants = await GetTenants();
            //find tenant
            var tenant = tenants.Items.SingleOrDefault(t => t.Name == name);
            if (null == tenant) throw new KeyNotFoundException($"Tenant {name} not found");
            return await GetTenantById(tenant.UID);
        }

        #endregion

        #region Enable / Disable Tenant

        /// <summary>
        /// Change the Enabled property of a tenant
        /// </summary>
        /// <param name="uid">Tenant ID</param>
        /// <param name="password">Tenant password: mandatory with the request, but will be updated if different</param>
        /// <param name="enable">True / False</param>
        /// <returns>True if update has succeeded, false otherwise</returns>
        public async Task<bool> EnableTenantById(string uid, string password, bool enable = true)
        {
            var tenant = await GetTenantById(uid);
            tenant.Enabled = enable;
            tenant.Password = password;
            return await httpHelper.PutAsync($"/api/cloud/tenants/{uid}", tenant);
        }

        /// <summary>
        /// Enable tenant. See EnableTenantById for details
        /// </summary>
        public async Task<bool> EnableTenantByName(string name, string password, bool enable = true)
        {
            var tenant = await GetTenantByName(name);
            return await EnableTenantById(tenant.UID, password, enable);
        }

        #endregion

        #region Allocate Resources For Tenant CRUD

        #region Backup

        /// <summary>
        /// Create backup resource for a tenant
        /// </summary>
        public async Task<CloudTenantResourceType> CreateTenantBackupResource(string tenantUid, CreateCloudTenantResourceSpecType resource) =>
            await httpHelper.PostAsync<CloudTenantResourceType, CreateCloudTenantResourceSpecType>
                    ($"/api/cloud/tenants/{tenantUid}/resources", resource);

        /// <summary>
        /// Get all backup resources existing for all tenants
        /// </summary>
        public async Task<CloudTenantResourceListType> GetTenantBackupResources(string tenantUid) =>
            await httpHelper.GetAsync<CloudTenantResourceListType>($"/api/cloud/tenants/{tenantUid}/resources");

        /// <summary>
        /// Get one specific backup resource information
        /// </summary>
        public async Task<CloudTenantResourceType> GetTenantBackupResource(string tenantUid, string resourceId) =>
            await httpHelper.GetAsync<CloudTenantResourceType>($"/api/cloud/tenants/{tenantUid}/resources/{resourceId}");

        /// <summary>
        /// Update backup resource of a tenant.
        /// Usage : Call GetTenantBackupResource, modify the CloudTenantResourceType and give it as a parameter to this method.
        /// Be aware that the used quota will be resetted.
        /// </summary>
        public async Task<CloudTenantResourceType> UpdateTenantBackupResource(string tenantUid, CloudTenantResourceType resource)
        {
            await DeleteTenantBackupResource(tenantUid, resource.Id);
            CreateCloudTenantResourceSpecType createCloudTenantResource = new CreateCloudTenantResourceSpecType()
            {
                RepositoryUid = resource.Item.RepositoryUid,
                Name = resource.Item.DisplayName,
                QuotaMb = (int)resource.Item.Quota,
                WanAcceleratorUid = resource.Item.WanAcceleratorUid
            };
            return await CreateTenantBackupResource(tenantUid, createCloudTenantResource);
        }

        /// <summary>
        /// Delete the backup resource of a tenant
        /// </summary>
        public async Task<bool> DeleteTenantBackupResource(string tenantUid, string resourceId) =>
            await httpHelper.DeleteAsync($"/api/cloud/tenants/{tenantUid}/resources/{resourceId}");

        #endregion

        #region Replica

        /// <summary>
        /// Create a replication resource for a tenant
        /// Subscribes a tenant account with the specified ID to the hardware plan. 
        /// Be aware that it will automatically replace any other subscription the tenant could have on that hardware plan.
        /// </summary>
        public async Task<CloudTenantComputeResourceType> CreateTenantReplicaResource(string tenantUid, CloudTenantComputeResourceCreateSpecType resource)
        {
            await httpHelper.PostAsync<CloudTenantComputeResourceType, CloudTenantComputeResourceCreateSpecType>
                    ($"/api/cloud/tenants/{tenantUid}/computeResources", resource);
            //The above request does not return the object created so we manually try to get it
            var tenantComputeResources = await GetTenantReplicaResources(tenantUid);
            var tenantComputeResource = tenantComputeResources.Items?.SingleOrDefault(r => r.CloudHardwarePlanUid == resource.CloudHardwarePlanUid);
            if (null == tenantComputeResource) throw new Exception(
                $"The compute resource for tenant { tenantUid } and { resource.CloudHardwarePlanUid } has not been found");
            return tenantComputeResource;
        }

        /// <summary>
        /// Get all replication resources
        /// </summary>
        public async Task<CloudTenantComputeResourceListType> GetTenantReplicaResources(string tenantUid) =>
            await httpHelper.GetAsync<CloudTenantComputeResourceListType>($"/api/cloud/tenants/{tenantUid}/computeResources");

        /// <summary>
        /// Get one replication resource
        /// </summary>
        public async Task<CloudTenantComputeResourceType> GetTenantReplicaResource(string tenantUid, string resourceId) =>
            await httpHelper.GetAsync<CloudTenantComputeResourceType>($"/api/cloud/tenants/{tenantUid}/computeResources/{resourceId}");

        /// <summary>
        /// Update a replication resource for a tenant.
        /// Be aware that it wil erase a previous subscription to that same hardware plan.
        /// </summary>
        public async Task<CloudTenantComputeResourceType> UpdateTenantReplicaResource(string tenantUid, CloudTenantComputeResourceType resource)
        {
            CloudTenantComputeResourceCreateSpecType createCloudTenantResource = new CloudTenantComputeResourceCreateSpecType()
            {
                CloudHardwarePlanUid = resource.CloudHardwarePlanUid,
                UseNetworkFailoverResources = resource.UseNetworkFailoverResources,
                NetworkAppliance = resource.NetworkAppliance,
                PlatformType = resource.PlatformType,
                WanAcceleratorUid = resource.WanAcceleratorUid
            };
            return await CreateTenantReplicaResource(tenantUid, createCloudTenantResource);
        }

        /// <summary>
        /// Delete specific replication resource
        /// </summary>
        public async Task<bool> DeleteTenantReplicaResource(string tenantUid, string resourceId) =>
            await httpHelper.DeleteAsync($"/api/cloud/tenants/{tenantUid}/computeResources/{resourceId}");

        #endregion

        #endregion

        #region Backup Servers

        public async Task<EntityReferenceListType> GetBackupServers() =>
            await httpHelper.GetAsync<EntityReferenceListType>("/api/backupServers");

        #endregion

        #region Repositories

        public async Task<EntityReferenceListType> GetRepositories() =>
            await httpHelper.GetAsync<EntityReferenceListType>("/api/repositories");

        #endregion

        #region Vlans

        public async Task<EntityReferenceListType> GetVlans() =>
            await httpHelper.GetAsync<EntityReferenceListType>("/api/cloud/vlans");

        #endregion

        #region Cloud Hardware Plans

        public async Task<EntityReferenceListType> GetCloudHardwarePlans() =>
            await httpHelper.GetAsync<EntityReferenceListType>("/api/cloud/hardwareplans");

        public async Task<CloudHardwarePlanEntityType> GetCloudHardwarePlanByName(string hardwarePlanName) =>
            await httpHelper.GetEntityAsync<CloudHardwarePlanEntityType>(
                await GetCloudHardwarePlans(), hardwarePlanName, "CloudHardwarePlan");

        #endregion

        #region Cloud Replica Resources

        public async Task<EntityReferenceListType> GetCloudReplicaResources() =>
            await httpHelper.GetAsync<EntityReferenceListType>("/api/cloud/replicas");

        #endregion

        #region Sessions

        public async Task<LogonSessionListType> GetLogonSessions() =>
            await httpHelper.GetAsync<LogonSessionListType>("/api/logonsessions");

        #endregion

        /// <summary>
        /// Logout
        /// </summary>
        public async Task Logout()
        {
            var sessions = await httpHelper.GetAsync<LogonSessionListType>("/api/logonSessions");
            //Should never have many but in case, cleanup every sessions
            var deleteLinks = sessions.Items.Select(s => s.Links.Single(l => l.Rel == nameof(Rel.Delete)).Href);
            foreach (var link in deleteLinks)
                await httpHelper.DeleteAsync(httpHelper.GetRequestUri(link));
        }

        ~VeeamClient()
        {
            Task.Run(() => Logout()).Wait();
            httpHelper.Client.Dispose();
        }
    }
}