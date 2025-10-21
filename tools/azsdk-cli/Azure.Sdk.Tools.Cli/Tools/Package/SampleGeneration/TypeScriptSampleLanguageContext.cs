// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.SampleGeneration;

public sealed class TypeScriptSampleLanguageContext : ISampleLanguageContext
{
    public string Language => "typescript";
    public string FileExtension => ".ts";
    public string GetSampleGenerationInstructions() => @"
Language-specific instructions for TypeScript:
- Filenames must be descriptive without file extension (e.g., ""createKey"", ""retrieveKeys"")
- Follow this template:
" + GetSampleExample();
    public string GetSampleExample() => @"// Copyright (c) Microsoft Corporation.
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
    public Task<string> GetClientLibrarySourceCodeAsync(string packagePath, int totalBudget, int perFileLimit, ILogger? logger = null, CancellationToken ct = default)
    {
        static int Priority(FileHelper.FileMetadata f)
        {
            var name = Path.GetFileNameWithoutExtension(f.FilePath);
            return name.Contains("client", StringComparison.OrdinalIgnoreCase) ? 1 : 10;
        }
        var provider = new TypeScriptSourceInputProvider();
        var inputs = provider.Create(packagePath);
        return FileHelper.LoadFilesAsync(inputs, packagePath, totalBudget, perFileLimit, Priority, logger, ct);
    }
}
