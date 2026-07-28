using System.Diagnostics;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class TokenUsageHelper(IRawOutputHelper outputHelper)
{
    private double promptTokens = 0;

    public double PromptTokens => promptTokens;
    public double CompletionTokens { get; private set; } = 0;
    public double TotalTokens => PromptTokens + CompletionTokens;
    private IEnumerable<string> ModelsUsed { get; set; } = [];

    public void Add(string model, double inputTokens, double outputTokens)
    {
        ModelsUsed = ModelsUsed.Union([model]);
        promptTokens += inputTokens;
        CompletionTokens += outputTokens;

        UpdateTelemetry();
    }

    /// <summary>
    /// Records token usage for APIs that report cumulative (not incremental) input tokens.
    /// 
    /// Some SDKs (e.g. Copilot SDK) report inputTokens as the cumulative context size,
    /// where each turn's inputTokens = system prompt + all previous messages + current turn.
    /// 
    /// Computes the delta between the cumulative inputTokens and the current prompt token
    /// count, then delegates to <see cref="Add"/> so prompt tokens reflect the cumulative total.
    /// The outputTokens value is incremental and is added to the running total.
    /// 
    /// Note: Agents should use either <see cref="Add"/> or <see cref="AddCumulative"/>, not both.
    /// Mixing the two will produce incorrect prompt token counts.
    /// </summary>
    /// <param name="model">The model used for this turn.</param>
    /// <param name="inputTokens">Cumulative context size (not incremental).</param>
    /// <param name="outputTokens">Output tokens for this turn (incremental — added to total).</param>
    public void AddCumulative(string model, double inputTokens, double outputTokens)
    {
        Add(model, inputTokens - promptTokens, outputTokens);
    }

    private void UpdateTelemetry()
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
