using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.DistributedTask.WebApi;

namespace AzureSDKDSpecTools.Models
{
    public class TypeSpecProject
    {
        static readonly string TSPCONFIG_FILENAME = "tspconfig.yaml";
        private string TypeSpecConfigYaml { get; set; }

        public string Name { get; set; }
        public string ProjectRootPath { get; set; }
       
        public bool IsDataPlane { get;}
        public string sdkServicePath { get; set; }


        private TypeSpecProject()
        {
            Name = string.Empty;
            ProjectRootPath = string.Empty;
            sdkServicePath = string.Empty;
            TypeSpecConfigYaml = string.Empty;
        }

        public static bool IsValidTypeSpecProjectPath(string typeSpecProjectPath)
        {
            if (string.IsNullOrEmpty(typeSpecProjectPath))
                return false;

            var path = typeSpecProjectPath;
            if (!path.EndsWith(TSPCONFIG_FILENAME))
            {
                path = Path.Combine(path, TSPCONFIG_FILENAME);
            }

            return File.Exists(path);
        }

        public static TypeSpecProject ParseTypeSpecConfig(string typeSpecProjectPath)
        {
            if (!IsValidTypeSpecProjectPath(typeSpecProjectPath))
            {
                throw new ArgumentException($"TypeSpec config file is not found in [{typeSpecProjectPath}].");
            }

            var path = typeSpecProjectPath ?? string.Empty;
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

        public bool IsManagementPlane
        {
            get
            {
                return TypeSpecConfigYaml.Contains("azure-resource-provider-folder: ./resource-manager") || TypeSpecConfigYaml.Contains("@azure-tools/typespec-azure-rulesets/resource-manager");
            }
        }       
    }
}
