using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Security;

namespace Azure.Tools.GeneratorAgent.Configuration
{
    internal class ValidationContext
    {
        public string ValidatedTypeSpecDir { get; private set; }
        public string ValidatedCommitId { get; private set; }
        public string ValidatedSdkDir { get; private set; }
        
        /// <summary>
        /// The current TypeSpec directory to use for compilation.
        /// This is used when GitHub files are downloaded to a temp directory.
        /// </summary>
        public string? CurrentTypeSpecDirForCompilation { get; set; }

        /// <summary>
        /// Gets the current TypeSpec directory that should be used for compilation.
        /// Returns the temp directory if set (GitHub case), otherwise the original validated directory.
        /// </summary>
        public string CurrentTypeSpecDir => CurrentTypeSpecDirForCompilation ?? ValidatedTypeSpecDir;

        private ValidationContext(string typeSpecPath, string commitId, string outputPath)
        {
            ValidatedTypeSpecDir = typeSpecPath;
            ValidatedCommitId = commitId;
            ValidatedSdkDir = outputPath;
        }

        /// <summary>
        /// Checks if this is a GitHub-based workflow (has commit ID).
        /// </summary>
        public bool IsGitHubWorkflow => !string.IsNullOrWhiteSpace(ValidatedCommitId);

        public static ValidationContext ValidateAndCreate(
            string? typespecPath, 
            string? commitId, 
            string sdkOutputPath)
        {
            bool isLocalPath = string.IsNullOrWhiteSpace(commitId);

            var validatedTypeSpecPath = InputValidator.ValidateandNormalizeTypeSpecDir(typespecPath, isLocalPath);
            var validatedCommitId = InputValidator.ValidateCommitId(commitId);
            var validatedOutputPath = InputValidator.ValidateOutputDirectory(sdkOutputPath);

            return new ValidationContext(
                validatedTypeSpecPath,
                validatedCommitId,
                validatedOutputPath);
        }
    }
}
