// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class TypeSpecProject
    {
        public static readonly string TSPCONFIG_FILENAME = "tspconfig.yaml";
        private string TypeSpecConfigYaml { get; set; }

        public string Name { get; set; }
        public string ProjectRootPath { get; set; }

        public bool IsDataPlane { get;}
        public string SdkServicePath { get; set; }

        public List<PackageInfo> Packages { get; set; } = [];

        public SdkType SdkType {
            get
            {
                return IsManagementPlane ? SdkType.Management : SdkType.Dataplane;
            }
        }

        private TypeSpecProject()
        {
            Name = string.Empty;
            ProjectRootPath = string.Empty;
            SdkServicePath = string.Empty;
            TypeSpecConfigYaml = string.Empty;
        }

        public static bool IsValidTypeSpecProjectPath(string typeSpecProjectPath)
        {
            if (string.IsNullOrEmpty(typeSpecProjectPath))
            {
                return false;
            }

            var path = GetTypeSpecProjectRootPath(typeSpecProjectPath);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            path = Path.Combine(path, TSPCONFIG_FILENAME);

            return File.Exists(path);
        }

        public static TypeSpecProject ParseTypeSpecConfig(string typeSpecProjectPath)
        {
            if (!IsValidTypeSpecProjectPath(typeSpecProjectPath))
            {
                throw new ArgumentException($"TypeSpec config file is not found in [{typeSpecProjectPath}].");
            }

            var path = GetTypeSpecProjectRootPath(typeSpecProjectPath);
            var typeSpecProject = new TypeSpecProject
            {
                ProjectRootPath = path,
                Name = Path.GetDirectoryName(path) ?? string.Empty,
            };
            string tspConfigYaml = File.ReadAllText(Path.Combine(path, TSPCONFIG_FILENAME));
            if(string.IsNullOrEmpty(tspConfigYaml))
            {
                throw new Exception($"Failed to load contents of tspconfig.yaml in the [{typeSpecProjectPath}]");
            }

            typeSpecProject.TypeSpecConfigYaml = tspConfigYaml;
            return typeSpecProject;
        }

        private static string GetTypeSpecProjectRootPath(string typeSpecProjectPath)
        {
            if (string.IsNullOrWhiteSpace(typeSpecProjectPath))
            {
                return string.Empty;
            }

            var path = typeSpecProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.GetFileName(path).Equals(TSPCONFIG_FILENAME, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(path) ?? string.Empty;
            }

            return path;
        }

        public static SdkType ClassifySdkType(string configurationYaml)
        {
            try
            {
                var yaml = new YamlStream();
                yaml.Load(new StringReader(configurationYaml));
                if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode configuration)
                {
                    return SdkType.Unknown;
                }
                var rulesNode = Child(Child(configuration, "linter"), "extends");
                IEnumerable<string?> ruleSets = rulesNode switch
                {
                    YamlScalarNode scalar => [scalar.Value],
                    YamlSequenceNode sequence => sequence.Children.OfType<YamlScalarNode>().Select(value => value.Value),
                    _ => []
                };
                var folder = Child(Child(Child(configuration, "options"), "@azure-tools/typespec-autorest"), "azure-resource-provider-folder") as YamlScalarNode;
                var management = ruleSets.Contains("@azure-tools/typespec-azure-rulesets/resource-manager", StringComparer.Ordinal) ||
                    folder?.Value is "resource-manager" or "./resource-manager";
                var data = ruleSets.Contains("@azure-tools/typespec-azure-rulesets/data-plane", StringComparer.Ordinal);
                return management == data ? SdkType.Unknown : management ? SdkType.Management : SdkType.Dataplane;
            }
            catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or FormatException)
            {
                return SdkType.Unknown;
            }

            static YamlNode? Child(YamlNode? parent, string name) =>
                parent is YamlMappingNode mapping && mapping.Children.TryGetValue(new YamlScalarNode(name), out var value) ? value : null;
        }

        public bool IsManagementPlane => ClassifySdkType(TypeSpecConfigYaml) == SdkType.Management;
    }
}
