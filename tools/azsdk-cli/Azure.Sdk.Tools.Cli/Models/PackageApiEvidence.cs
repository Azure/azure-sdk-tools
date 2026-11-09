namespace Azure.Sdk.Tools.Cli.Models;

public sealed class PackageApiEvidence(PackageInfo package, IReadOnlyList<PackageApiSource> sources, IReadOnlyList<string> issues)
{
    public PackageInfo Package { get; } = package;
    public IReadOnlyList<PackageApiSource> Sources { get; } = sources;
    public IReadOnlyList<string> Issues { get; } = issues;
}