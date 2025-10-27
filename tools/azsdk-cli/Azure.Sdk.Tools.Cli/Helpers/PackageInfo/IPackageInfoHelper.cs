// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Language-specific resolver that produces a fully populated <see cref="PackageInfo"/> for a given package path.
/// </summary>
public interface IPackageInfoHelper
{
    /// <summary>
    /// Resolves structural information and attaches strategy delegates for lazy aspects (samples directory, file extension, version parsing).
    /// </summary>
    Task<PackageInfo> ResolvePackageInfo(string packagePath, CancellationToken ct = default);
}
