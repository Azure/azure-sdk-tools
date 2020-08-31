using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GlobalConfigurationProvider : IGlobalConfigurationProvider
    {
        public string GetApplicationID()
        {
            return "61253";
        }

        public string GetApplicationName()
        {
            return "check-enforcer";
        }

        public string GetKeyVaultUri()
        {
            return "https://checkenforcerstaging.vault.azure.net/";
        }

        public string GetGitHubAppPrivateKeyName()
        {
            return "github-app-private-key";
        }

        public string GetGitHubAppWebhookSecretName()
        {
            return "github-app-webhook-secret";
        }
    }
}
