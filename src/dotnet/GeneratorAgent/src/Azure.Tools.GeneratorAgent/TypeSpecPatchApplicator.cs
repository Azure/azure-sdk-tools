using System.Text.Json;
using Azure.Tools.GeneratorAgent.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Handles applying patches to TypeSpec files as proposed by the AI agent
    /// </summary>
    internal class TypeSpecPatchApplicator
    {
        private readonly TypeSpecFileVersionManager VersionManager;
        private readonly ILogger<TypeSpecPatchApplicator> Logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public TypeSpecPatchApplicator(
            TypeSpecFileVersionManager versionManager,
            ILogger<TypeSpecPatchApplicator> logger)
        {
            ArgumentNullException.ThrowIfNull(versionManager);
            ArgumentNullException.ThrowIfNull(logger);

            VersionManager = versionManager;
            Logger = logger;
        }

        /// <summary>
        /// Applies a patch to a TypeSpec file based on the agent's proposal
        /// </summary>
        /// <param name="patchJson">JSON patch proposal from the agent</param>
        /// <param name="typeSpecDirectory">Directory containing the TypeSpec files</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if patch was successfully applied, false otherwise</returns>
        public async Task<bool> ApplyPatchAsync(string patchJson, string typeSpecDirectory, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(patchJson);
            ArgumentNullException.ThrowIfNull(typeSpecDirectory);

            Logger.LogInformation("Applying patch to TypeSpec files...");

            try
            {
                // Step 1: Parse the patch request
                var patchRequest = ParsePatchRequest(patchJson);
                if (patchRequest == null)
                {
                    return false;
                }

                Logger.LogDebug("Parsed patch for file '{File}' with {ChangeCount} changes. Reason: {Reason}", 
                    patchRequest.File, patchRequest.Changes.Count, patchRequest.Reason);

                // Step 2: Validate the patch
                if (!ValidatePatch(patchRequest, typeSpecDirectory))
                {
                    return false;
                }

                // Step 3: Apply the changes
                return await ApplyChangesToFileAsync(patchRequest, typeSpecDirectory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error applying patch: {Error}", ex.Message);
                return false;
            }
        }

        private PatchRequest? ParsePatchRequest(string patchJson)
        {
            try
            {
                var patchRequest = JsonSerializer.Deserialize<PatchRequest>(patchJson, JsonOptions);
                if (patchRequest == null)
                {
                    Logger.LogError("Failed to deserialize patch request - result was null");
                    return null;
                }

                return patchRequest;
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Failed to parse patch JSON: {Error}", ex.Message);
                return null;
            }
        }

        private bool ValidatePatch(PatchRequest patchRequest, string typeSpecDirectory)
        {
            // Check file exists
            string filePath = Path.Combine(typeSpecDirectory, patchRequest.File);
            if (!File.Exists(filePath))
            {
                Logger.LogError("Target file does not exist: {FilePath}", filePath);
                return false;
            }

            // Check version consistency
            var currentFileInfo = VersionManager.GetFileMetadata(patchRequest.File);
            if (currentFileInfo != null && currentFileInfo.Version != patchRequest.FromVersion)
            {
                Logger.LogWarning("Version mismatch for {File}. Expected: {Expected}, Current: {Current}. Proceeding anyway...",
                    patchRequest.File, patchRequest.FromVersion, currentFileInfo.Version);
            }

            // Validate change count (safety check)
            if (patchRequest.Changes.Count > 20)
            {
                Logger.LogError("Patch contains too many changes ({Count}). Maximum allowed: 20", patchRequest.Changes.Count);
                return false;
            }

            // Validate change types
            foreach (var change in patchRequest.Changes)
            {
                if (string.IsNullOrWhiteSpace(change.Type) || 
                    !IsValidChangeType(change.Type))
                {
                    Logger.LogError("Invalid change type: {Type}", change.Type);
                    return false;
                }

                if (change.StartLine <= 0 || change.EndLine <= 0 || change.StartLine > change.EndLine)
                {
                    Logger.LogError("Invalid line numbers in change. Start: {Start}, End: {End}", change.StartLine, change.EndLine);
                    return false;
                }
            }

            Logger.LogDebug("Patch validation passed for {File}", patchRequest.File);
            return true;
        }

        private static bool IsValidChangeType(string changeType)
        {
            return changeType.Equals("replace", StringComparison.OrdinalIgnoreCase) ||
                   changeType.Equals("insert", StringComparison.OrdinalIgnoreCase) ||
                   changeType.Equals("delete", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> ApplyChangesToFileAsync(PatchRequest patchRequest, string typeSpecDirectory, CancellationToken cancellationToken)
        {
            string filePath = Path.Combine(typeSpecDirectory, patchRequest.File);
            
            try
            {
                // Read current file content
                string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
                Logger.LogDebug("Read {LineCount} lines from {File}", lines.Length, patchRequest.File);

                // Sort changes by line number in descending order to avoid line number shifts
                var sortedChanges = patchRequest.Changes
                    .OrderByDescending(c => c.StartLine)
                    .ToList();

                var modifiedLines = lines.ToList();

                // Apply each change
                foreach (var change in sortedChanges)
                {
                    if (!ApplySingleChange(change, modifiedLines))
                    {
                        Logger.LogError("Failed to apply change at lines {Start}-{End}", change.StartLine, change.EndLine);
                        return false;
                    }
                }

                // Write the modified content back to file
                await File.WriteAllLinesAsync(filePath, modifiedLines, cancellationToken).ConfigureAwait(false);

                // Update version manager with new content
                string newContent = string.Join(Environment.NewLine, modifiedLines);
                VersionManager.UpdateFileMetadata(patchRequest.File, newContent);

                Logger.LogInformation("Successfully applied {ChangeCount} changes to {File}", 
                    patchRequest.Changes.Count, patchRequest.File);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error applying changes to file {File}: {Error}", patchRequest.File, ex.Message);
                return false;
            }
        }

        private bool ApplySingleChange(PatchChange change, List<string> lines)
        {
            try
            {
                switch (change.Type.ToLowerInvariant())
                {
                    case "replace":
                        return ApplyReplaceChange(change, lines);
                    case "insert":
                        return ApplyInsertChange(change, lines);
                    case "delete":
                        return ApplyDeleteChange(change, lines);
                    default:
                        Logger.LogError("Unknown change type: {Type}", change.Type);
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error applying single change: {Error}", ex.Message);
                return false;
            }
        }

        private bool ApplyReplaceChange(PatchChange change, List<string> lines)
        {
            // Convert from 1-based to 0-based indexing
            int startIndex = change.StartLine - 1;
            int endIndex = change.EndLine - 1;

            // Handle "append to end" case when trying to add content beyond file end
            if (startIndex == lines.Count && string.IsNullOrEmpty(change.OldContent.Trim()))
            {
                var newLines = change.NewContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                lines.AddRange(newLines);
                return true;
            }

            if (startIndex < 0 || endIndex >= lines.Count || startIndex > endIndex)
            {
                Logger.LogError("Replace change line numbers out of bounds. Start: {Start}, End: {End}, Total lines: {Total}",
                    change.StartLine, change.EndLine, lines.Count);
                return false;
            }

            // Verify old content matches (optional validation)
            if (change.StartLine == change.EndLine) // Single line replace
            {
                string currentLine = lines[startIndex];
                if (!currentLine.Contains(change.OldContent.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Old content validation failed for line {Line}. Expected: '{Expected}', Found: '{Found}'",
                        change.StartLine, change.OldContent.Trim(), currentLine);
                    // Continue anyway - the agent might have a different view
                }
            }

            // Apply replacement
            if (startIndex == endIndex)
            {
                // Single line replacement
                lines[startIndex] = change.NewContent;
                Logger.LogDebug("Replaced line {Line}: '{OldContent}' → '{NewContent}'", 
                    change.StartLine, change.OldContent.Trim(), change.NewContent.Trim());
            }
            else
            {
                // Multi-line replacement
                var newLines = change.NewContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Remove old lines
                for (int i = endIndex; i >= startIndex; i--)
                {
                    lines.RemoveAt(i);
                }

                // Insert new lines
                lines.InsertRange(startIndex, newLines);
                
                Logger.LogDebug("Replaced lines {Start}-{End} with {NewLineCount} new lines", 
                    change.StartLine, change.EndLine, newLines.Length);
            }

            return true;
        }

        private bool ApplyInsertChange(PatchChange change, List<string> lines)
        {
            // Convert from 1-based to 0-based indexing
            int insertIndex = change.StartLine - 1;

            if (insertIndex < 0 || insertIndex > lines.Count)
            {
                Logger.LogError("Insert change line number out of bounds: {Line}, Total lines: {Total}",
                    change.StartLine, lines.Count);
                return false;
            }

            var newLines = change.NewContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            lines.InsertRange(insertIndex, newLines);

            Logger.LogDebug("Inserted {LineCount} lines at position {Position}", newLines.Length, change.StartLine);
            return true;
        }

        private bool ApplyDeleteChange(PatchChange change, List<string> lines)
        {
            // Convert from 1-based to 0-based indexing
            int startIndex = change.StartLine - 1;
            int endIndex = change.EndLine - 1;

            if (startIndex < 0 || endIndex >= lines.Count || startIndex > endIndex)
            {
                Logger.LogError("Delete change line numbers out of bounds. Start: {Start}, End: {End}, Total lines: {Total}",
                    change.StartLine, change.EndLine, lines.Count);
                return false;
            }

            // Remove lines in reverse order to maintain indices
            for (int i = endIndex; i >= startIndex; i--)
            {
                lines.RemoveAt(i);
            }

            Logger.LogDebug("Deleted lines {Start}-{End}", change.StartLine, change.EndLine);
            return true;
        }
    }
}