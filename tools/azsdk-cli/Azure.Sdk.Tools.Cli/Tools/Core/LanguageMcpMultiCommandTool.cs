// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Tools.Core
{
    /// <summary>
    /// Multi-command variant of <see cref="LanguageMcpTool"/>.
    /// Extends <see cref="MCPMultiCommandTool"/> with language service helpers,
    /// for tools that need both multi-command CLI support and language detection.
    /// </summary>
    public abstract class LanguageMcpMultiCommandTool : MCPMultiCommandTool
    {
        private readonly LanguageToolHelper _languageHelper;

        protected IEnumerable<LanguageService> languageServices => _languageHelper.LanguageServices;
        protected ILogger<LanguageMcpMultiCommandTool> logger;
        protected IGitHelper gitHelper => _languageHelper.GitHelper;

        public LanguageMcpMultiCommandTool(IEnumerable<LanguageService> languageServices, IGitHelper gitHelper, ILogger<LanguageMcpMultiCommandTool> logger)
        {
            _languageHelper = new LanguageToolHelper(languageServices, gitHelper);
            this.logger = logger;
        }

#pragma warning disable MCP003 // Tool methods must return Response types, built-in value types, or string
        public Task<LanguageService> GetLanguageServiceAsync(string packagePath, CancellationToken ct = default)
            => _languageHelper.GetLanguageServiceAsync(packagePath, ct);

        public LanguageService GetLanguageService(SdkLanguage language)
            => _languageHelper.GetLanguageService(language);
#pragma warning restore MCP003 // Tool methods must return Response types, built-in value types, or string
    }
}
