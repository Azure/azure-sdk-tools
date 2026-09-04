// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>Owner validators for tests that are not about owner validity.</summary>
internal static class OwnerValidatorFake
{
    /// <summary>
    /// Builds a real <see cref="OwnerValidator"/> over in-memory caches, so tests that are about
    /// owner validity exercise the production rules rather than a reimplementation of them.
    /// </summary>
    public static IOwnerValidator Create(
        IEnumerable<string> writeTeamMembers,
        IDictionary<string, List<string>>? teams = null,
        IEnumerable<string>? orgVisible = null)
    {
        var members = writeTeamMembers.ToList();

        var teamUsers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Azure/azure-sdk-write"] = members,
        };

        foreach (var team in teams ?? new Dictionary<string, List<string>>())
        {
            teamUsers[team.Key] = team.Value;
        }

        var visibility = (orgVisible ?? members).ToDictionary(o => o, _ => true, StringComparer.OrdinalIgnoreCase);

        return new OwnerValidator(
            new FakeTeamUserCache(teamUsers),
            new UserOrgVisibilityCache { UserOrgVisibilityDict = visibility },
            new FakeCacheValidator());
    }

    /// <summary>For tests whose subject is something other than who the owners are.</summary>
    public static IOwnerValidator AcceptAll() => new Stub([]);

    public static IOwnerValidator Rejecting(params string[] aliases) => new Stub(aliases);

    private sealed class Stub(IEnumerable<string> rejected) : IOwnerValidator
    {
        private readonly HashSet<string> rejected = rejected.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task EnsureUsableAsync(CancellationToken ct) => Task.CompletedTask;

        public LintViolation? Validate(string owner, string? where) =>
            this.rejected.Contains(owner.TrimStart('@'))
                ? new LintViolation
                {
                    RuleId = "LNT-OWN-001",
                    Description = $"'{owner}' is not a valid code owner.",
                    SourceFile = where,
                    Detail = "Rejected by test stub.",
                }
                : null;

        /// <summary>
        /// No team data, so an individual stands for itself and a team resolves to nobody — which is
        /// what the real validator does for a team it has no cached membership for.
        /// </summary>
        public IReadOnlyList<string> ExpandToIndividuals(IEnumerable<string> owners) =>
        [
            .. (owners ?? [])
                .Select(o => o.TrimStart('@'))
                .Where(o => o.Length > 0 && !ParsingUtils.IsGitHubTeam(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private sealed class FakeTeamUserCache(Dictionary<string, List<string>> teams) : ITeamUserCache
    {
        public Dictionary<string, List<string>> TeamUserDict { get; set; } = teams;

        public List<string> GetUsersForTeam(string teamName) =>
            TeamUserDict.TryGetValue(teamName, out var users) ? users : [];
    }

    /// <summary>
    /// A builder for tests that call <c>Evaluate</c> directly with hand-built entries and never
    /// reach the loading or rendering path.
    /// </summary>
    public static ICodeownersModelBuilder UnusedModelBuilder() => new UnusedBuilder();

    private sealed class UnusedBuilder : ICodeownersModelBuilder
    {
        public Task<CodeownersModel> Build(string repoRoot, bool omitFallbackSections, CancellationToken ct) =>
            throw new NotSupportedException("This test supplies entries directly and must not build a model.");
    }

    private sealed class FakeCacheValidator : ICacheValidator
    {
        public Task ThrowIfCacheOlderThan(string cacheSource, DateTime minimumLastModifiedUtc, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
