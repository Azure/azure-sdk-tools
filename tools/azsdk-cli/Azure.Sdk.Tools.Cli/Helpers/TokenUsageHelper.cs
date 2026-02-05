using System.Diagnostics;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class TokenUsageHelper(IRawOutputHelper outputHelper)
{
    public double PromptTokens { get; protected set; } = 0;
    public double CompletionTokens { get; protected set; } = 0;
    public double TotalTokens => PromptTokens + CompletionTokens;
    protected IEnumerable<string> ModelsUsed { get; set; } = [];

    public virtual void Add(string model, long inputTokens, long outputTokens)
    {
        ModelsUsed = ModelsUsed.Union([model]);
        PromptTokens += inputTokens;
        CompletionTokens += outputTokens;

        UpdateTelemetry();
    }

    protected void UpdateTelemetry()
    {
        Activity.Current?.SetCustomProperty(TagName.PromptTokens, PromptTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.CompletionTokens, CompletionTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.TotalTokens, TotalTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.ModelsUsed, string.Join(",", ModelsUsed.OrderBy(m => m)));
    }

    public void LogUsage()
    {
        var models = string.Join(", ", ModelsUsed);

        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
        outputHelper.OutputConsole($"[token usage][{models}] input: {PromptTokens}, output: {CompletionTokens}, total: {TotalTokens}");
        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
    }
}
