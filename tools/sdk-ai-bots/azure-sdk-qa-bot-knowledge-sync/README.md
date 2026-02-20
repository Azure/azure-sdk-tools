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

```
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
