// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Tools.Core
{
    public abstract class LanguageMcpTool : MCPTool
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
        public async Task<LanguageService> GetLanguageServiceAsync(string packagePath, CancellationToken ct = default)
        {
            var language = await SdkLanguageHelpers.GetLanguageForRepoPathAsync(gitHelper, packagePath, ct);
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
