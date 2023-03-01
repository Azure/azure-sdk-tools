namespace Azure.Sdk.Tools.SecretRotation.Tests;

public class TestFiles
{
    private static readonly Lazy<string> assemblyDirectory = new(() =>
    {
        string assemblyPath = typeof(TestFiles).Assembly.Location;

        return Path.GetDirectoryName(assemblyPath)
            ?? throw new Exception($"Unable to resolve directory from assembly location '{assemblyPath}'");
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string ResolvePath(string relativePath)
    {
        string filePath = Path.Combine(assemblyDirectory.Value, relativePath);

        return filePath;
    }
}
