using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersMigration.Verification
{
    /// <summary>
    /// Replays GitHub's last-match-wins resolution for a document. Pathless blocks are excluded because they
    /// can never match a path; removing them preserves the relative order of the remaining entries and so does
    /// not change the outcome.
    /// </summary>
    public class PathResolver
    {
        private readonly List<CodeownersEntry> _pathedEntries;
        private readonly Dictionary<CodeownersEntry, EntryFacts> _factsByEntry = new Dictionary<CodeownersEntry, EntryFacts>();

        public PathResolver(CodeownersDocument document)
        {
            _pathedEntries = document.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.PathExpression))
                .ToList();
        }

        public EntryFacts Resolve(string targetPath)
        {
            CodeownersEntry match = CodeownersParser.GetMatchingCodeownersEntry(targetPath, _pathedEntries);
            if (match == null || string.IsNullOrWhiteSpace(match.PathExpression))
            {
                return null;
            }

            if (!_factsByEntry.TryGetValue(match, out EntryFacts facts))
            {
                facts = EntryFacts.FromEntry(match);
                _factsByEntry[match] = facts;
            }
            return facts;
        }
    }
}
