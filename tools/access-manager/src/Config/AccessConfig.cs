using System.Text;
using System.Text.Json;

/*
 * EXAMPLE entry
 [
    {
        "appDisplayName": "azure-sdk-github-actions-test1",
        "roleBasedAccessControls": [
            {
              "role": "Contributor",
              "scope": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-testfoobaraccessmanager"
            },
            {
              "role": "Key Vault Secrets User",
              "scope": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-testfoobaraccessmanager/providers/Microsoft.KeyVault/vaults/testfoobaraccessmanager"
            }
        ],
        "federatedIdentityCredentials": [
            {
              "audiences": [
                "api://azureadtokenexchange"
              ],
              "description": "event processor oidc main tools",
              "issuer": "https://token.actions.githubusercontent.com",
              "name": "githubactionscredential-tools-main",
              "subject": "repo:azure/azure-sdk-tools:ref:refs/heads/main"
            },
            {
              "audiences": [
                "api://azureadtokenexchange"
              ],
              "description": "event processor oidc main net",
              "issuer": "https://token.actions.githubusercontent.com",
              "name": "githubactionscredential-net-main",
              "subject": "repo:azure/azure-sdk-for-net:ref:refs/heads/main"
            }
        ]
    }
]
*/

public class AccessConfig
{
    public string ConfigPath { get; set; } = default!;

    public List<ApplicationAccessConfig>? ApplicationAccessConfigs { get; set; }

    public static AccessConfig Create(string configPath)
    {
        var accessConfig = new AccessConfig
        {
            ConfigPath = configPath
        };
        var contents = File.ReadAllText(accessConfig.ConfigPath);
        accessConfig.ApplicationAccessConfigs = JsonSerializer.Deserialize<List<ApplicationAccessConfig>>(contents);
        return accessConfig;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var app in ApplicationAccessConfigs!)
        {
            sb.AppendLine("---");
            sb.AppendLine($"AppDisplayName -> {app.AppDisplayName}");
            if (app.FederatedIdentityCredentials != null)
            {
                sb.AppendLine("FederatedIdentityCredentials ->");
                foreach (var cred in app.FederatedIdentityCredentials)
                {
                    sb.AppendLine(cred.ToIndentedString(1));
                }
            }
            if (app.RoleBasedAccessControls != null)
            {
                sb.AppendLine("RoleBasedAccessControls ->");
                foreach (var rbac in app.RoleBasedAccessControls)
                {
                    sb.AppendLine(rbac.ToIndentedString(1));
                }
            }
        }

        return sb.ToString();
    }
}