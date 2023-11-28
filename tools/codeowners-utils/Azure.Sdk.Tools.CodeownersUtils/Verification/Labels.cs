using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.CodeownersUtils.Verification
{
    /// <summary>
    /// Verification class for CODEOWNERS lines that contain labels
    /// </summary>
    public static class Labels
    {
        /// <summary>
        /// Verify that the labels, on a given CODEOWNERS metadata line, are defined for the repository.
        /// </summary>
        /// <param name="repoLabelData">The repository label data</param>
        /// <param name="line">The CODEOWNERS line to parse</param>
        /// <param name="moniker">The moniker being processed. Necessary for determining the number of allowed labels.</param>
        /// <param name="errorStrings">List &lt;string&gt, any error strings are added to this list.</param>
        public static void VerifyLabels(RepoLabelDataUtils repoLabelData, string line, string moniker, List<string> errorStrings)
        {
            List<string> labels = ParsingUtils.ParseLabelsFromLine(line);

            if (labels.Count == 0)
            {
                errorStrings.Add(ErrorMessageConstants.MissingLabelForMoniker);
            }

            // The Service Attention label is not valid in a PRLabel moniker
            if (MonikerConstants.PRLabel == moniker)
            {
                // Regardless of the number of labels on the moniker, ServiceAttention should not be there.
                if (labels.Contains(LabelConstants.ServiceAttention, StringComparer.InvariantCultureIgnoreCase))
                {
                    errorStrings.Add(ErrorMessageConstants.ServiceAttentionIsNotAValidPRLabel);
                }
            }
            // ServiceLabel cannot only have Service Attention
            else if (MonikerConstants.ServiceLabel == moniker)
            {
                if (labels.Count == 1 && labels.Contains(LabelConstants.ServiceAttention, StringComparer.InvariantCultureIgnoreCase))
                {
                    errorStrings.Add(ErrorMessageConstants.ServiceLabelMustContainAServiceLabel);
                }
            }

            // Verify each label in the list exists
            foreach (string label in labels)
            {
                if (!repoLabelData.LabelInRepo(label))
                {
                    errorStrings.Add($"'{label}'{ErrorMessageConstants.InvalidRepositoryLabelPartial}");
                }
            }
            return;
        }
    }
}
