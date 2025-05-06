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
  - [Azure Key Vault](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.KeyVault/vaults/AzureSDKQABotConfig/overview)


## Installation and Setup

### Azure Virtual Machine Setup
1. Create an Azure Virtual Machine:
   - Navigate to [Azure Portal - Virtual Machines](https://ms.portal.azure.com/#view/Microsoft_Azure_ComputeHub/ComputeHubMenuBlade/~/virtualMachinesBrowse)
   - Create a new VM with Ubuntu (recommended)

2. Configure Required Permissions:
   - Assign the following roles to your virtual machine:
     - [Storage Blob Data Contributor](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.Storage/storageAccounts/typespechelper4storage/iamAccessControl)
     - [Key Vault Secrets User](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/typespec_helper/providers/Microsoft.KeyVault/vaults/AzureSDKQABotConfig/users)

### Project Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/wanlwanl/wanl-fork-azure-sdk-tools.git
   git checkout azure-sdk-ai-bot
   cd wanl-fork-azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend
   ```

2. Install latest Go:
   ```bash
   apt install golang-go
   ```

3. Start the server:
   ```bash
   go run .
   ```

## API Usage

### Completion Endpoint
The main endpoint for querying the bot is `/completion`. Here's an example of how to use it:

```bash
curl --request POST \
  --url http://localhost:8088/completion \
  --header 'content-type: application/json; charset=utf8' \
  --header 'x-api-key: YOUR_API_KEY' \
  --data '{
    "tenant_id": "azure_sdk_qa_bot",
    "message": {
      "role": "user",
      "content": "What is typespec?"
    }
  }'
```

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

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request