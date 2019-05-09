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

            foreach (var pipelineYamlFile in pipelineYamlFiles)
            {
                var component = new SdkComponent()
                {
                    Name = pipelineYamlFile.Directory.Name,
                    Path = pipelineYamlFile.Directory
                };

                yield return component;
            }
        }
    }
}
