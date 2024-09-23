using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using NUnit.Framework;
using Octokit.Internal;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests
{
    /// <summary>
    /// Testing base class. Each static test is going to be using Octokit's SimpleJsonSerializer to deserialize
    /// the json payload.
    /// </summary>
    public class ProcessingTestBase
    {
        // SimpleJsonSerializer is OctoKit's serializer used for deserializing GitHub action payloads
        SimpleJsonSerializer _serializer = new SimpleJsonSerializer();

        public SimpleJsonSerializer SimpleJsonSerializer
        {
            get => _serializer;
        }
    }
}
