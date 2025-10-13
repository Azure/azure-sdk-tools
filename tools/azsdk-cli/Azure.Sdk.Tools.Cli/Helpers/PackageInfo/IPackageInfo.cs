// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Public abstraction for information & operations over an Azure SDK package directory.
/// </summary>
public interface IPackageInfo
{
    /// <summary>
    /// Gets the absolute path to the root of the azure-sdk repository that contains the package.
    /// </summary>
    /// <remarks>
    /// The expected repository structure is <c>&lt;repoRoot&gt;/sdk/&lt;serviceName&gt;/&lt;packageName&gt;</c>. Implementations
    /// derive the repo root by splitting on the <c>sdk</c> segment. Accessing this property before <see cref="Init"/>
    /// is called MUST throw an <see cref="InvalidOperationException"/>.
    /// </remarks>
    string RepoRoot { get; }
    /// <summary>
    /// Gets the relative path segment under the <c>sdk</c> folder that identifies the service and package.
    /// </summary>
    /// <example><c>storage/storage-blob</c></example>
    /// <remarks>
    /// This is everything after <c>.../sdk/</c> in the package path. Accessing prior to initialization SHOULD throw.
    /// </remarks>
    string RelativePath { get; }
    /// <summary>
    /// Gets the absolute path to the package root directory.
    /// </summary>
    /// <remarks>
    /// This is the path originally supplied to <see cref="Init"/> (normalized to a full path). Accessing prior to
    /// initialization SHOULD throw.
    /// </remarks>
    string PackagePath { get; }
    /// <summary>
    /// Gets the package (artifact) name inferred from the last directory segment of <see cref="PackagePath"/>.
    /// </summary>
    /// <remarks>
    /// For example, given <c>.../sdk/storage/storage-blob</c> this returns <c>azure-storage-blobs</c>.
    /// </remarks>
    string PackageName { get; }
    /// <summary>
    /// Gets the service name inferred from the parent directory of <see cref="PackagePath"/>.
    /// </summary>
    /// <remarks>
    /// For example, given <c>.../sdk/storage/storage-blob</c> this returns <c>storage</c>.
    /// </remarks>
    string ServiceName { get; }
    /// <summary>
    /// Gets the language identifier for the package implementation (e.g. <c>dotnet</c>, <c>java</c>, <c>python</c>, <c>typescript</c>, <c>go</c>).
    /// </summary>
    string Language { get; }
    /// <summary>Initialize the instance for the provided package path.</summary>
    /// <param name="packagePath">Path to the package root.</param>
    /// <remarks>
    /// Implementations MUST make this method idempotent for the same path and throw if called again with a different path.
    /// After successful initialization all other property getters MUST become available.
    /// </remarks>
    void Init(string packagePath);
    /// <summary>Returns true if initialization has completed.</summary>
    bool IsInitialized { get; }
    /// <summary>
    /// Gets (or computes) the path to the samples directory for the package.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The absolute path to the samples directory (may point to a directory that does not yet exist).</returns>
    /// <remarks>
    /// Implementations SHOULD NOT access the filesystem beyond inexpensive checks; callers may need to create the
    /// directory if it is absent.
    /// </remarks>
    Task<string> GetSamplesDirectoryAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets the canonical source file extension for the package's language (including the leading period).
    /// </summary>
    /// <returns>File extension such as <c>.cs</c>, <c>.java</c>, <c>.py</c>, <c>.ts</c>, or <c>.go</c>.</returns>
    string GetFileExtension();
    /// <summary>
    /// Attempts to extract the current package version from language-specific manifest or build files.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel IO operations.</param>
    /// <returns>The parsed version string or <c>null</c> if the version cannot be determined.</returns>
    Task<string?> GetPackageVersionAsync(CancellationToken cancellationToken = default);
}