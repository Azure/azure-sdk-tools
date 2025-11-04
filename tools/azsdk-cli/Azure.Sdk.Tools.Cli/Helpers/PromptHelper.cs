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
        
        // Individual regex patterns for each markdown link type for better readability
        private static readonly Regex inlineLinkRegex = InlineLinkRegex();
        private static readonly Regex referenceLinkRegex = ReferenceLinkRegex();
        private static readonly Regex shortcutReferenceLinkRegex = ShortcutReferenceLinkRegex();
        private static readonly Regex implicitReferenceLinkRegex = ImplicitReferenceLinkRegex();
        private static readonly Regex autolinkRegex = AutolinkRegex();
        private static readonly Regex referenceDefinitionRegex = ReferenceDefinitionRegex();

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
            
            // Find all markdown links using multiple specific patterns
            var allMatches = new List<(Match match, MarkdownLinkType type)>();
            
            // Collect all link types with their positions
            allMatches.AddRange(inlineLinkRegex.Matches(text).Cast<Match>().Select(m => (m, MarkdownLinkType.Inline)));
            allMatches.AddRange(referenceLinkRegex.Matches(text).Cast<Match>().Select(m => (m, MarkdownLinkType.Reference)));
            allMatches.AddRange(shortcutReferenceLinkRegex.Matches(text).Cast<Match>().Select(m => (m, MarkdownLinkType.ShortcutReference)));
            allMatches.AddRange(implicitReferenceLinkRegex.Matches(text).Cast<Match>().Select(m => (m, MarkdownLinkType.ImplicitReference)));
            allMatches.AddRange(autolinkRegex.Matches(text).Cast<Match>().Select(m => (m, MarkdownLinkType.Autolink)));
            
            // Remove overlapping matches - prefer more specific patterns (longer matches)
            // When matches overlap, keep the one that starts first and is longest
            var deduplicated = new List<(Match match, MarkdownLinkType type)>();
            foreach (var item in allMatches.OrderBy(m => m.match.Index).ThenByDescending(m => m.match.Length))
            {
                // Check if this match overlaps with any already added match
                bool overlaps = deduplicated.Any(existing => 
                    (item.match.Index >= existing.match.Index && item.match.Index < existing.match.Index + existing.match.Length) ||
                    (existing.match.Index >= item.match.Index && existing.match.Index < item.match.Index + item.match.Length));
                
                if (!overlaps)
                {
                    deduplicated.Add(item);
                }
            }
            
            if (deduplicated.Count == 0)
            {
                return text;
            }

            var result = text;
            var expandedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Track to prevent infinite loops

            logger.LogDebug("Found {linkCount} potential file links in prompt", deduplicated.Count);
            
            // Process matches in reverse order to maintain string positions
            foreach (var (match, linkType) in deduplicated.OrderByDescending(m => m.match.Index))
            {
                string linkText, linkPath;

                switch (linkType)
                {
                    case MarkdownLinkType.Inline:
                        // Inline link: [text](path)
                        linkText = match.Groups[1].Value;
                        linkPath = match.Groups[2].Value.Trim();
                        break;
                        
                    case MarkdownLinkType.Reference:
                        // Reference link: [text][ref]
                        linkText = match.Groups[1].Value;
                        var refName = match.Groups[2].Value.Trim();
                        
                        // Look up the reference definition
                        if (!referenceDefinitions.TryGetValue(refName, out var refPath))
                        {
                            logger.LogWarning("Reference definition not found for: {refName}", refName);
                            continue;
                        }
                        linkPath = refPath;
                        break;
                        
                    case MarkdownLinkType.ShortcutReference:
                        // Shortcut reference link: [text][]
                        linkText = match.Groups[1].Value;
                        var shortcutRefName = linkText.Trim();
                        
                        if (!referenceDefinitions.TryGetValue(shortcutRefName, out var shortcutRefPath))
                        {
                            logger.LogWarning("Reference definition not found for: {shortcutRefName}", shortcutRefName);
                            continue;
                        }
                        linkPath = shortcutRefPath;
                        break;
                        
                    case MarkdownLinkType.ImplicitReference:
                        // Implicit reference link: [text]
                        linkText = match.Groups[1].Value;
                        var implicitRefName = linkText.Trim();
                        
                        if (!referenceDefinitions.TryGetValue(implicitRefName, out var implicitRefPath))
                        {
                            logger.LogWarning("Reference definition not found for: {implicitRefName}", implicitRefName);
                            continue;
                        }
                        linkPath = implicitRefPath;
                        break;
                        
                    case MarkdownLinkType.Autolink:
                        // Autolink: <url> - replace with just the URL
                        var autoUrl = match.Groups[1].Value;
                        result = result.Substring(0, match.Index) + autoUrl + result.Substring(match.Index + match.Length);
                        continue;
                        
                    default:
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

        /// <summary>
        /// Enum representing different types of markdown links
        /// </summary>
        private enum MarkdownLinkType
        {
            Inline,              // [text](path)
            Reference,           // [text][ref]
            ShortcutReference,   // [text][]
            ImplicitReference,   // [text]
            Autolink             // <url>
        }

        // Inline link: [text](path) with optional title
        [GeneratedRegex(@"\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\]\(([^\s)]+)(?:\s+[""'][^""']*[""'])?\)", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex InlineLinkRegex();

        // Reference link: [text][ref]
        [GeneratedRegex(@"\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\]\[([^\[\]]+)\]", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex ReferenceLinkRegex();

        // Shortcut reference link: [text][]
        [GeneratedRegex(@"\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\]\[\]", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex ShortcutReferenceLinkRegex();

        // Implicit reference link: [text] (not followed by [ or ()
        [GeneratedRegex(@"\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\](?!\[)(?!\()", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex ImplicitReferenceLinkRegex();

        // Autolink: <url>
        [GeneratedRegex(@"\<([^<>\s]+)\>", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex AutolinkRegex();

        // Reference definition: [ref]: url
        [GeneratedRegex(@"^\s*\[([^\[\]]+)\]:\s*(?:<([^<>]+)>|([^\s]+))(?:\s+(""[^""]*""|'[^']*'|\([^)]*\)))?\s*$", RegexOptions.Compiled | RegexOptions.Multiline)]
        private static partial Regex ReferenceDefinitionRegex();
    }
}