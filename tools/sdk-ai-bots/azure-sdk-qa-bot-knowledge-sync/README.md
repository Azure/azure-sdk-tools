# Azure SDK QA Bot Knowledge Sync

This is a standalone TypeScript application which processes documentation from various repositories and uploads processed content for the Azure SDK QA Bot.

## Features

- Clones and processes documentation from multiple repositories
- Extracts and processes markdown files
- Handles Spector test files with @scenario annotations
- Uploads processed content to Azure Blob Storage
- Maintains change detection for efficient processing
- Supports multiple authentication methods (public, token, SSH)

## Project Structure

```text
azure-sdk-qa-bot-knowledge-sync/
├── src/
│   ├── index.ts                    # Main entry point (calls DailySyncKnowledge)
│   ├── DailySyncKnowledge.ts      # Core processing logic
│   └── services/
│       ├── ConfigurationLoader.ts # Configuration loading and transformation
│       ├── SpectorCaseProcessor.ts # TypeSpec spector tests processing
│       ├── StorageService.ts      # Azure Blob Storage operations
│       └── SearchService.ts       # Azure AI Search operations
├── config/
│   ├── knowledge-config.json      # Repository and documentation configuration
│   └── knowledge-config.schema.json # JSON schema for configuration
├── package.json
├── tsconfig.json
└── README.md
```

## Configuration

The application uses `config/knowledge-config.json` to define which repositories and documentation paths to process. The configuration includes:

- Repository URLs and authentication settings
- Documentation paths within repositories  
- Processing options and filters

## Building and Running

### Prerequisites

- Node.js 20 or higher
- TypeScript
- Access to the configured repositories

### Installation

```bash
npm install
```

### Building

```bash
npm run build
```

### Running

```bash
npm run start
```

Or for development:

```bash
npm run dev
```

## Environment Variables

- `AZURE_APPCONFIG_ENDPOINT`

## App Service WebJob

Build the ZIP on Linux x64 with Node 24, npm, curl, tar, zip, and unzip available.
Run `npm ci`, then `npm run build:webjob -- <output.zip>`. The package contains
compiled code, configuration, production dependencies, and a pinned Node runtime.
It does not install or build anything when the job runs.

The launcher uses the existing App Service environment, including
`AZURE_APPCONFIG_ENDPOINT` and `AZURE_CLIENT_ID`. Git and OpenSSH must be available
in the WebJob execution environment. App Configuration must provide
`ADO_RESOURCE_SCOPE` for private Azure Repos checkout; the identity also needs
Azure DevOps repository read access and access to the configured Azure resources.

Private GitHub repositories use the configured token environment variable when
provided; otherwise each checkout obtains a short-lived GitHub App installation
token. App Configuration must provide `GITHUB_APP_ID`, `GITHUB_APP_KEY_NAME`, and
`GITHUB_APP_KEYVAULT_URL`, with optional `GITHUB_APP_INSTALLATION_OWNER` (default
`Azure`). The identity needs signing access to the Key Vault key, and the GitHub
App installation must have contents read access to the private repositories.
Git credentials are passed through the child process environment, not clone URLs.

Deploy using an authenticated Azure CLI session with permission to publish through
SCM using Microsoft Entra authentication. Backend CD automatically builds and
publishes the ZIP and deploys the `knowledge-sync` WebJob for dev and production;
preview deploys only the backend. Each CD run builds the ZIP from its checked-out
source, including production runs.

For a standalone deployment, set `WEBJOB_ZIP`, `APP_NAME`, `RESOURCE_GROUP`, and
`WEBJOB_NAME` on the deployment machine, then run `node scripts/deploy-webjob.mjs`.
These are deployment inputs, not App Service application settings. Backend CD
supplies them using the artifact path and existing environment-specific variables.
The script uploads only the named triggered WebJob, refuses to overwrite a running
job, and verifies its schedule. Enabling Always On, the Kudu agent, and persistent
storage may restart the backend app.

The job runs daily at **02:00 UTC**, using the schedule in `webjob/settings.job`.
Disable the previous ADO sync schedules before deploying the scheduled WebJob to
avoid concurrent writers. Use the appropriate existing app configuration for each
dev and production deployment.

## Azure DevOps Pipeline

The project is designed to run in Azure DevOps pipelines via `sync_knowledge.yml`, which:

1. Sets up Node.js environment
2. Installs dependencies
3. Builds the TypeScript project
4. Executes the knowledge sync process

## Architecture

This standalone application about setting up knowleage base for chatbot:

1. **Configuration Loading**: Reads and transforms repository configurations
2. **Repository Management**: Clones/updates documentation repositories
3. **Content Processing**: Extracts and processes markdown files
4. **Change Detection**: Compares content with existing storage
5. **Upload**: Stores processed content in Azure Blob Storage
6. **Search Integration**: Updates Azure AI Search indexes

## Development

The `src/index.ts` file provides a minimal wrapper that:

- Calls the original `processDailySyncKnowledge` function
- Handles errors and logging

This approach allows the original Azure Function code to be reused with minimal modifications.
