// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

internal static class RepairProgress
{
    public static int CountReversedHunks(string candidateDiff, IEnumerable<string> previousDiffs)
    {
        var previous = previousDiffs.SelectMany(GetHunks).ToHashSet();
        return GetHunks(candidateDiff).Count(hunk => previous.Contains((hunk.File, hunk.Added, hunk.Removed)));
    }

    private static IEnumerable<(string File, string Removed, string Added)> GetHunks(string diff)
    {
        string file = "";
        var removed = new StringBuilder();
        var added = new StringBuilder();
        foreach (var line in diff.Replace("\r\n", "\n").Split('\n').Append("diff --git end"))
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal) || line.StartsWith("@@", StringComparison.Ordinal))
            {
                if (removed.Length > 0 || added.Length > 0)
                {
                    yield return (file, removed.ToString(), added.ToString());
                    removed.Clear();
                    added.Clear();
                }
                if (line.StartsWith("diff --git ", StringComparison.Ordinal)) { file = line; }
            }
            else if (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal))
            {
                removed.AppendLine(line[1..]);
            }
            else if (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
            {
                added.AppendLine(line[1..]);
            }
        }
    }
}
