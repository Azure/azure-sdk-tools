public class ConstData
{
    public static readonly string FormattedTime = DateTime.Now.ToString("yyyy_MMdd");
    public static readonly string ReportsDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../../../Reports"));
    public static readonly string EngDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../../../eng"));
    public static readonly string TotalIssuesJsonFileName = $"TotalIssues{FormattedTime}.json";
    public static readonly string DiffIssuesExcelFileName = $"DiffIssues{FormattedTime}.xlsx";
    public static readonly string TotalIssuesExcelFileName = $"TotalIssues{FormattedTime}.xlsx";
    public static readonly string DiffIssuesJsonFileName = $"DiffIssues{FormattedTime}.json";

    public static readonly string? LastPipelineAllPackageJsonFilePath = GetLastPipelineAllPackagesJsonFilePath();

    static string? GetLastPipelineAllPackagesJsonFilePath()
    {
        string ArtifactsDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../../../Artifacts"));
        if (string.IsNullOrEmpty(ArtifactsDirectory) || !Directory.Exists(ArtifactsDirectory))
        {
            return null;
        }

        string[] files = Directory.GetFiles(ArtifactsDirectory, "HistorySummaryTotalIssues.json");

        return files.Length > 0 ? files[0] : null;
    }

}