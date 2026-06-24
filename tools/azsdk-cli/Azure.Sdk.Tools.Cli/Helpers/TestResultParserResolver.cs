// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services;

public interface ITestResultParserResolver
{
    /// <summary>
    /// Returns the correct parser for the given file, or null if no parser can handle it
    /// or the file doesn't exist. Throws ArgumentException for null/empty path.
    /// </summary>
    ITestHelper? Resolve(string filePath);

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

    public ITestHelper? Resolve(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            return null;
        }

        return _parsers.FirstOrDefault(p => p.CanParse(filePath));
    }
}
