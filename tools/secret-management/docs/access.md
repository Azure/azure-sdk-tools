# Access Management

This directory contains a tool to manage various access configurations for identities. Current functionality includes:

1. Create or Update [Applications and Service Principals](https://learn.microsoft.com/azure/active-directory/develop/app-objects-and-service-principals) in Azure Active Directory
1. Create [Role Assignments](https://learn.microsoft.com/azure/role-based-access-control/overview) for a Service Principal
    - These can be used to authorize an Application to access Azure resources like KeyVault secrets.
1. Create or Update [Federated Identity Credentials](https://learn.microsoft.com/graph/api/resources/federatedidentitycredentials-overview?view=graph-rest-1.0) in Microsoft Graph for an Application.
    - These can be used to authorize Github Actions to provide tokens on behalf of an Application.
1. Create [GitHub Repository Secrets](https://docs.github.com/actions/security-guides/encrypted-secrets).
    - These can be used to add configuration details for an Application to allow a GitHub Action to login with that identity and make requests to Azure.

Access management is done via a declarative configuration model. See the [test-configs](../Azure.Sdk.Tools.AccessManagement.Tests/test-configs) for example usage. The tool attempts to reconcile the state of the configuration file with the state in Azure, Graph and GitHub.

## Running the tool

### Logging in

This tool requires at bare minimum that the user be logged into Azure. See [install Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) or [install Az PowerShell Module](https://learn.microsoft.com/powershell/azure/install-az-ps?view=azps-9.6.0&viewFallbackFrom=azps-9.1.0).

```
# az cli
az login

# az powershell
Connect-AzAccount
```

Additionally the user must be logged into GitHub if any repository secrets are configured. See [GitHub CLI installation](https://cli.github.com/manual/installation).

```
gh auth login
```

Alternatively, you can use a [GitHub Personal Access Token](https://docs.github.com/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token).

```
# bash
export GITHUB_TOKEN="<pat>"

# powershell
env:GITHUB_TOKEN="<pat>"
```

### Tool execution

To run the tool directly from source, create a configuration file (see [test-configs](./tests/test-configs)) and run the following commands in `<repo root>/tools/secret-management>`;

```
dotnet run --project ./Azure.Sdk.Tools.SecretManagement.Cli -- sync-access -f <path to configuration file json>
```

## Development

This tool requires the [Net 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) framework, and the [Net 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) tooling.

To build just this project:

```
cd <repo root>/tools/secret-management/
dotnet build ./Azure.Sdk.Tools.AccessManagement
```

To test just this project:

```
cd <repo root>/tools/secret-management/
dotnet test ./Azure.Sdk.Tools.AccessManagement.Tests
```
