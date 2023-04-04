using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azure.Sdk.Tools.CodeOwnersParser;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    /// <summary>
    /// Codeowners utility function wrapper. 
    /// </summary>
    public class CodeOwnerUtils
    {
        private static readonly string CodeownersFileName = "CODEOWNERS";
        private static readonly string CodeownersSubDirectory = ".github";

        static List<CodeownersEntry> _codeOwnerEntries = null;
        public static string codeOwnersFilePathOverride = null;

        /// <summary>
        /// Return the codeowners file path. It'll either be the overridden path, used for testing
        /// or it'll search for it in the repository under the well known location which is the 
        /// .github directory off the root of the repository.
        /// </summary>
        /// <returns>CODEOWNERS file path</returns>
        public static string GetCodeOwnersFilePath()
        {
            if (null != codeOwnersFilePathOverride)
            {
                // If the user overrode the location
                return codeOwnersFilePathOverride;
            }
            else
            {
                return DirectoryUtils.FindFileInRepository(CodeownersFileName, CodeownersSubDirectory);
            }
        }

        /// <summary>
        /// Wrapper function so don't end having to load the CODEOWNERS file multiple
        /// times if there's more than one call to get CodeOwnerEntries for information.
        /// </summary>
        /// <returns>List of CodeownersEntry</returns>
        public static List<CodeownersEntry> GetCodeOwnerEntries()
        {
            if (_codeOwnerEntries == null)
            {
                string codeOwnersFilePath = GetCodeOwnersFilePath();
                Console.WriteLine($"Loading codeowners file, {codeOwnersFilePath}");
                _codeOwnerEntries = CodeownersFile.GetCodeownersEntriesFromFileOrUrl(codeOwnersFilePath);
            }
            return _codeOwnerEntries;
        }

        /// <summary>
        /// Normally, there's only one CODEOWNER file per repository which is loaded and
        /// saved for the life of the action. This function is needed for static testing, 
        /// which loads fake CODEOWNER files for test scenarios.
        /// </summary>
        /// <returns>void</returns>
        public static void ResetCodeOwnerEntries()
        {
            if (_codeOwnerEntries != null)
            {
                _codeOwnerEntries.Clear();
                _codeOwnerEntries = null;
            }
        }


        /// <summary>
        /// Given a list of files from a pull request, return the list of PR labels that
        /// that need to get added to the PR.
        /// </summary>
        /// <param name="prLabels">the list of labels on the PR</param>
        /// <param name="prFiles">the list of files in the PR</param>
        /// <returns>String list of labels that need to get added to the PR</returns>
        public static List<string> GetPRAutoLabelsForFilePaths(IReadOnlyList<Label> prLabels, IReadOnlyList<PullRequestFile> prFiles)
        {
            List<string> labelsToAdd = new List<string>();
            var codeOwnerEntries = GetCodeOwnerEntries();

            foreach (var prFile in prFiles)
            {
                var codeOwnerEntry = CodeownersFile.GetMatchingCodeownersEntry(prFile.FileName, codeOwnerEntries);
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
        /// <returns>The string which contains a unique list of individuals/teams with the names formatted to @mention in github</returns>
        public static string GetPartiesToMentionForServiceAttention(IReadOnlyList<Label> labels)
        {
            List<string> partiesToMentionList = new List<string>();
            var codeOwnerEntries = GetCodeOwnerEntries();
            foreach(var codeOwnerEntry in codeOwnerEntries)
            {
                foreach (var serviceLabel in codeOwnerEntry.ServiceLabels)
                {
                    // Skip the Service Attention label. It's the other labels, that
                    // aren't Service Attention, that determine which people get added.
                    if (serviceLabel == LabelConstants.ServiceAttention)
                    {
                        continue;
                    }
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
