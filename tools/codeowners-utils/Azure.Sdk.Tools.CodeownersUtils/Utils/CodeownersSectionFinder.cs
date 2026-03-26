using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils;

/// <summary>
/// Utilities for finding sections in CODEOWNERS files.
/// </summary>
public static class CodeownersSectionFinder
{
    /// <summary>
    /// Finds a named section in a CODEOWNERS file.
    /// </summary>
    /// <returns>Tuple of (headerStart, contentStart, sectionEnd) line indices, or (-1, -1, -1) if not found.</returns>
    public static (int headerStart, int contentStart, int sectionEnd) FindSection(List<string> lines, string sectionName)
    {
        for (int i = 0; i < lines.Count - 2; i++)
        {
            if (IsSectionBorder(lines[i]) &&
                lines[i + 1].Trim() == $"# {sectionName}" &&
                IsSectionBorder(lines[i + 2]))
            {
                int headerStart = i;
                int contentStart = i + 3;
                int sectionEnd = FindNextSectionStart(lines, contentStart);
                return (headerStart, contentStart, sectionEnd);
            }
        }
        return (-1, -1, -1);
    }

    /// <summary>
    /// Finds where the next section starts (or end of file).
    /// </summary>
    public static int FindNextSectionStart(List<string> lines, int startFrom)
    {
        for (int i = startFrom; i < lines.Count; i++)
        {
            if (IsSectionBorder(lines[i]))
                return i;
        }
        return lines.Count;
    }

    private static bool IsSectionBorder(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length >= 3 && trimmed.All(c => c == '#');
    }
}
