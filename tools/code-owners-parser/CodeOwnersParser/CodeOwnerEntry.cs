using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    /// <summary>
    /// The entry for CODEOWNERS has the following structure:
    /// # PRLabel: %Label
    /// # ServiceLabel: %Label
    /// path @owner @owner
    /// </summary>
    public class CodeOwnerEntry
    {
        const char LabelSeparator = '%';
        const char OwnerSeparator = '@';
        public const string PRLabelMoniker = "PRLabel";
        public const string ServiceLabelMoniker = "ServiceLabel";
        public const string MissingFolder = "#/<NotInRepo>/";
       
        public string PathExpression { get; set; } = "";

        public bool ContainsWildcard => PathExpression.Contains("*");

        public List<string> Owners { get; set; } = new List<string>();

        public List<string> PRLabels { get; set; } = new List<string>();

        public List<string> ServiceLabels { get; set; } = new List<string>();

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(PathExpression);
            }
        }

        private static string[] SplitLine(string line, char splitOn)
        {
            return line.Split(new char[] { splitOn }, StringSplitOptions.RemoveEmptyEntries);
        }

        public override string ToString()
        {
            return $"HasWildcard:{ContainsWildcard} Expression:{PathExpression} Owners:{string.Join(",", Owners)}  PRLabels:{string.Join(",", PRLabels)}   ServiceLabels:{string.Join(",", ServiceLabels)}";
        }

        public bool ProcessLabelsOnLine(string line)
        {
            if (line.IndexOf(PRLabelMoniker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PRLabels.AddRange(ParseLabels(line, PRLabelMoniker));
                return true;
            }
            else if (line.IndexOf(ServiceLabelMoniker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ServiceLabels.AddRange(ParseLabels(line, ServiceLabelMoniker));
                return true;
            }
            return false;
        }

        private IEnumerable<string> ParseLabels(string line, string moniker)
        {
            // Parse a line that looks like # PRLabel: %Label, %Label
            if (line.IndexOf(moniker, StringComparison.OrdinalIgnoreCase) == -1)
            {
                yield break;
            }

            // If we don't have a ':', nothing to do
            int colonPosition = line.IndexOf(':');
            if (colonPosition == -1)
            {
                yield break;
            }

            line = line.Substring(colonPosition + 1).Trim();
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
            if (string.IsNullOrEmpty(line) ||
               (line.StartsWith("#") && !(line.IndexOf(CodeOwnerEntry.MissingFolder, StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                return;
            }

            line = ParsePath(line);

            //remove any comments from the line, if any.
            // this is the case when we have something like @user #comment
            int commentIndex = line.IndexOf("#");

            if (commentIndex >= 0)
            {
                line = line.Substring(0, commentIndex).Trim();
            }

            foreach (string author in SplitLine(line, OwnerSeparator).ToList())
            {
                if (!string.IsNullOrWhiteSpace(author))
                {
                    Owners.Add(author.Trim());
                }
            }
        }

        private string ParsePath(string line)
        {
            // Get the start of the owner in the string
            int ownerStartPosition = line.IndexOf('@');
            if (ownerStartPosition == -1)
            {
                return line;
            }

            string path = line.Substring(0, ownerStartPosition).Trim();
            // the first entry is the path/regex
            PathExpression = path;

            // remove the path from the string.
            return line.Substring(ownerStartPosition);
        }

        /// <summary>
        /// Remove all code owners which are not github alias.
        /// </summary>
        public void FilterOutNonUserAliases()
        {
            Owners.RemoveAll(r => !IsGitHubUserAlias(r));
        }


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
    }
}
