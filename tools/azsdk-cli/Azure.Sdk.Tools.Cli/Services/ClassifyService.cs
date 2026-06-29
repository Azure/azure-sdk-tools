using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Services
{
    public interface IClassifyService
    {
        Task<SdkBreakingChange[]> ClassifySDKBreakingChanges(string sdkchange, string sdkRepoRoot, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct);
    }
    public class ClassifyService: IClassifyService
    {
        private readonly ICopilotAgentRunner _agentRunner;
        public ClassifyService(ICopilotAgentRunner agentRunner)
        {
            _agentRunner = agentRunner;
        }
        public async Task<SdkBreakingChange[]> ClassifySDKBreakingChanges(string sdkchange, string sdkRepoRoot, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct)
        {
            //LanguageService languageService = await GetLanguageServiceAsync(sdkRepoRoot, ct);
            //var languageService = languageServices.FirstOrDefault(s => s.Language == language);
            //var sdkBreakingPattern = await languageService.GetSDKBreakingPattern(sdkRepoRoot, ct);
            var template = new SdkBreakingChangeClassificationTemplate(sdkBreakingPattern, sdkchange, language, tspProjectPath);
            var agent = new CopilotAgent<string>
            {
                Instructions = template.BuildPrompt(),
                Model = "claude-opus-4.5"
            };
            var result = await _agentRunner.RunAsync(agent, ct);
            var breakings = template.ParseClassifyResult(result);

            return breakings;
        }
        private string BuildClassifyInstructions(string sdkchange, string sdkchangeToBreakingPattern, string language, string tspProjectPath)
        {
            var template = new SdkBreakingChangeClassificationTemplate(sdkchangeToBreakingPattern, sdkchange, language, tspProjectPath);
            return template.BuildPrompt();
        }
    }
}
