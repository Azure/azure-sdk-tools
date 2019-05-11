using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PipelineGenerator
{
    public class RepositoryHelper
    {
        public string GetRepositoryRoot(DirectoryInfo path)
        {
            // This code is a little sloppy. Looking to bring in a dependency
            // on LibGit2Sharp to make this more robust, but this will do for now.
            var currentPath = path;

            while (true)
            {
                if (path.GetDirectories(".git").Length > 0)
                {
                    return path.FullName;
                }
                else
                {
                    path = path.Parent;
                }
            }
        }
    }
}
