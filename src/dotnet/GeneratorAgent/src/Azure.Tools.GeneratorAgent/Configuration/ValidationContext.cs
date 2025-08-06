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
                return Result<ValidationContext>.Failure($"TypeSpec path validation failed: {typespecValidation.Error}");
            }

            Result<string> commitValidation = InputValidator.ValidateCommitId(commitId);
            if (commitValidation.IsFailure)
            {
                return Result<ValidationContext>.Failure($"Commit ID validation failed: {commitValidation.Error}");
            }

            Result<string> outputValidation = InputValidator.ValidateOutputDirectory(sdkOutputPath);
            if (outputValidation.IsFailure)
            {
                return Result<ValidationContext>.Failure($"SDK output path validation failed: {outputValidation.Error}");
            }

            logger.LogInformation("All input validation completed successfully");

            return Result<ValidationContext>.Success(new ValidationContext(
                typespecValidation.Value,
                commitValidation.Value,
                outputValidation.Value));
        }
    }
}
