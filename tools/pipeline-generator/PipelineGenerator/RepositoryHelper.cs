using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineGenerator
{
    public class RepositoryHelper
    {
        public string GetRepositoryRelativePath(string path)
        {
            return "/sdk/servicebus/ci.yml";
        }

        public string GetRepositoryOrigin(string path)
        {
            return "https://github.com/azure/azure-sdk-for-net";
        }
    }
}
