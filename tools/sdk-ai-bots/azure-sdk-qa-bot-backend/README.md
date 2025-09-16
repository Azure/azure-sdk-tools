# Azure SDK QA Bot Backend

The Azure SDK QA Bot Backend is a service that powers a conversational assistant for Microsoft Teams, specifically designed to help developers with TypeSpec-related questions. It leverages Azure's AI services to provide accurate and context-aware responses by searching through comprehensive TypeSpec documentation.

## Overview

This service integrates with Microsoft Teams and provides an intelligent chatbot interface that can:
- Answer questions about TypeSpec syntax and usage
- Provide code examples and best practices
- Search through official TypeSpec documentation
- Assist with TypeSpec-related troubleshooting

## Features

The bot provides intelligent responses by searching through knowledge bases including:

- [TypeSpec documentation](https://typespec.io/docs/)
- [TypeSpec Azure documentation](https://azure.github.io/typespec-azure/docs/intro/)
- ......

Additional features include:

- Real-time document search and retrieval
- Context-aware responses
- Integration with Microsoft Teams
- Feedback collection for continuous improvement
- Intent recognition

## Prerequisites

- Go 1.23 or higher
- Azure subscription with access to the following services:
  - Azure App Configuration
  - Azure App Service
  - Azure AI Search
  - Azure Storage
  - Azure OpenAI
  - Azure Key Vault

## Installation and Setup

### Grant Resource Permissions

1. Configure Required Permissions:
   - **Assign the following roles to your azure account**
     - Storage Blob Data Contributor
     - Key Vault Secrets User
     - App Configuration Data Reader

### Project Setup

1. Clone the repository:

   ```bash
   cd tools/sdk-ai-bots/azure-sdk-qa-bot-backend
   ```

2. Install latest Go:

   ```bash
   sudo apt install golang-go
   ```

3. Install Azure Cli, reference https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?view=azure-cli-latest&pivots=msi

4. Login to Azure:

   ```bash
   az login
   ```

5. Start the server:

   ```bash
   ./run.sh start
   
   other commands:
   ./run.sh restart
   ./run.sh stop
   ./run.sh status
   ```

## API Usage

### Completion Endpoint
The main endpoint for querying the bot is `/completion`. [Here](test/api_test.rest) is an example of how to use it

The API_KEY could found in the [keyvalut](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.KeyVault/vaults/azuresdkqabotea/secrets)


## Development

### Project Structure

- `config/` - Configuration and Azure service setup
- `handler/` - HTTP request handlers
- `model/` - Data models and constants
- `service/` - Core business logic and service implementations
- `scripts/` - Utility scripts for maintenance
- `test/` - Test files and API tests

### Running Tests

```bash
go test ./...
```

## Deploy

  ```bash
  ./deploy.sh -t [tag] -e [dev|preview|prod]
  ```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request