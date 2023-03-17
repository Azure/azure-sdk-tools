using Octokit;
using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    public class LabelUtils
    {
        /// <summary>
        /// Check whether or not an label exists in a list of labels. This will be used on issues,
        /// and PRs, both of which contain a list of labels
        /// </summary>
        /// <param name="labels">IReadOnlyList of labels</param>
        /// <param name="labelToCheck">The label to look for on the issue</param>
        /// <returns>true if the label exists, false otherwise</returns>
        public static bool HasLabel(IReadOnlyList<Label> labels, string labelToCheck)
        {
            if (labels != null)
            {
                foreach (Label label in labels)
                {
                    if (label.Name.Equals(labelToCheck, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
