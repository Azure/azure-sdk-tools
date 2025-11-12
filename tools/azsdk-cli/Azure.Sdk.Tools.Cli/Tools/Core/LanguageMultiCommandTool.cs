using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Azure.Sdk.Tools.Cli.Tools.Core
{
    public abstract class LanguageMultiCommandTool: MCPMultiCommandTool
    {
        protected IEnumerable<LanguageService> languageServices;
        protected ILogger<LanguageMultiCommandTool> logger;
        private IGitHelper gitHelper;

        public LanguageMultiCommandTool(IEnumerable<LanguageService> languageServices, IGitHelper gitHelper, ILogger<LanguageMultiCommandTool> logger)
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
    }
}
