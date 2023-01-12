using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azure.Sdk.Tools.CodeOwnersParser;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    public class CodeOwnerUtils
    {
        static List<CodeOwnerEntry> _codeOwnerEntries = null;
        public static string codeOwnersFilePathOverride = null;

        internal static string GetCodeOwnersFilePath()
        {
            if (null != codeOwnersFilePathOverride)
            {
                // If the user overrode the location
                return codeOwnersFilePathOverride;
            }
            else
            {
                return DirectoryUtils.FindFileInRepository("CODEOWNERS", ".github");
            }
        }

        /// <summary>
        /// Wrapper function so don't end having to load the CODEOWNERS file multiple
        /// times if there's more than one call to get CodeOwnerEntries for information.
        /// </summary>
        /// <returns></returns>
        public static List<CodeOwnerEntry> GetCodeOwnerEntries()
        {
            if (_codeOwnerEntries == null)
            {
                _codeOwnerEntries = CodeOwnersFile.ParseFile(GetCodeOwnersFilePath());
            }
            return _codeOwnerEntries;
        }

        /// <summary>
        /// Given a list of files from a pull request, return the list of PR labels that
        /// that need to get added to the PR.
        /// </summary>
        /// <param name="prLabels">the list of labels on the PR</param>
        /// <param name="prFiles">the list of files in the PR</param>
        /// <returns>unique list of PR labels that need to get added to the PR</returns>
        public static List<string> GetPRAutoLabelsForFilePaths(IReadOnlyList<Label> prLabels, IReadOnlyList<PullRequestFile> prFiles)
        {
            List<string> labelsToAdd = new List<string>();
            var codeOwnerEntries = GetCodeOwnerEntries();

            foreach (var prFile in prFiles)
            {
                var codeOwnerEntry = CodeOwnersFile.FindOwnersForClosestMatch(codeOwnerEntries, prFile.FileName);
                foreach (var prLabel in codeOwnerEntry.PRLabels)
                {
                    // If PR doesn't already has the label and the label isn't already in the return list
                    if (!LabelUtils.HasLabel(prLabels, prLabel) && !labelsToAdd.Contains(prLabel))
                    {
                        labelsToAdd.Add(prLabel);
                    }
                }
            }
            return labelsToAdd;
        }

        /// <summary>
        /// When Service Attention is added to an issue, one or more individual teams will need to be
        /// @mentioned based upon the labels in the issue. Given the list of labels on the issue,
        /// retrieve the list of individuals or teams to @mention.
        /// </summary>
        /// <param name="labels">the list of labels on the PR</param>
        /// <returns>unique list of individuals/teams to @mention</returns>
        public static string GetPartiesToMentionForServiceAttention(IReadOnlyList<Label> labels)
        {
            List<string> partiesToMentionList = new List<string>();
            var codeOwnerEntries = GetCodeOwnerEntries();
            foreach(var codeOwnerEntry in codeOwnerEntries)
            {
                foreach (var serviceLabel in codeOwnerEntry.ServiceLabels)
                {
                    if (LabelUtils.HasLabel(labels, serviceLabel))
                    {
                        foreach (var owner in codeOwnerEntry.Owners)
                        {
                            if (!partiesToMentionList.Contains(owner))
                            {
                                partiesToMentionList.Add(owner);
                            }
                        }
                        // At this point it doesn't matter if any of the other service labels for
                        // this particular CodeOwnerEntry match, the list of owners is still the
                        // same and we can move on to the next to the next CodeOwnerEntry.
                        continue;
                    }
                }
            }
            string partiesToMention = null;
            foreach (string party in partiesToMentionList)
            {
                if (party.StartsWith("@"))
                {
                    if (null == partiesToMention)
                    {
                        partiesToMention = party;
                    }
                    else 
                    {
                        partiesToMention += " " + party;
                    }
                }
                else
                {
                    if (null == partiesToMention)
                    {
                        partiesToMention = "@"+party;
                    }
                    else
                    {
                        partiesToMention += " @" + party;
                    }
                }
            }
            return partiesToMention;
        }
    }
}
