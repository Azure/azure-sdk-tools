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
            var id = Environment.GetEnvironmentVariable("GITHUBAPP_ID");
            return id;
        }

        public string GetApplicationName()
        {
            var applicationName = Environment.GetEnvironmentVariable("CHECK_NAME");
            return applicationName;
        }

        public string GetKeyVaultUri()
        {
            var keyVaultUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            return keyVaultUri;
        }

        public string GetGitHubAppPrivateKeyName()
        {
            var gitHubAppPrivateKeyName = Environment.GetEnvironmentVariable("KEYVAULT_GITHUBAPP_KEY_NAME");
            return gitHubAppPrivateKeyName;
        }

        public string GetGitHubAppWebhookSecretName()
        {
            var gitHubAppWebhookSecretName = Environment.GetEnvironmentVariable("GITHUBAPP_WEBHOOK_SECRET");
            return gitHubAppWebhookSecretName;
        }

        public string GetDistributedLockStorageUri()
        {
            var distributedLockStorageUri = Environment.GetEnvironmentVariable("DISTRIBUTED_LOCK_STORAGE_URI");
            return distributedLockStorageUri;
        }

        public string GetDistributedLockContainerName()
        {
            var distributedLockContainerName = Environment.GetEnvironmentVariable("DISTRIBUTED_LOCK_CONTAINER_NAME");
            return distributedLockContainerName;
        }
    }
}
