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
        public async Task ApplyPatchAsync(PatchRequest patchRequest, string typeSpecDirectory, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(patchRequest);
            ArgumentNullException.ThrowIfNull(typeSpecDirectory);

            Logger.LogInformation("Applying patch to TypeSpec files...");

            try
            {
            // Step 1: Validate the patch
            ValidatePatch(patchRequest, typeSpecDirectory); // Throws on failure

            // Step 2: Apply the changes
            await ApplyChangesToFileAsync(patchRequest, typeSpecDirectory, cancellationToken);
            
            Logger.LogInformation("Successfully applied {ChangeCount} changes to {File}", 
                patchRequest.Changes.Count, patchRequest.File);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying patch: {ex.Message}", ex);
            }
        }

        

        private void ValidatePatch(PatchRequest patchRequest, string typeSpecDirectory)
        {
            // Check file exists
            string filePath = Path.Combine(typeSpecDirectory, patchRequest.File);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Target file does not exist: {filePath}");
            }

            // Check version consistency
            var currentFileInfo = VersionManager.GetFileMetadata(patchRequest.File);
            if (currentFileInfo != null && currentFileInfo.Version != patchRequest.FromVersion)
            {
                Logger.LogWarning("Version mismatch for {File}. Expected: {Expected}, Current: {Current}. Proceeding anyway...",
                    patchRequest.File, patchRequest.FromVersion, currentFileInfo.Version);
            }

            // Validate change types
            foreach (var change in patchRequest.Changes)
            {
                if (string.IsNullOrWhiteSpace(change.Type) || 
                    !IsValidChangeType(change.Type))
                {
                    throw new InvalidOperationException($"Invalid change type: {change.Type}");
                }

                if (change.StartLine <= 0 || change.EndLine <= 0 || change.StartLine > change.EndLine)
                {
                    throw new InvalidOperationException($"Invalid line numbers in change. Start: {change.StartLine}, End: {change.EndLine}");
                }
            }

            Logger.LogDebug("Patch validation passed for {File}", patchRequest.File);
        }

        private static bool IsValidChangeType(string changeType)
        {
            return changeType.Equals("replace", StringComparison.OrdinalIgnoreCase) ||
                   changeType.Equals("insert", StringComparison.OrdinalIgnoreCase) ||
                   changeType.Equals("delete", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ApplyChangesToFileAsync(PatchRequest patchRequest, string typeSpecDirectory, CancellationToken cancellationToken)
        {
            string filePath = Path.Combine(typeSpecDirectory, patchRequest.File);
            
            try
            {
                // Read current file content
                string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);

                // Sort changes by line number in descending order to avoid line number shifts
                var sortedChanges = patchRequest.Changes
                    .OrderByDescending(c => c.StartLine)
                    .ToList();

                var modifiedLines = lines.ToList();

                // Apply each change
                foreach (var change in sortedChanges)
                {
                    ApplySingleChange(change, modifiedLines);
                }

                // Write the modified content back to file
                await File.WriteAllLinesAsync(filePath, modifiedLines, cancellationToken).ConfigureAwait(false);

                // Update version manager with new content
                string newContent = string.Join(Environment.NewLine, modifiedLines);
                VersionManager.UpdateFileMetadata(patchRequest.File, newContent);

                Logger.LogInformation("Successfully applied {ChangeCount} changes to {File}", 
                    patchRequest.Changes.Count, patchRequest.File);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply changes to file {patchRequest.File}: {ex.Message}");
            }
        }

        private void ApplySingleChange(PatchChange change, List<string> lines)
        {
            switch (change.Type.ToLowerInvariant())
            {
                case "replace":
                    ApplyReplaceChange(change, lines);
                    break;
                case "insert":
                    ApplyInsertChange(change, lines);
                    break;
                case "delete":
                    ApplyDeleteChange(change, lines);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown change type: {change.Type}");
            }
        }

        private void ApplyReplaceChange(PatchChange change, List<string> lines)
        {
            // Convert from 1-based to 0-based indexing
            int startIndex = change.StartLine - 1;
            int endIndex = change.EndLine - 1;

            // Handle "append to end" case when trying to add content beyond file end
            if (startIndex == lines.Count && string.IsNullOrEmpty(change.OldContent.Trim()))
            {
                var newLines = change.NewContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                lines.AddRange(newLines);
                return;
            }

            if (startIndex < 0 || endIndex >= lines.Count || startIndex > endIndex)
            {
                throw new InvalidOperationException($"Replace change line numbers out of bounds. Start: {change.StartLine}, End: {change.EndLine}, Total lines: {lines.Count}");
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
        }

        private void ApplyInsertChange(PatchChange change, List<string> lines)
        {
            // Convert from 1-based to 0-based indexing
            int insertIndex = change.StartLine - 1;

            if (insertIndex < 0 || insertIndex > lines.Count)
            {
                throw new InvalidOperationException($"Insert change line number out of bounds: {change.StartLine}, Total lines: {lines.Count}");
            }

            var newLines = change.NewContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            lines.InsertRange(insertIndex, newLines);

            Logger.LogDebug("Inserted {LineCount} lines at position {Position}", newLines.Length, change.StartLine);
        }

        private void ApplyDeleteChange(PatchChange change, List<string> lines)
        {
            // Convert from 1-based to 0-based indexing
            int startIndex = change.StartLine - 1;
            int endIndex = change.EndLine - 1;

            if (startIndex < 0 || endIndex >= lines.Count || startIndex > endIndex)
            {
                throw new InvalidOperationException($"Delete change line numbers out of bounds. Start: {change.StartLine}, End: {change.EndLine}, Total lines: {lines.Count}");
            }

            // Remove lines in reverse order to maintain indices
            for (int i = endIndex; i >= startIndex; i--)
            {
                lines.RemoveAt(i);
            }

            Logger.LogDebug("Deleted lines {Start}-{End}", change.StartLine, change.EndLine);
        }
    }
}