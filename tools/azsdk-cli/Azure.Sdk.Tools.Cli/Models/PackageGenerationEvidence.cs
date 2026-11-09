namespace Azure.Sdk.Tools.Cli.Models;

public sealed class PackageGenerationEvidence(IReadOnlyList<PackageApiSource> sources, string plane, IReadOnlyList<string> issues)
{
    public IReadOnlyList<PackageApiSource> Sources { get; } = sources;
    public string Plane { get; } = plane;
    public IReadOnlyList<string> Issues { get; } = issues;
}