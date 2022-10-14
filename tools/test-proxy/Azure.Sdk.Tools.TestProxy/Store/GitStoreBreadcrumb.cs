using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
            var values = line.Split(";;").Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            // we should have exactly 3 values.
            if (values.Count() != 3)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable to parse breadcrumb line \"{line}\".");
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

        public void Update(GitAssetsConfiguration configuration)
        {
            var breadcrumbFile = Path.Join(configuration.ResolveAssetsStoreLocation(), ".breadcrumb");
            var targetKey = configuration.AssetsJsonRelativeLocation.Replace("\\", "/");

            BreadCrumbWorker.Enqueue(() =>
            {
                IEnumerable<string> linesForWriting = null;

                if (!File.Exists(breadcrumbFile))
                {
                    linesForWriting = new List<string>() { new BreadcrumbLine(configuration).ToString() };
                }
                else
                {
                    var lines = File.ReadAllLines(breadcrumbFile).Select(x => new BreadcrumbLine(x)).ToDictionary(x => x.PathToAssetsJson, x => x);
                    lines[targetKey] = new BreadcrumbLine(configuration);
                    linesForWriting = lines.Values.Select(x => x.ToString());
                }
                
                File.WriteAllLines(breadcrumbFile, linesForWriting, System.Text.Encoding.UTF8);
            });
        }
    }

}
