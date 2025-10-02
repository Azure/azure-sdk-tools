using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Models;

namespace Azure.Tools.GeneratorAgent.Tools
{
    /// <summary>
    /// Interface for handling TypeSpec-related tool calls from the AI agent
    /// </summary>
    internal interface ITypeSpecToolHandler
    {
        /// <summary>
        /// Handles the list_typespec_files tool call
        /// </summary>
        Task<ListTypeSpecFilesResponse> ListTypeSpecFilesAsync(ValidationContext validationContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the content of a specific TypeSpec file with metadata
        /// </summary>
        Task<TypeSpecFileInfo> GetTypeSpecFileAsync(string filename, ValidationContext validationContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available version numbers for a file
        /// </summary>
        List<int> GetAvailableVersions(string fileName);
    }
}