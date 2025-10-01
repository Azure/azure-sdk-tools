// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.SampleGeneration
{
    /// <summary>
    /// Contains code templates for different programming languages used in sample generation.
    /// </summary>
    internal static class CodeTemplates
    {
        /// <summary>
        /// .NET sample template following Azure SDK conventions.
        /// </summary>
        public const string Dotnet = @"// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.TestFramework;
using Azure.Template.Models;
using NUnit.Framework;

namespace Azure.Template.Tests.Samples
{
    public partial class TemplateSamples: SamplesBase<TemplateClientTestEnvironment>
    {
        [Test]
        [AsyncOnly]
        public async Task GettingASecretAsync()
        {
            #region Snippet:Azure_Template_GetSecretAsync
#if SNIPPET
            string endpoint = ""https://myvault.vault.azure.net"";
            var credential = new DefaultAzureCredential();
#else
            string endpoint = TestEnvironment.KeyVaultUri;
            var credential = TestEnvironment.Credential;
#endif
            var client = new TemplateClient(endpoint, credential);

            SecretBundle secret = await client.GetSecretValueAsync(""TestSecret"");

            Console.WriteLine(secret.Value);
            #endregion

            Assert.NotNull(secret.Value);
        }
    }
}";

        /// <summary>
        /// TypeScript sample template following Azure SDK conventions.
        /// </summary>
        public const string TypeScript = @"// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/**
 * @summary Demonstrates the use of a ConfigurationClient to retrieve a setting value.
 */

import { ConfigurationClient } from ""@azure/template"";
import { DefaultAzureCredential } from ""@azure/identity"";

// Load the .env file if it exists
import ""dotenv/config"";

async function main(): Promise<void> {
  const endpoint = process.env.APPCONFIG_ENDPOINT || ""<endpoint>"";
  const key = process.env.APPCONFIG_TEST_SETTING_KEY || ""<test-key>"";

  const client = new ConfigurationClient(endpoint, new DefaultAzureCredential());

  const setting = await client.getConfigurationSetting(key);

  console.log(""The setting has a value of:"", setting.value);
  console.log(""Details:"", setting);
}

main().catch((err) => {
  console.error(""The sample encountered an error:"", err);
});";
    }
}
