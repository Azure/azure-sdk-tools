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
    /// <summary>Gets the name of this validator.</summary>
    public string Name { get; }

    /// <summary>Gets the ordered list of expected tool calls.</summary>
    public IReadOnlyList<ExpectedToolCall> ExpectedToolCalls { get; }

    /// <summary>Gets the list of optional tool names that may appear but aren't required.</summary>
    public IReadOnlyList<string> OptionalToolNames { get; }

    /// <summary>Gets the list of forbidden tool names that must not be called.</summary>
    public IReadOnlyList<string> ForbiddenToolNames { get; }

    /// <summary>Gets whether tool call order is enforced.</summary>
    public bool EnforceOrder { get; }

    /// <summary>
    /// Creates a validator using rich <see cref="ExpectedToolCall"/> objects that can
    /// validate both tool names and their inputs.
    /// </summary>
    /// <param name="name">Human-readable name for the validator.</param>
    /// <param name="expectedToolCalls">Ordered list of expected tool calls with optional input expectations.</param>
    /// <param name="optionalToolNames">Optional tool names that may appear but won't cause failure if absent.</param>
    /// <param name="forbiddenToolNames">Tool names that must not appear in actual calls.</param>
    /// <param name="enforceOrder">Whether to enforce the order of expected tool calls (default: true).</param>
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
    /// Creates a validator that only checks tool names and order (no input validation).
    /// </summary>
    /// <param name="name">Human-readable name for the validator.</param>
    /// <param name="expectedToolNames">Ordered list of expected tool names.</param>
    /// <param name="optionalToolNames">Optional tool names that may appear but won't cause failure if absent.</param>
    /// <param name="forbiddenToolNames">Tool names that must not appear in actual calls.</param>
    /// <param name="enforceOrder">Whether to enforce the order of expected tool calls (default: true).</param>
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
            return Task.FromResult(ValidationResult.Fail(Name, "No expected tool calls configured."));
        }

        // Check for forbidden tool calls
        if (ForbiddenToolNames.Count > 0)
        {
            var forbiddenCalls = context.ToolCalls
                .Where(call => ForbiddenToolNames.Any(f => MatchesToolName(call.ToolName, f)))
                .ToList();

            if (forbiddenCalls.Count > 0)
            {
                var forbiddenStr = string.Join(", ", forbiddenCalls.Select(c => c.ToolName));
                return Task.FromResult(ValidationResult.Fail(Name,
                    $"Forbidden tool(s) were called: [{forbiddenStr}].",
                    $"Forbidden tools: [{string.Join(", ", ForbiddenToolNames)}]"));
            }
        }

        var expectedNames = ExpectedToolCalls.Select(tc => tc.ToolName).ToList();

        // Filter actual tool calls to only those matching expected or optional tools.
        // Uses suffix matching because the SDK may capture prefixed names
        // like "mcp_azure-sdk-mcp_azsdk_create_release_plan".
        var allRelevantNames = expectedNames.Concat(OptionalToolNames).ToList();

        var relevantCalls = context.ToolCalls
            .Where(call => allRelevantNames.Any(name => MatchesToolName(call.ToolName, name)))
            .ToList();

        // Keep only required calls (filter out optional-only tools).
        var requiredCalls = relevantCalls
            .Where(call => expectedNames.Any(exp => MatchesToolName(call.ToolName, exp)))
            .ToList();

        // Match each expected tool to an actual call using subsequence matching.
        // This naturally tolerates duplicate calls of the same tool.
        var matchedCalls = new List<ToolCallRecord>();

        if (EnforceOrder)
        {
            int searchStart = 0;
            for (int i = 0; i < ExpectedToolCalls.Count; i++)
            {
                var expected = ExpectedToolCalls[i];
                int foundIndex = -1;

                for (int j = searchStart; j < requiredCalls.Count; j++)
                {
                    if (MatchesToolName(requiredCalls[j].ToolName, expected.ToolName))
                    {
                        foundIndex = j;
                        break;
                    }
                }

                if (foundIndex == -1)
                {
                    var actualStr = string.Join(", ", requiredCalls.Select(c => c.ToolName));
                    return Task.FromResult(ValidationResult.Fail(Name,
                        $"Expected tool '{expected.ToolName}' not found{(i > 0 ? $" after '{ExpectedToolCalls[i - 1].ToolName}'" : "")}.",
                        $"Expected order: [{string.Join(", ", expectedNames)}]\nActual: [{actualStr}]\nAll tool calls: [{string.Join(", ", context.ToolCalls.Select(tc => tc.ToolName))}]"));
                }

                matchedCalls.Add(requiredCalls[foundIndex]);
                searchStart = foundIndex + 1;
            }
        }
        else
        {
            var usedIndices = new HashSet<int>();
            for (int i = 0; i < ExpectedToolCalls.Count; i++)
            {
                var expected = ExpectedToolCalls[i];
                int foundIndex = -1;

                for (int j = 0; j < requiredCalls.Count; j++)
                {
                    if (!usedIndices.Contains(j) && MatchesToolName(requiredCalls[j].ToolName, expected.ToolName))
                    {
                        foundIndex = j;
                        break;
                    }
                }

                if (foundIndex == -1)
                {
                    var actualStr = string.Join(", ", requiredCalls.Select(c => c.ToolName));
                    return Task.FromResult(ValidationResult.Fail(Name,
                        $"Expected tool '{expected.ToolName}' not found.",
                        $"Expected: [{string.Join(", ", expectedNames)}]\nActual: [{actualStr}]\nAll tool calls: [{string.Join(", ", context.ToolCalls.Select(tc => tc.ToolName))}]"));
                }

                matchedCalls.Add(requiredCalls[foundIndex]);
                usedIndices.Add(foundIndex);
            }
        }

        // Validate each matched tool call's inputs
        var inputErrors = new List<string>();

        for (int i = 0; i < ExpectedToolCalls.Count; i++)
        {
            var expected = ExpectedToolCalls[i];
            var actual = matchedCalls[i];

            // Check inputs (if specified)
            if (expected.ExpectedInputs != null)
            {
                var actualArgs = actual.GetArgsAsDictionary();
                foreach (var (key, expectedValue) in expected.ExpectedInputs)
                {
                    if (!actualArgs.TryGetValue(key, out var actualJsonValue))
                    {
                        inputErrors.Add($"Tool '{expected.ToolName}': missing expected input '{key}'.");
                        continue;
                    }

                    var mismatch = CompareValue(key, expectedValue, actualJsonValue);
                    if (mismatch != null)
                    {
                        inputErrors.Add($"Tool '{expected.ToolName}': {mismatch}");
                    }
                }
            }
        }

        if (inputErrors.Count > 0)
        {
            return Task.FromResult(ValidationResult.Fail(Name,
                $"Tool inputs did not match: {inputErrors.Count} error(s).",
                string.Join("\n", inputErrors)));
        }

        var hasInputChecks = ExpectedToolCalls.Any(tc => tc.ExpectedInputs != null);
        var orderStr = EnforceOrder ? " in correct order" : "";
        var inputStr = hasInputChecks ? " with expected inputs" : "";
        var message = $"All {ExpectedToolCalls.Count} expected tool(s) called{orderStr}{inputStr}.";

        return Task.FromResult(ValidationResult.Pass(Name, message));
    }

    /// <summary>
    /// Compares an expected value against an actual JsonElement.
    /// String values use case-insensitive substring matching (handles variable path prefixes).
    /// Numeric and boolean values use exact matching.
    /// </summary>
    /// <returns>Error description if mismatch, null if match.</returns>
    private static string? CompareValue(string key, object? expectedValue, JsonElement actual)
    {
        if (expectedValue == null)
        {
            return actual.ValueKind == JsonValueKind.Null
                ? null
                : $"input '{key}': expected null but got '{actual}'.";
        }

        switch (expectedValue)
        {
            case string expectedStr:
                var actualStr = actual.ValueKind == JsonValueKind.String
                    ? actual.GetString()
                    : actual.GetRawText();

                // Normalize path separators so forward-slash expected values match
                // backslash actual values (and vice versa) on Windows.
                var normalizedActual = actualStr?.Replace('\\', '/');
                var normalizedExpected = expectedStr.Replace('\\', '/');

                if (normalizedActual == null || !normalizedActual.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase))
                {
                    return $"input '{key}': expected to contain '{expectedStr}' but got '{actualStr}'.";
                }
                return null;

            case bool expectedBool:
                if (actual.ValueKind != JsonValueKind.True && actual.ValueKind != JsonValueKind.False)
                {
                    return $"input '{key}': expected boolean {expectedBool} but got '{actual}'.";
                }
                if (actual.GetBoolean() != expectedBool)
                {
                    return $"input '{key}': expected {expectedBool} but got {actual.GetBoolean()}.";
                }
                return null;

            case int expectedInt:
                if (!actual.TryGetInt32(out var actualInt) || actualInt != expectedInt)
                {
                    return $"input '{key}': expected {expectedInt} but got '{actual}'.";
                }
                return null;

            case long expectedLong:
                if (!actual.TryGetInt64(out var actualLong) || actualLong != expectedLong)
                {
                    return $"input '{key}': expected {expectedLong} but got '{actual}'.";
                }
                return null;

            case double expectedDouble:
                if (!actual.TryGetDouble(out var actualDouble) || Math.Abs(actualDouble - expectedDouble) > 0.0001)
                {
                    return $"input '{key}': expected {expectedDouble} but got '{actual}'.";
                }
                return null;

            default:
                // For other types, compare string representations
                var expectedRepr = expectedValue.ToString();
                var actualRepr = actual.GetRawText().Trim('"');
                if (!string.Equals(expectedRepr, actualRepr, StringComparison.OrdinalIgnoreCase))
                {
                    return $"input '{key}': expected '{expectedRepr}' but got '{actualRepr}'.";
                }
                return null;
        }
    }

    /// <summary>
    /// Checks if an actual tool call name matches an expected tool name.
    /// Handles MCP prefix (e.g., "mcp_azure-sdk-mcp_azsdk_create_release_plan"
    /// matches "azsdk_create_release_plan").
    /// </summary>
    private static bool MatchesToolName(string actualCallName, string expectedToolName)
    {
        return actualCallName.EndsWith(expectedToolName, StringComparison.OrdinalIgnoreCase)
               || expectedToolName.EndsWith(actualCallName, StringComparison.OrdinalIgnoreCase);
    }
}
