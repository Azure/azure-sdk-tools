using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            string variantPattern = searchPattern.Replace(".yml", "\\.(?<variant>([a-z]+))\\.yml");
            Regex variantExtractionExpression = new Regex($"^{variantPattern}$");
            Logger.LogDebug($"Scanning directory '{path.FullName}' for components with search pattern '{searchPattern}' variant pattern '{variantPattern}'");

            if (!path.Exists)
            {
                throw new ArgumentException(nameof(path), "Path does not exist.");
            }

            var pipelineYamlFiles = path.EnumerateFiles(searchPattern, SearchOption.AllDirectories);
            pipelineYamlFiles = pipelineYamlFiles.Concat(path.EnumerateFiles(searchPattern.Replace(".yml", ".*.yml"), SearchOption.AllDirectories));

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

                // Append variant information.
                if (variantExtractionExpression.IsMatch(pipelineYamlFile.Name))
                {
                    var match = variantExtractionExpression.Match(pipelineYamlFile.Name);
                    var variant = match.Groups["variant"].Value;
                    component.Variant = variant;
                    Logger.LogDebug($"variant = {variant}");
                }

                yield return component;
            }
        }
    }
}
