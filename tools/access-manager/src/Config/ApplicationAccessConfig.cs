using System.Text.Json.Serialization;

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

    public void Render()
    {
        foreach (var rbac in RoleBasedAccessControls)
        {
            rbac.Render(Properties);
        }
        foreach (var fic in FederatedIdentityCredentials)
        {
            fic.Render(Properties);
        }
        foreach (var gh in GithubRepositorySecrets)
        {
            gh.Render(Properties);
        }
    }
}