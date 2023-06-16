using System.Text.Json;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

/// <summary>
/// A RunConfiguration is the basic unit of configuration for this maintenance tool. It contains a set of targeted language
/// repositories that should be scanned for for assets.jsons.
/// </summary>
public class RunConfiguration
{


    public RunConfiguration() {
        LanguageRepos = new List<RepoConfiguration>();
    }

    public RunConfiguration(string configPath)
    {
        if (File.Exists(configPath))
        {
            LanguageRepos = new List<RepoConfiguration>();

            using var stream = System.IO.File.OpenRead(configPath);
            using var doc = JsonDocument.Parse(stream);

            var results = JsonSerializer.Deserialize<RunConfiguration>(doc);

            if (results != null)
            {
                LanguageRepos = results.LanguageRepos;
            }
        }
        else
        {
            throw new ArgumentException($"The configuration file path \"{configPath}\" does not exist.");
        }
    }

    public List<RepoConfiguration> LanguageRepos { get; set; }
}
