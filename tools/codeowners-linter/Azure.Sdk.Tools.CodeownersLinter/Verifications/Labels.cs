using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Errors;
using Azure.Sdk.Tools.CodeownersLinter.Utils;
using Azure.Sdk.Tools.CodeOwnersParser.Constants;

namespace Azure.Sdk.Tools.CodeownersLinter.Verifications
{
    public static class Labels
    {
        /// <summary>
        /// Parse the labels from a given CODEOWNERS line
        /// </summary>
        /// <param name="line">The line to parse labels from</param>
        /// <returns>Empty List&lt;string&gt; if there were no labels otherwise the List&lt;string&gt; containing the labels</returns>
        public static List<string> ParseLabelsFromLine(string line)
        {
            // This might look a bit odd but old syntax for labels required they start with % because ServiceLabel entries contained
            // their service label + the Service Attention label. The new syntax just requires a single label be after the moniker
            // which ends with a colon. This verification needs to be able to handle both.
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
        /// Verify that the labels, on a given CODEOWNERS metadata line, are defined for the repository.
        /// </summary>
        /// <param name="repoLabelData">The repository label data</param>
        /// <param name="line">The CODEOWNERS line to parse</param>
        /// <param name="moniker">The moniker being processed. Necessary for determining the number of allowed labels.</param>
        /// <param name="errorStrings">List &lt;string&gt, any error strings are added to this list.</param>
        public static void VerifyLabels(RepoLabelDataUtils repoLabelData, string line, string moniker, List<string> errorStrings)
        {
            List<string> labels = ParseLabelsFromLine(line);

            if (labels.Count == 0)
            {
                errorStrings.Add(ErrorMessageConstants.MissingLabelForMoniker);
            }

            // The PRLabel moniker is limited to a single label but, the parser will
            // still parse multiple labels like it does today.
            if (MonikerConstants.PRLabel == moniker)
            {
                if (labels.Count > 1)
                {
                    errorStrings.Add(ErrorMessageConstants.TooManyPRLabels);
                }
                // Regardless of the number of labels on the moniker, ServiceAttention should not be there.
                if (labels.Contains(LabelConstants.ServiceAttention, StringComparer.InvariantCultureIgnoreCase))
                {
                    errorStrings.Add(ErrorMessageConstants.ServiceAttentionIsNotAValidPRLabel);
                }
            }
            else if (MonikerConstants.ServiceLabel == moniker)
            {
                // The ServiceLabel moniker should have a max of 2 labels if one of them is Service Attention. If
                // there are more than 2 labels or there are exactly 2 labels and one of them isn't Service Attention
                // then give the error
                if ((labels.Count > 2) ||
                    (labels.Count == 2 &&
                     !labels.Contains(LabelConstants.ServiceAttention, StringComparer.InvariantCultureIgnoreCase)))
                {
                    errorStrings.Add(ErrorMessageConstants.TooManyServiceLabels);
                }
                // If the only label on the moniker is ServiceAttention then it's an error.
                else if (labels.Count == 1 && labels.Contains(LabelConstants.ServiceAttention, StringComparer.InvariantCultureIgnoreCase))
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
