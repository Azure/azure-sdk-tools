using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Octokit;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    /// <summary>
    /// Codeowners utility function wrapper. 
    /// </summary>
    public class CodeOwnerUtils
    {
        public static readonly string CodeownersFileName = "CODEOWNERS";
        public static readonly string CodeownersSubDirectory = ".github";

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
                _codeOwnerEntries = CodeownersParser.ParseCodeownersFile(codeOwnersFilePath);
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
        /// Given a list of files from a pull request, return the list of PR serviceLabels that
        /// that need to get added to the PR.
        /// </summary>
        /// <param name="prLabels">the list of serviceLabels on the PR</param>
        /// <param name="prFiles">the list of files in the PR</param>
        /// <returns>String list of serviceLabels that need to get added to the PR</returns>
        public static List<string> GetPRAutoLabelsForFilePaths(IReadOnlyList<Label> prLabels, IReadOnlyList<PullRequestFile> prFiles)
        {
            List<string> labelsToAdd = new List<string>();
            var codeOwnerEntries = GetCodeOwnerEntries();

            foreach (var prFile in prFiles)
            {
                var codeOwnerEntry = CodeownersParser.GetMatchingCodeownersEntry(prFile.FileName, codeOwnerEntries);
                foreach (var prLabel in codeOwnerEntry.PRLabels)
                {
                    // If PR doesn't already has the label and the label isn't already in the return list
                    if (!LabelUtils.HasLabel(prLabels, prLabel) && !labelsToAdd.Contains(prLabel, StringComparer.OrdinalIgnoreCase))
                    {
                        labelsToAdd.Add(prLabel);
                    }
                }
            }
            return labelsToAdd;
        }

        /// <summary>
        /// Overloaded function to convert the list of Octokit.Labels to a list of string
        /// </summary>
        /// <param name="labels">The list of Octokit.Label</param>
        /// <returns>The string which contains a unique list of individuals/teams with the names formatted to @mention in github</returns>
        public static List<string> GetServiceOwnersForServiceLabels(IReadOnlyList<Label> labels)
        {
            List<string> labelOnlyList = labels.Select(l => l.Name).ToList();
            return GetServiceOwnersForServiceLabels(labelOnlyList);
        }

        /// <summary>
        /// When Service Attention is added to an issue, one or more individual teams will need to be
        /// @mentioned based upon the serviceLabels in the issue. Given the list of serviceLabels on the issue,
        /// retrieve the list of individuals or teams to @mention.
        /// </summary>
        /// <param name="serviceLabels">The list of serviceLabels to get owners to @ mention</param>
        /// <returns>A Distinct list containing the owners belonging to the labels or an empty list if there are none.</returns>
        public static List<string> GetServiceOwnersForServiceLabels(List<string> serviceLabels)
        {
            List<string> partiesToMentionList = new List<string>();
            var codeOwnerEntries = GetCodeOwnerEntries();
            foreach (string label in serviceLabels)
            {
                // This is just a safeguard considering that almost every existing CodeownersEntry with
                // ServiceLabels has ServiceAttention in its list and accidentally mentioning half the
                // repository should be avoided.
                if (string.Equals(label, LabelConstants.ServiceAttention))
                {
                    continue;
                }
                // Get the list of all entries that have this ServiceLabel
                var matches = codeOwnerEntries.Where(entry => entry.ServiceLabels.Contains(label, StringComparer.OrdinalIgnoreCase));
                foreach (var codeownersEntry in matches)
                {
                    partiesToMentionList.AddRange(codeownersEntry.ServiceOwners);
                }
            }

            // Return the distinct list performed with a case insensitive comparison. This needs to be done this way
            // because GitHub is case insensitive but case preserving and an owner could appear in the same CODEOWNERS
            // file with different casings. Eg. SomeUser or someuser
            return partiesToMentionList.OrderBy(e => e).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Return a distinct list of AzureSdkOwners for a given list of ServiceLabels.
        /// </summary>
        /// <param name="serviceLabels">The list of serviceLabels to get owners to @ mention</param>
        /// <returns>A Distinct list containing the owners belonging to the labels or an empty list if there are none.</returns>
        public static List<string> GetAzureSdkOwnersForServiceLabels(List<string> serviceLabels)
        {
            List<string> azureSdkOwners = new List<string>();
            var codeOwnerEntries = GetCodeOwnerEntries();
            foreach (string label in serviceLabels)
            {
                // This is just a safeguard considering that almost every existing CodeownersEntry with
                // ServiceLabels has ServiceAttention in its list and accidentally mentioning half the
                // repository should be avoided.
                if (string.Equals(label, LabelConstants.ServiceAttention))
                {
                    continue;
                }
                // Get the list of all entries that have this ServiceLabel
                var matches = codeOwnerEntries.Where(entry => entry.ServiceLabels.Contains(label, StringComparer.OrdinalIgnoreCase));
                foreach (var codeownersEntry in matches)
                {
                    azureSdkOwners.AddRange(codeownersEntry.AzureSdkOwners);
                }
            }

            // Normally ExcludeNonUserAliases would be run on the CodeownersEntry but that strips all team aliases
            // from AzureSdkOwners, ServiceOwners and SourceOwners. Only AzureSdkOwners need to be stripped since,
            // if this method is being called, it's going to be used for issue assignment which can't be assigned
            // to a team. ServiceOwners can still have an unexpanded team which would be used for an @ mention.
            azureSdkOwners.RemoveAll(r => ParsingUtils.IsGitHubTeam(r));

            // Return the distinct list performed with a case insensitive comparison. This needs to be done this way
            // because GitHub is case insensitive but case preserving and an owner could appear in the same CODEOWNERS
            // file with different casings. Eg. SomeUser or someuser
            return azureSdkOwners.OrderBy(e => e).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Common utility function to take a list of owners and format it into a string that
        /// @ mentions all of them which can be added to a GitHub comment.
        /// </summary>
        /// <param name="ownerList">The list of owners</param>
        /// <returns>The string which contains a unique list of individuals/teams with the names formatted to @mention in github or null if there aren't any.</returns>
        public static string CreateAtMentionForOwnerList(List<string> ownerList)
        {
            string partiesToMention = null;
            foreach (string owner in ownerList)
            {
                if (owner.StartsWith("@"))
                {
                    if (null == partiesToMention)
                    {
                        partiesToMention = owner;
                    }
                    else
                    {
                        partiesToMention += " " + owner;
                    }
                }
                else
                {
                    if (null == partiesToMention)
                    {
                        partiesToMention = "@" + owner;
                    }
                    else
                    {
                        partiesToMention += " @" + owner;
                    }
                }
            }
            return partiesToMention;
        }
    }
}
