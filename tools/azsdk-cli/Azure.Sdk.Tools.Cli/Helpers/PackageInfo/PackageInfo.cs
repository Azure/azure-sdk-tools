// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Plain data model representing inferred information about an Azure SDK package.
/// </summary>
public class PackageInfo
{
    /// <summary>
    /// Absolute path on disk to the root directory of the package that is being inspected / operated on.
    /// </summary>
    /// <remarks>
    /// This is typically a directory under the cloned azure-sdk-* repository, e.g. a path like
    /// <c>/home/user/azure-sdk-for-js/sdk/storage/storage-blob</c>.
    /// </remarks>
    public required string PackagePath { get; init; }

    /// <summary>
    /// Absolute path to the root of the git repository that contains the package (e.g. <c>azure-sdk-for-js</c>).
    /// </summary>
    /// <remarks>
    /// Useful when computing relative paths or when running repository level tooling (git operations, global config lookup, etc.).
    /// </remarks>
    public required string RepoRoot { get; init; }

    /// <summary>
    /// Path of the package relative to <see cref="RepoRoot"/> (no leading directory separators).
    /// </summary>
    /// <example><c>sdk/storage/storage-blob</c></example>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The name of the package folder.
    /// </summary>
    public required string PackageName { get; init; }

    /// <summary>
    /// Logical Azure service name that the package targets (e.g. <c>storage</c>, <c>keyvault</c>, <c>cosmos</c>).
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Language moniker (e.g. <c>typescript</c>, <c>dotnet</c>, <c>python</c>, <c>java</c>, <c>go</c> ).
    /// </summary>
    /// <remarks>
    /// Used for selecting language specific strategies (sample folder layout, file extensions, version extraction, etc.).
    /// </remarks>
    public required SdkLanguage Language { get; init; }

    // Internal strategy delegates filled by language-specific helper at construction time.
    /// <summary>
    /// Delegate that returns the absolute path to the directory containing runnable samples for the package.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>&lt;PackagePath&gt;/samples</c>. Languages with different conventions (e.g. <c>samples/v12</c>, <c>sample</c>, or nested per-feature
    /// folders) can override this when creating the <see cref="PackageInfo"/> instance.
    /// </remarks>
    internal Func<PackageInfo, CancellationToken, Task<string>> SamplesDirectoryProvider { get; init; } = (pi, ct) => Task.FromResult(Path.Combine(pi.PackagePath, "samples"));

    /// <summary>
    /// Delegate that returns the current package version string, or <c>null</c> if it cannot be determined cheaply.
    /// </summary>
    /// <remarks>
    /// Version extraction may rely on language specific manifest files (e.g. <c>package.json</c>, <c>AssemblyInfo.cs</c>, <c>setup.cfg</c>, etc.) so this indirection
    /// allows callers to request a version only when they need it and lets language helpers customize the lookup.
    /// </remarks>
    internal Func<PackageInfo, CancellationToken, Task<string?>> VersionProvider { get; init; } = (pi, ct) => Task.FromResult<string?>(null);

    /// <summary>
    /// Gets the absolute path to the directory containing samples for this package.
    /// </summary>
    /// <param name="ct">Cancellation token for the asynchronous operation.</param>
    /// <returns>Absolute directory path as a string.</returns>
    /// <exception cref="OperationCanceledException">If the provided <paramref name="ct"/> is canceled.</exception>
    public Task<string> GetSamplesDirectoryAsync(CancellationToken ct = default) => SamplesDirectoryProvider(this, ct);

    /// <summary>
    /// Attempts to resolve the package's current version.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A task whose result is the version string if available, otherwise <c>null</c> when the language helper cannot determine the version quickly.
    /// </returns>
    /// <remarks>
    /// Callers should tolerate a <c>null</c> result and avoid failing hard; some operations (like sample generation) may not require a version.
    /// </remarks>
    public Task<string?> GetPackageVersionAsync(CancellationToken ct = default) => VersionProvider(this, ct);
}
