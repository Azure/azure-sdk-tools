// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.CheckAllTool.Base;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool
{
    /// <summary>
    /// This tool runs link validation for SDK projects.
    /// </summary>
    [Description("Run link validation for SDK projects")]
    [McpServerToolType]
    public class LinkValidationTool : BaseValidationTool
    {
        public LinkValidationTool(ILogger<LinkValidationTool> logger, IOutputService output) 
            : base(logger, output)
        {
        }

        [McpServerTool(Name = "RunLinkValidation"), Description("Run link validation for SDK projects. Provide absolute path to project root as param.")]
        public async Task<DefaultCommandResponse> RunLinkValidation(string projectPath)
        {
            try
            {
                return await RunValidation(projectPath);
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected override async Task<(bool Success, string? ErrorMessage, List<string>? Details)> ExecuteValidation(string projectPath)
        {
            // TODO: Implement actual link validation logic
            await Task.Delay(100); // Simulate work
            
            return (true, null, null);
        }

        protected override string GetCommandName() => "linkValidation";

        protected override string GetCommandDescription() => "Run link validation for SDK projects";

        protected override string GetCheckType() => "Link Validation";
    }
}