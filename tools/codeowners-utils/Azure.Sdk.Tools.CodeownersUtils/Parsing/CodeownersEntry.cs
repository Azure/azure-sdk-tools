using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.CodeownersUtils.Parsing
{
    /// <summary>
    /// The entry for CODEOWNERS has one othe following structures:
    /// <para>
    /// A plain source path/owner entry
    /// </para>
    /// <code>
    ///   path @owner @owner
    /// </code>
    /// <para>
    /// A source path/owner entry with a PRLabel
    /// </para> 
    /// <code>
    ///   # PRLabel: %Label
    ///   path @owner @owner
    /// </code>
    /// <para>
    /// A source path/owner entry PRLabel and ServiceLabel entries. In this case Service Owners
    /// will be same as source owners.
    /// </para> 
    /// <code>
    ///   # PRLabel: %Label
    ///   # ServiceLabel: %Label
    ///   path @owner @owner
    /// </code>
    /// <para>
    /// A source path/owner entry PRLabel, ServiceLabel entries
    /// </para> 
    /// <code>
    ///   # PRLabel: %Label1 %Label2
    ///   # ServiceLabel: %Label3 %Label4
    ///   path @owner @owner
    /// </code>
    /// <para>
    /// ServiceLabel and ServiceOwners entry OR /&lt;NotInRepo&gt;/ (one or the other, not both). ServiceOwners is preferred going forward.
    /// </para> 
    /// <code>
    ///   # ServiceLabel: %Label3 %Label4
    ///   # ServiceOwners: @owner @owner
    ///   or, the old style
    ///   # ServiceLabel: %Label3 %Label4
    ///   # /&lt;NotInRepo&gt;/: @owner @owner
    /// </code>
    /// </summary>
    public class CodeownersEntry
    {
        public string PathExpression { get; set; } = "";

        public bool ContainsWildcard => !string.IsNullOrEmpty(PathExpression) && PathExpression.Contains('*');
        public bool ContainsDoubleWildcard => !string.IsNullOrEmpty(PathExpression) && PathExpression.Contains("**");

        public List<string> SourceOwners { get; set; } = new List<string>();

        public List<string> OriginalSourceOwners { get; set; } = new List<string>();

        // PRLabels are tied to a source path/owner line
        // In theory, there should only have be one PR Label.
        public List<string> PRLabels { get; set; } = new List<string>();

        // ServiceLabels are tied to either, ServiceOwners or /<NotInRepo>/ (MissingLabel moniker), or
        // the owners from a source path/owners line (meaning that the owners for the source are also
        // the service owners)
        // In theory, there should only have ever be one ServiceLabel, aside from the ServiceAttention label
        public List<string> ServiceLabels { get; set; } = new List<string>();

        public List<string> ServiceOwners { get; set; } = new List<string>();

        public List<string> OriginalServiceOwners { get; set; } = new List<string>();

        // AzureSdkOwners are directly tied to the source path. If the AzureSdkOwners are defined
        // on the same line as the moniker, it'll use those, if it's empty it'll use the owners from
        // the source path/owner line.
        public List<string> AzureSdkOwners { get; set; } = new List<string>();

        public List<string> OriginalAzureSdkOwners { get; set; } = new List<string>();

        public int startLine { get; set; } = -1;
        public int endLine { get; set; } = -1;

        public bool IsValid => !string.IsNullOrWhiteSpace(PathExpression);

        public CodeownersEntry()
        {
        }

        public CodeownersEntry(CodeownersEntry other)
        {
            if (other == null)
            {
                return;
            }

            PathExpression = other.PathExpression;
            SourceOwners = other.SourceOwners != null ? new List<string>(other.SourceOwners) : new List<string>();
            OriginalSourceOwners = other.OriginalSourceOwners != null ? new List<string>(other.OriginalSourceOwners) : new List<string>();
            PRLabels = other.PRLabels != null ? new List<string>(other.PRLabels) : new List<string>();
            ServiceLabels = other.ServiceLabels != null ? new List<string>(other.ServiceLabels) : new List<string>();
            ServiceOwners = other.ServiceOwners != null ? new List<string>(other.ServiceOwners) : new List<string>();
            OriginalServiceOwners = other.OriginalServiceOwners != null ? new List<string>(other.OriginalServiceOwners) : new List<string>();
            AzureSdkOwners = other.AzureSdkOwners != null ? new List<string>(other.AzureSdkOwners) : new List<string>();
            OriginalAzureSdkOwners = other.OriginalAzureSdkOwners != null ? new List<string>(other.OriginalAzureSdkOwners) : new List<string>();

            startLine = other.startLine;
            endLine = other.endLine;
        }

        public override string ToString()
        {
            return string.Join(
                        Environment.NewLine,
                        $"PathExpression:{PathExpression}, HasWildcard:{ContainsWildcard}, HasDoubleWildcard:{ContainsDoubleWildcard}",
                        $"SourceOwners:{string.Join(", ", SourceOwners)}",
                        $"PRLabels:{string.Join(", ", PRLabels)}",
                        $"ServiceLabels:{string.Join(", ", ServiceLabels)}",
                        $"ServiceOwners:{string.Join(", ", ServiceOwners)}",
                        $"AzureSdkOwners:{string.Join(", ", AzureSdkOwners)}",
                        $"Start line: {startLine}",
                        $"End line: {endLine}"
                   );
        }

        /// <summary>
        /// Remove all code owners which are not github alias.
        /// Even with team expansion there can still be teams in lists. This
        /// can happen if the team is not a child team under azure-sdk-write, which
        /// are the only teams expanded.
        /// </summary>
        public void ExcludeNonUserAliases()
        {
            SourceOwners.RemoveAll(r => ParsingUtils.IsGitHubTeam(r));
            ServiceOwners.RemoveAll(r => ParsingUtils.IsGitHubTeam(r));
            AzureSdkOwners.RemoveAll(r => ParsingUtils.IsGitHubTeam(r));
        }

        protected bool Equals(CodeownersEntry other)
            => PathExpression == other.PathExpression
               && SourceOwners.SequenceEqual(other.SourceOwners)
               && ServiceOwners.SequenceEqual(other.ServiceOwners)
               && AzureSdkOwners.SequenceEqual(other.AzureSdkOwners)
               && PRLabels.SequenceEqual(other.PRLabels)
               && ServiceLabels.SequenceEqual(other.ServiceLabels);

        public override bool Equals(object obj)
        {
            // @formatter:off
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CodeownersEntry)obj);
            // @formatter:on
        }

        /// <summary>
        /// Implementation of GetHashCode that properly hashes collections.
        /// Implementation based on
        /// https://stackoverflow.com/a/10567544/986533
        ///
        /// This implementation is candidate to be moved to:
        /// https://github.com/Azure/azure-sdk-tools/issues/5281
        /// </summary>
        public override int GetHashCode()
        {
            int hashCode = 0;
            // ReSharper disable NonReadonlyMemberInGetHashCode
            hashCode = AddHashCodeForObject(hashCode, PathExpression);
            hashCode = AddHashCodeForEnumerable(hashCode, SourceOwners);
            hashCode = AddHashCodeForEnumerable(hashCode, ServiceOwners);
            hashCode = AddHashCodeForEnumerable(hashCode, AzureSdkOwners);
            hashCode = AddHashCodeForEnumerable(hashCode, PRLabels);
            hashCode = AddHashCodeForEnumerable(hashCode, ServiceLabels);
            // ReSharper restore NonReadonlyMemberInGetHashCode
            return hashCode;

            // ReSharper disable once VariableHidesOuterVariable
            int AddHashCodeForEnumerable(int hashCode, IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    hashCode = AddHashCodeForObject(hashCode, item);
                }
                return hashCode;
            }

            int AddHashCodeForObject(int hc, object item)
            {
                // Based on https://stackoverflow.com/a/10567544/986533
                hc ^= item.GetHashCode();
                hc = (hc << 7) |
                     (hc >> (32 - 7)); // rotate hashCode to the left to swipe over all bits
                return hc;
            }
        }

        /// <summary>
        /// Formats a CodeownersEntry following the CODEOWNERS metadata block rules.
        ///
        /// There are three valid block types:
        /// 1. Source path block: AzureSdkOwners (optional) → ServiceLabel (optional) → PRLabel (optional) → path/owners
        /// 2. ServiceLabel/ServiceOwners block (no path): AzureSdkOwners (optional) → ServiceLabel → ServiceOwners
        /// 3. AzureSdkOwners/ServiceLabel block (no path, no ServiceOwners): AzureSdkOwners → ServiceLabel
        ///
        /// Key rules:
        /// - PRLabel must be part of a block that ends in a source path/owner line
        /// - ServiceOwners cannot be part of a source path/owner block (service owners are inferred from source owners)
        /// - AzureSdkOwners must be part of a block containing ServiceLabel
        /// - A block ends with a blank line or a single source path/owner line
        /// </summary>
        public string FormatCodeownersEntry(bool useOriginalOwners = false)
        {
            var lines = new List<string>();

            string path = this.PathExpression ?? string.Empty;
            List<string> serviceLabels = this.ServiceLabels ?? new List<string>();
            List<string> prLabels = this.PRLabels ?? new List<string>();
            List<string> serviceOwners = useOriginalOwners
                ? (this.OriginalServiceOwners ?? new List<string>())
                : (this.ServiceOwners ?? new List<string>());
            List<string> sourceOwners = useOriginalOwners
                ? (this.OriginalSourceOwners ?? new List<string>())
                : (this.SourceOwners ?? new List<string>());
            List<string> azureSdkOwners = useOriginalOwners
                ? (this.OriginalAzureSdkOwners ?? new List<string>())
                : (this.AzureSdkOwners ?? new List<string>());

            bool hasPath = !string.IsNullOrEmpty(path) && sourceOwners != null && sourceOwners.Count > 0;
            bool hasServiceLabels = serviceLabels.Any(lbl => !string.IsNullOrWhiteSpace(lbl));
            bool hasPRLabels = prLabels.Any(lbl => !string.IsNullOrWhiteSpace(lbl));
            bool hasServiceOwners = serviceOwners != null && serviceOwners.Any(o => !string.IsNullOrWhiteSpace(o));
            bool hasAzureSdkOwners = azureSdkOwners != null && azureSdkOwners.Any(o => !string.IsNullOrWhiteSpace(o));

            const string OWNER_PADDING = "    ";

            // Helper to format labels with % prefix
            IEnumerable<string> FormatLabels(List<string> labels)
            {
                return labels
                    .Where(lbl => !string.IsNullOrWhiteSpace(lbl))
                    .Select(lbl => lbl.StartsWith("%") ? lbl : $"%{lbl}");
            }

            // Helper to normalize and deduplicate owners
            List<string> NormalizeOwners(List<string> owners)
            {
                var normalized = owners
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim().TrimStart('@').Trim())
                    .ToList();

                var uniqueOwners = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var owner in normalized)
                {
                    if (seen.Add(owner))
                    {
                        uniqueOwners.Add("@" + owner);
                    }
                }
                return uniqueOwners;
            }

            // Determine which block type we're formatting
            if (hasPath)
            {
                // Block Type 1: Source path block
                // Order: AzureSdkOwners (optional) → ServiceLabel (optional) → PRLabel (optional) → path/owners
                // Note: ServiceOwners is NOT included in source path blocks (service owners are inferred from source owners)

                // Add AzureSdkOwners if present (must have ServiceLabel to be valid, but we format what we have)
                if (hasAzureSdkOwners)
                {
                    var uniqueAzureSdkOwners = NormalizeOwners(azureSdkOwners);
                    var azureSdkOwnersLine = "# AzureSdkOwners: " + string.Join(" ", uniqueAzureSdkOwners);
                    lines.Add(azureSdkOwnersLine);
                }

                // Add ServiceLabel if present
                if (hasServiceLabels)
                {
                    lines.Add($"# ServiceLabel: {string.Join(" ", FormatLabels(serviceLabels))}");
                }

                // Add PRLabel if present
                if (hasPRLabels)
                {
                    lines.Add($"# PRLabel: {string.Join(" ", FormatLabels(prLabels))}");
                }

                // Add the path and source owners line
                var uniqueSourceOwners = NormalizeOwners(sourceOwners);
                var pathLine = path + OWNER_PADDING + string.Join(" ", uniqueSourceOwners);
                lines.Add(pathLine);
            }
            else if (hasServiceLabels && hasServiceOwners)
            {
                // Block Type 2: ServiceLabel/ServiceOwners block (no source path)
                // Order: AzureSdkOwners (optional) → ServiceLabel → ServiceOwners

                // Add AzureSdkOwners if present
                if (hasAzureSdkOwners)
                {
                    var uniqueAzureSdkOwners = NormalizeOwners(azureSdkOwners);
                    var azureSdkOwnersLine = "# AzureSdkOwners: " + string.Join(" ", uniqueAzureSdkOwners);
                    lines.Add(azureSdkOwnersLine);
                }

                // Add ServiceLabel
                lines.Add($"# ServiceLabel: {string.Join(" ", FormatLabels(serviceLabels))}");

                // Add ServiceOwners
                var uniqueServiceOwners = NormalizeOwners(serviceOwners);
                var serviceOwnersLine = "# ServiceOwners: " + string.Join(" ", uniqueServiceOwners);
                lines.Add(serviceOwnersLine);
            }
            else if (hasAzureSdkOwners && hasServiceLabels)
            {
                // Block Type 3: AzureSdkOwners/ServiceLabel block (no path, no ServiceOwners)
                // Order: AzureSdkOwners → ServiceLabel

                var uniqueAzureSdkOwners = NormalizeOwners(azureSdkOwners);
                var azureSdkOwnersLine = "# AzureSdkOwners: " + string.Join(" ", uniqueAzureSdkOwners);
                lines.Add(azureSdkOwnersLine);

                // Add ServiceLabel
                lines.Add($"# ServiceLabel: {string.Join(" ", FormatLabels(serviceLabels))}");
            }

            var formattedCodeownersEntry = string.Join("\n", lines);
            return formattedCodeownersEntry;
        }
    }
}
