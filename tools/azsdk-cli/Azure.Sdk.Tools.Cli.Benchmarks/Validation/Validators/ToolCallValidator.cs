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
        {
            return Fail("No expected tool calls configured.");
        }

        // Check for forbidden tool calls
        if (ForbiddenToolNames.Count > 0)
        {
            var forbiddenCalls = context.ToolCalls
                .Where(c => ForbiddenToolNames.Any(f => MatchesToolName(c.ToolName, f)))
                .ToList();

            if (forbiddenCalls.Count > 0)
            {
                return Fail(
                    $"Forbidden tool(s) were called: [{FormatNames(forbiddenCalls)}].",
                    $"Forbidden tools: [{string.Join(", ", ForbiddenToolNames)}]");
            }
        }

        var expectedNames = ExpectedToolCalls.Select(tc => tc.ToolName).ToList();
        var allowedNames = expectedNames.Concat(OptionalToolNames).ToList();

        // Check for unexpected MCP tool calls not in the allow-list
        var unexpectedCalls = context.ToolCalls
            .Where(c => IsMcpToolCall(c.ToolName))
            .Where(c => !allowedNames.Any(n => MatchesToolName(c.ToolName, n)))
            .ToList();

        if (unexpectedCalls.Count > 0)
        {
            return Fail(
                $"Unexpected tool(s) called: [{FormatNames(unexpectedCalls)}].",
                $"Allowed: [{string.Join(", ", allowedNames)}]\n" +
                $"Add unexpected tools to OptionalToolNames if they are acceptable.");
        }

        // Filter to calls matching expected tools (suffix matching handles MCP prefixes)
        var requiredCalls = context.ToolCalls
            .Where(c => expectedNames.Any(n => MatchesToolName(c.ToolName, n)))
            .ToList();

        // Match each expected tool to an actual call (subsequence for ordered, set for unordered)
        var (matched, error) = FindMatches(requiredCalls, expectedNames, context);
        if (error != null)
        {
            return error;
        }

        // Check for extra calls to expected tools beyond what was configured
        var extraError = CheckExtraCalls(context);
        if (extraError != null)
        {
            return extraError;
        }

        // Validate inputs for matched calls
        var inputErrors = ValidateInputs(matched);
        if (inputErrors.Count > 0)
        {
            return Fail(
                $"Tool inputs did not match: {inputErrors.Count} error(s).",
                string.Join("\n", inputErrors));
        }

        var orderStr = EnforceOrder ? " in correct order" : "";
        var inputStr = ExpectedToolCalls.Any(tc => tc.ExpectedInputs != null) ? " with expected inputs" : "";
        return Pass($"All {ExpectedToolCalls.Count} expected tool(s) called{orderStr}{inputStr}.");
    }

    /// <summary>
    /// Checks that expected tools aren't called more times than configured.
    /// Tools that also appear in OptionalToolNames are exempt (retries allowed).
    /// </summary>
    private Task<ValidationResult>? CheckExtraCalls(
        ValidationContext context)
    {
        var expectedCounts = ExpectedToolCalls
            .GroupBy(tc => tc.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var (expectedName, maxCount) in expectedCounts)
        {
            // Extra calls are allowed if the tool is also optional
            if (OptionalToolNames.Any(o => string.Equals(o, expectedName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var actualCount = context.ToolCalls.Count(c => MatchesToolName(c.ToolName, expectedName));
            if (actualCount > maxCount)
            {
                return Fail(
                    $"Expected {maxCount} call(s) to '{expectedName}' but found {actualCount}.",
                    $"Add '{expectedName}' to OptionalToolNames if retries are acceptable.");
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if a tool call is from the MCP server (vs agent infrastructure like
    /// report_intent, powershell, view, etc). Uses the azsdk_ naming convention.
    /// </summary>
    private static bool IsMcpToolCall(string toolName) =>
        toolName.Contains("azsdk_", StringComparison.OrdinalIgnoreCase);

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
                {
                    continue;
                }
                if (!MatchesToolName(requiredCalls[j].ToolName, expected.ToolName))
                {
                    continue;
                }

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
            {
                continue;
            }

            var actualArgs = matchedCalls[i].GetArgsAsDictionary();
            foreach (var (key, expectedValue) in expected.ExpectedInputs)
            {
                if (!actualArgs.TryGetValue(key, out var actualJson))
                {
                    errors.Add($"Tool '{expected.ToolName}': missing input '{key}'.");
                    continue;
                }

                if (!InputMatches(expectedValue, actualJson, out var actualStr))
                {
                    errors.Add($"Tool '{expected.ToolName}': input '{key}' expected {FormatExpected(expectedValue)} but got '{actualStr}'.");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Checks if an actual JsonElement matches an expected value.
    /// Strings use case-insensitive substring matching with normalized path separators.
    /// Numerics and booleans use exact matching.
    /// </summary>
    private static bool InputMatches(object? expected, JsonElement actual, out string actualStr)
    {
        actualStr = actual.ToString() ?? "";

        if (expected == null)
        {
            return actual.ValueKind == JsonValueKind.Null;
        }

        switch (expected)
        {
            case string s:
                actualStr = actual.ValueKind == JsonValueKind.String
                    ? actual.GetString()! : actual.GetRawText();
                return actualStr.Replace('\\', '/').Contains(
                    s.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

            case bool b:
                return actual.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && actual.GetBoolean() == b;

            case int or long:
                return actual.TryGetInt64(out var al) && al == Convert.ToInt64(expected);

            case double d:
                return actual.TryGetDouble(out var ad) && Math.Abs(ad - d) <= 0.0001;

            default:
                actualStr = actual.GetRawText().Trim('"');
                return string.Equals(expected.ToString(), actualStr, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FormatExpected(object? value) => value is string s ? $"to contain '{s}'" : $"{value ?? "null"}";

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
