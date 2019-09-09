using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GitHubJwt;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHubJwt
{
    public class KeyVaultPrivateKeySource : IPrivateKeySource
    {
        public TextReader GetPrivateKeyReader()
        {
            try
            {
                // TODO: This is currently broken due to a compat issue with Azure Functions
                //       and Azure.Core, specifically a System.Diagnostics.DiagnosticSource
                //       version conflict. We currently depend on a preview version of 4.6
                //       and the Azure Functions runtimes depends on 4.5.1 which is missing
                //       some types/methods.
                var keyVaultUriEnvironmentVariable = Environment.GetEnvironmentVariable("KEYVAULT_URI");
                var keyVaultUri = new Uri(keyVaultUriEnvironmentVariable);
                var client = new SecretClient(keyVaultUri, new DefaultAzureCredential());

                var keyVaultGitHubSecretName = Environment.GetEnvironmentVariable("KEYVAULT_GITHUBAPP_SECRET_NAME");
                var response = client.Get(keyVaultGitHubSecretName);

                var reader = new StringReader(response.Value.Value);

                return reader;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
