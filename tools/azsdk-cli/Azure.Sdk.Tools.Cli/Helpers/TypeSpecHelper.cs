using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ITypeSpecHelper
    {
        public bool IsValidTypeSpecProjectPath(string path);
        public bool IsTypeSpecProjectForMgmtPlane(string Path);
        public bool IsRepoPathForPublicSpecRepo(string path);
        public string GetSpecRepoRootPath(string path);
        public string GetTypeSpecProjectRelativePath(string typeSpecProjectPath);
    }
    public class TypeSpecHelper : ITypeSpecHelper
    {
        private IGitHelper _gitHelper;
        private static readonly string SPEC_REPO_NAME = "azure-rest-api-specs";

        public TypeSpecHelper(IGitHelper gitHelper)
        {
            _gitHelper = gitHelper;
        }

        public bool IsValidTypeSpecProjectPath(string path)
        {
            return TypeSpecProject.IsValidTypeSpecProjectPath(path);
        }

        public bool IsTypeSpecProjectForMgmtPlane(string Path)
        {
            var typeSpecObject = TypeSpecProject.ParseTypeSpecConfig(Path);
            return typeSpecObject?.IsManagementPlane ?? false;
        }

        public bool IsRepoPathForPublicSpecRepo(string path)
        {
            var uri = _gitHelper.GetRepoRemoteUri(path);
            return uri.ToString().Contains(SPEC_REPO_NAME);
        }

        public string GetSpecRepoRootPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path cannot be null or empty.", nameof(path));
            }

            if (Directory.Exists(Path.Combine(path, "specification")))
            {
                return path;
            }

            // Get absolute path for repo root from given path.
            // Repo root is the parent of "specification" folder.
            var currentDirectory = new DirectoryInfo(path);
            while (currentDirectory != null && !currentDirectory.Name.Equals("specification"))
            {
                currentDirectory = currentDirectory.Parent;
            }
            return currentDirectory?.Parent?.FullName ?? string.Empty;
        }

        public string GetTypeSpecProjectRelativePath(string typeSpecProjectPath)
        {
            if (string.IsNullOrEmpty(typeSpecProjectPath) || !IsValidTypeSpecProjectPath(typeSpecProjectPath))
            {
                return string.Empty;
            }

            int specIndex = typeSpecProjectPath.IndexOf("specification");
            return specIndex >= 0 ? typeSpecProjectPath[specIndex..].Replace("\\", "/") : string.Empty;
        }
    }
}
