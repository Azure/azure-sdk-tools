using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public class SdkComponentScanner
    {
        public SdkComponentScanner(ILogger<SdkComponentScanner> logger)
        {
            Logger = logger;
        }

        private ILogger<SdkComponentScanner> Logger { get; }

        public IEnumerable<SdkComponent> Scan(DirectoryInfo path, string searchPattern)
        {
            Logger.LogDebug("Scanning directory '{0}' for components with search pattern '{1}'.", path.FullName, searchPattern);

            if (!path.Exists)
            {
                throw new ArgumentException(nameof(path), "Path does not exist.");
            }

            var pipelineYamlFiles = path.EnumerateFiles(searchPattern, SearchOption.AllDirectories);

            if (pipelineYamlFiles.Count() == 0)
            {
                Logger.LogWarning("Did not find any YAML files with search pattern '{0}' in path '{1}'.", searchPattern, path.FullName);
            }

            Logger.LogDebug("Finding repository root from '{0}'.", path.FullName);
            var repositoryHelper = new RepositoryHelper();
            var root = repositoryHelper.GetRepositoryRoot(path);
            Logger.LogDebug("Found repository root at: {0}", root);

            foreach (var pipelineYamlFile in pipelineYamlFiles)
            {
                var relativePath = Path.GetRelativePath(root, pipelineYamlFile.FullName);
                Logger.LogDebug("Repository root relative path for '{0}' is: {1}", pipelineYamlFile, relativePath);

                var component = new SdkComponent()
                {
                    Name = pipelineYamlFile.Directory.Name,
                    Path = pipelineYamlFile.Directory,
                    RelativeYamlPath = relativePath
                };

                yield return component;
            }
        }
    }
}
