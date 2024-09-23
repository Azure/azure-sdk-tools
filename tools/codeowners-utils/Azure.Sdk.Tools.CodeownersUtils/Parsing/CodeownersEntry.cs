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

        public bool ContainsWildcard => PathExpression.Contains('*');

        public List<string> SourceOwners { get; set; } = new List<string>();

        // PRLabels are tied to a source path/owner line
        // In theory, there should only have be one PR Label.
        public List<string> PRLabels { get; set; } = new List<string>();

        // ServiceLabels are tied to either, ServiceOwners or /<NotInRepo>/ (MissingLabel moniker), or
        // the owners from a source path/owners line (meaning that the owners for the source are also
        // the service owners)
        // In theory, there should only have ever be one ServiceLabel, aside from the ServiceAttention label
        public List<string> ServiceLabels { get; set; } = new List<string>();

        public List<string> ServiceOwners { get; set; } = new List<string>();

        // AzureSdkOwners are directly tied to the source path. If the AzureSdkOwners are defined
        // on the same line as the moniker, it'll use those, if it's empty it'll use the owners from
        // the source path/owner line.
        public List<string> AzureSdkOwners { get; set; } = new List<string>();

        public bool IsValid => !string.IsNullOrWhiteSpace(PathExpression);

        public CodeownersEntry()
        {
        }
        public override string ToString()
        {
            return string.Join(
                        Environment.NewLine,
                        $"PathExpression:{PathExpression}, HasWildcard:{ContainsWildcard}",
                        $"SourceOwners:{string.Join(", ", SourceOwners)}",
                        $"PRLabels:{string.Join(", ", PRLabels)}",
                        $"ServiceLabels:{string.Join(", ", ServiceLabels)}",
                        $"ServiceOwners:{string.Join(", ", ServiceOwners)}",
                        $"AzureSdkOwners:{string.Join(", ", AzureSdkOwners)}"
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
    }
}
