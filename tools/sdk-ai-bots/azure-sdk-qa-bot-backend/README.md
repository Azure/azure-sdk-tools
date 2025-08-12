# Azure SDK QA Bot Backend

The Azure SDK QA Bot Backend is a service that powers a conversational assistant for Microsoft Teams, specifically designed to help developers with TypeSpec-related questions. It leverages Azure's AI services to provide accurate and context-aware responses by searching through comprehensive TypeSpec documentation.

## Overview

This service integrates with Microsoft Teams and provides an intelligent chatbot interface that can:
- Answer questions about TypeSpec syntax and usage
- Provide code examples and best practices
- Search through official TypeSpec documentation
- Assist with TypeSpec-related troubleshooting

## Features

The bot provides intelligent responses by searching through comprehensive knowledge bases including:
- [TypeSpec documentation](https://typespec.io/docs/)
- [TypeSpec Azure documentation](https://azure.github.io/typespec-azure/docs/intro/)

Additional features include:
- Real-time document search and retrieval
- Context-aware responses
- Integration with Microsoft Teams
- Feedback collection for continuous improvement
- Intent recognition

## Prerequisites

- Go 1.23 or higher
- Azure subscription with access to the following services:
  - [Azure AI Search](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.Search/searchServices/typspehelper4search/overview)
  - [Azure Storage](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.Storage/storageAccounts/typespechelper4storage/overview)
  - [Azure OpenAI](https://ai.azure.com/build/deployments/model?wsid=/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.MachineLearningServices/workspaces/typespec-helper&tid=72f988bf-86f1-41af-91ab-2d7cd011db47)
  - [Azure Key Vault](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.KeyVault/vaults/azuresdkqabotea/overview)


## Installation and Setup

### Grant Resource Permissions

1. Configure Required Permissions:
   - **Assign the following roles to your virtual machine's managed identity/your azure account**
     - [Storage Blob Data Contributor](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.Storage/storageAccounts/typespechelper4storage/iamAccessControl)
     - [Key Vault Secrets User](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.KeyVault/vaults/azuresdkqabotea/users)

### Project Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/wanlwanl/wanl-fork-azure-sdk-tools.git
   cd wanl-fork-azure-sdk-tools
   git checkout azure-sdk-ai-bot
   cd tools/sdk-ai-bots/azure-sdk-qa-bot-backend
   ```

2. Install latest Go:
   ```bash
   sudo apt install golang-go
   ```
3. Install Azure Cli, reference https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?view=azure-cli-latest&pivots=msi
4. Login toAzure:
   ```bash
   az login
   ```
   Select subscription: Azure SDK Developer Playground
6. Start the server:
   ```bash
   ./run.sh start
   
   other commands:
   ./run.sh restart
   ./run.sh stop
   ./run.sh status
   ```

## API Usage

### Completion Endpoint
The main endpoint for querying the bot is `/completion`. [Here](test\api_test.rest) is an example of how to use it.

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
  ./deploy.sh -t [tag] -m [slot|preview|prod]
  ```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request