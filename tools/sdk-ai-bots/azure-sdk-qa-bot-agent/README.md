# Azure SDK Chat Agent

The Azure SDK Chat Agent helps developers with Azure SDK questions. This project is a migration of the [azure-sdk-qa-bot-backend](../azure-sdk-qa-bot-backend/) from Go to Python, built on the [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview) with Azure AI Foundry. It leverages Azure AI Search for knowledge retrieval and Foundry Memory for conversation context.

> **Note:** This project is currently in draft / active development.

## Project Structure

```
azure-sdk-qa-bot-agent/
├── agents/          # Agent definition, instructions, and tool implementations
├── config/          # Configuration (Azure App Configuration integration)
├── services/        # Core business logic (chat, conversation, feedback)
├── skills/          # Agent Skills (per-tenant + remote azure-api-review skill)
├── utils/           # Azure service clients (AI Foundry, AI Search, Cosmos DB, Storage, Memory)
├── models/          # Data models
├── pipelines/       # CI/CD pipeline definitions
├── tests/           # Test files
├── server.py        # Backend API entrypoint (FastAPI)
└── TROUBLESHOOTING.md
```

The project has two independently runnable components:

| Component | Description | Entrypoint | Port |
|-----------|-------------|------------|------|
| **Agent** | AI chat agent (Microsoft Agent Framework, Responses protocol) | `agents/chat_agent/init.py` | 8088 |
| **Server** | Backend API that the Teams App communicates with (FastAPI) | `server.py` | 8089 |

## Getting Started

### Prerequisites

- Python 3.10 or higher
- Azure CLI installed and authenticated (`az login`)
- Azure subscription with access to:
  - Azure App Configuration
  - Azure AI Search
  - Azure Storage
  - Azure OpenAI / Azure AI Foundry
  - Microsoft Foundry Project

### Required Azure Roles

Ensure your Azure identity has:

- App Configuration Data Reader
- Storage Blob Data Contributor
- Azure AI User (on the Foundry Project)
- Cosmos DB Built-in Data Contributor

### Setup

1. Navigate to the project:

   ```bash
   cd tools/sdk-ai-bots/azure-sdk-qa-bot-agent
   ```

2. Create and activate a virtual environment:

   **Windows (PowerShell):**

   ```powershell
   python -m venv .venv
   .\.venv\Scripts\Activate.ps1
   ```

   **macOS/Linux:**

   ```bash
   python -m venv .venv
   source .venv/bin/activate
   ```

3. Install dependencies:

   ```bash
   pip install -r requirements-dev.txt
   ```

   This installs all production dependencies plus development tools (`debugpy`, `agent-dev-cli`). CI/CD pipelines and Docker images use `requirements.txt` (production only).

4. Create a `.env` file in the project root:

   ```dotenv
   AZURE_APPCONFIG_ENDPOINT=https://azuresdkqabot-dev-config.azconfig.io
   ```

   Optional variables:

   | Variable | Purpose | Default |
   |----------|---------|---------|
   | `GITHUB_TOKEN` | [GitHub PAT](https://github.com/settings/tokens) for local GitHub MCP tool testing. Without it, the agent uses GitHub App JWT via Key Vault (production only). | — |
   | `MEMORY_UPDATE_DELAY` | Seconds before processing memory updates. Set to `0` for immediate updates during development. | `300` |

5. Log in to Azure:

   ```bash
   az login  # select the Azure SDK Engineering System subscription
   ```

## Running and Debugging Locally

### Debugging the Agent (F5 with AI Toolkit)

Use this to develop and test the AI agent itself (prompt tuning, tool integration, etc.).

1. Install the [AI Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-windows-ai-studio.windows-ai-studio) extension for VS Code.
2. Use this instruction to let your copilot set up local debugging with the AI Toolkit: `Help me configure the azure-sdk-qa-bot-agent/agents to work with AI Toolkit Agent Inspector. 1) Ensure the agent is serverized as an HTTP server. 2) Install 'agent-dev-cli' and use 'agentdev' to launch the agent. 3) Add VS Code configuration (tasks.json and launch.json) for debugging.`
3. Copilot will automatically generate the debug configuration (`.vscode/launch.json` and `.vscode/tasks.json`) for the project.
4. Press **F5** to start debugging.

This launches the agent via `agentdev run` on `http://localhost:8088/` with `debugpy` attached, and opens the AI Toolkit Agent Inspector for interactive testing.

![Agent Playground](images/agent_playground.png)

### Debugging the Server

1. Add a launch configuration for the FastAPI server:

   ```json
   {
       "name": "Debug Backend Server",
       "type": "debugpy",
       "request": "launch",
       "module": "uvicorn",
       "args": ["server:app", "--host", "0.0.0.0", "--port", "8089"],
       "cwd": "${workspaceFolder}"
   }
   ```

2. Press **F5** to start debugging the server.
3. Install the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension.
4. Run tests under `tests/api_test.rest` to verify the server is working.

## Testing Remote Endpoints

The main endpoint for querying the bot is `/agent/chat`. See [tests/api_test.rest](tests/api_test.rest) for example requests.

### Remote Endpoints

| Environment | Resource Group | App Service Name |
|---|---|---|
| **Dev** | `azure-sdk-qa-bot-dev` | `azuresdkqabot-dev-server` |
| **Preview** | `azure-sdk-qa-bot-test` | `azuresdkqabot-test-server` |
| **Prod** | `azure-sdk-qa-bot` | `azuresdkqabot-server` |

To find the App Service in the Azure Portal:

1. Go to the [Azure Portal](https://portal.azure.com) and sign in with your Microsoft account.
2. Navigate to the **Azure SDK Engineering System** subscription.
3. Open the resource group for the target environment (see table above).
4. Select the App Service resource.
5. The endpoint URL is the **Default domain** field in the App Service overview.

### Access Tokens

```bash
# Dev
az account get-access-token --resource api://azure-sdk-qa-bot-dev

# Preview
az account get-access-token --resource api://azure-sdk-qa-bot-test

# Prod
az account get-access-token --resource api://azure-sdk-qa-bot
```

## Deployment

The project has three independently deployable components, each with its own CD pipeline. All pipelines are manually triggered and parameterized by environment.

### Agent Deploy

Builds the agent container image, pushes to ACR, and deploys a new hosted agent version to Azure AI Foundry.

- **Pipeline**: [agent-cd.yml](pipelines/agent-cd.yml) | [Run in ADO](https://dev.azure.com/azure-sdk/internal/_build?definitionId=8159)
- **Deploy script**: [scripts/deploy_hosted_agent.py](scripts/deploy_hosted_agent.py)
- **Parameters**: `environment` (dev/prod), `agentName` (chat_agent)
- **What it does**:
  1. Builds the Docker image from `agents/chat_agent/Dockerfile`
  2. Pushes to `azuresdkqabotcontainer.azurecr.io`
  3. Runs `deploy_hosted_agent.py` to create a new agent version via Foundry API

**Manual deploy** (from project root):

```bash
python scripts/deploy_hosted_agent.py chat_agent --tag <image-tag>
```

### Server Deploy

Builds the backend API (FastAPI) container image and deploys to Azure App Service.

- **Pipeline**: [server-cd.yml](pipelines/server-cd.yml) | [Run in ADO](https://dev.azure.com/azure-sdk/internal/_build?definitionId=8128)
- **Parameters**: `environment` (dev/preview/prod), `slot` (default/agent)
- **What it does**:
  1. Resolves image tag from `_version.py` (prod) or git short SHA (dev)
  2. Builds and pushes image to ACR via `az acr build`
  3. Deploys to App Service using container image reference

### Logic App Deploy

Deploys the Logic App ARM template for Teams channel message mirroring.

- **Pipeline**: [logicapp-cd.yml](pipelines/logicapp-cd.yml) | [Run in ADO](https://dev.azure.com/azure-sdk/internal/_build?definitionId=8177)
- **Parameters**: `environment` (dev/test/prod)
- **What it does**:
  1. Runs `az deployment group create` with environment-specific ARM template parameters
  2. Idempotent — safe to re-run

### CI Pipeline

Runs linting (pyright) and unit tests on PRs that touch the bot agent code.

- **Pipeline**: [server-ci.yml](pipelines/server-ci.yml) | [View in ADO](https://dev.azure.com/azure-sdk/internal/_build?definitionId=8156)
- **Triggers**: PRs to `main` touching `tools/sdk-ai-bots/azure-sdk-qa-bot-agent`

## Troubleshooting

For incident response, agent tracing, and server log analysis, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
