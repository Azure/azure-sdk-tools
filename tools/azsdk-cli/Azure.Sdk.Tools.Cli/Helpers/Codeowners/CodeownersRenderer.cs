// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Renders a <see cref="OwnersRepository"/> into CODEOWNERS text. Deterministic: the same inputs
/// always produce a byte-identical file.
/// <para>
/// The renderer touches no files except to answer the trailing-slash question in
/// <see cref="OwnersPathResolver"/>. It returns every <c>CFG-*</c> error it finds rather than
/// stopping at the first, so one run tells an author everything they have to fix.
/// </para>
/// </summary>
public static class CodeownersRenderer
{
    /// <summary>Lines are joined with '\n' to match <c>CodeownersEntry.FormatCodeownersEntry</c>.</summary>
    private const string LineEnding = "\n";

    private const int MinimumBannerWidth = 20;

    private static readonly string[] GeneratedFileBanner =
    [
        "# ------------------------------------------------------------------------------",
        "# GENERATED FILE - DO NOT EDIT",
        "# Ownership is defined in .github/owners.config.yaml and sdk/*/owners.yaml",
        "# Regenerate with: azsdk config codeowners generate",
        "# ------------------------------------------------------------------------------",
    ];

    public static CodeownersRenderResult Render(OwnersRepository repository)
    {
        var errors = new List<OwnersValidationError>();
        var entries = BindEntries(repository, errors);

        ValidateNoDuplicatePaths(entries, errors);
        ValidateNoDuplicateLabelSets(entries, errors);

        if (errors.Count > 0)
        {
            return new CodeownersRenderResult(string.Empty, [], errors);
        }

        var ordered = OrderEntries(repository.Config, entries);
        return new CodeownersRenderResult(Emit(repository.Config, ordered), ordered, errors);
    }

    // ---------------------------------------------------------------- binding

    /// <summary>
    /// Turns every authored entry into a <see cref="RenderedEntry"/> bound to its target section,
    /// unioning the fragments' label-owner blocks along the way.
    /// </summary>
    private static List<RenderedEntry> BindEntries(OwnersRepository repository, List<OwnersValidationError> errors)
    {
        var sectionsByName = BuildSectionIndex(repository.Config, errors);

        var entries = new List<RenderedEntry>();
        entries.AddRange(BindStaticEntries(repository.Config, errors));
        entries.AddRange(BindFragmentPaths(repository, sectionsByName, errors));
        entries.AddRange(BindUnionedLabelOwners(repository, sectionsByName, errors));

        return entries;
    }

    private static Dictionary<string, OwnersSection> BuildSectionIndex(
        OwnersConfig config,
        List<OwnersValidationError> errors)
    {
        var index = new Dictionary<string, OwnersSection>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in config.Sections)
        {
            if (!index.TryAdd(section.Name, section))
            {
                errors.Add(new OwnersValidationError("CFG-SEC-001",
                    $".github/owners.config.yaml:{section.Line}: section '{section.Name}' is declared more than once."));
            }
        }

        if (!index.ContainsKey(config.Configs.DefaultSection))
        {
            errors.Add(new OwnersValidationError("CFG-SEC-001",
                $".github/owners.config.yaml: configs.default-section '{config.Configs.DefaultSection}' names no section."));
        }

        return index;
    }

    private static IEnumerable<RenderedEntry> BindStaticEntries(OwnersConfig config, List<OwnersValidationError> errors)
    {
        foreach (var section in config.Sections)
        {
            foreach (var pathEntry in section.Paths)
            {
                RejectSectionOverride(pathEntry.Section, section, pathEntry.Line, errors);

                var expression = OwnersPathResolver.ResolveConfigPath(section, pathEntry, errors);
                if (expression == null)
                {
                    continue;
                }

                yield return new RenderedEntry
                {
                    Entry = pathEntry.ToCodeownersEntry(expression),
                    SectionName = section.Name,
                    DeclaredAt = $".github/owners.config.yaml:{pathEntry.Line}",
                };
            }

            // Static label-owner entries are never merged, even when two declare the same label set:
            // they are carried over from a hand-written CODEOWNERS whose owner lists must render as
            // they were written.
            foreach (var labelEntry in section.LabelOwners)
            {
                RejectSectionOverride(labelEntry.Section, section, labelEntry.Line, errors);

                yield return new RenderedEntry
                {
                    Entry = labelEntry.ToCodeownersEntry(),
                    SectionName = section.Name,
                    DeclaredAt = $".github/owners.config.yaml:{labelEntry.Line}",
                };
            }
        }
    }

    private static void RejectSectionOverride(
        string? overrideName,
        OwnersSection section,
        int line,
        List<OwnersValidationError> errors)
    {
        if (overrideName != null)
        {
            errors.Add(new OwnersValidationError("CFG-SEC-001",
                $".github/owners.config.yaml:{line}: entry declares section '{overrideName}' but is already " +
                $"declared under '{section.Name}'. Section overrides are for fragments; move the entry instead."));
        }
    }

    private static IEnumerable<RenderedEntry> BindFragmentPaths(
        OwnersRepository repository,
        Dictionary<string, OwnersSection> sectionsByName,
        List<OwnersValidationError> errors)
    {
        foreach (var fragment in repository.Fragments)
        {
            foreach (var pathEntry in fragment.Paths)
            {
                var declaredAt = $"{fragment.FilePath}:{pathEntry.Line}";

                if (pathEntry.PrLabels.Count == 0)
                {
                    errors.Add(new OwnersValidationError("CFG-LBL-001",
                        $"{declaredAt}: path '{pathEntry.Path}' has no pr-labels. Every fragment path entry must " +
                        "carry at least one PR label, or the change lands outside label-driven triage."));
                }

                var expression = OwnersPathResolver.ResolveFragmentPath(fragment, pathEntry, repository.RepoRoot, errors);
                var section = ResolveFragmentSection(
                    repository.Config, sectionsByName, fragment, pathEntry.Section, declaredAt, errors);

                if (expression == null || section == null)
                {
                    continue;
                }

                yield return new RenderedEntry
                {
                    Entry = pathEntry.ToCodeownersEntry(expression),
                    SectionName = section.Name,
                    Sources = [fragment.FilePath],
                    DeclaredAt = declaredAt,
                };
            }
        }
    }

    /// <summary>
    /// Groups the fragments' label-owner entries by label set and unions their owners. This is the
    /// feature fragments exist to provide: two service teams can each claim a share of a shared
    /// triage label without coordinating an edit to a single shared line.
    /// </summary>
    private static IEnumerable<RenderedEntry> BindUnionedLabelOwners(
        OwnersRepository repository,
        Dictionary<string, OwnersSection> sectionsByName,
        List<OwnersValidationError> errors)
    {
        // Fragments arrive in provenance order, so grouping preserves it and the first contributor
        // of each group is the one whose labels, casing, and section win.
        var contributions =
            from fragment in repository.Fragments
            from labelEntry in fragment.LabelOwners
            select (fragment, labelEntry);

        foreach (var group in contributions.GroupBy(c => LabelSetKey(c.labelEntry.Labels), StringComparer.Ordinal))
        {
            var first = group.First();
            var declaredAt = $"{first.fragment.FilePath}:{first.labelEntry.Line}";

            var section = ResolveFragmentSection(
                repository.Config, sectionsByName, first.fragment, first.labelEntry.Section, declaredAt, errors);
            if (section == null)
            {
                continue;
            }

            var unioned = new OwnersLabelOwnerEntry
            {
                Labels = first.labelEntry.Labels,
                ServiceOwners = UnionPreservingOrder(group.Select(c => c.labelEntry.ServiceOwners)),
                AzureSdkOwners = UnionPreservingOrder(group.Select(c => c.labelEntry.AzureSdkOwners)),
            };

            yield return new RenderedEntry
            {
                Entry = unioned.ToCodeownersEntry(),
                SectionName = section.Name,
                Sources = [.. group.Select(c => c.fragment.FilePath).Distinct(StringComparer.Ordinal)],
                DeclaredAt = declaredAt,
            };
        }
    }

    private static OwnersSection? ResolveFragmentSection(
        OwnersConfig config,
        Dictionary<string, OwnersSection> sectionsByName,
        OwnersFragment fragment,
        string? entrySection,
        string declaredAt,
        List<OwnersValidationError> errors)
    {
        var name = entrySection ?? fragment.Section ?? config.Configs.DefaultSection;

        if (!sectionsByName.TryGetValue(name, out var section))
        {
            errors.Add(new OwnersValidationError("CFG-SEC-001",
                $"{declaredAt}: section '{name}' does not exist in .github/owners.config.yaml."));
            return null;
        }

        if (!section.DefinedInFiles)
        {
            errors.Add(new OwnersValidationError("CFG-SEC-001",
                $"{declaredAt}: section '{name}' does not accept fragment entries. " +
                "Set 'defined-in-files: true' on it, or route the entry to a section that has it."));
            return null;
        }

        return section;
    }

    /// <summary>
    /// Order-insensitive, case-insensitive identity of a label set. Sets differing by even one label
    /// are distinct blocks, because a ServiceLabel block's label set is what triage matches on.
    /// </summary>
    private static string LabelSetKey(IEnumerable<string> labels) =>
        string.Join("\u0000", labels
            .Select(CodeownersEntrySorter.NormalizeLabel)
            .Select(l => l.ToUpperInvariant())
            .Order(StringComparer.Ordinal));

    /// <summary>Concatenates in the given order and keeps the first spelling of each owner.</summary>
    private static List<string> UnionPreservingOrder(IEnumerable<List<string>> ownerLists)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. ownerLists.SelectMany(o => o).Where(seen.Add)];
    }

    // ------------------------------------------------------------- validation

    /// <summary>
    /// Ownership must be declared in exactly one place. Path expressions compare with
    /// <see cref="StringComparer.Ordinal"/> because GitHub evaluates CODEOWNERS case-sensitively, so
    /// <c>/sdk/Tables/</c> and <c>/sdk/tables/</c> are genuinely different expressions.
    /// </summary>
    private static void ValidateNoDuplicatePaths(List<RenderedEntry> entries, List<OwnersValidationError> errors)
    {
        var duplicates = entries
            .Where(e => e.Entry.PathExpression.Length > 0)
            .GroupBy(e => e.Entry.PathExpression, StringComparer.Ordinal)
            .Where(g => g.Count() > 1);

        foreach (var group in duplicates)
        {
            var fragmentCount = group.Count(e => e.IsFromFragment);
            var code = fragmentCount switch
            {
                0 => "CFG-DUP-003",              // two static entries
                var n when n == group.Count() => "CFG-DUP-002",   // two fragments
                _ => "CFG-DUP-001",              // config and fragment
            };

            errors.Add(new OwnersValidationError(code, DescribeDuplicate(
                $"Path '{group.Key}' is defined in more than one place.", group)));
        }
    }

    /// <summary>
    /// A label set declared in the owners config may not also be declared by a fragment; union is the
    /// sanctioned way for two owners to share a label, and it only applies to fragments.
    /// </summary>
    private static void ValidateNoDuplicateLabelSets(List<RenderedEntry> entries, List<OwnersValidationError> errors)
    {
        var labelBlocks = entries.Where(e => e.Entry.PathExpression.Length == 0);

        foreach (var group in labelBlocks.GroupBy(e => LabelSetKey(e.Entry.ServiceLabels), StringComparer.Ordinal))
        {
            if (group.Any(e => e.IsFromFragment) && group.Any(e => !e.IsFromFragment))
            {
                var labels = string.Join(", ", group.First().Entry.ServiceLabels);
                errors.Add(new OwnersValidationError("CFG-DUP-004", DescribeDuplicate(
                    $"Label set '{labels}' is declared both statically and by a fragment.", group)));
            }
        }
    }

    private static string DescribeDuplicate(string summary, IEnumerable<RenderedEntry> group)
    {
        var builder = new StringBuilder(summary);
        foreach (var entry in group)
        {
            builder.Append(LineEnding).Append("  ").Append(entry.DeclaredAt)
                .Append(" (section '").Append(entry.SectionName).Append("')");
        }

        return builder.ToString();
    }

    // ---------------------------------------------------------------- ordering

    /// <summary>
    /// Sections render in declaration order. What happens inside a section is controlled by
    /// <c>sections[].sort</c> and by nothing else — static and fragment entries are one set by this
    /// point, and provenance affects only the <c># Sources:</c> comment.
    /// </summary>
    private static List<RenderedEntry> OrderEntries(OwnersConfig config, List<RenderedEntry> entries)
    {
        var bySection = entries.ToLookup(e => e.SectionName, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<RenderedEntry>(entries.Count);

        foreach (var section in config.Sections)
        {
            var sectionEntries = bySection[section.Name].ToList();
            ordered.AddRange(section.Sort ? SortWithEntrySorter(sectionEntries) : sectionEntries);
        }

        return ordered;
    }

    /// <summary>
    /// Applies the repository's existing entry sort. Reusing it verbatim is what keeps the generated
    /// file consistent with the hand-maintained files it replaces.
    /// </summary>
    private static List<RenderedEntry> SortWithEntrySorter(List<RenderedEntry> entries)
    {
        var owner = new Dictionary<CodeownersEntry, RenderedEntry>(ReferenceEqualityComparer.Instance);
        foreach (var entry in entries)
        {
            owner[entry.Entry] = entry;
        }

        return [.. CodeownersEntrySorter.SortEntries([.. entries.Select(e => e.Entry)]).Select(e => owner[e])];
    }

    // ------------------------------------------------------------------- emit

    private static string Emit(OwnersConfig config, List<RenderedEntry> entries)
    {
        var bySection = entries.ToLookup(e => e.SectionName, StringComparer.OrdinalIgnoreCase);

        var blocks = new List<string> { string.Join(LineEnding, GeneratedFileBanner) };

        foreach (var section in config.Sections)
        {
            blocks.Add(FormatSectionBanner(section.Name));
            blocks.AddRange(bySection[section.Name].Select(FormatEntry));
        }

        return string.Join(LineEnding + LineEnding, blocks) + LineEnding;
    }

    private static string FormatEntry(RenderedEntry entry)
    {
        var formatted = entry.Entry.FormatCodeownersEntry();

        // Only unioned label-owner blocks need provenance; a path entry has exactly one author and
        // its expression already names the directory the fragment lives in.
        var needsSources = entry.IsFromFragment && entry.Entry.PathExpression.Length == 0;

        return needsSources
            ? $"# Sources: {string.Join(", ", entry.Sources)}{LineEnding}{formatted}"
            : formatted;
    }

    /// <summary>
    /// A '#' rule wide enough to satisfy <c>CodeownersSectionFinder.IsSectionBorder</c>, and never
    /// narrower than the 20 characters the hand-written files use.
    /// </summary>
    private static string FormatSectionBanner(string name)
    {
        var title = $"# {name}";
        var rule = new string('#', Math.Max(MinimumBannerWidth, title.Length));

        return string.Join(LineEnding, rule, title, rule);
    }
}

/// <param name="Content">Rendered file text, empty when <paramref name="Errors"/> is non-empty.</param>
/// <param name="Entries">Every block in render order. The audit reads ordering rules off this.</param>
/// <param name="Errors">Every <c>CFG-*</c> violation found, not just the first.</param>
public sealed record CodeownersRenderResult(
    string Content,
    IReadOnlyList<RenderedEntry> Entries,
    IReadOnlyList<OwnersValidationError> Errors);
