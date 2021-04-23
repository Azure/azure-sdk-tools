using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    public class RecordingHandler
    {
        public string CurrentBranch = "master";
        public IRepository Repository;
        public string RepoPath;

        public RecordingHandler(string targetDirectory)
        {
            try
            {
                Repository = new Repository(targetDirectory);
            }
            catch(Exception e)
            {
                Console.WriteLine("The configured storage directory is not a git repository. Git functionality will be unavailable.");
            }

            RepoPath = targetDirectory;
        }

        public string GetRecordingPath(string file)
        {
            return Path.Join(RepoPath, "recordings", file);
        }

        public void Commit()
        {
            foreach (var item in Repository.RetrieveStatus())
            {
                Commands.Stage(Repository, item.FilePath);
            }

            // TODO: pull the signature from local git creds, fall back to environment variable PAT. Some kind of generated message
            Repository.Commit("Updating Recordings.", new Signature("scbedd", "scbedd@microsoft.com", System.DateTimeOffset.Now), new Signature("scbedd", "scbedd@microsoft.com", System.DateTimeOffset.Now));
        }

        public void Checkout(string targetBranchName)
        {
            if (CurrentBranch != targetBranchName)
            {
                ResetAndCleanWorkingDirectory();

                var targetBranch = Repository.Branches[targetBranchName];

                if (targetBranch == null)
                {
                    Repository.CreateBranch(targetBranchName);
                }

                CurrentBranch = targetBranchName;
                Commands.Checkout(Repository, targetBranch);
            }
        }

        private void ResetAndCleanWorkingDirectory()
        {
            // Reset the index and the working tree.
            Repository.Reset(ResetMode.Hard);

            // Clean the working directory.
            Repository.RemoveUntrackedFiles();
        }
    }
}
