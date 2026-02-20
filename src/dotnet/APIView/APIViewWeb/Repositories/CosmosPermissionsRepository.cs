using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Repositories;

public class CosmosPermissionsRepository : ICosmosPermissionsRepository
{
    private readonly Container _permissionsContainer;
    private readonly ILogger<CosmosPermissionsRepository> _logger;

    public CosmosPermissionsRepository(IConfiguration configuration, CosmosClient cosmosClient,
        ILogger<CosmosPermissionsRepository> logger)
    {
        _permissionsContainer = cosmosClient.GetContainer(configuration["CosmosDBName"], "Permissions");
        _logger = logger;
    }

    public async Task<IEnumerable<GroupPermissionsModel>> GetGroupsForUserAsync(string userId)
    {
        string queryText = @"
                SELECT * FROM c 
                WHERE c.type = 'group' 
                AND ARRAY_CONTAINS(c.members, @userId)";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId);

        var groups = new List<GroupPermissionsModel>();
        var iterator = _permissionsContainer.GetItemQueryIterator<GroupPermissionsModel>(queryDefinition);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            groups.AddRange(response.Resource);
        }

        return groups;
    }

    public async Task<IEnumerable<GroupPermissionsModel>> GetAllGroupsAsync()
    {
        string queryText = "SELECT * FROM c WHERE c.type = 'group'";
        var queryDefinition = new QueryDefinition(queryText);

        var groups = new List<GroupPermissionsModel>();
        var iterator = _permissionsContainer.GetItemQueryIterator<GroupPermissionsModel>(queryDefinition);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            groups.AddRange(response.Resource);
        }

        return groups;
    }

    public async Task<GroupPermissionsModel> GetGroupAsync(string groupId)
    {
        try
        {
            string documentId = $"group-{groupId}";
            var response = await _permissionsContainer.ReadItemAsync<GroupPermissionsModel>(
                documentId,
                new PartitionKey(documentId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertGroupAsync(GroupPermissionsModel group)
    {
        group.Id = $"group-{group.GroupId}";
        group.LastUpdatedOn = DateTime.UtcNow;
        await _permissionsContainer.UpsertItemAsync(group, new PartitionKey(group.Id));
    }

    public async Task DeleteGroupAsync(string groupId)
    {
        string documentId = $"group-{groupId}";
        try
        {
            await _permissionsContainer.DeleteItemAsync<GroupPermissionsModel>(
                documentId,
                new PartitionKey(documentId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Attempted to delete non-existent group: {GroupId}", groupId);
        }
    }

    public async Task AddMembersToGroupAsync(string groupId, IEnumerable<string> userIds)
    {
        GroupPermissionsModel group = await GetGroupAsync(groupId);
        if (group == null)
        {
            throw new ArgumentException($"Group with ID '{groupId}' not found.");
        }

        var existingMembers = new HashSet<string>(group.Members, StringComparer.OrdinalIgnoreCase);
        foreach (var userId in userIds)
        {
            existingMembers.Add(userId);
        }

        group.Members = existingMembers.ToList();
        await UpsertGroupAsync(group);
    }

    public async Task RemoveMemberFromGroupAsync(string groupId, string userId)
    {
        GroupPermissionsModel group = await GetGroupAsync(groupId);
        if (group == null)
        {
            throw new ArgumentException($"Group with ID '{groupId}' not found.");
        }

        group.Members = group.Members
            .Where(m => !string.Equals(m, userId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await UpsertGroupAsync(group);
    }
}
