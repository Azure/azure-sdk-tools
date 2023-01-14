namespace Azure.Sdk.Tools.CodeOwnersParser;

public static class PathExtensions
{
    public static bool IsGlobFilePath(this string path)
        => GlobFilePath.IsGlobFilePath(path);
}
