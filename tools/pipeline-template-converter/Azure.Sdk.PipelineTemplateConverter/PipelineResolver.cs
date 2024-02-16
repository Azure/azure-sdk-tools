using System.Text.RegularExpressions;

namespace Azure.Sdk.PipelineTemplateConverter
{
    public static class PipelineResolver
    {
        private static List<string> IgnoreList = new()
        {
            "eng/common/pipelines/templates/steps/publish-artifact.yml"
        };

        public const string BAD_ARGUMENT_EXCEPTION =
            "The target path \"{0}\" does not exist within a git repository. This is disallowed when invoking the tool against a pipeline.";
        public const string TEMPLATE_EXPRESSION = @"template:\s*(.+)";

        public static DirectoryInfo AscendToRepoRoot(FileInfo startFile)
        {
            DirectoryInfo? directoryPath;
            if (startFile.Directory != null)
            {
                directoryPath = startFile.Directory;
            }
            else
            {
                throw new ArgumentException($"The input pipeline path of \"{startFile.FullName}\" does not have a directory parent. Check user input and try again with a valid file.");
            }

            while (true && directoryPath != null)
            {
                var possibleGitLocation = Path.Join(directoryPath.FullName, ".git");
                var isRoot = new DirectoryInfo(directoryPath.FullName).Parent == null;

                if (Directory.Exists(possibleGitLocation) || File.Exists(possibleGitLocation))
                {
                    return directoryPath;
                }
                else if (isRoot)
                {
                    throw new ArgumentException(string.Format(BAD_ARGUMENT_EXCEPTION, startFile.FullName));
                }

                directoryPath = directoryPath.Parent;
            }

            throw new ArgumentException(string.Format(BAD_ARGUMENT_EXCEPTION, startFile.FullName));
        }

        public static FileInfo ResolveTemplateReference(string partialPath, string sourceTemplatePath, DirectoryInfo repoRoot)
        {
            if (partialPath.EndsWith("@self"))
            {
                partialPath = partialPath.Substring(0, partialPath.Length - 5);
            }

            if (partialPath.StartsWith("/"))
            {
                partialPath = partialPath.Substring(1);
                return new FileInfo(Path.Combine(repoRoot.FullName, partialPath));
            }
            else
            {
                var sourceTemplateDirectory = Path.GetDirectoryName(sourceTemplatePath);
                if(sourceTemplateDirectory != null)
                {
                    return new FileInfo(Path.Combine(sourceTemplateDirectory, partialPath));
                }
                else
                {
                    throw new ArgumentException($"Unable to resolve the containing directory of template file \"{sourceTemplatePath}\", which should never occur.");
                }
            }
        }

        public static List<FileInfo> GetReferencedTemplates(string templatePath, DirectoryInfo repoRoot)
        {
            var templateReferences = new List<FileInfo>();

            var contentLines = File.ReadAllLines(templatePath);

            foreach (var line in contentLines)
            {
                Match match = Regex.Match(line, TEMPLATE_EXPRESSION);

                if (match.Success)
                {
                    string reffedTemplatePath = match.Groups[1].Value;

                    if(reffedTemplatePath.EndsWith(".yml") || reffedTemplatePath.EndsWith("@self"))
                    {
                        templateReferences.Add(ResolveTemplateReference(reffedTemplatePath, templatePath, repoRoot));
                    }
                }
            }

            if (templateReferences.Count == 0)
            {
                templateReferences.Add(new FileInfo(templatePath));
                return templateReferences;
            }
            else
            {
                var dependentTemplateReferences = new List<FileInfo>() { new FileInfo(templatePath) };
                foreach (var templateReference in templateReferences)
                {
                    if (templateReference.FullName == templatePath)
                    {
                        throw new Exception($"Found circular reference in template file \"{templatePath}\"");
                    }

                    if (IgnoreTemplate(templateReference))
                    {
                        continue;
                    }

                    dependentTemplateReferences.AddRange(GetReferencedTemplates(templateReference.FullName, repoRoot));
                }

                return dependentTemplateReferences.GroupBy(file => file.FullName).Select(group => group.First()).ToList();
            }
        }

        public static List<FileInfo> ResolvePipeline(FileInfo pipelineTemplate)
        {
            var root = AscendToRepoRoot(pipelineTemplate);
            var templateReferences = GetReferencedTemplates(pipelineTemplate.FullName, root);

            return templateReferences;
        }

        public static List<FileInfo> ResolveDirectory(DirectoryInfo directory)
        {
            var files = new List<FileInfo>();
            foreach (var file in directory.GetFiles("*.yml", SearchOption.AllDirectories))
            {
                var skip = false;
                foreach (var ignore in IgnoreList)
                {
                    if (file.FullName.Contains(ignore))
                    {
                        Console.WriteLine($"Skipping {file.FullName} in ignore list");
                        skip = true;
                    }
                }
                if (!skip)
                {
                    files.Add(file);
                }
            }

            return files;
        }

        public static bool IgnoreTemplate(FileInfo file)
        {
            foreach (var ignore in IgnoreList)
            {
                if (file.FullName.Contains(ignore))
                {
                    Console.WriteLine($"Skipping {file.FullName} in ignore list");
                    return true;
                }
            }

            return false;
        }
    }
}
