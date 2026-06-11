// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        public bool IsManagementPlane
        {
            get
            {
                return TypeSpecConfigYaml.Contains("azure-resource-provider-folder: ./resource-manager") || TypeSpecConfigYaml.Contains("@azure-tools/typespec-azure-rulesets/resource-manager");
            }
        }
    }
}
