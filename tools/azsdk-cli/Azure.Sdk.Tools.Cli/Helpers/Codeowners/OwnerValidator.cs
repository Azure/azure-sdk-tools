// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Decides whether a declared owner is someone GitHub will actually route reviews to.
/// <para>
/// Both lint and check-package ask this question, so it lives in one place. An individual qualifies
/// by being in <c>azure-sdk-write</c> and publicly visible in the Azure org; a team qualifies by
/// descending from <c>azure-sdk-write</c>, which is what grants write access in the first place.
/// </para>
/// </summary>
public interface IOwnerValidator
{
    /// <summary>Throws if the caches backing the decision are stale or empty.</summary>
    Task EnsureUsableAsync(CancellationToken ct);

    /// <summary>Returns null when <paramref name="owner"/> is a valid code owner.</summary>
    LintViolation? Validate(string owner, string? where);

    /// <summary>
    /// Resolves a list of declared owners to the distinct individuals GitHub would notify, replacing
    /// each team with its cached membership. A team with no cached membership contributes nobody.
    /// </summary>
    IReadOnlyList<string> ExpandToIndividuals(IEnumerable<string> owners);
}

public class OwnerValidator(
    ITeamUserCache teamUserCache,
    UserOrgVisibilityCache userOrgVisibilityCache,
    ICacheValidator cacheValidator) : IOwnerValidator
{
    /// <summary>How stale cached membership may be before we refuse to judge owners against it.</summary>
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(6);

    public const string AzureSdkWriteTeam = "Azure/azure-sdk-write";

    /// <summary>
    /// Several commands ask for the caches in one execution and the freshness check is a network
    /// round trip, so the result is kept rather than re-fetched. Failures are not memoized: a caller
    /// that retries after fixing the cache gets a real answer.
    /// </summary>
    private Task? ensureUsable;

    public Task EnsureUsableAsync(CancellationToken ct) => ensureUsable ??= EnsureUsableCoreAsync(ct);

    private async Task EnsureUsableCoreAsync(CancellationToken ct)
    {
        try
        {
            var minimumLastModifiedUtc = DateTime.UtcNow.Subtract(CacheMaxAge);

            await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.TeamUserBlobUri, minimumLastModifiedUtc, ct);
            await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.UserOrgVisibilityBlobStorageURI, minimumLastModifiedUtc, ct);

            // Fail closed. An empty cache would report every owner in the repository as invalid.
            if (teamUserCache.GetUsersForTeam(AzureSdkWriteTeam).Count == 0
                || userOrgVisibilityCache.UserOrgVisibilityDict.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Cached membership for '{AzureSdkWriteTeam}' or Azure org visibility came back empty. " +
                    "Refusing to validate owners against an empty cache; run 'azsdk config codeowners update-cache' and retry.");
            }
        }
        catch
        {
            ensureUsable = null;
            throw;
        }
    }

    public IReadOnlyList<string> ExpandToIndividuals(IEnumerable<string> owners)
    {
        var individuals = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var owner in owners ?? [])
        {
            var alias = owner?.TrimStart('@') ?? string.Empty;
            if (alias.Length == 0)
            {
                continue;
            }

            var resolved = ParsingUtils.IsGitHubTeam(alias)
                ? teamUserCache.GetUsersForTeam(alias)
                : (IReadOnlyCollection<string>)[alias];

            foreach (var individual in resolved)
            {
                if (seen.Add(individual))
                {
                    individuals.Add(individual);
                }
            }
        }

        return individuals;
    }

    public LintViolation? Validate(string owner, string? where)
    {
        var alias = owner.TrimStart('@');

        return ParsingUtils.IsGitHubTeam(alias)
            ? ValidateTeam(alias, where)
            : ValidateIndividual(alias, where);
    }

    private LintViolation? ValidateTeam(string alias, string? where)
    {
        var parts = alias.Split('/');
        if (parts.Length != 2 || !parts[0].Equals("Azure", StringComparison.OrdinalIgnoreCase) || parts[1].Length == 0)
        {
            return new LintViolation
            {
                RuleId = "LNT-OWN-002",
                Description = $"Team owner '{alias}' is malformed; expected Azure/<team>.",
                SourceFile = where,
            };
        }

        // A team with no cached membership is not a descendant of azure-sdk-write.
        if (teamUserCache.GetUsersForTeam(alias).Count == 0)
        {
            return new LintViolation
            {
                RuleId = "LNT-OWN-002",
                Description = $"Team '{alias}' does not descend from {AzureSdkWriteTeam}.",
                SourceFile = where,
                Detail = $"Only teams under {AzureSdkWriteTeam} have the write access GitHub requires of a code owner.",
            };
        }

        return null;
    }

    private LintViolation? ValidateIndividual(string alias, string? where)
    {
        var hasWriteAccess = teamUserCache
            .GetUsersForTeam(AzureSdkWriteTeam)
            .Contains(alias, StringComparer.OrdinalIgnoreCase);

        var isInAzureOrg = userOrgVisibilityCache.UserOrgVisibilityDict.TryGetValue(alias, out var visible) && visible;

        if (hasWriteAccess && isInAzureOrg)
        {
            return null;
        }

        return new LintViolation
        {
            RuleId = "LNT-OWN-001",
            Description = $"'{alias}' is not a valid code owner.",
            SourceFile = where,
            Detail = DescribeFailure(alias, hasWriteAccess, isInAzureOrg),
        };
    }

    /// <summary>
    /// The two halves fail for unrelated reasons and are fixed by different people, so name which one
    /// failed rather than reporting "invalid owner" and leaving the reader to guess.
    /// </summary>
    private static string DescribeFailure(string alias, bool hasWriteAccess, bool isInAzureOrg)
    {
        if (!hasWriteAccess && !isInAzureOrg)
        {
            return $"'{alias}' is not in {AzureSdkWriteTeam} and their Azure org membership is not public. " +
                   "Both are required.";
        }

        return hasWriteAccess
            ? $"'{alias}' has write access, but their Azure org membership is private. They can fix this " +
              "themselves at https://github.com/orgs/Azure/people by setting their visibility to Public."
            : $"'{alias}' is publicly visible in the Azure org but is not a member of {AzureSdkWriteTeam}, " +
              "so GitHub will not request reviews from them.";
    }
}
