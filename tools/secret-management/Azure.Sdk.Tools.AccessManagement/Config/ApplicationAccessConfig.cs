using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.AccessManagement;

public class ApplicationAccessConfig
{
    // New top-level UAMI fields (preferred)
    [JsonPropertyName("identityName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(0)]
    public string? IdentityName { get; set; }

    [JsonPropertyName("subscriptionId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(1)]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("resourceGroup"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(2)]
    public string? ResourceGroup { get; set; }

    [JsonPropertyName("location"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(3)]
    public string? Location { get; set; }

    [JsonPropertyName("properties"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(4)]
    public SortedDictionary<string, string> Properties { get; set; } = new SortedDictionary<string, string>();

    [JsonPropertyName("githubRepositorySecrets"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(5)]
    public List<GithubRepositorySecretsConfig> GithubRepositorySecrets { get; set; } = new List<GithubRepositorySecretsConfig>();

    [JsonPropertyName("roleBasedAccessControls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(6)]
    public List<RoleBasedAccessControlsConfig> RoleBasedAccessControls { get; set; } = new List<RoleBasedAccessControlsConfig>();

    [JsonPropertyName("federatedIdentityCredentials"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(7)]
    public List<FederatedIdentityCredentialsConfig> FederatedIdentityCredentials { get; set; } = new List<FederatedIdentityCredentialsConfig>();

    /// <summary>
    /// Resolves UAMI parameters from the properties dictionary when not set at the top level.
    /// Falls back: identityName from properties["identityname"];
    /// subscriptionId and resourceGroup from properties dict.
    /// </summary>
    public void ResolveIdentityParameters()
    {
        if (string.IsNullOrEmpty(IdentityName))
        {
            IdentityName = TryGetProperty("identityname")
                        ?? TryGetProperty("identityName");
        }

        if (string.IsNullOrEmpty(SubscriptionId))
        {
            SubscriptionId = TryGetProperty("subscriptionId");
        }

        if (string.IsNullOrEmpty(ResourceGroup))
        {
            ResourceGroup = TryGetProperty("resourceGroup");
        }

        var missing = new List<string>();
        if (string.IsNullOrEmpty(IdentityName)) missing.Add("identityName");
        if (string.IsNullOrEmpty(SubscriptionId)) missing.Add("subscriptionId");
        if (string.IsNullOrEmpty(ResourceGroup)) missing.Add("resourceGroup");

        if (missing.Any())
        {
            throw new InvalidOperationException(
                $"Missing required identity parameters: {string.Join(", ", missing)}. " +
                $"Set them as top-level fields or include them in the 'properties' dictionary.");
        }
    }

    private string? TryGetProperty(string key)
    {
        if (Properties.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;
        return null;
    }

    public void Render(bool failWhenMissingProperties = false)
    {
        var allUnrendered = new HashSet<string>();

        foreach (var rbac in RoleBasedAccessControls)
        {
            var unrendered = rbac.Render(Properties);
            allUnrendered.UnionWith(unrendered);
        }

        foreach (var fic in FederatedIdentityCredentials)
        {
            var unrendered = fic.Render(Properties);
            allUnrendered.UnionWith(unrendered);
        }

        foreach (var gh in GithubRepositorySecrets)
        {
            var unrendered = gh.Render(Properties);
            allUnrendered.UnionWith(unrendered);
        }

        if (failWhenMissingProperties && allUnrendered.Any())
        {
            var missing = string.Join(", ", allUnrendered.OrderBy(s => s));
            throw new Exception($"Missing properties for template values: {missing}");
        }
    }
}