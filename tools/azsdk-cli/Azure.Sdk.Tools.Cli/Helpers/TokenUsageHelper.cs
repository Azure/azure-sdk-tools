namespace Azure.Sdk.Tools.Cli.Helpers;

public class TokenUsageHelper(IOutputHelper outputHelper)
{
    private static Dictionary<string, (double inputPrice, double outputPrice)> modelPrices = new()
    {
        // Global pricing via https://platform.openai.com/docs/pricing
        { "gpt-5", (1.25, 10) },
        { "gpt-5-mini", (0.25, 2) },
        { "gpt-4.1", (2, 8) },
        { "gpt-4.1-mini", (0.40, 1.60) },
        { "gpt-4.1-nano", (0.10, 0.40) },
        { "gpt-4o", (2.50, 10) },
        { "gpt-4o-mini", (0.15, 0.60) },
        { "o3-mini", (1.10, 4.40) },
    };

    protected double PromptTokens { get; set; } = 0;
    protected double CompletionTokens { get; set; } = 0;
    protected double InputCost { get; set; } = 0;
    protected double OutputCost { get; set; } = 0;
    protected double TotalCost { get; set; } = 0;
    protected IEnumerable<string> ModelsUsed { get; set; } = [];

    public void Add(string model, long inputTokens, long outputTokens)
    {
        ModelsUsed = ModelsUsed.Union([model]);
        PromptTokens += inputTokens;
        CompletionTokens += outputTokens;
        SetCost(model);
    }

    private void SetCost(string model)
    {
        var oneMillion = 1_000_000;
        if (!modelPrices.TryGetValue(model, out (double inputPrice, double outputPrice) value))
        {
            throw new ArgumentException($"No pricing information found for model '{model}'.");
        }

        InputCost = PromptTokens / oneMillion * value.inputPrice;
        OutputCost = CompletionTokens / oneMillion * value.outputPrice;
    }

    public void LogCost()
    {
        var _inputCost = InputCost == 0 ? "?" : InputCost.ToString("F3");
        var _outputCost = OutputCost == 0 ? "?" : OutputCost.ToString("F3");
        var _totalCost = (InputCost + OutputCost) == 0 ? "?" : (InputCost + OutputCost).ToString("F3");
        var models = string.Join(", ", ModelsUsed);

        outputHelper.OutputConsole($"[{models}] Usage (cost / tokens):");
        outputHelper.OutputConsole($"  Input:  ${_inputCost} / {PromptTokens}");
        outputHelper.OutputConsole($"  Output: ${_outputCost} / {CompletionTokens}");
        outputHelper.OutputConsole($"  Total:  ${_totalCost} / {PromptTokens + CompletionTokens}");
        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
    }
}
