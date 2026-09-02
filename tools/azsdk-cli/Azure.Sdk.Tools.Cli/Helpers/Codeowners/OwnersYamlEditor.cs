// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Removes an owner alias from an ownership YAML file without disturbing anything else in it.
/// <para>
/// The edit is textual rather than a YamlDotNet round-trip because these files are hand-maintained
/// and their comments are the reason authors can read them. Every edit is then verified by reloading
/// the result and comparing it against the same removal applied to the parsed model; if the two
/// disagree the edit is discarded. That is what makes a text edit safe enough to run unattended.
/// </para>
/// </summary>
public static partial class OwnersYamlEditor
{
    /// <summary>
    /// Returns the file content with <paramref name="alias"/> removed from every owner list, or null
    /// if the alias is absent or the edit could not be verified.
    /// </summary>
    public static string? RemoveOwner(string yaml, string filePath, string alias)
    {
        var edited = RemoveAliasFromOwnerLines(yaml, alias);
        if (edited == null)
        {
            return null;
        }

        try
        {
            var expected = OwnersYamlLoader.LoadFragment(yaml, filePath);
            RemoveOwnerFromModel(expected, alias);

            var actual = OwnersYamlLoader.LoadFragment(edited, filePath);

            return Summarize(actual) == Summarize(expected) ? edited : null;
        }
        catch (OwnersYamlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Walks the file tracking which lines belong to an owner list, and removes the alias from those
    /// lines only. Trailing comments are excluded from the search so an alias mentioned in prose is
    /// never touched.
    /// </summary>
    private static string? RemoveAliasFromOwnerLines(string yaml, string alias)
    {
        var lines = yaml.Split('\n');
        var output = new List<string>(lines.Length);
        var changed = false;
        var inOwnerBlock = false;

        foreach (var line in lines)
        {
            var (code, comment) = SplitTrailingComment(line);
            var ownerKey = OwnerKeyLine().Match(code);

            if (ownerKey.Success)
            {
                // A key with an inline value is self-contained; a bare key opens a block sequence.
                inOwnerBlock = ownerKey.Groups["value"].Value.Trim().Length == 0;
            }
            else if (inOwnerBlock && code.Trim().Length > 0 && !OwnerListItem().IsMatch(code))
            {
                inOwnerBlock = false;
            }

            if (!ownerKey.Success && !inOwnerBlock)
            {
                output.Add(line);
                continue;
            }

            var stripped = StripAlias(code, alias);
            if (stripped == code)
            {
                output.Add(line);
                continue;
            }

            changed = true;

            // A list item that held only this alias leaves nothing behind; drop the line rather
            // than leave a dangling "-".
            if (stripped.Trim() == "-" && comment.Length == 0)
            {
                continue;
            }

            output.Add(stripped + comment);
        }

        return changed ? string.Join("\n", output) : null;
    }

    /// <summary>Removes the alias as a whole token, tolerating a leading '@' and list punctuation.</summary>
    private static string StripAlias(string code, string alias)
    {
        var pattern = $@"@?{Regex.Escape(alias)}\b";

        // Inside a flow sequence the separator has to go with the item, and either neighbour may
        // supply it: "[a, b]" -> "[a]" whether b or a is removed.
        var withSeparator = new Regex($@"(,\s*{pattern})|({pattern}\s*,\s*)|({pattern})");

        return withSeparator.Replace(code, string.Empty, 1);
    }

    private static (string Code, string Comment) SplitTrailingComment(string line)
    {
        var index = line.IndexOf(" #", StringComparison.Ordinal);
        return index < 0 ? (line, string.Empty) : (line[..index], line[index..]);
    }

    private static void RemoveOwnerFromModel(OwnersFragment fragment, string alias)
    {
        bool Matches(string owner) => owner.TrimStart('@').Equals(alias, StringComparison.OrdinalIgnoreCase);

        foreach (var path in fragment.Paths)
        {
            path.Owners.RemoveAll(Matches);
        }

        foreach (var labelOwner in fragment.LabelOwners)
        {
            labelOwner.ServiceOwners.RemoveAll(Matches);
            labelOwner.AzureSdkOwners.RemoveAll(Matches);
        }
    }

    /// <summary>
    /// A canonical rendering of everything the schema carries, used to prove a textual edit changed
    /// only what it was supposed to.
    /// </summary>
    private static string Summarize(OwnersFragment fragment)
    {
        var builder = new StringBuilder();
        builder.Append(fragment.Version).Append('|').Append(fragment.Section).Append('\n');

        foreach (var path in fragment.Paths)
        {
            builder.Append(path.Path).Append('|')
                .AppendJoin(',', path.Owners).Append('|')
                .AppendJoin(',', path.PrLabels).Append('|')
                .Append(path.Section).Append('\n');
        }

        foreach (var labelOwner in fragment.LabelOwners)
        {
            builder.AppendJoin(',', labelOwner.Labels).Append('|')
                .AppendJoin(',', labelOwner.ServiceOwners).Append('|')
                .AppendJoin(',', labelOwner.AzureSdkOwners).Append('|')
                .Append(labelOwner.Section).Append('\n');
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^\s*(owners|service-owners|azure-sdk-owners)\s*:(?<value>.*)$")]
    private static partial Regex OwnerKeyLine();

    /// <summary>
    /// A block-sequence item holding a bare alias. The absence of a colon is what distinguishes it
    /// from the start of a neighbouring mapping such as <c>- path: Foo/</c>.
    /// </summary>
    [GeneratedRegex(@"^\s*-\s*@?[\w./-]+\s*$")]
    private static partial Regex OwnerListItem();
}
