// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Samples;

public sealed class DotNetSampleLanguageContext : SampleLanguageContext
{
    public DotNetSampleLanguageContext(IFileHelper fileHelper) : base(fileHelper)
    {
    }

    public override string Language => "dotnet";
    public override string FileExtension => ".cs";

    protected override string GetLanguageSpecificInstructions() => @"Language-specific instructions for .NET:
- Filenames must be descriptive without file extension (e.g., ""CreateKey"", ""RetrieveKey"")
- IMPORTANT: When relevant, generate TWO separate samples for each scenario: one ending with 'Sync' (synchronous) and one ending with 'Async' (asynchronous)
- Use namespace of the form <client library namespace>.Tests.Samples
- The sample class should inherit from SamplesBase<T> where T is the appropriate TestEnvironment class
- Follow this template:
";
    public override string GetSampleExample() => @"// Copyright (c) Microsoft Corporation. All rights reserved.
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

    protected override ILanguageSourceInputProvider GetSourceInputProvider() => new DotNetSourceInputProvider();
}
