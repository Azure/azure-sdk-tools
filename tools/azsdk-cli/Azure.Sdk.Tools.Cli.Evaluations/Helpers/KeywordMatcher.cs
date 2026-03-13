using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    /// <summary>
    /// Matches prompts to tools using keyword overlap, modeled after
    /// GHCP4A's TriggerMatcher. Zero API calls, deterministic, scales linearly.
    /// </summary>
    public static class KeywordMatcher
    {
        // Common English stopwords to exclude from keyword extraction
        private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
            "been", "being", "have", "has", "had", "do", "does", "did", "will",
            "would", "could", "should", "may", "might", "shall", "can", "need",
            "that", "this", "these", "those", "it", "its", "my", "your", "our",
            "their", "me", "him", "her", "us", "them", "i", "you", "he", "she",
            "we", "they", "not", "no", "if", "when", "then", "than", "so",
            "all", "each", "every", "both", "few", "more", "most", "some", "any",
            "how", "what", "which", "who", "whom", "where", "about", "into",
            "through", "during", "before", "after", "above", "below", "between",
            "out", "off", "over", "under", "again", "further", "once", "here",
            "there", "just", "also", "very", "too", "only", "up", "down"
        };

        // Short words that are meaningful in this domain and should NOT be filtered
        private static readonly HashSet<string> DomainTerms = new(StringComparer.OrdinalIgnoreCase)
        {
            "sdk", "api", "cli", "pr", "ci", "cd", "mcp", "llm", "npm", "pip",
            "git", "run", "log", "bug", "fix", "set", "get", "new", "add"
        };

        /// <summary>
        /// Extract meaningful keywords from a text string.
        /// </summary>
        public static HashSet<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var words = text.ToLowerInvariant()
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(new[] { ' ', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '/', '\\', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);

            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in words)
            {
                if (Stopwords.Contains(word))
                    continue;

                if (DomainTerms.Contains(word))
                {
                    keywords.Add(word);
                    continue;
                }

                if (word.Length <= 2)
                    continue;

                keywords.Add(word);
            }

            return keywords;
        }

        /// <summary>
        /// Build a keyword set for a tool from its name and description.
        /// </summary>
        public static HashSet<string> ExtractToolKeywords(AIFunction tool)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Keywords from tool name (split on underscores — e.g., "azsdk_package_build_code")
            foreach (var kw in ExtractKeywords(tool.Name))
                keywords.Add(kw);

            // Keywords from tool description
            if (!string.IsNullOrWhiteSpace(tool.Description))
            {
                foreach (var kw in ExtractKeywords(tool.Description))
                    keywords.Add(kw);
            }

            return keywords;
        }

        /// <summary>
        /// Rank all tools against a prompt by keyword overlap.
        /// Returns tools sorted by (matchCount descending, overlapRatio descending).
        /// </summary>
        public static List<ToolMatchResult> RankTools(string prompt, IReadOnlyList<AIFunction> tools)
        {
            var promptKeywords = ExtractKeywords(prompt);
            var results = new List<ToolMatchResult>();

            foreach (var tool in tools)
            {
                var toolKeywords = ExtractToolKeywords(tool);
                var matched = promptKeywords.Intersect(toolKeywords, StringComparer.OrdinalIgnoreCase).ToList();
                var overlapRatio = toolKeywords.Count > 0
                    ? (double)matched.Count / toolKeywords.Count
                    : 0.0;

                results.Add(new ToolMatchResult(
                    tool.Name,
                    tool.Description ?? "",
                    matched.Count,
                    overlapRatio,
                    matched));
            }

            return results
                .OrderByDescending(r => r.MatchedCount)
                .ThenByDescending(r => r.OverlapRatio)
                .ToList();
        }

        /// <summary>
        /// Check whether a tool matches a prompt (≥2 keyword matches OR ≥20% overlap).
        /// Same threshold logic as GHCP4A's TriggerMatcher.
        /// </summary>
        public static bool IsMatch(ToolMatchResult result)
        {
            return result.MatchedCount >= 2 || result.OverlapRatio >= 0.20;
        }
    }

    public sealed record ToolMatchResult(
        string Name,
        string Description,
        int MatchedCount,
        double OverlapRatio,
        List<string> MatchedKeywords);
}
