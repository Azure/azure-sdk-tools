using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

namespace Azure.Sdk.Tools.TestProxy.Store
{

    /// <summary>
    /// Used to store and retrieve values from the breadcrumb file.
    /// </summary>
    public class BreadcrumbLine
    {
        public string PathToAssetsJson;
        public string ShortHash;
        public string Tag;

        /// <summary>
        /// Creates a breadcrumb (with relevant details) from an existing breadcrumb line.
        /// </summary>
        /// <param name="line"></param>
        public BreadcrumbLine(string line)
        {
            // split the line here. assign values
            var values = line.Split(";").Select(x => x.Trim()).ToList();

            if (values.Count() != 3)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable to parse the line {line}");
            }

            PathToAssetsJson = values[0];
            ShortHash = values[1];
            Tag = values[2];
        }

        /// <summary>
        /// Converts a configuration to the breadcrumb presentation.
        /// </summary>
        /// <param name="config"></param>
        public BreadcrumbLine(GitAssetsConfiguration config)
        {
            PathToAssetsJson = config.AssetsJsonRelativeLocation.ToString();
            ShortHash = config.AssetRepoShortHash;
            Tag = config.Tag;
        }

        /// <summary>
        /// Contract is AssetsJsonRelative;ShortHash;TargetTag
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{PathToAssetsJson.Replace("\\", "/")};{ShortHash};{Tag}";
        }
    }

    /// <summary>
    /// The breadcrumb store is shared across any asset repo within a given asset store. Given that, we need to control access
    /// to one update at any given time. This ensures that parallel updates can't accidentally flatten the breadcrumb lines from
    /// each other.
    /// 
    /// This simple class merely abstracts the enqueuing of that work so that users don't need to worry about the above.
    /// </summary>
    public class GitStoreBreadcrumb
    {
        TaskQueue BreadCrumbWorker = new TaskQueue();

        public GitStoreBreadcrumb() { }

        public string GetBreadCrumbLocation(GitAssetsConfiguration configuration)
        {
            var breadCrumbFolder = Path.Combine(configuration.ResolveAssetsStoreLocation().ToString(), "breadcrumb");

            if (!Directory.Exists(breadCrumbFolder))
            {
                Directory.CreateDirectory(breadCrumbFolder);
            }

            return Path.Join(breadCrumbFolder, $"{configuration.AssetRepoShortHash}.breadcrumb");
        }

        /// <summary>
        /// Updates an existing breadcrumb file with an assets configuration. Add/Update only. Should never remove.
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public async Task Update(GitAssetsConfiguration configuration)
        {
            var breadcrumbFile = GetBreadCrumbLocation(configuration);
            var targetKey = configuration.AssetsJsonRelativeLocation.ToString();

            await BreadCrumbWorker.EnqueueAsync(async () =>
            {
                IEnumerable<string> linesForWriting = null;

                if (!File.Exists(breadcrumbFile))
                {
                    linesForWriting = new List<string>() { new BreadcrumbLine(configuration).ToString() };
                }
                else
                {
                    var readLines = await File.ReadAllLinesAsync(breadcrumbFile);
                    var lines = readLines.Select(x => new BreadcrumbLine(x)).ToDictionary(x => x.PathToAssetsJson, x => x);
                    lines[targetKey] = new BreadcrumbLine(configuration);
                    linesForWriting = lines.Values.Select(x => x.ToString());
                }
                
                File.WriteAllLines(breadcrumbFile, linesForWriting, System.Text.Encoding.UTF8);
            });
        }

        public void RefreshLocalCache(ConcurrentDictionary<string, string> localCache, GitAssetsConfiguration config)
        {
            var breadLocation = GetBreadCrumbLocation(config);

            if (File.Exists(breadLocation))
            {
                var readLines = File.ReadAllLines(breadLocation);
                var lines = readLines.Select(x => new BreadcrumbLine(x)).ToDictionary(x => x.PathToAssetsJson, x => x);

                foreach (var line in lines)
                {
                    localCache.AddOrUpdate(line.Value.PathToAssetsJson, line.Value.Tag, (key, oldValue) => line.Value.Tag);
                }
            }
        }
    }

}
