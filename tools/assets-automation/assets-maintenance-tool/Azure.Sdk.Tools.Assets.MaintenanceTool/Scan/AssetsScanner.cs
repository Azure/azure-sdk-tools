using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Scan
{
    /// <summary>
    /// Used to walk through repo configurations and locate all assets.
    /// </summary>
    public class AssetsScanner
    {
        public AssetsScanner() {}

        public AssetsResultSet Scan(RunConfiguration config, AssetsResultSet? previousOutput)
        {
            var resultSet = new AssetsResultSet(new List<AssetsResult>());

            Parallel.ForEach(config.Repos, repoConfig =>
            {
                var results = ScanRepo(repoConfig, previousOutput);
                resultSet.Results.AddRange(results);
            });

            return resultSet;
        }

        public List<AssetsResult> ScanRepo(RepoConfiguration config, AssetsResultSet? previousOutput)
        {
            var targetRepoUri = $"https://github.com/{config.Repo}.git";
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            return new List<AssetsResult>();
        }
    }
}
