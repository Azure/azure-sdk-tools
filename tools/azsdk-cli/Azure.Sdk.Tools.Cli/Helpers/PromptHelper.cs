// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    /// <summary>
    /// Helper class for processing and manipulating prompt content.
    /// </summary>
    public static class PromptHelper
    {
        /// <summary>
        /// Expands relative file links in markdown-style text by inlining their content.
        /// Supports links in the format [text](relative/path/to/file.ext)
        /// </summary>
        /// <param name="text">The text containing potential file links</param>
        /// <param name="basePath">The base directory to resolve relative paths from</param>
        /// <param name="logger">Logger for debug and warning messages</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Text with file links expanded to include their content</returns>
        public static async Task<string> ExpandRelativeFileLinksAsync(string text, string basePath, ILogger logger, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            // Regex to match markdown links: [text](path)
            var linkPattern = @"\[([^\]]*)\]\(([^)]+)\)";
            var regex = new System.Text.RegularExpressions.Regex(linkPattern);
            
            var matches = regex.Matches(text);
            if (matches.Count == 0)
            {
                return text;
            }

            var result = text;
            var expandedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Track to prevent infinite loops

            logger.LogDebug("Found {linkCount} potential file links in prompt", matches.Count);

            // Process matches in reverse order to maintain string positions
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                var linkText = match.Groups[1].Value;
                var linkPath = match.Groups[2].Value.Trim();

                // Skip if it looks like a URL (starts with http/https)
                if (linkPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    linkPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Skip anchors within the same document
                if (linkPath.StartsWith("#"))
                {
                    continue;
                }

                try
                {
                    // Resolve relative path
                    string fullPath;
                    if (Path.IsPathRooted(linkPath))
                    {
                        fullPath = linkPath;
                    }
                    else
                    {
                        fullPath = Path.GetFullPath(Path.Combine(basePath, linkPath));
                    }

                    // Check if file exists and we haven't already processed it
                    if (File.Exists(fullPath) && !expandedFiles.Contains(fullPath))
                    {
                        expandedFiles.Add(fullPath);
                        logger.LogDebug("Expanding file link: {linkPath} -> {fullPath}", linkPath, fullPath);

                        var fileContent = await File.ReadAllTextAsync(fullPath, ct);
                        
                        // Create expanded content with clear boundaries
                        var expandedContent = $@"

## Referenced File: {linkText} ({linkPath})

```
{fileContent}
```

";

                        // Replace the link with the expanded content
                        result = result.Substring(0, match.Index) + expandedContent + result.Substring(match.Index + match.Length);
                    }
                    else if (!File.Exists(fullPath))
                    {
                        logger.LogWarning("Referenced file not found: {linkPath} (resolved to {fullPath})", linkPath, fullPath);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to expand file link: {linkPath}", linkPath);
                }
            }

            return result;
        }
    }
}