---
name: dev-setup
description: "Set up the local development environment for APIView Copilot. Use for: dev setup, environment setup, install dependencies, create venv, configure env, permissions, get started, onboarding, new developer, RBAC setup, .env file."
---

# Development Environment Setup

## Procedure

### 1. Create and activate a virtual environment

```bash
# Windows
python -m venv .venv
.venv\Scripts\activate

# Linux/macOS
python -m venv .venv
source .venv/bin/activate
```

### 2. Install dependencies

```bash
# Runtime + dev + eval dependencies
pip install -r dev_requirements.txt

# Runtime only
pip install -r requirements.txt
```

### 3. Create the `.env` file

Create a `.env` file in the project root with:
```
ENVIRONMENT_NAME="staging"
```

All other settings (Cosmos DB, Search, Key Vault, etc.) are resolved from App Configuration at runtime.

### 4. Grant Azure RBAC permissions

```bash
avc ops grant
```

This grants your identity the required roles:
- **App Configuration Data Reader** on `avc-appconfig-staging`
- **Search Index Data Reader** on Azure AI Search
- **Cosmos DB Contributor** on `avc-cosmos-staging`
- **Key Vault access** on `avc-vault-staging`
- **Cognitive Services OpenAI User** on `azsdk-engsys-openai`
- **Azure AI User** on `azsdk-engsys-ai`

Pass `--assignee-id <ID>` to grant to a specific user instead of yourself.

### 5. Verify the setup

```bash
# Run unit tests
pytest tests

# Generate a review (needs a test API view file in scratch/apiviews/<lang>/)
avc review generate -l python -t scratch/apiviews/python/test.txt

# Health check the deployed service
avc ops check
```

## Gotchas

- **Always activate the venv** before running any commands (`avc`, `pytest`, `pylint`, etc.)
- **`avc` not found?** Use `.\avc` on Windows PowerShell, or ensure the project root is in your PATH
- **`ModuleNotFoundError: prompty`** — You're running outside the venv. Activate it first.
- **Azure auth errors** — Run `az login` first, then `avc ops grant` to set up RBAC
- **Staging vs production** — The `.env` defaults to staging. To use production, swap to the production App Config endpoint and set `ENVIRONMENT_NAME="production"`
