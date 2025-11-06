// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages
{
    public abstract class LanguageService
    {
        protected IProcessHelper processHelper;
        protected IGitHelper gitHelper;
        protected ILogger<LanguageService> logger;
        protected ICommonValidationHelpers commonValidationHelpers;

        /*public LanguageService(IProcessHelper processHelper, IGitHelper gitHelper, ILogger<LanguageService> logger, ICommonValidationHelpers commonValidationHelpers)
        {
            this.processHelper = processHelper;
            this.gitHelper = gitHelper;
            this.logger = logger;
            this.commonValidationHelpers = commonValidationHelpers;
        }*/

        public abstract SdkLanguage Language { get; }
        public virtual bool IsTspClientupdatedSupported => false;
#pragma warning disable CS1998
        public async virtual Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPackageInfo is not implemented for this language.");
        }
#pragma warning restore CS1998

        /// <summary>
        /// Analyzes dependencies for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix dependency issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the dependency analysis</returns>
        public virtual Task<PackageCheckResponse> AnalyzeDependencies(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }
        /// <summary>
        /// Validates the README for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the README validation</returns>
        public virtual Task<PackageCheckResponse> ValidateReadme(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Checks spelling in the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the spelling check</returns>
        public virtual Task<PackageCheckResponse> CheckSpelling(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Updates code snippets in the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix snippet issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the snippet update operation</returns>
        public virtual Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Lints code in the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to automatically fix linting issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the code linting operation</returns>
        public virtual Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Formats code in the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to automatically apply code formatting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the code formatting operation</returns>  
        public virtual Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// Validate samples for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the sample validation</returns>
        public virtual Task<PackageCheckResponse> ValidateSamples(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Checks AOT compatibility for the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the AOT compatibility check</returns>
        public virtual Task<PackageCheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Checks generated code for the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the generated code check</returns>
        public virtual Task<PackageCheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }
        /// <summary>
        /// Validates the changelog for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the changelog validation</returns>
        public virtual Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(1, "", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Runs all tests in the specified package.
        /// </summary>
        /// <param name="packagePath">The path to the package containing the tests.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>True if all tests pass; otherwise, false.</returns>
        public virtual Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Produces an API change list by diffing file contents between two generated source trees.
        /// Implementations may perform a structural or textual diff; when <paramref name="oldGenerationPath"/> is null
        /// they should treat the operation as an initial generation (returning an empty change list).
        /// </summary>
        /// <param name="oldGenerationPath">Previous generation</param>
        /// <param name="newGenerationPath">New/current generation root.</param>
        /// <returns>List of detected API changes (empty if no differences).</returns>
        public virtual Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
        {
            List<ApiChange> result = [];
            return Task.FromResult(result);
        }

        /// <summary>
        /// Locates the customization (hand-authored) root directory for the language, if any.
        /// </summary>
        /// <param name="generationRoot">Root folder of newly generated sources (e.g. a <c>src</c> directory).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Absolute path to customization root or <c>null</c> if none is found / applicable.</returns>
        public virtual string? GetCustomizationRoot(string generationRoot, CancellationToken ct)
        {
            return "This is not an applicable operation for this language";
        }

        /// <summary>
        /// Applies automated patches directly to customization code using intelligent analysis.
        /// </summary>
        /// <param name="commitSha">The commit SHA from TypeSpec changes for context</param>
        /// <param name="customizationRoot">Path to the customization root directory</param>
        /// <param name="packagePath">Path to the package directory containing generated code</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if patches were successfully applied; false otherwise</returns>
        public virtual Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct)
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// Performs language-specific validation (build, compile, tests, lint, type-check, etc.).
        /// </summary>
        /// <param name="packagePath">Path to the package directory containing generated code.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="ValidationResult"/> indicating success or a list of validation errors.</returns>
        public virtual async Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return ValidationResult.CreateFailure("Package path not specified");
            }
            if (!Directory.Exists(packagePath))
            {
                return ValidationResult.CreateFailure($"Package path not found: {packagePath}");
            }

            try
            {
                var depResult = await AnalyzeDependencies(packagePath, false, ct);
                if (depResult.ExitCode == 0)
                {
                    return ValidationResult.CreateSuccess();
                }
                var errorMessage = string.IsNullOrWhiteSpace(depResult.ResponseError) ? depResult.CheckStatusDetails : depResult.ResponseError;
                return ValidationResult.CreateFailure(errorMessage ?? "Validation failed");
            }
            catch (Exception ex)
            {
                return ValidationResult.CreateFailure($"Validation exception: {ex.Message}");
            }
        }               

        public virtual List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
        {
            throw new NotImplementedException("Environment requirements are not implemented for this language.");
        }
    }
}
