using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    public class DirectoryUtils
    {
        internal class DirectoryEvaluation
        {
            public bool IsRoot;
            public bool IsGitRoot;
        }

        public static string AscendToRepoRoot(string path)
        {
            var originalPath = path.Clone();
            var fileAttributes = File.GetAttributes(path);
            if (!(fileAttributes.HasFlag(FileAttributes.Directory)))
            {
                path = Path.GetDirectoryName(path);
            }

            while (true)
            {
                var evaluation = EvaluateDirectory(path);

                if (evaluation.IsGitRoot)
                {
                    return path;
                }
                else if (evaluation.IsRoot)
                {
                    return null;
                }

                path = Path.GetDirectoryName(path);
            }
        }

        internal static DirectoryEvaluation EvaluateDirectory(string directoryPath)
        {
            var fileAttributes = File.GetAttributes(directoryPath);

            if (!(fileAttributes.HasFlag(FileAttributes.Directory)))
            {
                directoryPath = Path.GetDirectoryName(directoryPath);
            }

            var gitLocation = Path.Join(directoryPath, ".git");

            return new DirectoryEvaluation()
            {
                IsGitRoot = Directory.Exists(gitLocation) || File.Exists(gitLocation),
                IsRoot = new DirectoryInfo(directoryPath).Parent == null
            };
        }

        public static string FindFileInRepository(string fileName, string subdir = null)
        {
            // Ascend to the repo root and look for the file
            string root = AscendToRepoRoot(Directory.GetCurrentDirectory());
            if (null != root)
            {
                string startDir = root;
                if (null != subdir)
                {
                    startDir = Path.Combine(root, subdir);
                }
                List<string> files =
                    new DirectoryInfo(startDir)
                    .EnumerateFiles(fileName, SearchOption.AllDirectories)
                    .Select(d => d.FullName).ToList();

                if (files.Count == 1)
                {
                    return files[0];
                }
            } 
            else
            {
                // JRS - need to error here
            }
            return null;
        }
    }
}
