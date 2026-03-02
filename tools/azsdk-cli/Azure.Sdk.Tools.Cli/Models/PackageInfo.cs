// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Plain data model representing inferred information about an Azure SDK package.
/// JSON property names match the expected CI pipeline format.
/// Path properties use NormalizedPath for cross-platform consistency.
/// </summary>
public class PackageInfo
{
    /// <summary>
    /// The package name as defined in the manifest file.
    /// </summary>
    [JsonPropertyName("Name")]
    public string? PackageName { get; set; }

    /// <summary>
    /// Artifact name for CI/packaging (usually same as PackageName).
    /// </summary>
    [JsonPropertyName("ArtifactName")]
    public string? ArtifactName { get; set; }

    /// <summary>
    /// Current package version string.
    /// </summary>
    [JsonPropertyName("Version")]
    public string? PackageVersion { get; set; }

    /// <summary>
    /// Directory path relative to repo root (e.g., "sdk/storage/Azure.Storage.Blobs").
    /// </summary>
    [JsonPropertyName("DirectoryPath")]
    public NormalizedPath DirectoryPath { get; set; }

    /// <summary>
    /// Service directory under sdk/ (may include group/service for Go).
    /// </summary>
    [JsonPropertyName("ServiceDirectory")]
    public NormalizedPath? ServiceDirectory { get; set; }

    /// <summary>
    /// Path to README.md relative to repo root.
    /// </summary>
    [JsonPropertyName("ReadMePath")]
    public NormalizedPath ReadMePath { get; set; }

    /// <summary>
    /// Path to CHANGELOG.md relative to repo root.
    /// </summary>
    [JsonPropertyName("ChangeLogPath")]
    public NormalizedPath ChangeLogPath { get; set; }

    /// <summary>
    /// Optional group identifier (e.g., Maven groupId).
    /// </summary>
    [JsonPropertyName("Group")]
    public string? Group { get; set; }

    /// <summary>
    /// SDK type for JSON serialization: "mgmt", "client", "spring", "functions", or empty string.
    /// Empty string is used for unknown/unset types to match PowerShell output format.
    /// Use the <see cref="SdkType"/> property for type-safe access.
    /// </summary>
    [JsonPropertyName("SdkType")]
    public string SdkTypeString { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a track 2 (new SDK) package.
    /// </summary>
    [JsonPropertyName("IsNewSdk")]
    public bool IsNewSdk { get; set; }

    /// <summary>
    /// Release status from changelog (e.g., "Unreleased" or a date).
    /// </summary>
    [JsonPropertyName("ReleaseStatus")]
    public string ReleaseStatus { get; set; } = string.Empty;

    /// <summary>
    /// Whether the package is included only for validation (not direct changes).
    /// </summary>
    [JsonPropertyName("IncludedForValidation")]
    public bool IncludedForValidation { get; set; }

    /// <summary>
    /// Additional packages that should be validated when this package changes.
    /// Null when empty for PowerShell parity.
    /// </summary>
    [JsonPropertyName("AdditionalValidationPackages")]
    public List<NormalizedPath>? AdditionalValidationPackages { get; set; }

    /// <summary>
    /// Artifact details (reserved for future use).
    /// </summary>
    [JsonPropertyName("ArtifactDetails")]
    public object? ArtifactDetails { get; set; }

    /// <summary>
    /// CI parameters extracted from ci*.yml.
    /// </summary>
    [JsonPropertyName("CIParameters")]
    public CiPipelineParameters CiParameters { get; set; } = new();

    /// <summary>
    /// Path to the TypeSpec project (for TypeSpec-based packages).
    /// </summary>
    [JsonPropertyName("SpecProjectPath")]
    public string? SpecProjectPath { get; set; }

    /// <summary>
    /// Dev version (set when addDevVersion is true).
    /// </summary>
    [JsonPropertyName("DevVersion")]
    public string? DevVersion { get; set; }

    // ============================================================
    // Non-serialized properties used internally during processing
    // ============================================================

    /// <summary>
    /// Absolute path on disk to the root directory of the package.
    /// </summary>
    [JsonIgnore]
    public NormalizedPath PackagePath { get; set; }

    /// <summary>
    /// Absolute path to the root of the git repository.
    /// </summary>
    [JsonIgnore]
    public NormalizedPath RepoRoot { get; set; }

    /// <summary>
    /// Path of the package relative to sdk/ directory (e.g., "storage/Azure.Storage.Blobs").
    /// </summary>
    [JsonIgnore]
    public NormalizedPath RelativePath { get; set; }

    /// <summary>
    /// Azure service name (e.g., storage, keyvault).
    /// </summary>
    [JsonIgnore]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// SDK language (dotnet, java, python, etc.).
    /// </summary>
    [JsonIgnore]
    public SdkLanguage Language { get; set; }

    /// <summary>
    /// Absolute path to the samples directory.
    /// </summary>
    [JsonIgnore]
    public NormalizedPath SamplesDirectory { get; set; }

    /// <summary>
    /// SDK type as a strongly-typed enum. Maps to/from <see cref="SdkTypeString"/> for serialization.
    /// <see cref="SdkType.Unknown"/> maps to empty string to match PowerShell parity.
    /// </summary>
    [JsonIgnore]
    public SdkType SdkType
    {
        get => SdkTypeString switch
        {
            "mgmt" => SdkType.Management,
            "client" => SdkType.Dataplane,
            "spring" => SdkType.Spring,
            "functions" => SdkType.Functions,
            _ => SdkType.Unknown
        };
        set => SdkTypeString = value switch
        {
            SdkType.Management => "mgmt",
            SdkType.Dataplane => "client",
            SdkType.Spring => "spring",
            SdkType.Functions => "functions",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Whether the package opts out of AOT compatibility checks (.NET only).
    /// </summary>
    [JsonIgnore]
    public bool? AotCompatOptOut { get; set; }

    /// <summary>
    /// Paths that trigger CI for this package when changed (internal use only).
    /// </summary>
    [JsonIgnore]
    public List<NormalizedPath> TriggeringPaths { get; set; } = [];
}
