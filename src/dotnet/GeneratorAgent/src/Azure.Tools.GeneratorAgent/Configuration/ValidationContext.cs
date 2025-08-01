using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Security;

namespace Azure.Tools.GeneratorAgent.Configuration
{
    internal class ValidationContext
    {
        public string ValidatedTypeSpecDir { get; private set; }
        public string ValidatedCommitId { get; private set; }
        public string ValidatedSdkDir { get; private set; }

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

        public static ValidationContext ValidateAndCreate(
            string? typespecPath, 
            string? commitId, 
            string sdkOutputPath, 
            ILogger logger)
        {
            logger.LogInformation("Starting input validation...");


            bool isLocalPath = string.IsNullOrWhiteSpace(commitId);

            ValidationResult typespecValidation = InputValidator.ValidateTypeSpecDir(typespecPath, isLocalPath);
            if (!typespecValidation.IsValid)
            {
                logger.LogError("TypeSpec path validation failed: {Error}", typespecValidation.ErrorMessage);
                throw new ArgumentException($"TypeSpec path validation failed: {typespecValidation.ErrorMessage}", nameof(typespecPath));
            }

            ValidationResult commitValidation = InputValidator.ValidateCommitId(commitId);
            if (!commitValidation.IsValid)
            {
                logger.LogError("Commit ID validation failed: {Error}", commitValidation.ErrorMessage);
                throw new ArgumentException($"Commit ID validation failed: {commitValidation.ErrorMessage}", nameof(commitId));
            }

            ValidationResult outputValidation = InputValidator.ValidateOutputDirectory(sdkOutputPath);
            if (!outputValidation.IsValid)
            {
                logger.LogError("SDK output path validation failed: {Error}", outputValidation.ErrorMessage);
                throw new ArgumentException($"SDK output path validation failed: {outputValidation.ErrorMessage}", nameof(sdkOutputPath));
            }

            logger.LogInformation("All input validation completed successfully");
            logger.LogInformation("Validated TypeSpec path: {TypeSpecPath}", typespecValidation.Value);
            logger.LogInformation("Validated commit ID: {CommitId}", string.IsNullOrEmpty(commitValidation.Value) ? "[None]" : commitValidation.Value);
            logger.LogInformation("Validated SDK output path: {SdkOutputPath}", outputValidation.Value);

            return new ValidationContext(
                typespecValidation.Value,
                commitValidation.Value,
                outputValidation.Value);
        }
    }
}
