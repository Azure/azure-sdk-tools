// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper class for parsing and updating CHANGELOG.md files following Azure SDK conventions.
/// Based on eng/common/scripts/ChangeLog-Operations.ps1
/// </summary>
public interface IChangelogHelper
{
    /// <summary>
    /// Gets the path to the CHANGELOG.md file in the package directory.
    /// </summary>
    /// <param name="packagePath">The package directory path.</param>
    /// <returns>The path to CHANGELOG.md if it exists, null otherwise.</returns>
    string? GetChangelogPath(string packagePath);

    /// <summary>
    /// Parses a changelog file into structured entries.
    /// </summary>
    /// <param name="changelogPath">Path to the CHANGELOG.md file.</param>
    /// <returns>Parsed changelog data, or null if parsing fails.</returns>
    ChangelogData? ParseChangelog(string changelogPath);

    /// <summary>
    /// Extracts the release status (date or "Unreleased") from the first version entry in CHANGELOG.md.
    /// Format: ## [version] ([date]) or ## [version] (Unreleased)
    /// </summary>
    /// <param name="changelogPath">Absolute path to the CHANGELOG.md file.</param>
    /// <returns>The release status string (e.g., "2022-04-26" or "Unreleased"), or empty string if not found.</returns>
    Task<string> GetReleaseStatus(string changelogPath, CancellationToken ct);

    /// <summary>
    /// Updates the release status (date) for a specific version entry.
    /// </summary>
    /// <param name="changelogPath">Path to the CHANGELOG.md file.</param>
    /// <param name="version">The version to update.</param>
    /// <param name="releaseDate">The release date in yyyy-MM-dd format.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    ChangelogUpdateResult UpdateReleaseDate(string changelogPath, string version, string releaseDate);
}

/// <summary>
/// Represents parsed changelog data.
/// </summary>
public class ChangelogData
{
    /// <summary>
    /// The markdown header level used in this changelog (e.g., "#" for H1).
    /// </summary>
    public required string InitialAtxHeader { get; init; }

    /// <summary>
    /// The header block before any version entries (title, description, etc.).
    /// </summary>
    public required string HeaderBlock { get; init; }

    /// <summary>
    /// List of version entries in file order (newest first typically).
    /// Use <see cref="TryGetEntry"/> for O(1) lookup by version.
    /// </summary>
    public required List<ChangelogEntry> Entries { get; init; }

    // Lazy-initialized lookup dictionary for O(1) access by version
    private Dictionary<string, ChangelogEntry>? _entriesLookup;

    /// <summary>
    /// Tries to get an entry by version string.
    /// </summary>
    /// <param name="version">The version to look up.</param>
    /// <param name="entry">The entry if found.</param>
    /// <returns>True if the entry exists.</returns>
    public bool TryGetEntry(string version, out ChangelogEntry? entry)
    {
        _entriesLookup ??= Entries.ToDictionary(e => e.Version, e => e, StringComparer.OrdinalIgnoreCase);
        return _entriesLookup.TryGetValue(version, out entry);
    }
}

/// <summary>
/// Represents a single version entry in the changelog.
/// </summary>
public class ChangelogEntry
{
    /// <summary>
    /// The version string (e.g., "1.0.0", "1.0.0-beta.1").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The release status (e.g., "(Unreleased)", "(2025-01-30)").
    /// </summary>
    public string ReleaseStatus { get; set; } = string.Empty;

    /// <summary>
    /// The full release title line (e.g., "## 1.0.0 (2025-01-30)").
    /// </summary>
    public string ReleaseTitle { get; set; } = string.Empty;

    /// <summary>
    /// The content lines after the title (sections and their content).
    /// </summary>
    public List<string> ReleaseContent { get; set; } = [];
}

/// <summary>
/// Result of a changelog update operation.
/// </summary>
public class ChangelogUpdateResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static ChangelogUpdateResult CreateSuccess(string message) => new() { Success = true, Message = message };
    public static ChangelogUpdateResult CreateFailure(string message) => new() { Success = false, Message = message };
}

/// <summary>
/// Implementation of changelog helper operations.
/// </summary>
public partial class ChangelogHelper : IChangelogHelper
{
    private readonly ILogger<ChangelogHelper> _logger;

    // Constants matching PowerShell script
    private const string ChangelogFileName = "CHANGELOG.md";
    private const string UnreleasedStatus = "(Unreleased)";
    private const string DateFormat = "yyyy-MM-dd";

    // Regex patterns based on ChangeLog-Operations.ps1
    // SemVer pattern: major.minor.patch[-prerelease][+build]
    private const string SemVerPattern = @"(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?";

    // Release title pattern: ## VERSION (STATUS) where VERSION is semver and STATUS is date or "Unreleased"
    // Compiled once at class level for performance
    private static readonly Regex ReleaseTitleRegex = new(
        $@"^(?<headerLevel>#+)\s+v?(?<version>{SemVerPattern})(\s+(?<releaseStatus>\(.+\)))?",
        RegexOptions.Compiled);

    public ChangelogHelper(ILogger<ChangelogHelper> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string? GetChangelogPath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return null;
        }

        var changelogPath = Path.Combine(packagePath, ChangelogFileName);
        return File.Exists(changelogPath) ? changelogPath : null;
    }

    /// <inheritdoc/>
    public ChangelogData? ParseChangelog(string changelogPath)
    {
        if (!File.Exists(changelogPath))
        {
            _logger.LogError("Changelog file does not exist: {ChangelogPath}", changelogPath);
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(changelogPath);
            return ParseChangelogContent(lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing changelog: {ChangelogPath}", changelogPath);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetReleaseStatus(string changelogPath, CancellationToken ct)
    {
        if (!File.Exists(changelogPath))
        {
            return string.Empty;
        }

        await foreach (var line in File.ReadLinesAsync(changelogPath, ct))
        {
            // Match lines like: ## 1.0.3-beta.20 (2022-04-26), ### 1.0.0 (Unreleased), ## v1.0.0 (...)
            var match = ReleaseTitleRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            if (!match.Groups["releaseStatus"].Success)
            {
                continue;
            }

            var status = match.Groups["releaseStatus"].Value.Trim().Trim('(', ')').Trim();
            return status;
        }

        return string.Empty;
    }

    /// <inheritdoc/>
    public ChangelogUpdateResult UpdateReleaseDate(string changelogPath, string version, string releaseDate)
    {
        if (!File.Exists(changelogPath))
        {
            return ChangelogUpdateResult.CreateFailure($"Changelog file does not exist: {changelogPath}");
        }

        // Validate date format
        if (!DateTime.TryParseExact(releaseDate, DateFormat, null, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            return ChangelogUpdateResult.CreateFailure($"Invalid release date format: {releaseDate}. Expected format: {DateFormat}");
        }

        var data = ParseChangelog(changelogPath);
        if (data == null)
        {
            return ChangelogUpdateResult.CreateFailure("Failed to parse changelog file.");
        }

        if (!data.TryGetEntry(version, out var entry) || entry == null)
        {
            return ChangelogUpdateResult.CreateFailure(
                $"No changelog entry found for version {version}. Run another tool first to update changelog content for this version.");
        }

        // Format the new release status
        var formattedDate = parsedDate.ToString(DateFormat);
        var newReleaseStatus = $"({formattedDate})";

        // Check if already has this date
        if (entry.ReleaseStatus == newReleaseStatus)
        {
            _logger.LogDebug("Version {Version} already has release date {ReleaseDate}. No change made.", version, formattedDate);
            return ChangelogUpdateResult.CreateSuccess($"Version {version} already has release date {formattedDate}. No change needed.");
        }

        // Update the entry
        var releaseTitleAtxHeader = data.InitialAtxHeader + "#";
        entry.ReleaseStatus = newReleaseStatus;
        entry.ReleaseTitle = $"{releaseTitleAtxHeader} {version} {newReleaseStatus}";

        // Write back to file
        try
        {
            WriteChangelog(changelogPath, data);
            _logger.LogInformation("Updated changelog entry for version {Version} with release date {ReleaseDate}", version, formattedDate);
            return ChangelogUpdateResult.CreateSuccess($"Updated release date for version {version} to {formattedDate}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write changelog file: {ChangelogPath}", changelogPath);
            return ChangelogUpdateResult.CreateFailure($"Failed to write changelog file: {ex.Message}");
        }
    }

    private ChangelogData ParseChangelogContent(string[] lines)
    {
        var entries = new List<ChangelogEntry>();
        var headerLines = new List<string>();
        ChangelogEntry? currentEntry = null;

        // Determine the initial ATX header level from the first line
        var initialAtxHeader = "#";
        if (lines.Length > 0)
        {
            var headerMatch = Regex.Match(lines[0], @"^(?<HeaderLevel>#+)\s.*");
            if (headerMatch.Success)
            {
                initialAtxHeader = headerMatch.Groups["HeaderLevel"].Value;
            }
        }

        foreach (var line in lines)
        {
            var match = ReleaseTitleRegex.Match(line);
            if (match.Success)
            {
                // Found a version entry
                var version = match.Groups["version"].Value;
                var releaseStatus = match.Groups["releaseStatus"].Success ? match.Groups["releaseStatus"].Value : string.Empty;

                currentEntry = new ChangelogEntry
                {
                    Version = version,
                    ReleaseStatus = releaseStatus,
                    ReleaseTitle = line,
                    ReleaseContent = []
                };
                entries.Add(currentEntry);
            }
            else if (currentEntry != null)
            {
                // Add to current entry's content
                currentEntry.ReleaseContent.Add(line);
            }
            else
            {
                // Header content before any version entry
                headerLines.Add(line);
            }
        }

        return new ChangelogData
        {
            InitialAtxHeader = initialAtxHeader,
            HeaderBlock = string.Join(Environment.NewLine, headerLines),
            Entries = entries
        };
    }

    private void WriteChangelog(string changelogPath, ChangelogData data)
    {
        var lines = new List<string>();

        // Add header block
        if (!string.IsNullOrEmpty(data.HeaderBlock))
        {
            lines.Add(data.HeaderBlock);
        }
        else
        {
            lines.Add($"{data.InitialAtxHeader} Release History");
            lines.Add(string.Empty);
        }

        // Write entries in their original file order
        foreach (var entry in data.Entries)
        {
            lines.Add(entry.ReleaseTitle);
            if (entry.ReleaseContent.Count == 0)
            {
                lines.Add(string.Empty);
                lines.Add(string.Empty);
            }
            else
            {
                lines.AddRange(entry.ReleaseContent);
            }
        }

        File.WriteAllLines(changelogPath, lines);
    }
}
