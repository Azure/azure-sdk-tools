// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Checks the ownership YAML against the things it cannot verify about itself: whether its owners
/// are real code owners, whether it names enough of them, and whether its labels exist.
/// <para>
/// Lint reads and reports; it never edits the repository and never renders CODEOWNERS. Anything
/// wrong enough to stop the file loading is reported as-is rather than analysed further, because no
/// rule below would mean anything against ownership that does not parse.
/// </para>
/// </summary>
public class CodeownersLintHelper(
    IOwnerValidator ownerValidator,
    ICommonLabelSource commonLabelSource,
    ILogger<CodeownersLintHelper> logger) : ICodeownersLintHelper
{
    public async Task<CodeownersLintResponse> RunLint(string repoRoot, CancellationToken ct)
    {
        var response = new CodeownersLintResponse { RepoRoot = repoRoot };

        var loadErrors = new List<OwnersValidationError>();
        var repository = OwnersRepositoryLoader.Load(repoRoot, loadErrors);
        var rendered = CodeownersRenderer.Render(repository);

        // The lint rules assume ownership that renders. Anything that stops it from rendering is a
        // generate-time error and is reported as-is rather than analysed further.
        var blocking = loadErrors.Concat(rendered.Errors).ToList();
        if (blocking.Count > 0)
        {
            response.Violations.AddRange(blocking.Select(error => new LintViolation
            {
                RuleId = error.Code,
                Description = error.Message,
            }));

            return response;
        }

        await ownerValidator.EnsureUsableAsync(ct);

        response.Violations.AddRange(CheckOwners(repository));
        response.Violations.AddRange(CheckOwnerCounts(repository, rendered));
        response.Violations.AddRange(await CheckLabels(repository, ct));

        logger.LogInformation("Lint found {Count} violation(s).", response.Violations.Count);

        return response;
    }

    /// <summary>LNT-OWN-001 and LNT-OWN-002: owners GitHub would not route a review to.</summary>
    private IEnumerable<LintViolation> CheckOwners(OwnersRepository repository) =>
        EnumerateOwners(repository)
            .Select(declared => ownerValidator.Validate(declared.Owner, declared.Where))
            .OfType<LintViolation>();

    /// <summary>
    /// LNT-OWN-003 and LNT-OWN-004: too few individual owners to survive one person leaving. Teams do
    /// not count, because a team is one point of failure however many people are in it.
    /// <para>
    /// Counted against the rendered entries rather than the YAML so a label-owner block is measured
    /// after the union across fragments, which is the number that actually gets paged.
    /// </para>
    /// </summary>
    private static IEnumerable<LintViolation> CheckOwnerCounts(
        OwnersRepository repository,
        CodeownersRenderResult rendered)
    {
        var settings = repository.Config.Configs;

        foreach (var entry in rendered.Entries.Where(e => e.IsFromFragment))
        {
            var isPathEntry = entry.Entry.PathExpression.Length > 0;
            var count = CountIndividuals(isPathEntry ? entry.Entry.SourceOwners : entry.Entry.ServiceOwners);
            var minimum = isPathEntry ? settings.MinimumPathOwners : settings.MinimumLabelOwners;

            if (count >= minimum)
            {
                continue;
            }

            var subject = isPathEntry
                ? $"Path '{entry.Entry.PathExpression}'"
                : $"Label set '{string.Join(", ", entry.Entry.ServiceLabels)}'";

            yield return new LintViolation
            {
                RuleId = isPathEntry ? "LNT-OWN-003" : "LNT-OWN-004",
                Description = $"{subject} has {count} individual owner(s); at least {minimum} are required.",
                SourceFile = entry.DeclaredAt,
                Detail = $"Declared in: {string.Join(", ", entry.Sources)}",
            };
        }
    }

    /// <summary>Owners are already de-duplicated by the loader, so this only drops teams.</summary>
    private static int CountIndividuals(IEnumerable<string>? owners) =>
        (owners ?? []).Count(owner => !ParsingUtils.IsGitHubTeam(owner.TrimStart('@')));

    /// <summary>
    /// LNT-LBL-001: a label outside the sanctioned common set. Fragments only — the repo-level config
    /// is not linted today.
    /// </summary>
    private async Task<List<LintViolation>> CheckLabels(OwnersRepository repository, CancellationToken ct)
    {
        var knownLabels = await commonLabelSource.GetLabelsAsync(ct);

        return
        [
            .. repository.Fragments.SelectMany(fragment =>
                fragment.Paths
                    .SelectMany(path => UnknownLabels(path.PrLabels, knownLabels, $"{fragment.FilePath}:{path.Line}"))
                    .Concat(fragment.LabelOwners.SelectMany(labelOwner =>
                        UnknownLabels(labelOwner.Labels, knownLabels, $"{fragment.FilePath}:{labelOwner.Line}"))))
        ];
    }

    private static IEnumerable<LintViolation> UnknownLabels(
        IEnumerable<string> labels,
        IReadOnlySet<string> knownLabels,
        string where) =>
        labels
            .Where(label => !knownLabels.Contains(label))
            .Select(label => new LintViolation
            {
                RuleId = "LNT-LBL-001",
                Description = $"Label '{label}' is not in the common label set.",
                SourceFile = where,
                Detail =
                    "Labels used by ownership files must exist in " +
                    $"{CommonLabelSource.CommonLabelsCsvUrl}. Add it there first if the label is new.",
            });

    /// <summary>Every owner declared in every fragment, with a file:line to report it against.</summary>
    private static IEnumerable<(string Owner, string Where)> EnumerateOwners(OwnersRepository repository)
    {
        foreach (var fragment in repository.Fragments)
        {
            foreach (var path in fragment.Paths)
            {
                foreach (var owner in path.Owners)
                {
                    yield return (owner, $"{fragment.FilePath}:{path.Line}");
                }
            }

            foreach (var labelOwner in fragment.LabelOwners)
            {
                foreach (var owner in labelOwner.ServiceOwners.Concat(labelOwner.AzureSdkOwners))
                {
                    yield return (owner, $"{fragment.FilePath}:{labelOwner.Line}");
                }
            }
        }
    }
}
