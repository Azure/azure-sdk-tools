namespace Azure.Sdk.Tools.Cli.Helpers;

public class TokenUsageHelper
{
    protected double PromptTokens { get; set; }
    protected double CompletionTokens { get; set; }
    protected double InputCost { get; set; }
    protected double OutputCost { get; set; }
    protected double TotalCost { get; set; }
    public List<string> Models { get; set; } = [];

    public TokenUsageHelper(string model, long inputTokens, long outputTokens)
    {
        PromptTokens = inputTokens;
        CompletionTokens = outputTokens;
        Models = [model];
        SetCost(model);
    }

    protected TokenUsageHelper() { }

    private void SetCost(string model)
    {
        var oneMillion = 1000000;
        double inputPrice, outputPrice;

        // Prices assume the slightly more expensive regional model pricing
        if (model == "gpt-4o")
        {
            (inputPrice, outputPrice) = (2.75, 11);
        }
        else if (model == "gpt-4o-mini")
        {
            (inputPrice, outputPrice) = (0.165, 0.66);
        }
        if (model == "gpt-4.1")
        {
            (inputPrice, outputPrice) = (2, 8);
        }
        else if (model == "gpt-4.1-mini")
        {
            (inputPrice, outputPrice) = (0.4, 1.60);
        }
        else if (model == "o3-mini")
        {
            (inputPrice, outputPrice) = (1.21, 4.84);
        }
        else
        {
            return;
        }


        InputCost = PromptTokens / oneMillion * inputPrice;
        OutputCost = CompletionTokens / oneMillion * outputPrice;
    }

    public void LogCost()
    {
        var _inputCost = InputCost == 0 ? "?" : InputCost.ToString("F3");
        var _outputCost = OutputCost == 0 ? "?" : OutputCost.ToString("F3");
        var _totalCost = (InputCost + OutputCost) == 0 ? "?" : (InputCost + OutputCost).ToString("F3");
        var models = string.Join(", ", Models);
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"[{models}] Usage (cost / tokens):");
        Console.WriteLine($"  Input:  ${_inputCost} / {PromptTokens}");
        Console.WriteLine($"  Output: ${_outputCost} / {CompletionTokens}");
        Console.WriteLine($"  Total:  ${_totalCost} / {PromptTokens + CompletionTokens}");
        Console.WriteLine("--------------------------------------------------------------------------------");
    }

    public static TokenUsageHelper operator +(TokenUsageHelper a, TokenUsageHelper b) => new()
    {
        Models = a.Models.Union(b.Models).ToList(),
        PromptTokens = a.PromptTokens + b.PromptTokens,
        CompletionTokens = a.CompletionTokens + b.CompletionTokens,
        InputCost = a.InputCost + b.InputCost,
        OutputCost = a.OutputCost + b.OutputCost,
    };
}