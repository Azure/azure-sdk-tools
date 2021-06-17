
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Graph.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Notifications.WebApi.Clients;
using Microsoft.VisualStudio.Services.Notifications.WebApi;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity.Client;
using Microsoft.VisualStudio.Services.Identity;
using System.Threading;
using System.Linq;

namespace NotificationConfiguration.Services
{
    /// <summary>
    /// Provides access to DevOps entities
    /// </summary>
    public class AzureDevOpsService
    {
        private readonly VssConnection connection;
        private readonly ILogger<AzureDevOpsService> logger;
        private Dictionary<Type, VssHttpClientBase> clientCache = new Dictionary<Type, VssHttpClientBase>();
        private SemaphoreSlim clientCacheSemaphore = new SemaphoreSlim(1);

        public static AzureDevOpsService CreateAzureDevOpsService(string token, string url, ILogger<AzureDevOpsService> logger)
        {
            var devOpsCreds = new VssBasicCredential("nobody", token);
            var devOpsConnection = new VssConnection(new Uri(url), devOpsCreds);
            var result = new AzureDevOpsService(devOpsConnection, logger);

            return result;
        }

        /// <summary>
        /// Creates a new AzureDevOpsService
        /// </summary>
        /// <param name="connection">VssConnection to use</param>
        /// <param name="logger">Logger</param>
        public AzureDevOpsService(VssConnection connection, ILogger<AzureDevOpsService> logger)
        {
            this.connection = connection;
            this.logger = logger;
        }

        private async Task<T> GetClientAsync<T>() 
            where T : VssHttpClientBase
        {
            var type = typeof(T);
            T result;
            await clientCacheSemaphore.WaitAsync();
            if (clientCache.ContainsKey(type))
            {
                
                result = (T)clientCache[type];
            }
            else
            {
                result = await connection.GetClientAsync<T>();
                clientCache.Add(type, result);
            }

            clientCacheSemaphore.Release();
            return result;
        }

        /// <summary>
        /// Gets build definitions
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <param name="pathPrefix">Prefix of the path in the build folder tree</param>
        /// <returns>IEnumerable of build definitions that satisfy the given criteria</returns>
        public async Task<IEnumerable<BuildDefinition>> GetPipelinesAsync(string projectName, string pathPrefix = null)
        {
            var client = await GetClientAsync<BuildHttpClient>();

            logger.LogInformation("GetScheduledPipelinesAsync ProjectName = {0} PathPrefix = {1}", projectName, pathPrefix);
            var definitions = await client.GetFullDefinitionsAsync2(project: projectName, path: pathPrefix);

            return definitions;
        }

        /// <summary>
        /// Gets a build definition for the given ID
        /// </summary>
        /// <param name="pipelineId"></param>
        /// <returns></returns>
        public async Task<BuildDefinition> GetPipelineAsync(string projectName, int pipelineId)
        {
            var client = await GetClientAsync<BuildHttpClient>();
            BuildDefinition result;
            try
            {
                logger.LogInformation("GetPipelineAsync ProjectName = {0} PipelineId = {1}", projectName, pipelineId);
                result = await client.GetDefinitionAsync(projectName, pipelineId);
            }
            catch (DefinitionNotFoundException)
            {
                result = default;
            }


            return result;
        }

        /// <summary>
        /// Returns teams in the given project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <param name="skip">Number of entries to skip</param>
        /// <param name="top">Maximum number of entries to return</param>
        /// <returns>Teams that satisfy given criteria</returns>
        internal async Task<IEnumerable<WebApiTeam>> GetTeamsAsync(string projectName, int skip = 0, int top = int.MaxValue)
        {
            var client = await GetClientAsync<TeamHttpClient>();

            logger.LogInformation("GetTeamsAsync ProjectName = {0}, skip = {1}", projectName, skip);
            var teams = await client.GetTeamsAsync(projectName, skip: skip, top: top);

            return teams;
        }

        /// <summary>
        /// Returns all teams in the given project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>All teams which satisfy the given criteria</returns>
        public async Task<IEnumerable<WebApiTeam>> GetAllTeamsAsync(string projectName)
        {
            var accumulator = new List<WebApiTeam>();
            var skip = 0;
            IEnumerable<WebApiTeam> teams;

            while (true)
            {
                teams = await GetTeamsAsync(projectName, skip: skip);

                if (!teams.Any())
                {
                    break;
                }

                accumulator.AddRange(teams);
                skip = accumulator.Count;
            }

            return accumulator;

        }

        /// <summary>
        /// Creates a team in the given project
        /// </summary>
        /// <param name="projectId">ID of project to associate with team</param>
        /// <param name="team">Team to create</param>
        /// <returns>Team with properties set from creation</returns>
        public async Task<WebApiTeam> CreateTeamForProjectAsync(string projectId, WebApiTeam team)
        {
            var client = await GetClientAsync<TeamHttpClient>();

            logger.LogInformation("CreateTeamForProjectAsync TeamName = {0} ProjectId = {1}", team.Name, projectId);

            var result = await client.CreateTeamAsync(team, projectId);
            return result;
        }

        /// <summary>
        /// Updates a team in the given project
        /// </summary>
        /// <param name="projectId">ID of project to associate with team</param>
        /// <param name="team">Team to create</param>
        /// <returns>Team with properties updated</returns>
        public async Task<WebApiTeam> UpdateTeamForProjectAsync(string projectId, WebApiTeam team)
        {
            var client = await GetClientAsync<TeamHttpClient>();

            logger.LogInformation("UpdateTeamForProjectAsync TeamName = {0} ProjectId = {1}", team.Name, projectId);

            var result = await client.UpdateTeamAsync(team, projectId, team.Id.ToString());
            return result;
        }

        /// <summary>
        /// Checks whether a child is a member of the parent entity
        /// </summary>
        /// <param name="parent">Parent descriptor</param>
        /// <param name="child">Child descriptor</param>
        /// <returns>True if the child is a member of the parent</returns>
        public async Task<bool> CheckMembershipAsync(string parent, string child)
        {
            var client = await GetClientAsync<GraphHttpClient>();
            
            logger.LogInformation("CheckMembership ParentId = {0} ChildId = {1}", parent, child);
            var result = await client.CheckMembershipExistenceAsync(child, parent);

            return result;
        }

        /// <summary>
        /// Gets the descriptor for an object
        /// </summary>
        /// <param name="id">GUID of the object</param>
        /// <returns>A descriptor string suitable for use in some Graph queries</returns>
        public async Task<string> GetDescriptorAsync(Guid id)
        {
            var client = await GetClientAsync<GraphHttpClient>();

            logger.LogInformation("GetDescriptor Id = {0}", id);
            var descriptor = await client.GetDescriptorAsync(id);
            return descriptor.Value;
        }

        /// <summary>
        /// Gets the descriptor for a given User Principal
        /// </summary>
        /// <param name="userPrincipal">User Principal (e.g. alias@contoso.com)</param>
        /// <returns></returns>
        public async Task<string> GetDescriptorForPrincipal(string userPrincipal)
        {
            var client = await GetClientAsync<GraphHttpClient>();

            logger.LogInformation("GetDescriptorForAlias UserPrincipal = {0}", userPrincipal);
            var context = new GraphUserPrincipalNameCreationContext()
            {
                PrincipalName = userPrincipal,
            };
            var user = await client.CreateUserAsync(context);

            return user.Descriptor;
        }

        /// <summary>
        /// Gets a list of TeamMembers
        /// </summary>
        /// <param name="team">Team</param>
        /// <returns>List of TeamMembers for the given team</returns>
        public async Task<List<TeamMember>> GetMembersAsync(WebApiTeam team)
        {
            var client = await GetClientAsync<TeamHttpClient>();

            logger.LogInformation("GetMembersAsync TeamId = {0}, TeamName = {1}", team.Id, team.Name);
            var members = await client.GetTeamMembersWithExtendedPropertiesAsync(
                team.ProjectId.ToString(), 
                team.Id.ToString()
            );
            return members;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<Identity> GetUserFromId(Guid id)
        {
            var client = await GetClientAsync<IdentityHttpClient>();

            logger.LogInformation("GetUserFromId Id = {0}", id);
            var result = await client.ReadIdentityAsync(id);
            return result;
        }

        public async Task RemoveMember(string groupDescriptor, string memberDescriptor)
        {
            var client = await GetClientAsync<GraphHttpClient>();

            logger.LogInformation("RemoveMember GroupDescriptor = {0}, MemberDescriptor = {1}", groupDescriptor, memberDescriptor);
            await client.RemoveMembershipAsync(memberDescriptor, groupDescriptor);
        }

        /// <summary>
        /// Adds a child item to a parent item
        /// </summary>
        /// <param name="parent">Parent descriptor</param>
        /// <param name="child">Child descriptor</param>
        /// <returns>GraphMembership object resulting from adding the child to the parent</returns>
        public async Task<GraphMembership> AddToTeamAsync(string parent, string child)
        {
            var client = await GetClientAsync<GraphHttpClient>();

            logger.LogInformation("AddTeamToTeamAsync ParentId = {0} ChildId = {1}", parent, child);
            var result = await client.AddMembershipAsync(child, parent);
            return result;
        }

        /// <summary>
        /// Gets subscriptions for a given target GUID
        /// </summary>
        /// <param name="targetId">GUID of the subscription target</param>
        /// <returns>Complete subscription objects</returns>
        public async Task<IEnumerable<NotificationSubscription>> GetSubscriptionsAsync(Guid targetId)
        {
            var client = await GetClientAsync<NotificationHttpClient>();

            logger.LogInformation("GetSubscriptionsAsync TargetId = {0}", targetId);
            var result = await client.ListSubscriptionsAsync(targetId, queryFlags: SubscriptionQueryFlags.IncludeFilterDetails);

            return result;
        }

        /// <summary>
        /// Creates a subscription
        /// </summary>
        /// <param name="newSubscription">Subscription to create</param>
        /// <returns>The newly created subscription</returns>
        public async Task<NotificationSubscription> CreateSubscriptionAsync(NotificationSubscriptionCreateParameters newSubscription)
        {
            var client = await GetClientAsync<NotificationHttpClient>();

            logger.LogInformation("CreateSubscriptionAsync Description = {0}", newSubscription.Description);
            var result = await client.CreateSubscriptionAsync(newSubscription);
            return result;
        }

        /// <summary>
        /// Updates a subscription
        /// </summary>
        /// <param name="newSubscription">Subscription to update</param>
        /// <returns>The updated subscription</returns>
        public async Task<NotificationSubscription> UpdatedSubscriptionAsync(NotificationSubscriptionUpdateParameters updateParameters, string subscriptionId)
        {
            var client = await GetClientAsync<NotificationHttpClient>();

            logger.LogInformation("UpdateSubscriptionAsync Id = {0}", subscriptionId);
            var result = await client.UpdateSubscriptionAsync(updateParameters, subscriptionId);
            return result;
        }
    }
}