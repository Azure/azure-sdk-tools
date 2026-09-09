using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.CodeownersMigration.Verification
{
    public enum DifferenceKind
    {
        ParseError,
        PathOnlyInBaseline,
        PathOnlyInCandidate,
        PathDeclarationChanged,
        LabelSetOnlyInBaseline,
        LabelSetOnlyInCandidate,
        LabelOwnersChanged,
        ResolvedOwnersChanged,
        ResolvedLabelsChanged
    }

    public class Difference
    {
        public DifferenceKind Kind { get; set; }
        public string Subject { get; set; }
        public string Baseline { get; set; }
        public string Candidate { get; set; }

        public override string ToString()
        {
            var text = $"[{Kind}] {Subject}";
            if (Baseline != null || Candidate != null)
            {
                text += $"\n    baseline : {Baseline ?? "(none)"}\n    candidate: {Candidate ?? "(none)"}";
            }
            return text;
        }
    }

    public class ComparisonReport
    {
        public List<Difference> Differences { get; } = new List<Difference>();
        public int PathsCompared { get; set; }
        public int LabelSetsCompared { get; set; }
        public int ResolutionsCompared { get; set; }

        public bool IsEquivalent => Differences.Count == 0;

        public void Add(DifferenceKind kind, string subject, string baseline = null, string candidate = null)
        {
            Differences.Add(new Difference { Kind = kind, Subject = subject, Baseline = baseline, Candidate = candidate });
        }
    }

    /// <summary>
    /// Compares two CODEOWNERS files for semantic equivalence. Comments, blank lines, block ordering within a
    /// section, section membership and team expansion are all ignored; only the data the parser produces is
    /// compared.
    ///
    /// Three independent passes run, because no single one is sufficient:
    ///
    /// 1. Path declarations - catches added, removed or re-owned path entries.
    /// 2. Label ownership   - catches added, removed or re-owned service labels. Union-aware: every block that
    ///                        declares the same label set is merged before comparing, so the new renderer
    ///                        collapsing two identical-label blocks into one union block is not a difference.
    /// 3. Path resolution   - replays GitHub's last-match-wins rule over real paths. This is the only pass that
    ///                        can catch an ordering regression, where both files declare identical entries but
    ///                        a different one wins.
    /// </summary>
    public static class SemanticComparer
    {
        public static ComparisonReport Compare(CodeownersDocument baseline,
                                               CodeownersDocument candidate,
                                               IEnumerable<string> resolutionTargets)
        {
            var report = new ComparisonReport();

            foreach (string diagnostic in baseline.ParseDiagnostics)
            {
                report.Add(DifferenceKind.ParseError, $"{baseline.Path}: {diagnostic}");
            }
            foreach (string diagnostic in candidate.ParseDiagnostics)
            {
                report.Add(DifferenceKind.ParseError, $"{candidate.Path}: {diagnostic}");
            }

            ComparePathDeclarations(baseline, candidate, report);
            CompareLabelOwnership(baseline, candidate, report);
            CompareResolution(baseline, candidate, resolutionTargets, report);

            return report;
        }

        /// <summary>
        /// Pass 1. Every path expression and the owners and PR labels declared against it. A path may legally be
        /// declared more than once, so declarations are compared as a multiset per path rather than collapsed.
        /// </summary>
        private static void ComparePathDeclarations(CodeownersDocument baseline, CodeownersDocument candidate, ComparisonReport report)
        {
            Dictionary<string, List<string>> baselineDeclarations = GroupPathDeclarations(baseline);
            Dictionary<string, List<string>> candidateDeclarations = GroupPathDeclarations(candidate);

            var allPaths = new SortedSet<string>(baselineDeclarations.Keys, StringComparer.Ordinal);
            allPaths.UnionWith(candidateDeclarations.Keys);
            report.PathsCompared = allPaths.Count;

            foreach (string path in allPaths)
            {
                bool inBaseline = baselineDeclarations.TryGetValue(path, out List<string> baselineSignatures);
                bool inCandidate = candidateDeclarations.TryGetValue(path, out List<string> candidateSignatures);

                if (inBaseline && !inCandidate)
                {
                    report.Add(DifferenceKind.PathOnlyInBaseline, path, string.Join(" | ", baselineSignatures));
                }
                else if (!inBaseline && inCandidate)
                {
                    report.Add(DifferenceKind.PathOnlyInCandidate, path, null, string.Join(" | ", candidateSignatures));
                }
                else if (!baselineSignatures.SequenceEqual(candidateSignatures, StringComparer.Ordinal))
                {
                    report.Add(DifferenceKind.PathDeclarationChanged,
                               path,
                               string.Join(" | ", baselineSignatures),
                               string.Join(" | ", candidateSignatures));
                }
            }
        }

        private static Dictionary<string, List<string>> GroupPathDeclarations(CodeownersDocument document)
        {
            var declarations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (EntryFacts facts in document.Facts.Where(f => f.HasPath))
            {
                string signature = $"owners={Join(EntryFacts.OwnerSet(facts.SourceOwners))}; " +
                                   $"pr-labels={Join(EntryFacts.LabelSet(facts.PRLabels))}";
                if (!declarations.TryGetValue(facts.Path, out List<string> list))
                {
                    list = new List<string>();
                    declarations[facts.Path] = list;
                }
                list.Add(signature);
            }

            // Sort so that re-ordering two declarations of the same path is not reported as a change; pass 3
            // is responsible for detecting ordering that actually changes the outcome.
            foreach (List<string> list in declarations.Values)
            {
                list.Sort(StringComparer.Ordinal);
            }
            return declarations;
        }

        /// <summary>
        /// Pass 2. Service label ownership, keyed by the normalized label set and unioned across every block
        /// that declares it. This models the union rule in the new design: N owners.yaml files contributing the
        /// same label set produce one block owning the union of their owners.
        /// </summary>
        private static void CompareLabelOwnership(CodeownersDocument baseline, CodeownersDocument candidate, ComparisonReport report)
        {
            Dictionary<string, LabelOwnership> baselineLabels = GroupLabelOwnership(baseline);
            Dictionary<string, LabelOwnership> candidateLabels = GroupLabelOwnership(candidate);

            var allLabelSets = new SortedSet<string>(baselineLabels.Keys, StringComparer.Ordinal);
            allLabelSets.UnionWith(candidateLabels.Keys);
            report.LabelSetsCompared = allLabelSets.Count;

            foreach (string labelSet in allLabelSets)
            {
                bool inBaseline = baselineLabels.TryGetValue(labelSet, out LabelOwnership baselineOwnership);
                bool inCandidate = candidateLabels.TryGetValue(labelSet, out LabelOwnership candidateOwnership);

                if (inBaseline && !inCandidate)
                {
                    report.Add(DifferenceKind.LabelSetOnlyInBaseline, labelSet, baselineOwnership.ToString());
                }
                else if (!inBaseline && inCandidate)
                {
                    report.Add(DifferenceKind.LabelSetOnlyInCandidate, labelSet, null, candidateOwnership.ToString());
                }
                else if (!baselineOwnership.Equals(candidateOwnership))
                {
                    report.Add(DifferenceKind.LabelOwnersChanged, labelSet, baselineOwnership.ToString(), candidateOwnership.ToString());
                }
            }
        }

        private class LabelOwnership
        {
            public SortedSet<string> ServiceOwners { get; } = new SortedSet<string>(StringComparer.Ordinal);
            public SortedSet<string> AzureSdkOwners { get; } = new SortedSet<string>(StringComparer.Ordinal);

            public bool Equals(LabelOwnership other)
            {
                return other != null
                    && ServiceOwners.SetEquals(other.ServiceOwners)
                    && AzureSdkOwners.SetEquals(other.AzureSdkOwners);
            }

            public override string ToString()
            {
                return $"service-owners={Join(ServiceOwners)}; azure-sdk-owners={Join(AzureSdkOwners)}";
            }
        }

        private static Dictionary<string, LabelOwnership> GroupLabelOwnership(CodeownersDocument document)
        {
            var ownership = new Dictionary<string, LabelOwnership>(StringComparer.Ordinal);
            foreach (EntryFacts facts in document.Facts)
            {
                SortedSet<string> labels = EntryFacts.LabelSet(facts.ServiceLabels);
                if (labels.Count == 0)
                {
                    continue;
                }

                string key = Join(labels);
                if (!ownership.TryGetValue(key, out LabelOwnership existing))
                {
                    existing = new LabelOwnership();
                    ownership[key] = existing;
                }
                existing.ServiceOwners.UnionWith(EntryFacts.OwnerSet(facts.ServiceOwners));
                existing.AzureSdkOwners.UnionWith(EntryFacts.OwnerSet(facts.AzureSdkOwners));
            }
            return ownership;
        }

        /// <summary>
        /// Pass 3. Replays last-match-wins resolution for a set of concrete paths. Two files can declare exactly
        /// the same entries and still behave differently if the entries are ordered differently, so this is the
        /// pass that actually proves behavioural equivalence.
        /// </summary>
        private static void CompareResolution(CodeownersDocument baseline,
                                              CodeownersDocument candidate,
                                              IEnumerable<string> resolutionTargets,
                                              ComparisonReport report)
        {
            List<string> targets = (resolutionTargets ?? Enumerable.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            if (targets.Count == 0)
            {
                return;
            }

            var baselineResolver = new PathResolver(baseline);
            var candidateResolver = new PathResolver(candidate);
            report.ResolutionsCompared = targets.Count;

            foreach (string target in targets)
            {
                EntryFacts baselineMatch = baselineResolver.Resolve(target);
                EntryFacts candidateMatch = candidateResolver.Resolve(target);

                SortedSet<string> baselineOwners = EntryFacts.OwnerSet(baselineMatch?.SourceOwners);
                SortedSet<string> candidateOwners = EntryFacts.OwnerSet(candidateMatch?.SourceOwners);
                if (!baselineOwners.SetEquals(candidateOwners))
                {
                    report.Add(DifferenceKind.ResolvedOwnersChanged,
                               target,
                               $"{Join(baselineOwners)} (from {Describe(baselineMatch)})",
                               $"{Join(candidateOwners)} (from {Describe(candidateMatch)})");
                }

                SortedSet<string> baselineLabels = EntryFacts.LabelSet(baselineMatch?.PRLabels);
                SortedSet<string> candidateLabels = EntryFacts.LabelSet(candidateMatch?.PRLabels);
                if (!baselineLabels.SetEquals(candidateLabels))
                {
                    report.Add(DifferenceKind.ResolvedLabelsChanged,
                               target,
                               $"{Join(baselineLabels)} (from {Describe(baselineMatch)})",
                               $"{Join(candidateLabels)} (from {Describe(candidateMatch)})");
                }
            }
        }

        private static string Describe(EntryFacts facts)
        {
            return facts == null || !facts.HasPath ? "no match" : facts.Path;
        }

        private static string Join(IEnumerable<string> values)
        {
            var list = (values ?? Enumerable.Empty<string>()).ToList();
            return list.Count == 0 ? "(none)" : string.Join(", ", list);
        }
    }
}
