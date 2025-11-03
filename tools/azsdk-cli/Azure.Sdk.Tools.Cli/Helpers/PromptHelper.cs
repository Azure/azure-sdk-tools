// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    /// <summary>
    /// Helper class for processing and manipulating prompt content.
    /// </summary>
    public static partial class PromptHelper
    {
        // Maximum file size to read (1 MB)
        private const long MaxFileSizeBytes = 1024 * 1024;
        
        // Compiled regex for better performance when matching markdown links: [text](path)
        private static readonly Regex markdownLinkRegex =
            MarkdownLinkRegex();

        // Regex for matching reference definitions: [ref]: path
        private static readonly Regex referenceDefinitionRegex =
            ReferenceDefinitionRegex();

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
            
            // First, parse all reference definitions in the text
            var referenceDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var refMatches = referenceDefinitionRegex.Matches(text);
            foreach (Match refMatch in refMatches)
            {
                var refName = refMatch.Groups[1].Value.Trim();
                // Handle both angle bracket URLs and bare URLs
                var refPath = !string.IsNullOrEmpty(refMatch.Groups[2].Value) 
                    ? refMatch.Groups[2].Value.Trim()  // <url> format
                    : refMatch.Groups[3].Value.Trim(); // bare url format
                referenceDefinitions[refName] = refPath;
            }
            
            var matches = markdownLinkRegex.Matches(text);
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
                string linkText, linkPath;

                // Determine link type based on which groups are captured
                if (!string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    // Inline link: [text](path)
                    linkText = match.Groups[1].Value;
                    linkPath = match.Groups[2].Value.Trim();
                }
                else if (!string.IsNullOrEmpty(match.Groups[4].Value))
                {
                    // Reference link: [text][ref]
                    linkText = match.Groups[3].Value;
                    var refName = match.Groups[4].Value.Trim();
                    
                    // Look up the reference definition
                    if (referenceDefinitions.TryGetValue(refName, out var refPath))
                    {
                        linkPath = refPath;
                    }
                    else
                    {
                        // Reference not found, skip this link
                        logger.LogWarning("Reference definition not found for: {refName}", refName);
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(match.Groups[5].Value))
                {
                    // Shortcut reference link: [text][]
                    linkText = match.Groups[5].Value;
                    var refName = linkText.Trim();
                    
                    // Look up the reference definition using the link text as reference name
                    if (referenceDefinitions.TryGetValue(refName, out var refPath))
                    {
                        linkPath = refPath;
                    }
                    else
                    {
                        // Reference not found, skip this link
                        logger.LogWarning("Reference definition not found for: {refName}", refName);
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(match.Groups[6].Value))
                {
                    // Implicit reference link: [text]
                    linkText = match.Groups[6].Value;
                    var refName = linkText.Trim();
                    
                    // Look up the reference definition using the link text as reference name
                    if (referenceDefinitions.TryGetValue(refName, out var refPath))
                    {
                        linkPath = refPath;
                    }
                    else
                    {
                        // Reference not found, skip this link
                        logger.LogWarning("Reference definition not found for: {refName}", refName);
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(match.Groups[7].Value))
                {
                    // Autolink: <url> - replace with just the URL
                    var autoUrl = match.Groups[7].Value;
                    result = result.Substring(0, match.Index) + autoUrl + result.Substring(match.Index + match.Length);
                    continue;
                }
                else
                {
                    // This shouldn't happen with our regex, but just in case
                    continue;
                }

                // Skip if it looks like a URL (starts with http/https/mailto/etc.)
                if (linkPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    linkPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    linkPath.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    linkPath.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                    linkPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Skip anchors within the same document
                if (linkPath.StartsWith("#"))
                {
                    continue;
                }

                string fullPath = Path.IsPathRooted(linkPath)
                        ? linkPath
                        : Path.GetFullPath(Path.Combine(basePath, linkPath));

                // Check if file exists and we haven't already processed it
                if (File.Exists(fullPath))
                {
                    if (!expandedFiles.Contains(fullPath)) {
                        // Check file size before reading
                        var fileInfo = new FileInfo(fullPath);
                        if (fileInfo.Length > MaxFileSizeBytes)
                        {
                            logger.LogWarning("File '{filePath}' is too large ({fileSize} bytes, max: {maxSize} bytes). Skipping expansion.", 
                                linkPath, fileInfo.Length, MaxFileSizeBytes);
                            continue;
                        }
                        
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
                }
                else
                {
                    logger.LogWarning("Referenced file not found: {linkPath} (resolved to {fullPath})", linkPath, fullPath);
                }
            }

            return result;
        }

        [GeneratedRegex(@"\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\]\(([^\s)]+)(?:\s+[""'][^""']*[""'])?\)|\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\]\[([^\[\]]*)\]|\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\]\[\]|\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\](?!\[)(?!\()|\<([^<>\s]+)\>", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex MarkdownLinkRegex();

        [GeneratedRegex(@"^\s*\[([^\[\]]+)\]:\s*(?:<([^<>]+)>|([^\s]+))(?:\s+(""[^""]*""|'[^']*'|\([^)]*\)))?\s*$", RegexOptions.Compiled | RegexOptions.Multiline)]
        private static partial Regex ReferenceDefinitionRegex();
    }
}