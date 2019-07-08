
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Graph.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Notifications.WebApi.Clients;
using Microsoft.VisualStudio.Services.Notifications.WebApi;
using System;
using Microsoft.Extensions.Logging;

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
            if (clientCache.ContainsKey(type))
            {
                return (T)clientCache[type];
            }

            var result = await connection.GetClientAsync<T>();
            clientCache.Add(type, result);
            return result;
        }

        /// <summary>
        /// Returns build definitions that have at least one schedule trigger
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <param name="pathPrefix">Prefix of the path in the build folder tree</param>
        /// <returns>IEnumerable of build definitions that satisfy the given criteria</returns>
        public async Task<IEnumerable<BuildDefinition>> GetScheduledPipelinesAsync(string projectName, string pathPrefix = null)
        {
            var client = await GetClientAsync<BuildHttpClient>();

            logger.LogInformation("GetScheduledPipelinesAsync ProjectName = {0} PathPrefix = {1}", projectName, pathPrefix);
            var definitions = await client.GetFullDefinitionsAsync2(project: projectName, path: pathPrefix);

            var filteredDefinitions = definitions.Where(
                def => def.Triggers.Any(
                    trigger => trigger.TriggerType == DefinitionTriggerType.Schedule));

            return filteredDefinitions;
        }

        /// <summary>
        /// Returns teams in the given project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>Teams that satisfy given criteria</returns>
        public async Task<IEnumerable<WebApiTeam>> GetTeamsAsync(string projectName)
        {
            var client = await GetClientAsync<TeamHttpClient>();

            logger.LogInformation("GetTeamsAsync ProjectName = {0}", projectName);
            var teams = await client.GetTeamsAsync(projectName);

            return teams;
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
        /// Checks whether a child is a member of the parent enttiy
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
        /// Adds a child item to a parent item
        /// </summary>
        /// <param name="parent">Parent descriptor</param>
        /// <param name="child">Child descriptor</param>
        /// <returns>GraphMembership object resulting from adding the child to the parent</returns>
        public async Task<GraphMembership> AddTeamToTeamAsync(string parent, string child)
        {
            var client = await GetClientAsync<GraphHttpClient>();

            logger.LogInformation("AddTeamToTeamAsync ParentId = {0} ChildId = {1}", parent, child);
            var result = await client.AddMembershipAsync(child, parent);
            return result;
        }

        /// <summary>
        /// Gets subscriptions for a given target GUID
        /// </summary>
        /// <remarks>
        /// Some properties of the NotificationSubscription (like "Filter") are
        /// not resolved in this API call. Expansion with a followup call may
        /// be required
        /// </remarks>
        /// <param name="targetId">GUID of the subscription target</param>
        /// <returns>Complete subscription objects</returns>
        public async Task<IEnumerable<NotificationSubscription>> GetSubscriptionsAsync(Guid targetId)
        {
            var client = await GetClientAsync<NotificationHttpClient>();

            logger.LogInformation("GetSubscriptionsAsync TargetId = {0}", targetId);
            var result = await client.ListSubscriptionsAsync(targetId);

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
    }
}