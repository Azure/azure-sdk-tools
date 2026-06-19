// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.ApiReview;

public class ApiReviewArtifact
{
    [JsonPropertyName("source_path")]
    public required string SourcePath { get; set; }

    [JsonPropertyName("review_path")]
    public required string ReviewPath { get; set; }
}

public class ApiReviewArtifactRequest
{
    public required string PackageName { get; set; }
    public required string PackagePath { get; set; }
    public required string PackageRelativePath { get; set; }
    public required string RepoRoot { get; set; }
    public required string WorktreeRoot { get; set; }
    public required string Ref { get; set; }
    public required string OutputDirectory { get; set; }
}

public class ApiReviewArtifactResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PackagePath { get; set; }
    public List<ApiReviewArtifact> Artifacts { get; set; } = [];

    public static ApiReviewArtifactResult CreateFailure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public static ApiReviewArtifactResult CreateSuccess(string packagePath, List<ApiReviewArtifact> artifacts) => new()
    {
        Success = true,
        PackagePath = packagePath,
        Artifacts = artifacts
    };
}
