using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.AccessManager;

public class ApplicationAccessConfig
{
    [JsonRequired, JsonPropertyName("appDisplayName"), JsonPropertyOrder(0)]
    public string AppDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("properties"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(1)]
    public SortedDictionary<string, string> Properties { get; set; } = new SortedDictionary<string, string>();

    [JsonPropertyName("githubRepositorySecrets"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(2)]
    public List<GithubRepositorySecretsConfig> GithubRepositorySecrets { get; set; } = new List<GithubRepositorySecretsConfig>();

    [JsonPropertyName("roleBasedAccessControls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(3)]
    public List<RoleBasedAccessControlsConfig> RoleBasedAccessControls { get; set; } = new List<RoleBasedAccessControlsConfig>();

    [JsonPropertyName("federatedIdentityCredentials"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(4)]
    public List<FederatedIdentityCredentialsConfig> FederatedIdentityCredentials { get; set; } = new List<FederatedIdentityCredentialsConfig>();

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