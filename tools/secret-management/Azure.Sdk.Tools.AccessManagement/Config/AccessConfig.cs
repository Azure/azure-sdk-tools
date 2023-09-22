using System.Text;
using System.Text.Json;

namespace Azure.Sdk.Tools.AccessManagement;

public class AccessConfig
{
    public struct ConfigData {
        public FileInfo File { get; set; }
        public ApplicationAccessConfig ApplicationAccessConfig { get; set; }
        public ApplicationAccessConfig RawApplicationAccessConfig { get; set; }
    }

    public List<ConfigData> Configs { get; set; } = new List<ConfigData>();

    public AccessConfig(ILogger logger, List<string> configPaths)
    {
        foreach (var configPath in configPaths)
        {
            var config = new FileInfo(configPath);
            logger.LogInformation($"Using config -> {config.FullName}{Environment.NewLine}");
            var contents = File.ReadAllText(config.FullName);
            var configData = new ConfigData { File = config };

            configData.ApplicationAccessConfig =
                JsonSerializer.Deserialize<ApplicationAccessConfig>(contents) ?? new ApplicationAccessConfig();
            configData.ApplicationAccessConfig.Render();

            // Keep an unrendered version of config values so we can retain templating
            // when we need to serialize back to the config file
            configData.RawApplicationAccessConfig =
                JsonSerializer.Deserialize<ApplicationAccessConfig>(contents) ?? new ApplicationAccessConfig();

            Configs.Add(configData);
        }
    }

    public void SyncProperties()
    {
        foreach (var config in Configs)
        {
            config.RawApplicationAccessConfig.Properties =
                new SortedDictionary<string, string>(config.ApplicationAccessConfig.Properties);
        }
    }

    public async Task Save()
    {
        foreach (var config in Configs)
        {
            var contents = JsonSerializer.Serialize(
                config.RawApplicationAccessConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(config.File.FullName, contents);
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var config in Configs)
        {
            sb.AppendLine("---");
            sb.AppendLine($"AppDisplayName -> {config.ApplicationAccessConfig.AppDisplayName}");
            if (config.ApplicationAccessConfig.FederatedIdentityCredentials != null)
            {
                sb.AppendLine("FederatedIdentityCredentials ->");
                foreach (var cred in config.ApplicationAccessConfig.FederatedIdentityCredentials)
                {
                    sb.AppendLine(cred.ToIndentedString(1));
                }
            }
            if (config.ApplicationAccessConfig.RoleBasedAccessControls != null)
            {
                sb.AppendLine("RoleBasedAccessControls ->");
                foreach (var rbac in config.ApplicationAccessConfig.RoleBasedAccessControls)
                {
                    sb.AppendLine(rbac.ToIndentedString(1));
                }
            }
            if (config.ApplicationAccessConfig.GithubRepositorySecrets != null)
            {
                sb.AppendLine("GithubRepositorySecrets ->");
                foreach (var secret in config.ApplicationAccessConfig.GithubRepositorySecrets)
                {
                    sb.AppendLine(secret.ToIndentedString(1));
                }
            }
        }

        return sb.ToString();
    }
}