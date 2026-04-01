# Technology Stack

**Analysis Date:** 2024-12-19

## Languages

**Primary:**
- Python 3.12 - All application code, services, agents, and utilities

**Secondary:**
- TypeSpec - API contract definition in `tsp/` directory

## Runtime

**Environment:**
- Python 3.12 (specified in `Dockerfile`)
- Azure Linux base image: `mcr.microsoft.com/azurelinux/base/python:3.12`

**Package Manager:**
- pip
- Lockfile: Not present (dependencies managed via `requirements.txt`)

## Frameworks

**Core:**
- FastAPI - Backend REST API server (`server.py`)
- Microsoft Agent Framework 1.0.0rc3 - AI agent orchestration
- Azure AI Agent Server Agent Framework 1.0.0b16 - Agent hosting infrastructure

**Testing:**
- pytest - Unit and integration testing (inferred from `@pytest.mark.asyncio` decorators)
- pytest-asyncio - Async test support

**Build/Dev:**
- uvicorn[standard] - ASGI server for FastAPI
- debugpy - Python debugger for development
- agent-dev-cli - Agent development CLI tool (preview)
- docker - Container builds (Dockerfile present)

## Key Dependencies

**Critical:**
- azure-ai-projects >=2.0.0b4 - Azure AI Foundry project client, agent orchestration
- azure-ai-agentserver-agentframework ==1.0.0b16 - Agent hosting protocol
- agent-framework >=1.0.0rc3 - Base agent framework with MCP tool support

**Azure Services:**
- azure-identity >=1.17.0 - Azure authentication (Managed Identity + CLI credential chain)
- azure-appconfiguration >=1.7.0 - Centralized configuration management
- azure-search-documents >=11.7.0b2 - AI Search SDK for knowledge retrieval
- azure-cosmos >=4.9.1 - Cosmos DB client for conversation storage
- azure-storage-blob >=12.20.0 - Blob storage for feedback Excel files
- azure-keyvault-keys >=4.9.0 - Key Vault crypto operations for GitHub App JWT signing

**Infrastructure:**
- python-dotenv >=1.0.0 - Environment variable management
- pyyaml >=6.0 - YAML configuration parsing
- openpyxl >=3.1.0 - Excel file manipulation for feedback storage
- httpx - HTTP client for GitHub API and Teams image fetching
- pydantic - Data validation and models

## Configuration

**Environment:**
- Configuration loaded from Azure App Configuration via `AZURE_APPCONFIG_ENDPOINT` env var
- Local development uses `.env` file (loaded via python-dotenv)
- Multi-tier credential chain: Managed Identity (production) → Azure CLI (local dev)
- Configuration centralized in `config/app_config.py` - all modules read from shared settings dict

**Build:**
- `Dockerfile` - Multi-stage container build
- `requirements.txt` - Python dependencies
- TypeSpec compilation: `tsp/main.tsp` defines API contracts
- Azure Pipelines: `pipelines/agent-cd.yml`, `pipelines/server-cd.yml`

**Required env vars (local dev):**
- `AZURE_APPCONFIG_ENDPOINT` - Azure App Configuration endpoint (mandatory)
- `GITHUB_TOKEN` - GitHub personal access token (optional, for local GitHub MCP tool testing)
- `UMI_BACKEND_CLIENT_ID` - User-assigned managed identity client ID (production)
- `UMI_FRONTEND_CLIENT_ID` - Frontend identity client ID (production)
- `BOT_CLIENT_ID` - Bot Framework identity for Teams image retrieval (production)
- `BOT_TENANT_ID` - Azure AD tenant for bot credential (production)

## Platform Requirements

**Development:**
- Python 3.10 or higher (3.12 recommended)
- Azure CLI installed and authenticated (`az login`)
- Virtual environment recommended (`.venv/`)
- VS Code with AI Toolkit extension (for agent debugging via F5)
- REST Client extension (for API testing via `tests/api_test.rest`)

**Production:**
- Azure App Service (Linux container) - Backend server deployment
- Azure AI Foundry - Hosted agent deployment
- Azure Container Registry - Container image storage
- Docker runtime - Container execution

**Azure Service Dependencies:**
- Azure App Configuration - Configuration management
- Azure AI Search - Knowledge base retrieval
- Azure Cosmos DB - Conversation history storage
- Azure Storage - Feedback Excel file storage
- Azure OpenAI / Azure AI Foundry - LLM inference
- Azure Key Vault - Secrets management (GitHub App private key)
- Microsoft Foundry Project - Agent hosting with chat model (e.g., gpt-5.4)

---

*Stack analysis: 2024-12-19*
