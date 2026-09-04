// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Why an authored entry, or an owner on one, did not survive into the rendered file.
/// </summary>
/// <param name="Where">Source file and line the entry was declared on.</param>
/// <param name="Subject">The path expression, label set, or owner alias that was dropped.</param>
/// <param name="Reason">Human-readable explanation, suitable for a build log.</param>
/// <param name="RuleId">The rule that rejected it, so a reader can look it up.</param>
public sealed record DroppedItem(
    [property: JsonPropertyName("where")] string Where,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("rule_id")] string RuleId);

/// <summary>
/// The rendered CODEOWNERS file plus everything a caller needs to explain it.
/// </summary>
/// <param name="OutputPath">Repo-relative path the config nominates for the rendered file.</param>
/// <param name="Settings">The config's <c>configs</c> block, carried so callers need not reload it.</param>
/// <param name="Content">Rendered file text. Always populated.</param>
/// <param name="Entries">Every block in render order.</param>
/// <param name="Dropped">Entries and owners excluded from <paramref name="Content"/>, with reasons.</param>
public sealed record CodeownersModel(
    string OutputPath,
    OwnersConfigSettings Settings,
    string Content,
    IReadOnlyList<RenderedEntry> Entries,
    IReadOnlyList<DroppedItem> Dropped);

/// <summary>
/// Composes the pieces: load the YAML, drop what the caches reject, render.
/// </summary>
public class CodeownersModelBuilder(IOwnerValidator ownerValidator) : ICodeownersModelBuilder
{
    public async Task<CodeownersModel> Build(string repoRoot, bool omitFallbackSections, CancellationToken ct)
    {
        await ownerValidator.EnsureUsableAsync(ct);

        var dropped = new List<DroppedItem>();
        var loadErrors = new List<OwnersValidationError>();

        var repository = OwnersRepositoryLoader.Load(repoRoot, loadErrors);

        // Fragments are authored by service teams and decay as people move on, so they are filtered.
        // Config entries are maintained by repository maintainers and carried over from a CODEOWNERS
        // file GitHub already enforces; they render exactly as written.
        DropInvalidFragmentOwners(repository, dropped);

        if (omitFallbackSections)
        {
            OmitFallbackSections(repository);
        }

        var rendered = CodeownersRenderer.Render(repository);

        foreach (var error in loadErrors.Concat(rendered.Errors))
        {
            dropped.Add(new DroppedItem(string.Empty, string.Empty, error.Message, error.Code));
        }

        return new CodeownersModel(
            repository.Config.Configs.Output,
            repository.Config.Configs,
            rendered.Content,
            rendered.Entries,
            dropped);
    }

    /// <summary>
    /// Removes owners the membership caches reject, then removes any entry left with nobody.
    /// <para>
    /// An entry with no owners is not rendered as an ownerless path. In CODEOWNERS that would mean
    /// "nobody owns this", which stops the path from falling through to the broader match that would
    /// otherwise catch it — so a decayed service entry would silently disown its directory instead of
    /// deferring to the repository backstop.
    /// </para>
    /// </summary>
    private void DropInvalidFragmentOwners(OwnersRepository repository, List<DroppedItem> dropped)
    {
        foreach (var fragment in repository.Fragments)
        {
            foreach (var path in fragment.Paths)
            {
                path.Owners = KeepValid(path.Owners, $"{fragment.FilePath}:{path.Line}", dropped);
            }

            foreach (var block in fragment.LabelOwners)
            {
                var where = $"{fragment.FilePath}:{block.Line}";
                block.ServiceOwners = KeepValid(block.ServiceOwners, where, dropped);
                block.AzureSdkOwners = KeepValid(block.AzureSdkOwners, where, dropped);
            }

            fragment.Paths = DropEmptied(
                fragment.Paths,
                entry => entry.Owners.Count == 0,
                entry => new DroppedItem(
                    $"{fragment.FilePath}:{entry.Line}",
                    entry.Path,
                    "Every owner on this path was rejected by the membership cache, so the path is not "
                        + "rendered and falls through to the next broader match.",
                    "GEN-DROP-001"),
                dropped);

            fragment.LabelOwners = DropEmptied(
                fragment.LabelOwners,
                entry => entry.ServiceOwners.Count == 0 && entry.AzureSdkOwners.Count == 0,
                entry => new DroppedItem(
                    $"{fragment.FilePath}:{entry.Line}",
                    string.Join(", ", entry.Labels),
                    "Every owner of this label set was rejected by the membership cache, so the block is "
                        + "not rendered.",
                    "GEN-DROP-002"),
                dropped);
        }
    }

    private List<string> KeepValid(List<string> owners, string where, List<DroppedItem> dropped)
    {
        var kept = new List<string>(owners.Count);

        foreach (var owner in owners)
        {
            var violation = ownerValidator.Validate(owner, where);
            if (violation == null)
            {
                kept.Add(owner);
            }
            else
            {
                dropped.Add(new DroppedItem(where, owner, violation.Detail ?? violation.Description, violation.RuleId));
            }
        }

        return kept;
    }

    private static List<T> DropEmptied<T>(
        List<T> entries,
        Func<T, bool> isEmpty,
        Func<T, DroppedItem> describe,
        List<DroppedItem> dropped)
    {
        dropped.AddRange(entries.Where(isEmpty).Select(describe));

        return [.. entries.Where(entry => !isEmpty(entry))];
    }

    /// <summary>
    /// Drops the repo-wide guardrail sections. Rendering keeps them — GitHub needs the backstop — but
    /// ownership resolution steps over them so a package with no ownership of its own reports as
    /// unowned rather than resolving to <c>/sdk/ @Azure/azure-sdk-write</c> and passing.
    /// </summary>
    private static void OmitFallbackSections(OwnersRepository repository)
    {
        var kept = repository.Config.Sections.Where(section => !section.ExcludeFromCheckPackage).ToList();
        var keptNames = kept.Select(section => section.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        repository.Config.Sections = kept;

        // A fragment can route itself into an excluded section, so filter the fragments too rather
        // than relying on the section list alone.
        foreach (var fragment in repository.Fragments)
        {
            fragment.Paths = [.. fragment.Paths.Where(entry => IsKept(entry.Section, fragment.Section))];
            fragment.LabelOwners = [.. fragment.LabelOwners.Where(entry => IsKept(entry.Section, fragment.Section))];
        }

        bool IsKept(string? entrySection, string? fragmentSection)
        {
            var target = entrySection ?? fragmentSection;

            // No explicit target means the config's default section, which the loop above already
            // kept or dropped by name.
            return target == null
                ? keptNames.Contains(repository.Config.Configs.DefaultSection)
                : keptNames.Contains(target);
        }
    }
}
