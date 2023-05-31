using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    /// <summary>
    /// A given RunConfiguration contains multiple repo configurations. This class provides such a container as well as
    /// mappings to pick up configurations from an incoming path.
    /// </summary>
    public class RunConfiguration
    {
        public RunConfiguration() {
            Repos = new List<RepoConfiguration>();
        }

        public RunConfiguration(string configPath)
        {
            if (File.Exists(configPath))
            {
                Repos = new List<RepoConfiguration>();

                using var stream = System.IO.File.OpenRead(configPath);
                using var doc = JsonDocument.Parse(stream);

                var results = JsonSerializer.Deserialize<RunConfiguration>(doc);

                if (results != null)
                {
                    Repos = results.Repos;
                }
            }
            else
            {
                throw new ArgumentException($"The configuration file path \"{configPath}\" does not exist.");
            }
        }

        public List<RepoConfiguration> Repos { get; set; }
    }
}
