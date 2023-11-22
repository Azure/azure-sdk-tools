using Octokit;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    public class LabelUtils
    {
        /// <summary>
        /// Overload that takes the Octokit's IReadOnlyList of Labels and converts it
        /// to a string list.
        /// </summary>
        /// <param name="labels">IReadOnlyList of labels</param>
        /// <param name="labelToCheck">The label to look for on the issue</param>
        /// <returns>true if the label exists, false otherwise</returns>
        public static bool HasLabel(IReadOnlyList<Label> labels, string labelToCheck)
        {
            return HasLabel(labels.Select(l => l.Name).ToList(), labelToCheck);
        }

        /// <summary>
        /// Check whether or not an label exists in a list of labels. This will be used on issues,
        /// and PRs, both of which contain a list of labels
        /// </summary>
        /// <param name="labels">List of labels</param>
        /// <param name="labelToCheck">The label to look for in the list</param>
        /// <returns>true if the label exists, false otherwise</returns>
        public static bool HasLabel(List<string> labels, string labelToCheck)
        {
            if (labels != null)
            {
                if (labels.Contains(labelToCheck, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
