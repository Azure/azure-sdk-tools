# Azure SDK QA Bot Backend

The Azure SDK QA Bot Backend is designed to help developers with Azure SDK questions. This project builds on RAG framework, leverages Azure's AI services, AI search to provide accurate and context-aware responses by searching through knowledge base.

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
     - Storage Blob Data Contributor
     - Key Vault Secrets User
     - App Configuration Data Reader

### Project Setup

1. Clone the repository:

   ```bash
   cd tools/sdk-ai-bots/azure-sdk-qa-bot-backend
   ```

2. Install [Go 1.23.x](https://go.dev/doc/install) or later

3. Install Azure Cli, reference https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?view=azure-cli-latest&pivots=msi

4. Login to Azure:

   ```bash
   az login ### select the Azure SDK Engineering System subscription
   ```

5. Set environment
   Create .env file under the project root with this content:

   ```bash
   AZURE_APPCONFIG_ENDPOINT=https://azuresdkqabot-dev-config.azconfig.io
   ```

6. Start the server:

   ```bash
   go run main.go
   ```

## API Usage

The main endpoint for querying the bot is `/completion`. [Here](test/api_test.rest) is an example of how to use it

### How to get remote endpoint?

| Environment | Resource Group | App Service Name |
|---|---|---|
| **Prod** | `azure-sdk-qa-bot` | `azuresdkqabot-server` |
| **Preview** | `azure-sdk-qa-bot-test` | `azuresdkqabot-test-server` |
| **Dev** | `azure-sdk-qa-bot-dev` | `azuresdkqabot-dev-server` |

To find the endpoint:

1. Go to the [Azure Portal](https://portal.azure.com) and sign in with your Microsoft account.
2. Navigate to the **Azure SDK Engineering System** subscription.
3. Open the resource group for the target environment (see table above).
4. Select the App Service resource.
5. The endpoint URL is the **Default domain** field in the App Service overview.

### How to get access token?

- Prod: `az account get-access-token --resource api://azure-sdk-qa-bot`
- Preview: `az account get-access-token --resource api://azure-sdk-qa-bot-test`
- Dev: `az account get-access-token --resource api://azure-sdk-qa-bot-dev`

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

Before deploy, you need to install [Docker](https://docs.docker.com/engine/install/)

  ```bash
  ./deploy.sh -t [tag] -e [dev|preview|prod]
  ```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
