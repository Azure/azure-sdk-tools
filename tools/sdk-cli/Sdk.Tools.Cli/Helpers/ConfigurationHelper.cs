using System.Text.Json;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Helpers;

public class ConfigurationHelper
{
    private const string ConfigFileName = "sdk-cli-config.json";
    
    public async Task<SdkCliConfig?> TryLoadConfigAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(packagePath, ConfigFileName);
        
        if (!File.Exists(configPath))
            return null;
        
        var json = await File.ReadAllTextAsync(configPath, cancellationToken);
        return JsonSerializer.Deserialize<SdkCliConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<SdkCliConfig> LoadConfigAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        return await TryLoadConfigAsync(packagePath, cancellationToken) ?? new SdkCliConfig();
    }
}
