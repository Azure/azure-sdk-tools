// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

/// <summary>
/// Validates that the agent called the expected tools in the expected order,
/// and optionally validates the inputs sent to each tool.
/// </summary>
public class ToolCallValidator : IValidator
{
    public string Name { get; }
    public IReadOnlyList<ExpectedToolCall> ExpectedToolCalls { get; }
    public IReadOnlyList<string> OptionalToolNames { get; }
    public IReadOnlyList<string> ForbiddenToolNames { get; }
    public bool EnforceOrder { get; }

    public ToolCallValidator(
        string name,
        IEnumerable<ExpectedToolCall> expectedToolCalls,
        IEnumerable<string>? optionalToolNames = null,
        IEnumerable<string>? forbiddenToolNames = null,
        bool enforceOrder = true)
    {
        Name = name;
        ExpectedToolCalls = expectedToolCalls.ToList();
        OptionalToolNames = optionalToolNames?.ToList() ?? [];
        ForbiddenToolNames = forbiddenToolNames?.ToList() ?? [];
        EnforceOrder = enforceOrder;
    }

    /// <summary>
    /// Convenience constructor that only checks tool names (no input validation).
    /// </summary>
    public ToolCallValidator(
        string name,
        IEnumerable<string> expectedToolNames,
        IEnumerable<string>? optionalToolNames = null,
        IEnumerable<string>? forbiddenToolNames = null,
        bool enforceOrder = true)
        : this(name,
              expectedToolNames.Select(n => new ExpectedToolCall(n)),
              optionalToolNames,
              forbiddenToolNames,
              enforceOrder)
    {
    }

    public Task<ValidationResult> ValidateAsync(
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (ExpectedToolCalls.Count == 0)
            return Fail("No expected tool calls configured.");

        // Check for forbidden tool calls
        if (ForbiddenToolNames.Count > 0)
        {
            var forbiddenCalls = context.ToolCalls
                .Where(c => ForbiddenToolNames.Any(f => MatchesToolName(c.ToolName, f)))
                .ToList();

            if (forbiddenCalls.Count > 0)
                return Fail(
                    $"Forbidden tool(s) were called: [{FormatNames(forbiddenCalls)}].",
                    $"Forbidden tools: [{string.Join(", ", ForbiddenToolNames)}]");
        }

        var expectedNames = ExpectedToolCalls.Select(tc => tc.ToolName).ToList();

        // Filter to calls matching expected tools (suffix matching handles MCP prefixes)
        var requiredCalls = context.ToolCalls
            .Where(c => expectedNames.Any(n => MatchesToolName(c.ToolName, n)))
            .ToList();

        // Match each expected tool to an actual call (subsequence for ordered, set for unordered)
        var (matched, error) = FindMatches(requiredCalls, expectedNames, context);
        if (error != null)
            return error;

        // Validate inputs for matched calls
        var inputErrors = ValidateInputs(matched);
        if (inputErrors.Count > 0)
            return Fail(
                $"Tool inputs did not match: {inputErrors.Count} error(s).",
                string.Join("\n", inputErrors));

        var orderStr = EnforceOrder ? " in correct order" : "";
        var inputStr = ExpectedToolCalls.Any(tc => tc.ExpectedInputs != null) ? " with expected inputs" : "";
        return Pass($"All {ExpectedToolCalls.Count} expected tool(s) called{orderStr}{inputStr}.");
    }

    /// <summary>
    /// Matches expected tool calls against actual calls. Uses subsequence matching when
    /// order is enforced, or unordered set matching otherwise.
    /// </summary>
    private (List<ToolCallRecord> Matched, Task<ValidationResult>? Error) FindMatches(
        List<ToolCallRecord> requiredCalls,
        List<string> expectedNames,
        ValidationContext context)
    {
        var matched = new List<ToolCallRecord>();
        var used = new HashSet<int>();
        int searchStart = 0;

        for (int i = 0; i < ExpectedToolCalls.Count; i++)
        {
            var expected = ExpectedToolCalls[i];
            int foundIndex = -1;

            for (int j = EnforceOrder ? searchStart : 0; j < requiredCalls.Count; j++)
            {
                if (!EnforceOrder && used.Contains(j))
                    continue;
                if (!MatchesToolName(requiredCalls[j].ToolName, expected.ToolName))
                    continue;

                foundIndex = j;
                break;
            }

            if (foundIndex == -1)
            {
                var afterHint = EnforceOrder && i > 0
                    ? $" after '{ExpectedToolCalls[i - 1].ToolName}'" : "";
                var label = EnforceOrder ? "Expected order" : "Expected";
                return (matched, Fail(
                    $"Expected tool '{expected.ToolName}' not found{afterHint}.",
                    $"{label}: [{string.Join(", ", expectedNames)}]\n" +
                    $"Actual: [{FormatNames(requiredCalls)}]\n" +
                    $"All tool calls: [{FormatNames(context.ToolCalls)}]"));
            }

            matched.Add(requiredCalls[foundIndex]);
            used.Add(foundIndex);
            searchStart = foundIndex + 1;
        }

        return (matched, null);
    }

    private List<string> ValidateInputs(List<ToolCallRecord> matchedCalls)
    {
        var errors = new List<string>();

        for (int i = 0; i < ExpectedToolCalls.Count; i++)
        {
            var expected = ExpectedToolCalls[i];
            if (expected.ExpectedInputs == null)
                continue;

            var actualArgs = matchedCalls[i].GetArgsAsDictionary();
            foreach (var (key, expectedValue) in expected.ExpectedInputs)
            {
                if (!actualArgs.TryGetValue(key, out var actualJson))
                {
                    errors.Add($"Tool '{expected.ToolName}': missing expected input '{key}'.");
                    continue;
                }

                var mismatch = CompareValue(key, expectedValue, actualJson);
                if (mismatch != null)
                    errors.Add($"Tool '{expected.ToolName}': {mismatch}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Compares an expected value against an actual JsonElement.
    /// Strings use case-insensitive substring matching (handles path prefixes).
    /// Numerics and booleans use exact matching.
    /// </summary>
    private static string? CompareValue(string key, object? expectedValue, JsonElement actual)
    {
        if (expectedValue == null)
            return actual.ValueKind == JsonValueKind.Null
                ? null : $"input '{key}': expected null but got '{actual}'.";

        switch (expectedValue)
        {
            case string expectedStr:
                var actualStr = actual.ValueKind == JsonValueKind.String
                    ? actual.GetString() : actual.GetRawText();
                // Normalize path separators for cross-platform matching
                var normActual = actualStr?.Replace('\\', '/');
                var normExpected = expectedStr.Replace('\\', '/');
                return normActual != null && normActual.Contains(normExpected, StringComparison.OrdinalIgnoreCase)
                    ? null : $"input '{key}': expected to contain '{expectedStr}' but got '{actualStr}'.";

            case bool expectedBool:
                return actual.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && actual.GetBoolean() == expectedBool
                    ? null : $"input '{key}': expected {expectedBool} but got '{actual}'.";

            case int expectedInt:
                return actual.TryGetInt32(out var ai) && ai == expectedInt
                    ? null : $"input '{key}': expected {expectedInt} but got '{actual}'.";

            case long expectedLong:
                return actual.TryGetInt64(out var al) && al == expectedLong
                    ? null : $"input '{key}': expected {expectedLong} but got '{actual}'.";

            case double expectedDouble:
                return actual.TryGetDouble(out var ad) && Math.Abs(ad - expectedDouble) <= 0.0001
                    ? null : $"input '{key}': expected {expectedDouble} but got '{actual}'.";

            default:
                var expectedRepr = expectedValue.ToString();
                var actualRepr = actual.GetRawText().Trim('"');
                return string.Equals(expectedRepr, actualRepr, StringComparison.OrdinalIgnoreCase)
                    ? null : $"input '{key}': expected '{expectedRepr}' but got '{actualRepr}'.";
        }
    }

    /// <summary>
    /// Checks if an actual tool call name matches an expected name.
    /// Handles MCP prefix (e.g., "mcp_azure-sdk-mcp_azsdk_create_release_plan"
    /// matches "azsdk_create_release_plan").
    /// </summary>
    private static bool MatchesToolName(string actualCallName, string expectedToolName) =>
        actualCallName.EndsWith(expectedToolName, StringComparison.OrdinalIgnoreCase)
        || expectedToolName.EndsWith(actualCallName, StringComparison.OrdinalIgnoreCase);

    private Task<ValidationResult> Pass(string message) =>
        Task.FromResult(ValidationResult.Pass(Name, message));

    private Task<ValidationResult> Fail(string message, string? details = null) =>
        Task.FromResult(ValidationResult.Fail(Name, message, details));

    private static string FormatNames(IEnumerable<ToolCallRecord> calls) =>
        string.Join(", ", calls.Select(c => c.ToolName));
}
