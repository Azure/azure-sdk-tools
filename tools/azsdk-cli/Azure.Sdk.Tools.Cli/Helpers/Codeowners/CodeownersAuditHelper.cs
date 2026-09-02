// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Validates declared ownership against things the YAML cannot know on its own: cached GitHub team
/// and org membership, cached repository labels, and the working tree.
/// <para>
/// Audit and generate divide the work. Audit is the only command that reaches outside the repository,
/// and the only one that changes ownership YAML. Generate then renders whatever the repository says,
/// with no opinion about whether the owners still exist.
/// </para>
/// </summary>
public class CodeownersAuditHelper(
    ITeamUserCache teamUserCache,
    RepoLabelCache repoLabelCache,
    UserOrgVisibilityCache userOrgVisibilityCache,
    ICacheValidator cacheValidator,
    ILogger<CodeownersAuditHelper> logger) : ICodeownersAuditHelper
{
    /// <summary>How stale cached membership data may be before the audit refuses to act on it.</summary>
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(6);

    /// <summary>
    /// Above this many owner removals in one run, <c>--fix</c> stops and asks for <c>--force</c>. A
    /// membership cache that was published truncated looks exactly like a mass departure, and the
    /// difference matters: the first is a bug and the second is real.
    /// </summary>
    private const int RemovalSafetyThreshold = 5;

    private const string AzureSdkWriteTeam = "Azure/azure-sdk-write";
    private const string ServiceAttentionLabel = "Service Attention";

    public async Task<CodeownersAuditResponse> RunAudit(
        string repoRoot,
        string repo,
        bool fix,
        bool force,
        CancellationToken ct)
    {
        var response = new CodeownersAuditResponse
        {
            RepoRoot = repoRoot,
            FixRequested = fix,
            ForceRequested = force,
        };

        var loadErrors = new List<OwnersValidationError>();
        var repository = OwnersRepositoryLoader.Load(repoRoot, loadErrors);
        var rendered = CodeownersRenderer.Render(repository);

        // The audit rules assume ownership that renders. Anything that stops it from rendering is a
        // generate-time error and is reported as-is rather than analysed further.
        var blocking = loadErrors.Concat(rendered.Errors).ToList();
        if (blocking.Count > 0)
        {
            response.Violations.AddRange(blocking.Select(error => new AuditViolation
            {
                RuleId = error.Code,
                Description = error.Message,
            }));

            return response;
        }

        await EnsureCachesAreFresh(ct);

        var invalidOwners = FindInvalidOwners(repository);

        response.Violations.AddRange(invalidOwners.Select(o => o.Violation));
        response.Violations.AddRange(CheckOwnerCounts(repository, rendered));
        response.Violations.AddRange(CheckLabels(repository, repo));
        response.Violations.AddRange(CheckPaths(repository, rendered));
        response.Violations.AddRange(CheckOrdering(rendered));

        if (fix)
        {
            response.FixResults.AddRange(RemoveInvalidOwners(repoRoot, invalidOwners, force));
        }

        return response;
    }

    // ------------------------------------------------------------- owner rules

    /// <summary>An owner the audit believes should no longer be in the file, and where it lives.</summary>
    private sealed record InvalidOwner(string FilePath, string Alias, AuditViolation Violation);

    /// <summary>
    /// AUD-OWN-001 (individuals), AUD-OWN-002 (malformed team aliases) and AUD-OWN-003 (teams outside
    /// azure-sdk-write). These run together because they all walk the same owner lists.
    /// </summary>
    private List<InvalidOwner> FindInvalidOwners(OwnersRepository repository)
    {
        var writeUsers = teamUserCache.GetUsersForTeam(AzureSdkWriteTeam).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orgVisibility = userOrgVisibilityCache.UserOrgVisibilityDict;

        // Fail closed. An empty cache would mark every owner in the repository invalid, and with
        // --fix would empty the files.
        if (writeUsers.Count == 0 || orgVisibility.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cached membership for '{AzureSdkWriteTeam}' or Azure org visibility came back empty. " +
                "Refusing to validate owners against an empty cache; run 'azsdk config codeowners update-cache' and retry.");
        }

        var invalid = new List<InvalidOwner>();

        foreach (var (fragment, owner, where) in EnumerateOwners(repository))
        {
            var alias = owner.TrimStart('@');

            if (ParsingUtils.IsGitHubTeam(alias))
            {
                var violation = ValidateTeam(alias, where);
                if (violation != null)
                {
                    // Only AUD-OWN-003 is removable; a malformed alias needs a human to say what was
                    // meant, so it is reported without a file to edit.
                    invalid.Add(new InvalidOwner(
                        violation.RuleId == "AUD-OWN-003" ? fragment.FilePath : string.Empty, owner, violation));
                }

                continue;
            }

            var hasWriteAccess = writeUsers.Contains(alias);
            var isInAzureOrg = orgVisibility.TryGetValue(alias, out var visible) && visible;

            if (hasWriteAccess && isInAzureOrg)
            {
                continue;
            }

            logger.LogWarning("Owner '{Alias}' at {Where} is not a valid code owner.", alias, where);

            invalid.Add(new InvalidOwner(fragment.FilePath, owner, new AuditViolation
            {
                RuleId = "AUD-OWN-001",
                Description = $"Owner '{alias}' is not a valid code owner.",
                SourceFile = where,
                Detail = $"azure-sdk-write member: {hasWriteAccess}, Azure org visible: {isInAzureOrg}",
            }));
        }

        return invalid;
    }

    private AuditViolation? ValidateTeam(string alias, string where)
    {
        var parts = alias.Split('/');
        if (parts.Length != 2 || !parts[0].Equals("Azure", StringComparison.OrdinalIgnoreCase) || parts[1].Length == 0)
        {
            return new AuditViolation
            {
                RuleId = "AUD-OWN-002",
                Description = $"Team owner '{alias}' is malformed; expected Azure/<team>.",
                SourceFile = where,
            };
        }

        // A team with no cached membership is not a descendant of azure-sdk-write, which is what
        // grants write access in the first place.
        if (teamUserCache.GetUsersForTeam(alias).Count == 0)
        {
            return new AuditViolation
            {
                RuleId = "AUD-OWN-003",
                Description = $"Team '{alias}' does not descend from {AzureSdkWriteTeam}.",
                SourceFile = where,
            };
        }

        return null;
    }

    /// <summary>Every owner declared in every fragment, with a file:line to report it against.</summary>
    private static IEnumerable<(OwnersFragment Fragment, string Owner, string Where)> EnumerateOwners(
        OwnersRepository repository)
    {
        foreach (var fragment in repository.Fragments)
        {
            foreach (var path in fragment.Paths)
            {
                foreach (var owner in path.Owners)
                {
                    yield return (fragment, owner, $"{fragment.FilePath}:{path.Line}");
                }
            }

            foreach (var labelOwner in fragment.LabelOwners)
            {
                foreach (var owner in labelOwner.ServiceOwners.Concat(labelOwner.AzureSdkOwners))
                {
                    yield return (fragment, owner, $"{fragment.FilePath}:{labelOwner.Line}");
                }
            }
        }
    }

    /// <summary>
    /// AUD-OWN-004 and AUD-OWN-005: too few individual owners to survive one person leaving. Counted
    /// against the rendered entries so a label-owner block is measured after the union, which is the
    /// number that actually gets paged.
    /// </summary>
    private static IEnumerable<AuditViolation> CheckOwnerCounts(
        OwnersRepository repository,
        CodeownersRenderResult rendered)
    {
        var settings = repository.Config.Configs;

        foreach (var entry in rendered.Entries.Where(e => e.IsFromFragment))
        {
            var isPathEntry = entry.Entry.PathExpression.Length > 0;
            var owners = Individuals(isPathEntry ? entry.Entry.SourceOwners : entry.Entry.ServiceOwners);
            var minimum = isPathEntry ? settings.MinimumPathOwners : settings.MinimumLabelOwners;

            if (owners.Count >= minimum)
            {
                continue;
            }

            var subject = isPathEntry
                ? $"Path '{entry.Entry.PathExpression}'"
                : $"Label set '{string.Join(", ", entry.Entry.ServiceLabels)}'";

            yield return new AuditViolation
            {
                RuleId = isPathEntry ? "AUD-OWN-004" : "AUD-OWN-005",
                Description = $"{subject} has {owners.Count} individual owner(s); at least {minimum} are required.",
                SourceFile = entry.DeclaredAt,
                Detail = $"Declared in: {string.Join(", ", entry.Sources)}",
            };
        }
    }

    // ------------------------------------------------------------- label rules

    /// <summary>AUD-LBL-001 (unknown label) and AUD-LBL-002 (Service Attention misuse).</summary>
    private IEnumerable<AuditViolation> CheckLabels(OwnersRepository repository, string repo)
    {
        var knownLabels = repoLabelCache.RepoLabelDict.TryGetValue(repo, out var labels) ? labels : null;
        if (knownLabels == null || knownLabels.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cached repository labels for '{repo}' came back empty. " +
                "Refusing to validate labels against an empty cache; run 'azsdk config codeowners update-cache' and retry.");
        }

        foreach (var fragment in repository.Fragments)
        {
            foreach (var path in fragment.Paths)
            {
                var where = $"{fragment.FilePath}:{path.Line}";

                foreach (var label in path.PrLabels)
                {
                    if (!knownLabels.Contains(label))
                    {
                        yield return UnknownLabel(label, repo, where);
                    }

                    if (label.Equals(ServiceAttentionLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new AuditViolation
                        {
                            RuleId = "AUD-LBL-002",
                            Description = $"'{ServiceAttentionLabel}' is used as a PR label on '{path.Path}'.",
                            SourceFile = where,
                        };
                    }
                }
            }

            foreach (var labelOwner in fragment.LabelOwners)
            {
                var where = $"{fragment.FilePath}:{labelOwner.Line}";

                foreach (var label in labelOwner.Labels.Where(l => !knownLabels.Contains(l)))
                {
                    yield return UnknownLabel(label, repo, where);
                }

                // Service Attention is a routing label, not an identity. On its own it names no
                // service, so the block owns everything and nothing.
                if (labelOwner.Labels.Count == 1
                    && labelOwner.Labels[0].Equals(ServiceAttentionLabel, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new AuditViolation
                    {
                        RuleId = "AUD-LBL-002",
                        Description = $"'{ServiceAttentionLabel}' is the only label on a label-owners block.",
                        SourceFile = where,
                    };
                }
            }
        }
    }

    private static AuditViolation UnknownLabel(string label, string repo, string where) => new()
    {
        RuleId = "AUD-LBL-001",
        Description = $"Label '{label}' does not exist in {repo}.",
        SourceFile = where,
    };

    // -------------------------------------------------------------- path rules

    /// <summary>
    /// AUD-PATH-001: an expression that matches nothing in the checkout. Usually a directory that was
    /// renamed or removed without the fragment following it, which leaves the entry owning nothing.
    /// </summary>
    private static IEnumerable<AuditViolation> CheckPaths(
        OwnersRepository repository,
        CodeownersRenderResult rendered)
    {
        foreach (var entry in rendered.Entries.Where(e => e.IsFromFragment && e.Entry.PathExpression.Length > 0))
        {
            if (MatchesSomethingOnDisk(repository.RepoRoot, entry.Entry.PathExpression))
            {
                continue;
            }

            yield return new AuditViolation
            {
                RuleId = "AUD-PATH-001",
                Description = $"Path '{entry.Entry.PathExpression}' matches nothing in the working tree.",
                SourceFile = entry.DeclaredAt,
            };
        }
    }

    private static bool MatchesSomethingOnDisk(string repoRoot, string expression)
    {
        var relative = expression.TrimStart('/').TrimEnd('/');
        if (relative.Length == 0)
        {
            return true;
        }

        var native = relative.Replace('/', Path.DirectorySeparatorChar);

        if (!relative.Contains('*'))
        {
            var full = Path.Combine(repoRoot, native);
            return File.Exists(full) || Directory.Exists(full);
        }

        // Only the final segment may be a glob in practice, so expanding its parent is enough and
        // avoids a full-tree walk per entry.
        var separator = native.LastIndexOf(Path.DirectorySeparatorChar);
        var parent = separator < 0 ? repoRoot : Path.Combine(repoRoot, native[..separator]);
        var pattern = separator < 0 ? native : native[(separator + 1)..];

        return Directory.Exists(parent)
            && Directory.EnumerateFileSystemEntries(parent, pattern, SearchOption.TopDirectoryOnly).Any();
    }

    /// <summary>
    /// AUD-ORD-001: a narrow entry rendered before an entry that contains it. CODEOWNERS is
    /// last-match-wins, so the broader entry below silently takes the narrow entry's paths and the
    /// narrow entry has no effect.
    /// <para>
    /// Only entries in the same section are compared. Across sections, ordering is a deliberate
    /// authoring decision -- guardrail sections are placed first precisely so later sections win --
    /// and comparing across them would flag every entry below the repo-wide catch-alls.
    /// </para>
    /// <para>
    /// Only literal expressions are compared. Deciding whether one glob contains another is the
    /// analysis this design deliberately avoids, and prefix comparison on concrete paths answers the
    /// case that actually occurs.
    /// </para>
    /// </summary>
    private static IEnumerable<AuditViolation> CheckOrdering(CodeownersRenderResult rendered)
    {
        var sections = rendered.Entries
            .Where(e => e.Entry.PathExpression.Length > 0 && !e.Entry.PathExpression.Contains('*'))
            .GroupBy(e => e.SectionName, StringComparer.OrdinalIgnoreCase);

        foreach (var section in sections)
        {
            var entries = section.ToList();

            for (var shadowed = 0; shadowed < entries.Count; shadowed++)
            {
                var narrow = entries[shadowed].Entry.PathExpression;

                for (var below = shadowed + 1; below < entries.Count; below++)
                {
                    var broad = entries[below].Entry.PathExpression;

                    if (!broad.EndsWith('/') || broad.Length >= narrow.Length
                        || !narrow.StartsWith(broad, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return new AuditViolation
                    {
                        RuleId = "AUD-ORD-001",
                        Description =
                            $"Path '{narrow}' renders before '{broad}', which contains it. " +
                            "Under last-match-wins the broader entry below it wins and this entry has no effect.",
                        SourceFile = entries[shadowed].DeclaredAt,
                        Detail = $"Broader entry declared at {entries[below].DeclaredAt}",
                    };
                }
            }
        }
    }

    // -------------------------------------------------------------------- fix

    /// <summary>
    /// Removes invalid owners from the fragments that declare them. The rendered CODEOWNERS is not
    /// touched: generate owns that file, and it will pick these edits up on its next run.
    /// </summary>
    private List<AuditFixResult> RemoveInvalidOwners(string repoRoot, List<InvalidOwner> invalid, bool force)
    {
        // An alias is reported once per place it appears but removed once per file, because the
        // editor clears every occurrence in a file in one pass.
        var removals = invalid
            .Where(o => o.FilePath.Length > 0)
            .DistinctBy(o => (o.FilePath, Alias: o.Alias.TrimStart('@').ToUpperInvariant()))
            .ToList();

        // The threshold counts people, not edits. One departure touching ten files is one departure;
        // ten departures in one file is the signal that the membership cache is wrong.
        var affectedAliases = removals
            .Select(o => o.Alias.TrimStart('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (affectedAliases > RemovalSafetyThreshold && !force)
        {
            throw new InvalidOperationException(
                $"{affectedAliases} owners would be removed, which is more than the safety threshold of " +
                $"{RemovalSafetyThreshold}. Review the reported violations and rerun with --force to apply them.");
        }

        var results = new List<AuditFixResult>();

        foreach (var owner in removals)
        {
            var file = Path.Combine(repoRoot, owner.FilePath.Replace('/', Path.DirectorySeparatorChar));
            var description = $"Remove owner '{owner.Alias}' from {owner.FilePath}";

            var edited = OwnersYamlEditor.RemoveOwner(File.ReadAllText(file), owner.FilePath, owner.Alias);
            if (edited == null)
            {
                results.Add(new AuditFixResult
                {
                    RuleId = owner.Violation.RuleId,
                    Description = description,
                    Success = false,
                    ErrorMessage = "The edit could not be applied without also changing something else in the file. Remove the owner by hand.",
                });

                continue;
            }

            File.WriteAllText(file, edited);
            logger.LogInformation("Removed owner '{Alias}' from {FilePath}.", owner.Alias, owner.FilePath);

            results.Add(new AuditFixResult
            {
                RuleId = owner.Violation.RuleId,
                Description = description,
                Success = true,
            });
        }

        return results;
    }

    // ----------------------------------------------------------------- shared

    private static List<string> Individuals(IEnumerable<string>? owners) =>
        (owners ?? [])
            .Select(o => o.TrimStart('@'))
            .Where(o => o.Length > 0 && !ParsingUtils.IsGitHubTeam(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task EnsureCachesAreFresh(CancellationToken ct)
    {
        var minimumLastModifiedUtc = DateTime.UtcNow.Subtract(CacheMaxAge);

        await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.TeamUserBlobUri, minimumLastModifiedUtc, ct);
        await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.UserOrgVisibilityBlobStorageURI, minimumLastModifiedUtc, ct);
        await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.RepoLabelBlobStorageURI, minimumLastModifiedUtc, ct);
    }
}
