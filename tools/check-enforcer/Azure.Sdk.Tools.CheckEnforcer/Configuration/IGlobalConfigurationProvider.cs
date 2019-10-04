using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Configuration
{
    public interface IGlobalConfigurationProvider
    {
        string GetApplicationID();
        string GetApplicationName();
        string GetGitHubAppPrivateKeyName();
        string GetGitHubAppWebhookSecretName();
        string GetKeyVaultUri();

    }
}
