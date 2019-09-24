using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Handlers;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubWebhookProcessor
    {
        public GitHubWebhookProcessor(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider)
        {
            this.globalConfigurationProvider = globalConfigurationProvider;
            this.gitHubClientProvider = gitHubClientProvider;
            this.repositoryConfigurationProvider = repositoryConfigurationProvider;
        }
        
        public IGitHubClientProvider gitHubClientProvider;
        public IGlobalConfigurationProvider globalConfigurationProvider;
        private IRepositoryConfigurationProvider repositoryConfigurationProvider;

        private const string GitHubEventHeader = "X-GitHub-Event";

        public async Task ProcessWebhookAsync(HttpRequest request, ILogger logger, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValue(GitHubEventHeader, out StringValues eventName))
            {
                if (eventName == "check_run")
                {
                    var handler = new CheckRunHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger);
                    await handler.HandleAsync(request.Body, cancellationToken);
                }
                else if (eventName == "check_suite")
                {
                    var handler = new CheckSuiteHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger);
                    await handler.HandleAsync(request.Body, cancellationToken);
                }
                else if (eventName == "issue_comment")
                {
                    var handler = new IssueCommentHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger);
                    await handler.HandleAsync(request.Body, cancellationToken);
                }
                else
                {
                    throw new CheckEnforcerUnsupportedEventException(eventName);
                }
            }
            else
            {
                throw new CheckEnforcerException($"Could not find header '{GitHubEventHeader}'.");
            }
        }
    }
}
