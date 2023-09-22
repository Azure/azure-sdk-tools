using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public static class DirectoryHelper
    {
        /// <summary>
        /// Recursively delete a git directory. Calling Directory.Delete(path, true), to recursiverly delete a directory
        /// that was populated from sparse-checkout, will fail. This is because the git files under .git\objects\pack 
        /// have file attributes on them that will cause an UnauthorizedAccessException when trying to delete them. In order
        /// to delete it, the file attributes need to be set to Normal.
        /// </summary>
        /// <param name="directory">The git directory to delete</param>
        public static void DeleteGitDirectory(string directory)
        {
            File.SetAttributes(directory, FileAttributes.Normal);

            string[] files = Directory.GetFiles(directory);
            string[] dirs = Directory.GetDirectories(directory);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteGitDirectory(dir);
            }

            Directory.Delete(directory, false);
        }
    }
}
