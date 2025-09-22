namespace Azure.Sdk.Tools.Cli.Helpers;

public class TokenUsageHelper(IRawOutputHelper outputHelper)
{
    protected double PromptTokens { get; set; } = 0;
    protected double CompletionTokens { get; set; } = 0;
    protected IEnumerable<string> ModelsUsed { get; set; } = [];

    public void Add(string model, long inputTokens, long outputTokens)
    {
        ModelsUsed = ModelsUsed.Union([model]);
        PromptTokens += inputTokens;
        CompletionTokens += outputTokens;
    }

    public void LogUsage()
    {
        var models = string.Join(", ", ModelsUsed);

        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
        outputHelper.OutputConsole($"[token usage][{models}] input: {PromptTokens}, output: {CompletionTokens}, total: {PromptTokens + CompletionTokens}");
        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
    }
}
