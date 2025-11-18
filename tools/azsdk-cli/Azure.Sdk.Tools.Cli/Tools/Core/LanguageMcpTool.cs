using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Azure.Sdk.Tools.Cli.Tools.Core
{
    public abstract class LanguageMcpTool: MCPTool
    {
        protected IEnumerable<LanguageService> languageServices;
        protected ILogger<LanguageMcpTool> logger;
        protected IGitHelper gitHelper;

        public LanguageMcpTool(IEnumerable<LanguageService> languageServices, IGitHelper gitHelper, ILogger<LanguageMcpTool> logger)
        {
            this.languageServices = languageServices;
            this.logger = logger;
            this.gitHelper = gitHelper;
        }

#pragma warning disable MCP003 // Tool methods must return Response types, built-in value types, or string
        public LanguageService GetLanguageService(string packagePath)
        {
            var language = SdkLanguageHelpers.GetLanguageForRepoPath(gitHelper, packagePath);
            if (language == SdkLanguage.Unknown)
            {
                return null;
            }
            return GetLanguageService(language);
        }

        public LanguageService GetLanguageService(SdkLanguage language)
        {
            var service = languageServices.FirstOrDefault(s => s.Language == language);
            return service;
        }
#pragma warning restore MCP003 // Tool methods must return Response types, built-in value types, or string

        /// <summary>
        /// Validates that the package path is not null/empty, is an absolute path, and exists.
        /// </summary>
        /// <param name="packagePath">The package path to validate.</param>
        /// <returns>A PackageOperationResponse with failure details if validation fails, otherwise null.</returns>
        protected PackageOperationResponse? ValidatePackagePath(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return PackageOperationResponse.CreateFailure("Package path is required and cannot be empty.");
            }

            if (!Path.IsPathRooted(packagePath))
            {
                return PackageOperationResponse.CreateFailure($"Package path must be an absolute path: {packagePath}");
            }

            if (!Directory.Exists(packagePath))
            {
                return PackageOperationResponse.CreateFailure($"Package path does not exist: {packagePath}");
            }

            return null;
        }
    }
}
