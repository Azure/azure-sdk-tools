using System;
using System.Collections.Generic;
using System.Linq;

using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersUtils.Editing
{
    public class CodeownersEditor
    {
        private string codeownersContent;
        private const string AzureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";
        private static readonly string standardServiceCategory = "# Client Libraries";
        private static readonly string standardManagementCategory = "# Management Libraries";

        public CodeownersEditor(string codeownersContent)
        {
            this.codeownersContent = codeownersContent ?? throw new ArgumentNullException(nameof(codeownersContent));
        }

        /// <summary>
        /// Adds a new codeowners entry or updates an existing one, based on path or service label.
        /// Updates the related codeowners content string to include the modified codeowners entry.
        /// Handles all normalization and validation internally.
        /// </summary>
        /// <param name="path">The path for the codeowners entry (optional if serviceLabel is provided).</param>
        /// <param name="serviceLabel">The service label for the codeowners entry (optional if path is provided).</param>
        /// <param name="serviceOwners">List of service owners to add/update.</param>
        /// <param name="sourceOwners">List of source owners to add/update.</param>
        /// <param name="isMgmtPlane">Whether this is a management-plane entry.</param>
        /// <returns>The added or updated CodeownersEntry.</returns>
        public CodeownersEntry AddOrUpdateEntry(string path = "", string serviceLabel = "", List<string> serviceOwners = null, List<string> sourceOwners = null, bool isMgmtPlane = false)
        {
            // Normalize path
            string normalizedPath = path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                normalizedPath = CodeownersHelper.NormalizePath(path);
            }

            var codeownersContentList = codeownersContent.Split('\n').ToList();

            var (startLine, endLine) = isMgmtPlane
                ? CodeownersHelper.FindBlock(codeownersContent, standardManagementCategory)
                : CodeownersHelper.FindBlock(codeownersContent, standardServiceCategory);

            var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, AzureWriteTeamsBlobUrl, startLine, endLine);
            var matchingEntry = CodeownersHelper.FindMatchingEntries(codeownersEntries, normalizedPath, serviceLabel);

            CodeownersEntry updatedEntry;
            if (matchingEntry != null)
            {
                updatedEntry = CodeownersHelper.UpdateCodeownersEntry(matchingEntry, serviceOwners, sourceOwners, true);
                codeownersContent = CodeownersHelper.AddCodeownersEntryToFile(codeownersEntries, codeownersContent, updatedEntry, true);
            }
            else
            {
                updatedEntry = CodeownersHelper.CreateCodeownersEntry(normalizedPath, serviceLabel, serviceOwners, sourceOwners, isMgmtPlane);
                codeownersContent = CodeownersHelper.AddCodeownersEntryToFile(codeownersEntries, codeownersContent, updatedEntry, false);
            }
            return updatedEntry;
        }

        /// <summary>
        /// Removes owners from an existing codeowners entry, based on path or service label.
        /// Updates the related codeowners content string to include the modified codeowners entry.
        /// Handles all normalization internally.
        /// </summary>
        /// <param name="path">The path for the codeowners entry (optional if serviceLabel is provided).</param>
        /// <param name="serviceLabel">The service label for the codeowners entry (optional if path is provided).</param>
        /// <param name="serviceOwnersToRemove">List of service owners to remove.</param>
        /// <param name="sourceOwnersToRemove">List of source owners to remove.</param>
        /// <param name="isMgmtPlane">Whether this is a management-plane entry.</param>
        /// <returns>The updated CodeownersEntry after removal.</returns>
        public CodeownersEntry RemoveOwners(string path = "", string serviceLabel = "", List<string> serviceOwnersToRemove = null, List<string> sourceOwnersToRemove = null, bool isMgmtPlane = false)
        {
            string normalizedPath = path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                normalizedPath = CodeownersHelper.NormalizePath(path);
            }

            var codeownersContentList = codeownersContent.Split('\n').ToList();

            var (startLine, endLine) = isMgmtPlane
                ? CodeownersHelper.FindBlock(codeownersContent, standardManagementCategory)
                : CodeownersHelper.FindBlock(codeownersContent, standardServiceCategory);

            var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, AzureWriteTeamsBlobUrl, startLine, endLine);
            var matchingEntry = CodeownersHelper.FindMatchingEntries(codeownersEntries, normalizedPath, serviceLabel);

            if (matchingEntry == null)
            {
                throw new InvalidOperationException("No matching entry found to remove owners from.");
            }
            var updatedEntry = CodeownersHelper.UpdateCodeownersEntry(matchingEntry, serviceOwnersToRemove, sourceOwnersToRemove, false);
            codeownersContent = CodeownersHelper.AddCodeownersEntryToFile(codeownersEntries, codeownersContent, updatedEntry, true);
            return updatedEntry;
        }

        public override string ToString()
        {
            return codeownersContent;
        }
    }
}
