using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public class SdkComponentScanner
    {
        public IEnumerable<SdkComponent> Scan(DirectoryInfo path, string searchPattern)
        {
            if (!path.Exists) throw new ArgumentException(nameof(path), "Path does not exist.");

            var pipelineYamlFiles = path.EnumerateFiles(searchPattern, SearchOption.AllDirectories);

            var repositoryHelper = new RepositoryHelper();
            var root = repositoryHelper.GetRepositoryRoot(path);

            foreach (var pipelineYamlFile in pipelineYamlFiles)
            {
                var relativePath = Path.GetRelativePath(root, pipelineYamlFile.FullName);

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
