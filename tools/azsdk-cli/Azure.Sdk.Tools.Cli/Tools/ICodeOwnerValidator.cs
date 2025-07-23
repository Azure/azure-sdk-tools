using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// Interface for validating GitHub users for Azure SDK code owner requirements.
    /// </summary>
    public interface ICodeOwnerValidator
    {
        /// <summary>
        /// Validates if a GitHub user meets the requirements to be an Azure SDK code owner.
        /// </summary>
        /// <param name="username">The GitHub username to validate</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Validation result with detailed information</returns>
        Task<CodeOwnerValidationResult> ValidateCodeOwnerAsync(string username, bool verbose = false);
    }
}
