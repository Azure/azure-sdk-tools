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

        public static ValidationContext CreateFromValidatedInputs(string validatedTypeSpecPath, string validatedCommitId, string validatedOutputPath)
        {
            return new ValidationContext(validatedTypeSpecPath, validatedCommitId, validatedOutputPath);
        }

        /// <summary>
        /// Checks if this is a GitHub-based workflow (has commit ID).
        /// </summary>
        public bool IsGitHubWorkflow => !string.IsNullOrWhiteSpace(ValidatedCommitId);

        public static Result<ValidationContext> TryValidateAndCreate(
            string? typespecPath, 
            string? commitId, 
            string sdkOutputPath, 
            ILogger logger)
        {
            bool isLocalPath = string.IsNullOrWhiteSpace(commitId);

            Result<string> typespecValidation = InputValidator.ValidateTypeSpecDir(typespecPath, isLocalPath);
            if (typespecValidation.IsFailure)
            {
                return Result<ValidationContext>.Failure(new ArgumentException($"TypeSpec path validation failed: {typespecValidation.Exception?.Message}"));
            }

            Result<string> commitValidation = InputValidator.ValidateCommitId(commitId);
            if (commitValidation.IsFailure)
            {
                return Result<ValidationContext>.Failure(new ArgumentException($"Commit ID validation failed: {commitValidation.Exception?.Message}"));
            }

            Result<string> outputValidation = InputValidator.ValidateOutputDirectory(sdkOutputPath);
            if (outputValidation.IsFailure)
            {
                return Result<ValidationContext>.Failure(new ArgumentException($"SDK output path validation failed: {outputValidation.Exception?.Message}"));
            }

            logger.LogInformation("All input validation completed successfully");

            return Result<ValidationContext>.Success(new ValidationContext(
                typespecValidation.Value!,
                commitValidation.Value!,
                outputValidation.Value!));
        }
    }
}
