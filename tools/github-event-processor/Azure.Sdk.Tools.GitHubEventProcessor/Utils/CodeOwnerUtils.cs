using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Octokit;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using System.Reflection;

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
        /// <returns>CodeownersEntry whose ServiceLabel list is fully contained within the list of labels or an empty CodeownersEntry</returns>
        public static CodeownersEntry GetCodeownersEntryForLabelList(IReadOnlyList<Label> labels)
        {
            List<string> labelOnlyList = labels.Select(l => l.Name).ToList();
            return GetCodeownersEntryForLabelList(labelOnlyList);
        }

        /// <summary>
        /// This function will start from end of the CODEOWNERS entries and work it's way back until
        /// a CodeownersEntry whose list of ServiceLabels is fully contained in the list of labels 
        /// that's passed in. The list of labels will be coming from the AI Label Service or the list
        /// list of labels from an Issue payload.
        /// </summary>
        /// <param name="labelList">List of labels</param>
        /// <returns>CodeownersEntry whose ServiceLabel list is fully contained within the list of labels or an empty CodeownersEntry</returns>
        public static CodeownersEntry GetCodeownersEntryForLabelList(List<string> labelList)
        {
            var codeOwnerEntries = GetCodeOwnerEntries();
            // While a foreach might look nicer, it would require using IEnumerable.Reverse which
            // is known to have abysmal performance. This code also reads better and is far easier
            // for everyone to understand.
            for (int i = codeOwnerEntries.Count - 1; i >= 0; i--)
            {
                if (codeOwnerEntries[i].ServiceLabels.Count > 0)
                {
                    // Yes, this is a copy to keep the original list in tack but the ServiceLabel list should only have 1-3 labels
                    var tempServiceLabels = codeOwnerEntries[i].ServiceLabels.ToList();
                    // Unfortunately, some ServiceLabel entries can still have Service Attention which needs to be removed.
                    // Also unfortunately, there's no List<T>.Remove that's case insensitive
                    tempServiceLabels.RemoveAll(o => o.Equals(TriageLabelConstants.ServiceAttention, StringComparison.OrdinalIgnoreCase));
                    if (labelList.Intersect(tempServiceLabels, StringComparer.OrdinalIgnoreCase).Count() == tempServiceLabels.Count)
                    {
                        return codeOwnerEntries[i];
                    }
                }
            }
            // No matches, just return an empty CodeownersEntry
            return new CodeownersEntry();
        }

        /// <summary>
        /// Common utility function to take a list of owners and format it into a string that
        /// @ mentions all of them which can be added to a GitHub comment.
        /// </summary>
        /// <param name="ownerList">The list of owners</param>
        /// <returns>The string which contains a unique list of individuals/teams with the names formatted to @mention in github or null if there aren't any.</returns>
        public static string CreateAtMentionForOwnerList(List<string> ownerList)
        {
            var tempOwnerList = ownerList.OrderBy(e => e).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string partiesToMention = null;
            foreach (string owner in tempOwnerList)
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
