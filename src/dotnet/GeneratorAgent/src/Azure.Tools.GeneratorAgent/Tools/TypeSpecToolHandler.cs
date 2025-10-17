using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Models;
using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.Tools
{
    /// <summary>
    /// Handles TypeSpec-related tool calls from the AI agent by orchestrating specialized services
    /// </summary>
    internal class TypeSpecToolHandler : ITypeSpecToolHandler
    {
        private readonly TypeSpecFileService FileService;
        private readonly TypeSpecFileVersionManager VersionManager;

        private readonly ILogger<TypeSpecToolHandler> Logger;

        public TypeSpecToolHandler(
            TypeSpecFileService fileService,
            TypeSpecFileVersionManager versionManager,
            ILogger<TypeSpecToolHandler> logger)
        {
            ArgumentNullException.ThrowIfNull(fileService);
            ArgumentNullException.ThrowIfNull(versionManager);
            ArgumentNullException.ThrowIfNull(logger);

            FileService = fileService;
            VersionManager = versionManager;
            Logger = logger;
        }

        public async Task<ListTypeSpecFilesResponse> ListTypeSpecFilesAsync(ValidationContext validationContext, CancellationToken cancellationToken = default)
        {
            try
            {
                var typeSpecFiles = await FileService.GetTypeSpecFilesAsync(validationContext, cancellationToken).ConfigureAwait(false);

                // Get or create metadata for current files
                var fileInfoList = new List<TypeSpecFileInfo>();
                foreach (var kvp in typeSpecFiles)
                {
                    var fileInfo = VersionManager.GetOrCreateFileMetadata(kvp.Key, kvp.Value);
                    
                    // Always include content for comprehensive analysis
                    fileInfo.Content = kvp.Value;
                    
                    fileInfoList.Add(fileInfo);
                }

                var response = new ListTypeSpecFilesResponse
                {
                    Files = fileInfoList
                };

                Logger.LogInformation("Returning {Count} TypeSpec files with complete content for analysis", response.Files.Count);
                return response;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling list_typespec_files tool call");
                throw;
            }
        }

        public async Task<TypeSpecFileInfo> GetTypeSpecFileAsync(string filename, ValidationContext validationContext, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get the TypeSpec files dictionary
                var typeSpecFiles = await FileService.GetTypeSpecFilesAsync(validationContext, cancellationToken).ConfigureAwait(false);

                // Find the file by name (case-insensitive)
                var fileEntry = typeSpecFiles
                    .FirstOrDefault(kvp => string.Equals(Path.GetFileName(kvp.Key), filename, StringComparison.OrdinalIgnoreCase));

                if (fileEntry.Key == null)
                {
                    throw new FileNotFoundException($"TypeSpec file '{filename}' not found. Available files: {string.Join(", ", typeSpecFiles.Keys.Select(Path.GetFileName))}");
                }

                // Get or create metadata for current file
                var fileInfo = VersionManager.GetOrCreateFileMetadata(fileEntry.Key, fileEntry.Value);

                // Add the file content to the response for agent analysis
                fileInfo.Content = fileEntry.Value;

                return fileInfo;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling get_typespec_file tool call for {Filename}", filename);
                throw;
            }
        }

        public List<int> GetAvailableVersions(string fileName)
        {
            return VersionManager.GetAvailableVersions(fileName);
        }
    }
}