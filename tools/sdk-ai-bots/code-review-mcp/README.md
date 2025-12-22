# Azure SDK Code Review MCP Server

A Model Context Protocol (MCP) server that provides a tool to review SDK code against Azure SDK guidelines.

## Prerequisites

- Python 3.10+
- The Azure SDK QA Bot backend running locally at `http://localhost:8088`

## Installation

```bash
cd tools/sdk-ai-bots/code-review-mcp
pip install -r requirements.txt
```

## Usage

### 1. Start the Code Review Backend

First, make sure the Azure SDK QA Bot backend is running:

```bash
cd tools/sdk-ai-bots/azure-sdk-qa-bot-backend
go run main.go
```

### 2. Configure VS Code to use this MCP Server

Add the following to your VS Code `settings.json` or workspace settings:

```json
{
  "mcp": {
    "servers": {
      "azure-sdk-code-review": {
        "command": "python",
        "args": ["c:/Users/jiaqzhang/work/azure-sdk-tools/tools/sdk-ai-bots/code-review-mcp/server.py"]
      }
    }
  }
}
```

Or if you prefer using a virtual environment:

```json
{
  "mcp": {
    "servers": {
      "azure-sdk-code-review": {
        "command": "c:/path/to/venv/Scripts/python.exe",
        "args": ["c:/Users/jiaqzhang/work/azure-sdk-tools/tools/sdk-ai-bots/code-review-mcp/server.py"]
      }
    }
  }
}
```

### 3. Use the Tool in Copilot Chat

Once configured, you can use the `review_sdk_code` tool in GitHub Copilot Chat:

```
@workspace Use the review_sdk_code tool to check this Go code:

package armstorage

type storageAccountsClient struct {
    internal       *arm.Client
    subscriptionID string
}

func NewstorageAccountsClient(subscriptionID string, credential azcore.TokenCredential, options *arm.ClientOptions) (*storageAccountsClient, error) {
    // ...
}
```

## Tool: review_sdk_code

### Description

Reviews SDK code against Azure SDK guidelines and returns comments about potential violations.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `language` | string | Yes | Programming language: `go`, `python`, `java`, `javascript`, `dotnet` |
| `code` | string | Yes | The code content to review |
| `file_path` | string | No | Relative file path (helps identify file type for appropriate guidelines) |

### Example Response

```
# Code Review Results
**Review ID:** 0034e570-3d91-47bf-8874-c714ad9c61c7
**Language:** go
**Summary:** Found 7 potential issues or suggestions for improvement.

## Found 7 Issue(s):

### Issue 1

**Problem:** Service client type should be exported and use the Client suffix in its name.

**Bad Code:**
```
type storageAccountsClient struct {
```

**Suggested Fix:**
```
type StorageAccountsClient struct {
```

**Guideline:** [golang-client-naming](https://azure.github.io/azure-sdk/golang_introduction.html)

---
```

## Configuration

To change the API URL, modify the `CODE_REVIEW_API_URL` constant in `server.py`:

```python
CODE_REVIEW_API_URL = "http://localhost:8088/code_review"
```

## Testing Locally

You can test the MCP server directly:

```bash
# Run the server (it communicates via stdin/stdout)
python server.py

# Or test with mcp-cli if installed
mcp-cli --server "python server.py"
```

## Deploying to Azure

The MCP server can be deployed to Azure App Service for production use. See [AZURE_DEPLOYMENT.md](./AZURE_DEPLOYMENT.md) for detailed instructions.

### Quick Deployment

```bash
# Make the script executable
chmod +x deploy-azure.sh

# Deploy to dev environment (default)
./deploy-azure.sh

# Or with custom app name
./deploy-azure.sh -n my-custom-mcp-server
```

### Using the Deployed MCP Server

Once deployed to Azure, configure GitHub Copilot in VS Code:

```json
{
  "github.copilot.chat.mcp.servers": {
    "azure-sdk-code-review": {
      "type": "sse",
      "url": "https://azure-sdk-code-review-mcp-dev.azurewebsites.net/sse",
      "authentication": {
        "type": "bearer",
        "token": {
          "command": "az",
          "args": ["account", "get-access-token", "--resource", "api://azure-sdk-qa-bot-dev", "--query", "accessToken", "-o", "tsv"]
        }
      }
    }
  }
}
```

The server will automatically authenticate to the backend using Azure Managed Identity.
