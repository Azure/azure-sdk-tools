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

`path` may select a directory or an individual Markdown/MDX file.
`relativeByRepoPath: true` keeps blob filenames relative to the repository root
for citations; scanning is limited to the selected `path`. Without a `path`,
processing still scans the entire available repository checkout. Ordinary sources
without `relativeByRepoPath` retain directory-relative filenames, or the basename
for an individual file. Existing Markdown/MDX, ignored-path, reference, dated
release-note, and metadata filters remain unchanged. Release-note index discovery
still checks the source root's `release-notes` directory, but skips it when outside
the selected path; it does not discover new nested release-note directories.

Previously, `relativeByRepoPath: true` used `path` only for sparse checkout and
scanned the shared repository root for every entry. Multiple entries sharing a
destination folder could produce duplicate blob keys and overwrite metadata.
Each entry now ingests only its configured path while preserving repository-relative
citations. Selected paths sharing a destination folder should not overlap,
especially when assigning different metadata.

This change applies globally to all `relativeByRepoPath` sources. Root or ancestor
Markdown incidentally fetched by sparse checkout but outside selected paths will
no longer be ingested. On a later authorized sync, `cleanupExpiredBlobs` can remove
previously indexed incidental blobs absent from the new results. Review the full
configuration and this migration impact before syncing; never run sync against a
partial source list, because cleanup removes blobs absent from its results.
Code/configuration changes alone do not sync, index, or deploy knowledge.
The companion .NET knowledge-source change, [#17253](https://github.com/Azure/azure-sdk-tools/pull/17253),
depends on this generic path-scoping change merging first.

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

SSH checkout uses the base64-encoded `SSH_PRIVATE_KEY` App Service setting.
Backend CD provisions it from the ADO Secure File named EMU-SSH-PRIVATE-KEY
for dev and production. Authorize the backend CD pipeline to use that secure file;
it may contain the original PEM/OpenSSH private key or its base64-encoded value.
The key is not included in the container image or WebJob ZIP. Local execution
requires the same environment variable for SSH repositories.

Private GitHub repositories use the configured token environment variable when
provided; otherwise each checkout obtains a short-lived GitHub App installation
token. App Configuration must provide `GITHUB_APP_ID`, `GITHUB_APP_KEY_NAME`, and
`GITHUB_APP_KEYVAULT_URL`, with optional `GITHUB_APP_INSTALLATION_OWNER` (default
`Azure`). App Service must provide `BOT_CLIENT_ID` to select the managed identity
with signing access to the shared Key Vault key. `AZURE_CLIENT_ID` selects the
identity for other Azure resources. The GitHub
App installation must have contents read access to the private repositories.
Git credentials are passed through the child process environment, not clone URLs.

Deploy using an authenticated Azure CLI session with permission to publish through
SCM using Microsoft Entra authentication. Backend CD automatically builds and
publishes the ZIP and deploys the `knowledge-sync` WebJob for dev and production;
preview deploys only the backend. The stage has separate `DeployServer` and
`DeployKnowledgeSync` jobs. Knowledge sync builds, tests, packages, publishes, and
deploys after the server job succeeds. Each CD run builds the ZIP from its own
checkout, including production runs.

Select the backend CD parameter `deploymentTarget`:
- `all` (default): deploy the server, then knowledge sync; preview deploys only the server.
- `server`: deploy only the server.
- `knowledge-sync`: build, test, and deploy only the knowledge-sync WebJob in dev
	or production, skipping the server and SSH key provisioning. Existing App Service
	settings must already be configured. Preview rejects this target.

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

## Continuous Integration

Azure DevOps runs `ci.yml` for knowledge-sync changes. It:

1. Installs dependencies
2. Builds the TypeScript project
3. Runs tests and publishes test results

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
