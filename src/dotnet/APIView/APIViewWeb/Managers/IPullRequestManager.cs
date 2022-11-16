using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface IPullRequestManager
    {
        public Task<string> DetectApiChanges(string buildId, string artifactName, string originalFileName,
            string commitSha, string repoName, string packageName, int prNumber, string hostName, string codeFileName = null,
            string baselineCodeFileName = null, bool commentOnPR = true, string language = null, string project = "public");
        public Task CleanupPullRequestData();
    }
}
