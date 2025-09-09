// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service interface for managing TypeSpec documentation operations
/// </summary>
public interface ITypeSpecDocsService
{
    /// <summary>
    /// Gets the list of available TypeSpec documentation topics from configured sources
    /// </summary>
    /// <returns>A response containing the list of available topics</returns>
    Task<ListTypeSpecTopicsResponse> GetTopicsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the documentation content for specified TypeSpec topics
    /// </summary>
    /// <param name="topics">List of topic names to fetch documentation for</param>
    /// <returns>A response containing the documentation content for the requested topics</returns>
    Task<GetTypeSpecTopicsDocsResponse> GetTopicDocsAsync(List<string> topics, CancellationToken ct = default);
}

/// <summary>
/// Metadata for a TypeSpec topic including description and content URL
/// </summary>
internal class TopicMetadata
{
    public string Description { get; set; } = string.Empty;
    public string ContentUrl { get; set; } = string.Empty;
}

/// <summary>
/// Service for managing TypeSpec documentation operations with caching and thread safety
/// </summary>
public class TypeSpecDocsService : ITypeSpecDocsService
{
    private readonly ILogger<TypeSpecDocsService> logger;
    private readonly IHttpClientFactory httpClientFactory;

    // Thread-safe caching using ConcurrentDictionary (available in .NET standard)
    private readonly ConcurrentDictionary<string, List<LlmsJsonItem>> llmsCache;
    private readonly ConcurrentDictionary<string, string> contentCache;

    // For deduplicating in-flight requests (like Promise deduplication in JS)
    private readonly ConcurrentDictionary<string, Task<string>> inflightRequests;

    // Stores the merged topic metadata (description + contentUrl)
    private readonly ConcurrentDictionary<string, TopicMetadata> topicToMetadata;

    // Flag to track if topics have been loaded
    private volatile bool topicsLoaded = false;
    private readonly SemaphoreSlim loadTopicsLock;

    // Default locations for TypeSpec/TypeSpec Azure documentation
    private static readonly string[] defaultTspDocSources =
    [
        "https://typespec.io/docs/llms.json",
        "https://azure.github.io/typespec-azure/docs/llms.json"
    ];

    // Instance set of sources (may be overridden for tests or configuration)
    private readonly string[] tspDocSources;

    public TypeSpecDocsService(ILogger<TypeSpecDocsService> logger, IHttpClientFactory httpClientFactory, IEnumerable<string>? sources = null)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        llmsCache = new ConcurrentDictionary<string, List<LlmsJsonItem>>();
        contentCache = new ConcurrentDictionary<string, string>();
        inflightRequests = new ConcurrentDictionary<string, Task<string>>();
        topicToMetadata = new ConcurrentDictionary<string, TopicMetadata>();
        loadTopicsLock = new SemaphoreSlim(1, 1);
        // DI is causing `sources` to always be an empty enumerable instead of null when not provided
        var sourceArray = sources?.ToArray();
        tspDocSources = (sourceArray?.Length > 0) ? sourceArray : defaultTspDocSources;
    }

    public async Task<ListTypeSpecTopicsResponse> GetTopicsAsync(CancellationToken ct = default)
    {
        try
        {
            if (topicsLoaded)
            {
                // No need to fetch again, always return cached topics
                return new ListTypeSpecTopicsResponse
                {
                    IsSuccessful = true,
                    Topics = GetCachedTopics()
                };
            }

            // Only want one thread to load topics at a time
            await loadTopicsLock.WaitAsync(ct);
            try
            {
                // Double-check after acquiring lock that another thread hasn't already retrieved them
                if (topicsLoaded)
                {
                    return new ListTypeSpecTopicsResponse
                    {
                        IsSuccessful = true,
                        Topics = GetCachedTopics()
                    };
                }

                // Fetch the llms.json files - these contain the topic metadata
                var loadTasks = tspDocSources.Select(u => LoadLlmsJsonAsync(u, ct));
                var results = await Task.WhenAll(loadTasks);

                // Merge all results
                foreach (var items in results.Where(r => r != null))
                {
                    foreach (var item in items)
                    {
                        topicToMetadata[item.Topic] = new TopicMetadata
                        {
                            Description = item.Description,
                            ContentUrl = item.ContentUrl
                        };
                    }
                }

                if (topicToMetadata.IsEmpty)
                {
                    return new ListTypeSpecTopicsResponse
                    {
                        IsSuccessful = false,
                        ResponseError = "No topics could be loaded from any source"
                    };
                }

                topicsLoaded = true;
                return new ListTypeSpecTopicsResponse
                {
                    IsSuccessful = true,
                    Topics = GetCachedTopics()
                };
            }
            finally
            {
                loadTopicsLock.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get TypeSpec topics");
            return new ListTypeSpecTopicsResponse
            {
                IsSuccessful = false,
                ResponseError = $"Failed to load topics: {ex.Message}"
            };
        }
    }

    private async Task<List<LlmsJsonItem>> LoadLlmsJsonAsync(string sourceUrl, CancellationToken ct)
    {
        // Check cache first
        if (llmsCache.TryGetValue(sourceUrl, out var cached))
        {
            return cached;
        }

        try
        {
            // Fetch with deduplication
            var json = await FetchWithDeduplicationAsync(sourceUrl, ct);
            var items = JsonSerializer.Deserialize<List<LlmsJsonItem>>(json) ?? new List<LlmsJsonItem>();

            // Cache the result
            llmsCache[sourceUrl] = items;
            return items;
        }
        catch (Exception ex)
        {
            // Log error but don't fail the whole operation
            // This allows partial success if one source is down
            logger.LogWarning("Failed to load {SourceUrl}: {ErrorMessage}", sourceUrl, ex.Message);
            return new List<LlmsJsonItem>();
        }
    }

    private async Task<string> FetchWithDeduplicationAsync(string url, CancellationToken ct)
    {
        // Deduplicate requests
        // If a request is in flight for the same URL, return the associated Task
        // otherwise create a new task, add it, and return that.
        var task = inflightRequests.GetOrAdd(url, async (urlKey) =>
            {
                try
                {
                    using var httpClient = httpClientFactory.CreateClient();
                    var response = await httpClient.GetStringAsync(urlKey, ct);
                    return response;
                }
                finally
                {
                    // Remove from in-flight when done
                    inflightRequests.TryRemove(urlKey, out _);
                }
            });

        return await task;
    }

    private List<TypeSpecTopic> GetCachedTopics()
    {
        return [.. topicToMetadata
            .Select(kvp => new TypeSpecTopic
            {
                Topic = kvp.Key,
                Description = kvp.Value.Description
            })
            .OrderBy(t => t.Topic)];
    }

    // Helper method for future GetTopicDocsAsync implementation
    private string? GetTopicContentUrl(string topic)
    {
        return topicToMetadata.TryGetValue(topic, out var metadata) ? metadata.ContentUrl : null;
    }

    public async Task<GetTypeSpecTopicsDocsResponse> GetTopicDocsAsync(List<string> topics, CancellationToken ct = default)
    {
        try
        {
            // Ensure topics are loaded first
            if (!topicsLoaded)
            {
                var loadResult = await GetTopicsAsync(ct);
                if (!loadResult.IsSuccessful)
                {
                    return new GetTypeSpecTopicsDocsResponse
                    {
                        IsSuccessful = false,
                        ResponseError = loadResult.ResponseError
                    };
                }
            }

            var results = new List<TypeSpecTopicDoc>();
            var fetchTasks = new List<Task<TypeSpecTopicDoc>>();
            var unknownTopics = new List<string>();

            foreach (var topic in topics)
            {
                var contentUrl = GetTopicContentUrl(topic);
                if (contentUrl == null)
                {
                    unknownTopics.Add(topic);
                    continue;
                }

                fetchTasks.Add(FetchTopicContentAsync(topic, contentUrl, ct));
            }

            if (unknownTopics.Count != 0)
            {
                return new GetTypeSpecTopicsDocsResponse
                {
                    IsSuccessful = false,
                    ResponseError = $"Unknown topics: {string.Join(", ", unknownTopics)}"
                };
            }

            var docs = await Task.WhenAll(fetchTasks);
            return new GetTypeSpecTopicsDocsResponse
            {
                IsSuccessful = true,
                Docs = [.. docs]
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get TypeSpec topic docs");
            return new GetTypeSpecTopicsDocsResponse
            {
                IsSuccessful = false,
                ResponseError = $"Failed to fetch documentation: {ex.Message}"
            };
        }
    }

    private async Task<TypeSpecTopicDoc> FetchTopicContentAsync(string topic, string url, CancellationToken ct)
    {
        // Check cache - may have already fetched contents for this topic
        if (contentCache.TryGetValue(url, out var cachedContent))
        {
            return new TypeSpecTopicDoc { Topic = topic, Contents = cachedContent };
        }

        var content = await FetchWithDeduplicationAsync(url, ct);

        // Cache the result
        contentCache[url] = content;

        return new TypeSpecTopicDoc { Topic = topic, Contents = content };
    }
}
