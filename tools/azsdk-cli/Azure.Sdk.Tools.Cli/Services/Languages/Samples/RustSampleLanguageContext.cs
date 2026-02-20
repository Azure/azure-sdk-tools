// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Resources;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Azure.Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class RustSampleLanguageContext(IFileHelper fileHelper, IGitHelper gitHelper) : SampleLanguageContext(fileHelper)
{
    private string? workspacePath = null;

    public override string Language => "rust";

    public override string FileExtension => ".rs";

    protected override string GetLanguageSpecificInstructions()
    {
        // Rust already has a lot of language-specific instructions in the repository that are used by GitHub coding agent, Copilot, etc., that we want to rely on.
        var workspacePath = this.workspacePath ?? throw new InvalidOperationException("Workspace directory not set");
        return @$"
Language-specific instructions for Rust:
- Follow instructions in the [AGENTS.md](${workspacePath}/AGENTS.md) file
";
    }

    public override string GetSampleExample()
    {
        // Though the Rust repository alreayd has a lot of examples, at this time they are probably not what most services will want or need:
        //
        // 1. Rust examples that work either as standalone executables or as mocked tests.
        // 2. Extensive CLIs like `sdk/cosmos/azure_data_cosmos/examples/cosmos/` that are far larger than most services would want or from which consumers would benefit.
        var workspacePath = this.workspacePath ?? throw new InvalidOperationException("Workspace directory not set");
        var samplePath = Path.Combine(workspacePath, "sdk", "keyvault", "azure_security_keyvault_secrets", "examples", "secret_client_list_secrets.rs");

        if (File.Exists(samplePath))
        {
            return File.ReadAllText(samplePath);
        }

        return """
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

use azure_identity::DeveloperToolsCredential;
use azure_security_keyvault_secrets::{ResourceExt, SecretClient};
use futures::TryStreamExt;
use std::env;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Get Key Vault URL from command line argument or environment variable
    let vault_url = env::args()
        .nth(1)
        .or_else(|| env::var("AZURE_KEYVAULT_URL").ok())
        .ok_or("Key Vault URL must be provided as an argument or in AZURE_KEYVAULT_URL environment variable")?;

    // Create a new secret client
    let credential = DeveloperToolsCredential::new(None)?;
    let client = SecretClient::new(&vault_url, credential, None)?;

    // List all secrets and collect their names
    let mut secret_names = Vec::new();
    let mut pager = client.list_secret_properties(None)?;
    while let Some(secret) = pager.try_next().await? {
        let name = secret.resource_id()?.name;
        secret_names.push(name);
    }

    // Sort the secret names
    secret_names.sort();

    // Print each secret name on its own line
    for name in secret_names {
        println!("{}", name);
    }

    Ok(())
}
""";
    }

    public override async Task<string> LoadContextAsync(IEnumerable<string> paths, int totalBudget, int perFileLimit, CancellationToken ct = default)
    {
        if (!paths.Any())
        {
            throw new ArgumentException("At least one path must be provided", nameof(paths));
        }

        var allPaths = paths.ToList();
        var packagePath = paths.First();

        workspacePath = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        allPaths.Add(workspacePath);

        return await base.LoadContextAsync(allPaths, totalBudget, perFileLimit, ct);
    }

    protected override ILanguageSourceInputProvider GetSourceInputProvider() => new RustSourceInputProvider(workspacePath ?? throw new InvalidOperationException("Workspace directory not set"));
}
