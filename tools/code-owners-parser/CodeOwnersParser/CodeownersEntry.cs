using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    /// <summary>
    /// The entry for CODEOWNERS has the following structure:
    ///
    /// <code>
    ///   # PRLabel: %Label
    ///   # ServiceLabel: %Label
    ///   path @owner @owner
    /// </code>
    /// </summary>
    public class CodeownersEntry
    {
        const char LabelSeparator = '%';
        const char OwnerSeparator = '@';
        public const string PRLabelMoniker = "PRLabel";
        public const string ServiceLabelMoniker = "ServiceLabel";
        public const string MissingFolder = "#/<NotInRepo>/";
       
        public string PathExpression { get; set; } = "";

        public bool ContainsWildcard => PathExpression.Contains('*');

        public List<string> Owners { get; set; } = new List<string>();

        public List<string> PRLabels { get; set; } = new List<string>();

        public List<string> ServiceLabels { get; set; } = new List<string>();

        public bool IsValid => !string.IsNullOrWhiteSpace(PathExpression);

        public CodeownersEntry()
        {
        }

        public CodeownersEntry(string pathExpression, List<string> owners)
        {
            PathExpression = pathExpression;
            Owners = owners;
        }

        private static string[] SplitLine(string line, char splitOn)
            => line.Split(new char[] { splitOn }, StringSplitOptions.RemoveEmptyEntries);

        public override string ToString()
            => $"HasWildcard:{ContainsWildcard} Expression:{PathExpression} " +
               $"Owners:{string.Join(",", Owners)}  PRLabels:{string.Join(",", PRLabels)}   " +
               $"ServiceLabels:{string.Join(",", ServiceLabels)}";

        public bool ProcessLabelsOnLine(string line)
        {
            if (line.Contains(PRLabelMoniker, StringComparison.OrdinalIgnoreCase))
            {
                PRLabels.AddRange(ParseLabels(line, PRLabelMoniker));
                return true;
            }
            else if (line.Contains(ServiceLabelMoniker, StringComparison.OrdinalIgnoreCase))
            {
                ServiceLabels.AddRange(ParseLabels(line, ServiceLabelMoniker));
                return true;
            }
            return false;
        }

        private static IEnumerable<string> ParseLabels(string line, string moniker)
        {
            // Parse a line that looks like # PRLabel: %Label, %Label
            if (!line.Contains(moniker, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            // If we don't have a ':', nothing to do
            int colonPosition = line.IndexOf(':');
            if (colonPosition == -1)
            {
                yield break;
            }

            line = line[(colonPosition + 1)..].Trim();
            foreach (string label in SplitLine(line, LabelSeparator).ToList())
            {
                if (!string.IsNullOrWhiteSpace(label))
                {
                    yield return label.Trim();
                }
            }
        }

        public void ParseOwnersAndPath(string line)
        {
            if (
                string.IsNullOrEmpty(line)
                || (IsComment(line)
                    && !line.Contains(
                        CodeownersEntry.MissingFolder, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            line = ParsePath(line);
            line = RemoveCommentIfAny(line);

            foreach (string author in SplitLine(line, OwnerSeparator).ToList())
            {
                if (!string.IsNullOrWhiteSpace(author))
                    Owners.Add(author.Trim());
            }
        }

        private static bool IsComment(string line)
            => line.StartsWith("#");

        private static string RemoveCommentIfAny(string line)
        {
            // this is the case when we have something like @user #comment

            int commentIndex = line.IndexOf("#", StringComparison.OrdinalIgnoreCase);

            if (commentIndex >= 0)
                line = line[..commentIndex].Trim();

            return line;
        }

        private string ParsePath(string line)
        {
            // Get the start of the owner in the string
            int ownerStartPosition = line.IndexOf('@');
            if (ownerStartPosition == -1)
            {
                return line;
            }

            string path = line[..ownerStartPosition].Trim();
            // the first entry is the path/regex
            PathExpression = path;

            // remove the path from the string.
            return line[ownerStartPosition..];
        }

        /// <summary>
        /// Remove all code owners which are not github alias.
        /// </summary>
        public void ExcludeNonUserAliases()
            => Owners.RemoveAll(r => !IsGitHubUserAlias(r));

        /// <summary>
        /// Helper method to check if it is valid github alias.
        /// </summary>
        /// <param name="alias">Alias string.</param>
        /// <returns>True if it is a github alias, Otherwise false.</returns>
        private static bool IsGitHubUserAlias(string alias)
        {
            // We used to call the github users api but we often got 403 returned
            // due to rate limiting. So instead we are approximating the check
            // by check for a slash in the name if there is one then we will consider
            // it to be a team instead of a users. 
            if (alias.Contains('/'))
            {
                return false;
            }
            return true;
        }

        protected bool Equals(CodeownersEntry other)
            => PathExpression == other.PathExpression
               && Owners.SequenceEqual(other.Owners)
               && PRLabels.SequenceEqual(other.PRLabels)
               && ServiceLabels.SequenceEqual(other.ServiceLabels);

        public override bool Equals(object? obj)
        {
            // @formatter:off
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CodeownersEntry)obj);
            // @formatter:on
        }

        public override int GetHashCode()
            => HashCode.Combine(PathExpression, Owners, PRLabels, ServiceLabels);
    }
}
