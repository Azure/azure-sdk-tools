using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

public static class ToolHelpers
{
    /// <summary>
    /// Try to resolve a provided path against a base directory, ensuring the result stays within the base directory.
    /// </summary>
    /// <param name="baseDirectory">The base directory that bounds all operations.</param>
    /// <param name="relativePath">A relative path provided by the user or caller.</param>
    /// <param name="fullPath">Outputs the resolved full path if successful; otherwise it's null.</param>
    /// <returns>True if the path resolves within the base directory; otherwise false.</returns>
    public static bool TryGetSafeFullPath(string baseDirectory, string relativePath, out string fullPath)
    {
        fullPath = default;

        try
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            if (Path.IsPathRooted(relativePath))
            {
                return false;
            }

            var baseFullPath = Path.GetFullPath(baseDirectory);
            var combinedFullPath = Path.GetFullPath(Path.Join(baseFullPath, relativePath));

            var baseWithSep = baseFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!combinedFullPath.StartsWith(baseWithSep, StringComparison.Ordinal) &&
                !string.Equals(combinedFullPath, baseFullPath, StringComparison.Ordinal))
            {
                return false;
            }

            fullPath = combinedFullPath;
            return true;
        }
        catch
        {
            // Catch any path-related exceptions (e.g., invalid chars) and return false per Try* contract.
            return false;
        }
    }
}
