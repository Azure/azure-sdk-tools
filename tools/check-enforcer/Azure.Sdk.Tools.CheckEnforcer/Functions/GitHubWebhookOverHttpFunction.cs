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

namespace Azure.Sdk.Tools.CheckEnforcer.Functions
{
    public class GitHubWebhookOverHttpFunction
    {
        public GitHubWebhookOverHttpFunction(GitHubWebhookProcessor processor)
        {
            this.processor = processor;
        }

        private GitHubWebhookProcessor processor;

        [FunctionName("webhook")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log, CancellationToken cancellationToken)
        {
            try
            {
                await processor.ProcessWebhookAsync(req, log, cancellationToken);
                return new OkResult();
            }
            catch (CheckEnforcerSecurityException ex)
            {
                log.LogError(ex, "Webhook failed to pass security checks.");
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
