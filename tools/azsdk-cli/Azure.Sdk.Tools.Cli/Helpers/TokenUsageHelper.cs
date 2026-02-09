using System.Diagnostics;
using System.Text.Json;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Tracks token usage for a single agent session within a tool/command invocation.
/// Each agent (microagent, Copilot SDK agent, Azure agent, etc.) should create its
/// own session via <see cref="TokenUsageHelper.NewSession"/>.
/// </summary>
public class TokenUsageSession
{
    private double promptTokens;

    /// <summary>
    /// A descriptive name for this session (e.g. agent name or purpose).
    /// </summary>
    public string Name { get; }

    public double PromptTokens => promptTokens;
    public double CompletionTokens { get; private set; }
    public double TotalTokens => PromptTokens + CompletionTokens;
    internal IEnumerable<string> ModelsUsed { get; private set; } = [];

    private readonly Action onUpdated;

    internal TokenUsageSession(string name, Action onUpdated)
    {
        Name = name;
        this.onUpdated = onUpdated;
    }

    /// <summary>
    /// Adds incremental token usage for this session.
    /// Use this when the SDK reports per-turn (incremental) input and output tokens.
    /// </summary>
    public void Add(string model, double inputTokens, double outputTokens)
    {
        ModelsUsed = ModelsUsed.Union([model]);
        promptTokens += inputTokens;
        CompletionTokens += outputTokens;
        onUpdated();
    }

    /// <summary>
    /// Sets prompt tokens to a cumulative value and adds output tokens.
    /// 
    /// Some SDKs (e.g. Copilot SDK) report inputTokens as the cumulative context size,
    /// where each turn's inputTokens = system prompt + all previous messages + current turn.
    /// 
    /// This method sets promptTokens to the given cumulative inputTokens value
    /// and adds outputTokens to the running completion total.
    /// 
    /// Note: A session should use either <see cref="Add"/> or <see cref="Set"/>, not both.
    /// Mixing the two will produce incorrect prompt token counts.
    /// </summary>
    public void Set(string model, double inputTokens, double outputTokens)
    {
        ModelsUsed = ModelsUsed.Union([model]);
        promptTokens = inputTokens;
        CompletionTokens += outputTokens;
        onUpdated();
    }
}

/// <summary>
/// Manages token usage tracking across multiple agent sessions within a single
/// tool/command invocation scope. Each agent should call <see cref="NewSession"/>
/// to get an isolated <see cref="TokenUsageSession"/> for its own tracking.
/// </summary>
public class TokenUsageHelper(IRawOutputHelper outputHelper)
{
    private readonly List<TokenUsageSession> sessions = [];

    /// <summary>Aggregate prompt tokens across all sessions.</summary>
    public double PromptTokens => sessions.Sum(s => s.PromptTokens);

    /// <summary>Aggregate completion tokens across all sessions.</summary>
    public double CompletionTokens => sessions.Sum(s => s.CompletionTokens);

    /// <summary>Aggregate total tokens across all sessions.</summary>
    public double TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>All sessions tracked by this helper.</summary>
    public IReadOnlyList<TokenUsageSession> Sessions => sessions;

    /// <summary>
    /// Creates a new token usage session for an agent.
    /// </summary>
    /// <param name="name">A descriptive name for the session (e.g. agent name).</param>
    /// <returns>A <see cref="TokenUsageSession"/> that the agent uses to record its token usage.</returns>
    public TokenUsageSession NewSession(string name)
    {
        var session = new TokenUsageSession(name, UpdateTelemetry);
        sessions.Add(session);
        return session;
    }

    private void UpdateTelemetry()
    {
        var allModels = sessions
            .SelectMany(s => s.ModelsUsed)
            .Distinct()
            .OrderBy(m => m);

        var sessionData = sessions.Select(s => new
        {
            name = s.Name,
            prompt_tokens = s.PromptTokens.ToString("F0"),
            completion_tokens = s.CompletionTokens.ToString("F0"),
            total_tokens = s.TotalTokens.ToString("F0"),
            models_used = string.Join(",", s.ModelsUsed.OrderBy(m => m))
        });

        Activity.Current?.SetCustomProperty(TagName.PromptTokens, PromptTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.CompletionTokens, CompletionTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.TotalTokens, TotalTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.ModelsUsed, string.Join(",", allModels));
        Activity.Current?.SetCustomProperty(TagName.TokenUsageSessions, JsonSerializer.Serialize(sessionData));
    }

    public void LogUsage()
    {
        outputHelper.OutputConsole("--------------------------------------------------------------------------------");

        foreach (var session in sessions)
        {
            var models = string.Join(", ", session.ModelsUsed);
            outputHelper.OutputConsole($"[token usage][{session.Name}][{models}] input: {session.PromptTokens}, output: {session.CompletionTokens}, total: {session.TotalTokens}");
        }

        if (sessions.Count > 1)
        {
            var allModels = string.Join(", ", sessions.SelectMany(s => s.ModelsUsed).Distinct());
            outputHelper.OutputConsole($"[token usage][total][{allModels}] input: {PromptTokens}, output: {CompletionTokens}, total: {TotalTokens}");
        }

        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
    }
}
