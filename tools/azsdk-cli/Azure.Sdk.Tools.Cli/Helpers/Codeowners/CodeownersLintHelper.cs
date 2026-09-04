// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Checks an <c>owners.yaml</c> fragment on its own terms.
/// <para>
/// A fragment is required to be valid by itself, so linting one does not load the rest of the
/// repository. That is what makes this usable as a pull request gate: the answer depends only on the
/// file the author changed, and cannot swing because an unrelated service edited its own ownership.
/// </para>
/// </summary>
public interface ICodeownersLintHelper
{
    /// <summary>
    /// Lints the named fragments, or every fragment under <paramref name="repoRoot"/> when
    /// <paramref name="fragmentPaths"/> is empty.
    /// </summary>
    /// <param name="fragmentPaths">Repo-relative paths to <c>owners.yaml</c> files.</param>
    Task<CodeownersLintResult> Lint(string repoRoot, IReadOnlyList<string> fragmentPaths, CancellationToken ct);
}

/// <summary>Who owns a directory, according to the fragment that governs it.</summary>
/// <param name="Directory">Repo-relative directory path.</param>
/// <param name="Owners">Individuals resolved from the matching path entry, teams expanded.</param>
/// <param name="PrLabels">PR labels the matching path entry declares.</param>
/// <param name="MatchedPath">The authored path expression that matched, or null when none did.</param>
public sealed record DirectoryOwners(
    string Directory,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> PrLabels,
    string? MatchedPath);

/// <summary>Everything one fragment had to say.</summary>
public sealed record FragmentLintResult(
    string FilePath,
    IReadOnlyList<LintViolation> Violations,
    IReadOnlyList<DirectoryOwners> Directories);

public sealed record CodeownersLintResult(IReadOnlyList<FragmentLintResult> Fragments)
{
    public IEnumerable<LintViolation> AllViolations => Fragments.SelectMany(f => f.Violations);
}

public class CodeownersLintHelper(
    IOwnerValidator ownerValidator,
    ICommonLabelSource commonLabelSource) : ICodeownersLintHelper
{
    public async Task<CodeownersLintResult> Lint(
        string repoRoot,
        IReadOnlyList<string> fragmentPaths,
        CancellationToken ct)
    {
        await ownerValidator.EnsureUsableAsync(ct);
        var commonLabels = await commonLabelSource.GetLabelsAsync(ct);

        // The minimums are repository policy, so they come from the config even though nothing else
        // about the rest of the repository does.
        var settings = LoadSettings(repoRoot);

        var targets = fragmentPaths.Count > 0
            ? fragmentPaths
            : [.. OwnersRepositoryLoader.FindFragmentFiles(repoRoot)];

        return new CodeownersLintResult(
            [.. targets.Select(path => LintOne(repoRoot, path, settings, commonLabels))]);
    }

    /// <summary>
    /// Reads only the <c>configs</c> block. A missing or unreadable config falls back to the model
    /// defaults so that linting a fragment still works in a checkout that has not adopted the config
    /// yet.
    /// </summary>
    private static OwnersConfigSettings LoadSettings(string repoRoot)
    {
        try
        {
            return OwnersRepositoryLoader.Load(repoRoot, []).Config.Configs;
        }
        catch (OwnersYamlException)
        {
            return new OwnersConfigSettings();
        }
    }

    private FragmentLintResult LintOne(
        string repoRoot,
        string fragmentPath,
        OwnersConfigSettings settings,
        IReadOnlySet<string> commonLabels)
    {
        var violations = new List<LintViolation>();

        OwnersFragment fragment;
        try
        {
            var file = Path.Combine(repoRoot, fragmentPath.Replace('/', Path.DirectorySeparatorChar));
            fragment = OwnersYamlLoader.LoadFragment(File.ReadAllText(file), fragmentPath);
        }
        catch (Exception ex) when (ex is OwnersYamlException or IOException)
        {
            violations.Add(new LintViolation
            {
                RuleId = "LNT-SCHEMA-001",
                Description = ex.Message,
                SourceFile = fragmentPath,
            });

            return new FragmentLintResult(fragmentPath, violations, []);
        }

        var declaredLabelOwners = fragment.LabelOwners
            .SelectMany(block => block.Labels)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        CheckPaths(fragment, repoRoot, settings, declaredLabelOwners, violations);
        CheckLabelOwnerBlocks(fragment, settings, commonLabels, violations);

        return new FragmentLintResult(fragmentPath, violations, DescribeDirectories(fragment, repoRoot));
    }

    private void CheckPaths(
        OwnersFragment fragment,
        string repoRoot,
        OwnersConfigSettings settings,
        HashSet<string> declaredLabelOwners,
        List<LintViolation> violations)
    {
        foreach (var entry in fragment.Paths)
        {
            var where = $"{fragment.FilePath}:{entry.Line}";

            // Containment first. generate silently drops an entry that escapes its subtree, so this
            // is the only thing standing between a '..' path and ownership disappearing unnoticed.
            var pathErrors = new List<OwnersValidationError>();
            OwnersPathResolver.ResolveFragmentPath(fragment, entry, repoRoot, pathErrors);

            violations.AddRange(pathErrors.Select(error => new LintViolation
            {
                RuleId = error.Code,
                Description = error.Message,
                SourceFile = where,
            }));

            ValidateOwners(entry.Owners, where, violations);

            var individuals = ownerValidator.ExpandToIndividuals(entry.Owners);
            if (individuals.Count < settings.MinimumPathOwners)
            {
                violations.Add(new LintViolation
                {
                    RuleId = "LNT-OWN-003",
                    Description =
                        $"Path '{entry.Path}' resolves to {individuals.Count} owner(s); " +
                        $"at least {settings.MinimumPathOwners} are required.",
                    SourceFile = where,
                    Detail = $"Declared owners: [{string.Join(", ", entry.Owners)}]. Team owners count as their members.",
                });
            }

            if (entry.PrLabels.Count == 0)
            {
                violations.Add(new LintViolation
                {
                    RuleId = "LNT-LBL-002",
                    Description = $"Path '{entry.Path}' declares no pr-labels.",
                    SourceFile = where,
                    Detail = "Every path entry in a fragment must name the PR label that routes its pull requests.",
                });
            }

            foreach (var label in entry.PrLabels.Where(label => !declaredLabelOwners.Contains(label)))
            {
                violations.Add(new LintViolation
                {
                    RuleId = "LNT-LBL-003",
                    Description = $"Path '{entry.Path}' uses PR label '{label}', which no label-owners block in this file claims.",
                    SourceFile = where,
                    Detail = "Add the label to a label-owners entry in this file so issues carrying it have owners.",
                });
            }
        }
    }

    private void CheckLabelOwnerBlocks(
        OwnersFragment fragment,
        OwnersConfigSettings settings,
        IReadOnlySet<string> commonLabels,
        List<LintViolation> violations)
    {
        foreach (var block in fragment.LabelOwners)
        {
            var where = $"{fragment.FilePath}:{block.Line}";

            foreach (var label in block.Labels.Where(label => !commonLabels.Contains(label)))
            {
                violations.Add(new LintViolation
                {
                    RuleId = "LNT-LBL-001",
                    Description = $"Label '{label}' is not in the common label set.",
                    SourceFile = where,
                    Detail = $"Add it to {CommonLabelSource.CommonLabelsCsvUrl} first, so the label means the same thing in every language repo.",
                });
            }

            ValidateOwners(block.ServiceOwners, where, violations);
            ValidateOwners(block.AzureSdkOwners, where, violations);

            var individuals = ownerValidator.ExpandToIndividuals(block.ServiceOwners);
            if (individuals.Count < settings.MinimumLabelOwners)
            {
                violations.Add(new LintViolation
                {
                    RuleId = "LNT-OWN-004",
                    Description =
                        $"Labels [{string.Join(", ", block.Labels)}] resolve to {individuals.Count} service owner(s); " +
                        $"at least {settings.MinimumLabelOwners} are required.",
                    SourceFile = where,
                    Detail = $"Declared service owners: [{string.Join(", ", block.ServiceOwners)}]. Team owners count as their members.",
                });
            }
        }
    }

    private void ValidateOwners(IEnumerable<string> owners, string where, List<LintViolation> violations) =>
        violations.AddRange(owners
            .Select(owner => ownerValidator.Validate(owner, where))
            .OfType<LintViolation>());

    /// <summary>
    /// Reports who owns each immediate subdirectory of the fragment's directory, so a reviewer can
    /// see the effect of the file rather than reading path expressions. Directories that are not
    /// packages are included: recognizing a package means knowing five languages' project layouts,
    /// and the answer is useful without it.
    /// </summary>
    private IReadOnlyList<DirectoryOwners> DescribeDirectories(OwnersFragment fragment, string repoRoot)
    {
        var root = Path.Combine(repoRoot, fragment.Directory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateDirectories(root)
                .Select(dir => Path.GetRelativePath(repoRoot, dir).Replace('\\', '/'))
                .OrderBy(dir => dir, StringComparer.Ordinal)
                .Select(dir => Describe(dir, fragment, repoRoot))
        ];
    }

    private DirectoryOwners Describe(string directory, OwnersFragment fragment, string repoRoot)
    {
        // Last match wins, the way GitHub resolves the rendered file.
        for (var i = fragment.Paths.Count - 1; i >= 0; i--)
        {
            var entry = fragment.Paths[i];
            var expression = OwnersPathResolver.ResolveFragmentPath(fragment, entry, repoRoot, []);

            if (expression != null && Matches(expression, directory))
            {
                return new DirectoryOwners(
                    directory,
                    ownerValidator.ExpandToIndividuals(entry.Owners),
                    entry.PrLabels,
                    entry.Path);
            }
        }

        return new DirectoryOwners(directory, [], [], null);
    }

    /// <summary>
    /// Prefix match against the resolved expression. Only whole path segments count, so
    /// <c>/sdk/storage/</c> matches <c>sdk/storage/blob</c> but not <c>sdk/storage-extras</c>.
    /// </summary>
    private static bool Matches(string expression, string directory)
    {
        var prefix = expression.Trim('/');
        var candidate = directory.Trim('/');

        if (prefix.Contains('*'))
        {
            var stem = prefix[..prefix.IndexOf('*')].TrimEnd('/');

            return stem.Length == 0 || IsSelfOrUnder(candidate, stem);
        }

        return IsSelfOrUnder(candidate, prefix);
    }

    private static bool IsSelfOrUnder(string candidate, string prefix) =>
        candidate.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase);
}
