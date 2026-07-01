// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services;

public interface ITestResultParserResolver
{
    /// <summary>
    /// Returns the parser for the given file. Throws <see cref="FileNotFoundException"/> if the file
    /// does not exist, or <see cref="InvalidOperationException"/> if no registered parser recognizes
    /// the format. Throws <see cref="ArgumentException"/> for a null/empty path.
    /// </summary>
    Task<ITestHelper> ResolveAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// The list of format names this resolver supports (for error messages).
    /// </summary>
    IReadOnlyList<string> SupportedFormats { get; }
}

public class TestResultParserResolver(IEnumerable<ITestHelper> parsers) : ITestResultParserResolver
{
    private readonly ITestHelper[] _parsers = parsers.ToArray();

    public IReadOnlyList<string> SupportedFormats =>
        _parsers.Select(p => p.FormatName).ToList().AsReadOnly();

    public async Task<ITestHelper> ResolveAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test result file not found: {filePath}", filePath);
        }

        var matches = new List<ITestHelper>();
        foreach (var parser in _parsers)
        {
            if (await parser.CanParseAsync(filePath, ct))
            {
                matches.Add(parser);
            }
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple parsers recognize the test result format for: {filePath}. " +
                $"Matching formats: {string.Join(", ", matches.Select(p => p.FormatName))}");
        }

        return matches.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Unrecognized test result format for: {filePath}. Supported formats: {string.Join(", ", SupportedFormats)}");
    }
}
