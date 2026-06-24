// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.ApiReview;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class PythonLanguageService : LanguageService
{
    private static readonly string[] PythonApiReviewArtifactNames = ["api.md", "api.metadata.yml"];

    public override async Task<ApiReviewArtifactResult> GenerateApiReviewArtifactsAsync(ApiReviewArtifactRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PackagePath) || !Directory.Exists(request.PackagePath))
            {
                return ApiReviewArtifactResult.CreateFailure($"Unable to find Python package '{request.PackageName}' at {request.PackagePath}.");
            }

            Directory.CreateDirectory(request.OutputDirectory);
            var tempPath = Path.Combine(request.OutputDirectory, "temp");
            Directory.CreateDirectory(tempPath);

            var result = await pythonHelper.Run(new PythonOptions(
                    "apistubgen",
                    ["--pkg-path", request.PackagePath, "--out-path", request.OutputDirectory, "--temp-path", tempPath, "--skip-pylint"],
                    workingDirectory: request.WorktreeRoot,
                    timeout: TimeSpan.FromMinutes(10),
                    logOutputStream: true),
                ct);

            if (result.ExitCode != 0)
            {
                return ApiReviewArtifactResult.CreateFailure($"apistubgen failed for package '{request.PackageName}' at ref '{request.Ref}'. Output:{Environment.NewLine}{result.Output}".Trim());
            }

            var reviewDirectory = Path.Combine(request.PackageRelativePath, "api-review").Replace(Path.DirectorySeparatorChar, '/');
            var artifacts = new List<ApiReviewArtifact>();
            foreach (var artifactName in PythonApiReviewArtifactNames)
            {
                var sourcePath = Path.Combine(request.OutputDirectory, artifactName);
                if (!File.Exists(sourcePath))
                {
                    return ApiReviewArtifactResult.CreateFailure($"Expected Python API review artifact '{artifactName}' was not generated for package '{request.PackageName}' at ref '{request.Ref}'.");
                }

                artifacts.Add(new ApiReviewArtifact
                {
                    SourcePath = sourcePath,
                    ReviewPath = $"{reviewDirectory}/{artifactName}"
                });
            }

            return ApiReviewArtifactResult.CreateSuccess(request.PackagePath, artifacts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate Python API review artifacts for {PackageName}", request.PackageName);
            return ApiReviewArtifactResult.CreateFailure($"Failed to generate Python API review artifacts: {ex.Message}");
        }
    }
}
