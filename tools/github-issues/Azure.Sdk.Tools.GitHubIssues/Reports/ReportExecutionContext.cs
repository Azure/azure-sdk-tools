using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Azure.Sdk.Tools.GitHubIssues.Reports
{
    public class ReportExecutionContext
    {
        public ReportExecutionContext(ILogger log, string fromAddress, string sendGridToken, string gitHubPersonalAccessToken, IEnumerable<RepositoryConfiguration> repositoryConfigurations, GitHubClient gitHubClient)
        {
            this.Log = log;
            this.FromAddress = fromAddress;
            this.SendGridToken = sendGridToken;
            this.GitHubPersonalAccessToken = gitHubPersonalAccessToken;
            this.RepositoryConfigurations = repositoryConfigurations;
            this.GitHubClient = gitHubClient;
        }

        public ILogger Log { get; private set; }
        public string FromAddress { get; private set; }
        public string SendGridToken { get; private set; }
        public string GitHubPersonalAccessToken { get; private set; }
        public IEnumerable<RepositoryConfiguration> RepositoryConfigurations { get; set; }
        public GitHubClient GitHubClient { get; private set; }
    }
}
