

using System.IO;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface IDevopsArtifactRepository
    {
        public Task<Stream> DownloadPackageArtifact(string repoName, string buildId, string artifactName, string filePath, string project, string format = "file");

        public Task RunPipeline(string pipelineName, string reviewDetails, string originalStorageUrl);
    }
}
