namespace Azure.Sdk.Tools.Cli.Helpers;

public class TokenUsageHelper(IRawOutputHelper outputHelper)
{
    public double PromptTokens { get; private set; } = 0;
    public double CompletionTokens { get; private set; } = 0;
    public double TotalTokens => PromptTokens + CompletionTokens;
    public string[] ModelsUsed { get; private set; } = [];

    public void Add(string model, long inputTokens, long outputTokens)
    {
        ModelsUsed = [.. ModelsUsed.Union([model])];
        PromptTokens += inputTokens;
        CompletionTokens += outputTokens;
    }

    public void LogUsage()
    {
        var models = string.Join(", ", ModelsUsed);

        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
        outputHelper.OutputConsole($"[token usage][{models}] input: {PromptTokens}, output: {CompletionTokens}, total: {TotalTokens}");
        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
    }
}
