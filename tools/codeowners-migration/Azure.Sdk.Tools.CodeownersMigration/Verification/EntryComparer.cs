using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersMigration.Verification
{
    /// <summary>
    /// Compares two lists of <see cref="CodeownersEntry"/> objects position by position and reports the
    /// semantic differences between them.
    ///
    /// The comparison is deliberately ordinal: entry <c>i</c> of the base file is compared against entry
    /// <c>i</c> of the compare file. CODEOWNERS resolution is last-match-wins, so the order of entries is
    /// itself semantic — two files holding identical entries in a different order assign ownership
    /// differently. A set-based comparison would call such a pair equal, which is the single defect class
    /// a regeneration gate exists to catch.
    /// </summary>
    public class EntryComparer
    {
        /// <summary>
        /// Compares the semantic content of two parsed CODEOWNERS files.
        /// </summary>
        /// <param name="baseEntries">Entries parsed from the base (hand-maintained) file.</param>
        /// <param name="compareEntries">Entries parsed from the generated file.</param>
        /// <returns>The differences found, in entry order. An empty list means the files are equivalent.</returns>
        public List<EntryDifference> Compare(List<CodeownersEntry> baseEntries, List<CodeownersEntry> compareEntries)
        {
            List<EntryFacts> baseFacts = (baseEntries ?? new List<CodeownersEntry>()).Select(EntryFacts.FromEntry).ToList();
            List<EntryFacts> compareFacts = (compareEntries ?? new List<CodeownersEntry>()).Select(EntryFacts.FromEntry).ToList();

            var differences = new List<EntryDifference>();
            int shared = Math.Min(baseFacts.Count, compareFacts.Count);

            for (int i = 0; i < shared; i++)
            {
                differences.AddRange(CompareEntry(i, baseFacts[i], compareFacts[i]));
            }

            for (int i = shared; i < baseFacts.Count; i++)
            {
                differences.Add(new EntryDifference
                {
                    Index = i,
                    Kind = DifferenceKind.MissingFromCompare,
                    Path = baseFacts[i].Path,
                    BaseLine = baseFacts[i].StartLine,
                    BaseValue = baseFacts[i].Describe()
                });
            }

            for (int i = shared; i < compareFacts.Count; i++)
            {
                differences.Add(new EntryDifference
                {
                    Index = i,
                    Kind = DifferenceKind.MissingFromBase,
                    Path = compareFacts[i].Path,
                    CompareLine = compareFacts[i].StartLine,
                    CompareValue = compareFacts[i].Describe()
                });
            }

            return differences;
        }

        private static IEnumerable<EntryDifference> CompareEntry(int index, EntryFacts baseFacts, EntryFacts compareFacts)
        {
            var differences = new List<EntryDifference>();

            void Report(string field, string baseValue, string compareValue)
            {
                differences.Add(new EntryDifference
                {
                    Index = index,
                    Kind = DifferenceKind.FieldChanged,
                    Field = field,
                    // Name the entry by its base path so the reader can find it, falling back to the
                    // compare path for pathless (service-label-only) blocks.
                    Path = baseFacts.Path ?? compareFacts.Path ?? "(no path)",
                    BaseLine = baseFacts.StartLine,
                    CompareLine = compareFacts.StartLine,
                    BaseValue = baseValue,
                    CompareValue = compareValue
                });
            }

            // Path expressions are compared exactly. '/sdk/foo' and '/sdk/foo/' are different expressions to
            // GitHub, so normalizing one into the other would hide a real ownership change.
            if (!string.Equals(baseFacts.Path ?? string.Empty, compareFacts.Path ?? string.Empty, StringComparison.Ordinal))
            {
                Report("PathExpression", Quote(baseFacts.Path), Quote(compareFacts.Path));
            }

            CompareOwners("SourceOwners", baseFacts.SourceOwners, compareFacts.SourceOwners);
            CompareOwners("ServiceOwners", baseFacts.ServiceOwners, compareFacts.ServiceOwners);
            CompareOwners("AzureSdkOwners", baseFacts.AzureSdkOwners, compareFacts.AzureSdkOwners);
            CompareLabels("PRLabels", baseFacts.PRLabels, compareFacts.PRLabels);
            CompareLabels("ServiceLabels", baseFacts.ServiceLabels, compareFacts.ServiceLabels);

            void CompareOwners(string field, List<string> baseValues, List<string> compareValues)
            {
                SortedSet<string> baseSet = EntryFacts.OwnerSet(baseValues);
                SortedSet<string> compareSet = EntryFacts.OwnerSet(compareValues);
                if (!baseSet.SetEquals(compareSet))
                {
                    Report(field, Format(baseSet), Format(compareSet));
                }
            }

            void CompareLabels(string field, List<string> baseValues, List<string> compareValues)
            {
                SortedSet<string> baseSet = EntryFacts.LabelSet(baseValues);
                SortedSet<string> compareSet = EntryFacts.LabelSet(compareValues);
                if (!baseSet.SetEquals(compareSet))
                {
                    Report(field, Format(baseSet), Format(compareSet));
                }
            }

            return differences;
        }

        private static string Quote(string value)
        {
            return value == null ? "(none)" : $"'{value}'";
        }

        private static string Format(IEnumerable<string> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? "(none)" : string.Join(" ", list);
        }
    }
}
