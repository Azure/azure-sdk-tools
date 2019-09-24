using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Octokit;
using Octokit.Internal;
using System.Collections;
using System.Collections.Generic;
using Azure.Security.KeyVault.Keys;
using Azure.Identity;
using System.Threading;
using Azure.Security.KeyVault.Keys.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;
using Azure.Core;
using System.Web.Http;
using System.Runtime.CompilerServices;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public static class GitHubWebhookFunction
    {
        private static IGlobalConfigurationProvider globalConfigurationProvider = new GlobalConfigurationProvider();
        private static IGitHubClientProvider gitHubClientProvider = new GitHubClientProvider(globalConfigurationProvider);
        private static IRepositoryConfigurationProvider repositoryConfigurationProvider = new RepositoryConfigurationProvider(gitHubClientProvider);

        [FunctionName("webhook")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, CancellationToken cancellationToken)
        {
            try
            {
                var processor = new GitHubWebhookProcessor(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider);
                await processor.ProcessWebhookAsync(req, log, cancellationToken);
                return new OkResult();
            }
            catch (CheckEnforcerUnsupportedEventException ex)
            {
                log.LogWarning(ex, "An error occured because the event is not supported.");
                return new BadRequestResult();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "An error occured processing the webhook.");
                return new InternalServerErrorResult();
            }
        }

    }
}
