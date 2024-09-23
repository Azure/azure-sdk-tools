using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// The parsing utils contain the methods for parsing labels and owners from CODEOWNERS lines. It also
    /// contains the method for determining whether or not a given line is a moniker or source path line (aka
    /// something that needs to be parsed)
    /// </summary>
    public static class ParsingUtils
    {

        /// <summary>
        /// Parse the source path a CODEOWNERS line
        /// </summary>
        /// <param name="line">The line to parse labels from</param>
        /// <returns>The source path</returns>
        public static string ParseSourcePathFromLine(string line)
        {
            string pathWithoutOwners = line;
            // Owners, or lack thereof, are tracked elsewhere.
            if (pathWithoutOwners.Contains(SeparatorConstants.Owner))
            {
                // Grab the string up to the character before the owner constant
                pathWithoutOwners = pathWithoutOwners.Substring(0, pathWithoutOwners.IndexOf(SeparatorConstants.Owner));
            }
            pathWithoutOwners = pathWithoutOwners.Substring(0).Replace('\t', ' ').Trim();
            return pathWithoutOwners;
        }

        /// <summary>
        /// Parse the labels from a given CODEOWNERS line
        /// </summary>
        /// <param name="line">The line to parse labels from</param>
        /// <returns>Empty List&lt;string&gt; if there were no labels otherwise the List&lt;string&gt; containing the labels</returns>
        public static List<string> ParseLabelsFromLine(string line)
        {
            // This might look a bit odd but syntax for labels required they start with % because entries can contain
            // multiple labels. If there's no % sign it's assumed that everything after the : is the label.
            List<string> labels = new List<string>();
            string lineWithoutMoniker = line.Substring(line.IndexOf(SeparatorConstants.Colon) + 1).Trim();
            if (!string.IsNullOrWhiteSpace(lineWithoutMoniker))
            {
                if (lineWithoutMoniker.Contains(SeparatorConstants.Label))
                {
                    labels.AddRange(lineWithoutMoniker.Split(SeparatorConstants.Label, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());
                }
                else
                {
                    labels.Add(lineWithoutMoniker);
                }
            }
            return labels;
        }

        /// <summary>
        /// Parse the owners from a given CODEOWNERS line
        /// </summary>
        /// <param name="line">The line to parse owners from</param>
        /// <param name="expandTeams">Whether or not to expand teams into their lists of owners when parsing. Linting will not expand teams but parsing will.</param>
        /// <returns>Empty List&lt;string&gt; if there were no users otherwise the List&lt;string&gt; containing the users</returns>
        public static List<string> ParseOwnersFromLine(OwnerDataUtils ownerData, string line, bool expandTeams)
        {
            // If there are no SeparatorConstants.Owner in the string then the string isn't formatted correctly.
            // Every team/user needs to start with @ in the codeowners file. Split the codeownersLine on the
            // SeparatorConstants.Owner character, the first entry is everything prior to the character and the users
            // are all entries after that.
            if (!line.Contains(SeparatorConstants.Owner))
            {
                // return an empty the list
                return new List<string>();
            }
            string justOwners = line.Substring(line.IndexOf(SeparatorConstants.Owner));
            List<string> ownersWithoutTeamExpansion = justOwners.Split(SeparatorConstants.Owner, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (!expandTeams)
            {
                return ownersWithoutTeamExpansion;
            }
            List<string> ownersWithTeamExpansion = new List<string>();
            foreach (string owner in ownersWithoutTeamExpansion)
            {
                if (IsAzureTeam(owner))
                {
                    // If the list comes back empty it means it wasn't an azure-sdk-write team. At
                    // this point just add the non-expanded team entry which is the behavior of
                    // the parser today
                    var expandedTeam = ownerData.ExpandTeam(owner);
                    if (expandedTeam.Count == 0)
                    {
                        ownersWithTeamExpansion.Add(owner);
                    }
                    else
                    {
                        // Don't bother doing the union here to make the distinct list,
                        // that'll be done below
                        ownersWithTeamExpansion.AddRange(expandedTeam);
                    }
                }
            }
            // Ensure that any owners that are on the line, which are also part of any teams on the line, only
            // exist once in the list. Because git is case insensitive but case preserving the distinct list
            // need to do the case insensitive comparison
            return ownersWithTeamExpansion.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Check whether or not a given CODEOWNERS line is a moniker or source line.
        /// </summary>
        /// <param name="line">string, the line to check</param>
        /// <returns>true if the line is a moniker or source line, false otherwise</returns>
        public static bool IsMonikerOrSourceLine(string line)
        {
            // If the line is blank or whitespace. Note, 
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }
            // if the line isn't blank or whitespace and isn't a comment then
            // it's a source path line
            else if (!line.StartsWith(SeparatorConstants.Comment))
            {
                return true;
            }
            // At this point it's either a moniker or a comment
            else
            {
                return MonikerUtils.IsMonikerLine(line);
            }
        }

        /// <summary>
        /// Given a line, check to see if the line is a source path/owner line. Basically, any line in CODEOWNERS
        /// that isn't a comment and isn't blank is considered to be a source path/owner line.
        /// </summary>
        /// <param name="line">string, the CODEOWNERS line to check</param>
        /// <returns>true, if the line is a source path/owner line, false otherwise</returns>
        public static bool IsSourcePathOwnerLine(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                   !line.StartsWith(SeparatorConstants.Comment);
        }

        /// <summary>
        /// Given the first line number of a block, find the block's end line number. The end block line number can be
        /// the same as the start block line number if it's a source path line or a single, dangling moniker if the
        /// block is malformatted.
        /// </summary>
        /// <param name="startBlockLineNumber">The starting line number of the block</param>
        /// <param name="codeownersFile">The List&lt;string&gt; that represents the CODEOWNERS file</param>
        /// <returns>int, the line number of the block's end</returns>
        public static int FindBlockEnd(int startBlockLineNumber, List<string> codeownersFile)
        {
            // The block end will be a source path/owners line, the end of the file or the line prior to a blank line.
            int endBlockLineNumber;

            for (endBlockLineNumber = startBlockLineNumber; endBlockLineNumber < codeownersFile.Count; endBlockLineNumber++)
            {
                string line = codeownersFile[endBlockLineNumber].Trim();
                // Blank lines aren't part of any block. If a blank line is encountered, the line prior to the blank line is
                // the end of the block.
                if (string.IsNullOrWhiteSpace(line))
                {
                    // This function needs to be called with the start of a block which is not a blank line. If things get
                    // here, the calling method is in error.
                    if (startBlockLineNumber == endBlockLineNumber)
                    {
                        throw new ArgumentException($"The block starting at line number, {startBlockLineNumber + 1}, was a blank line, which cannot the start of a block.");
                    }
                    endBlockLineNumber--;
                    break;
                }
                // If the line starts with a comment then it might not the end of the block, go to the next line
                else if (line.StartsWith(SeparatorConstants.Comment))
                {
                    continue;
                }
                // If the line isn't null or whitespace and isn't a comment, then it's a source path/owner line
                // which is the end of the block
                else
                {
                    break;
                }
            }
            // This is the case where the very last line was a block or a source path/owner line.
            // Set the end of the block to the last line.
            if (endBlockLineNumber >= codeownersFile.Count)
            {
                endBlockLineNumber = codeownersFile.Count - 1;
            }
            return endBlockLineNumber;
        }

        /// <summary>
        /// Determine whether or not the owner is a team. Note, only Azure teams are allowed since all
        /// teams in CODEOWNERS files need to be under azure-sdk-write. If the team name doesn't begin
        /// with "Azure/" then it's not considered a team and later processing won't attempt to expand it.
        /// </summary>
        /// <param name="owner">The owner to check</param>
        /// <returns>True if the owner is a GitHub Azure team, false otherwise.</returns>
        public static bool IsAzureTeam(string owner)
        {

            if (owner.StartsWith($"{OrgConstants.Azure}/{SeparatorConstants.Team}"))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Helper method to check if an owner is a GitHub team. This differs from the 
        /// OwnerIsAzureTeam, because it's only used to exclude owner aliases from
        /// Codeowners entries.
        /// </summary>
        /// <param name="owner">The owner to check.</param>
        /// <returns>True if it is a GitHub team, false otherwise.</returns>
        public static bool IsGitHubTeam(string owner)
        {
            if (owner.Contains(SeparatorConstants.Team))
            {
                return true;
            }
            return false;
        }
    }
}
