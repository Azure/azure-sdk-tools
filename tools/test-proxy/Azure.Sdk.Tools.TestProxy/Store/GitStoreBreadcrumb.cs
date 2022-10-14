using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

namespace Azure.Sdk.Tools.TestProxy.Store
{

    public class BreadcrumbLine
    {
        public string PathToAssetsJson;
        public string ShortHash;
        public string Tag;

        public BreadcrumbLine(string line)
        {
            // split the line here. assign values
            var values = line.Split(";;").Select(x => x.Trim()).ToList();

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
            PathToAssetsJson = config.AssetsJsonRelativeLocation;
            ShortHash = config.AssetRepoShortHash;
            Tag = config.Tag;
        }

        /// <summary>
        /// Contract is AssetsJsonRelative;;ShortHash;;TargetTag
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{PathToAssetsJson.Replace("\\", "/")};;{ShortHash};;{Tag}";
        }
    }

    public class GitStoreBreadcrumb
    {
        TaskQueue BreadCrumbWorker = new TaskQueue();

        public GitStoreBreadcrumb() { }

        public async Task Update(GitAssetsConfiguration configuration)
        {
            var breadcrumbFile = Path.Join(configuration.ResolveAssetsStoreLocation(), ".breadcrumb");
            var targetKey = configuration.AssetsJsonRelativeLocation.Replace("\\", "/");

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
    }

}
