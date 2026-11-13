namespace Azure.Sdk.Tools.Cli.Models;

public sealed class TypeSpecLocation(string directory, string commit, string repository)
{
    public string Directory { get; } = directory;
    public string Commit { get; } = commit;
    public string Repository { get; } = repository;
}