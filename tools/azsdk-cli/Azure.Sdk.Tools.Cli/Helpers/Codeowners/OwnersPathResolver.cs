// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Turns authored path expressions into the repo-absolute expressions that render into CODEOWNERS,
/// and enforces the <c>CFG-PATH-*</c> rules while doing it.
/// <para>
/// Normalization is deliberately minimal: a fragment path is joined to its fragment's directory and
/// a config path is used as written. Whether an expression names a file or a directory is never
/// inferred from the string — authors write the trailing slash and <c>CFG-PATH-005</c> checks it
/// against the working tree.
/// </para>
/// </summary>
public static class OwnersPathResolver
{
    /// <summary>
    /// Resolves a fragment path entry, or returns null and appends the rule that rejected it.
    /// </summary>
    /// <param name="repoRoot">Checkout the trailing-slash rule is verified against.</param>
    public static string? ResolveFragmentPath(
        OwnersFragment fragment,
        OwnersPathEntry entry,
        string repoRoot,
        List<OwnersValidationError> errors)
    {
        var authored = entry.Path?.Trim() ?? string.Empty;
        var where = $"{fragment.FilePath}:{entry.Line}";

        if (authored.Length == 0)
        {
            errors.Add(new OwnersValidationError("CFG-PATH-003", $"{where}: path is required."));
            return null;
        }

        // Textual, and first, so no clever expression can reach the resolver below.
        if (HasParentSegment(authored))
        {
            errors.Add(new OwnersValidationError("CFG-PATH-001",
                $"{where}: path '{authored}' contains a '..' segment. A fragment may only own its own subtree."));
            return null;
        }

        if (authored.StartsWith('/'))
        {
            errors.Add(new OwnersValidationError("CFG-PATH-003",
                $"{where}: path '{authored}' is repo-absolute. Fragment paths are relative to {fragment.Directory}/; " +
                "repo-absolute expressions belong in .github/owners.config.yaml."));
            return null;
        }

        var expression = authored == "."
            ? $"/{fragment.Directory}/"
            : $"/{fragment.Directory}/{authored}";

        if (!expression.StartsWith($"/{fragment.Directory}/", StringComparison.Ordinal))
        {
            errors.Add(new OwnersValidationError("CFG-PATH-002",
                $"{where}: path '{authored}' resolves to '{expression}', which is outside {fragment.Directory}/."));
            return null;
        }

        return RequireAuthoredTrailingSlash(expression, authored, where, repoRoot, errors) ? expression : null;
    }

    /// <summary>
    /// Validates a static owners config path entry. Config expressions are used exactly as authored:
    /// they are carried over from a CODEOWNERS file GitHub already enforces, and some of them are not
    /// expressible under the fragment rules or even accepted by our own matcher.
    /// </summary>
    public static string? ResolveConfigPath(
        OwnersSection section,
        OwnersPathEntry entry,
        List<OwnersValidationError> errors)
    {
        var authored = entry.Path?.Trim() ?? string.Empty;

        if (!authored.StartsWith('/'))
        {
            errors.Add(new OwnersValidationError("CFG-PATH-004",
                $".github/owners.config.yaml:{entry.Line}: path '{authored}' in section '{section.Name}' " +
                "must be repo-absolute (start with '/')."));
            return null;
        }

        return authored;
    }

    private static bool HasParentSegment(string path) =>
        path.Replace('\\', '/').Split('/').Any(segment => segment == "..");

    /// <summary>
    /// A glob-free expression that names a directory on disk must be authored with a trailing slash;
    /// one that names a file must not. Globs are skipped because they cannot be resolved by stat, and
    /// an expression matching nothing on disk is an orphan reported by the audit rather than a
    /// generation error.
    /// </summary>
    private static bool RequireAuthoredTrailingSlash(
        string expression,
        string authored,
        string where,
        string repoRoot,
        List<OwnersValidationError> errors)
    {
        if (expression.Contains('*', StringComparison.Ordinal))
        {
            return true;
        }

        var onDisk = Path.Combine(repoRoot, expression.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var endsWithSlash = expression.EndsWith('/');

        if (Directory.Exists(onDisk) && !endsWithSlash)
        {
            errors.Add(new OwnersValidationError("CFG-PATH-005",
                $"{where}: path '{authored}' names a directory and must be authored with a trailing '/'. " +
                "Without it the expression also matches a file of the same name."));
            return false;
        }

        if (File.Exists(onDisk.TrimEnd(Path.DirectorySeparatorChar)) && endsWithSlash)
        {
            errors.Add(new OwnersValidationError("CFG-PATH-005",
                $"{where}: path '{authored}' names a file and must not be authored with a trailing '/'."));
            return false;
        }

        return true;
    }
}
