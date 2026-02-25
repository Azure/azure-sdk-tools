# SearchIndexCreator

A console application for setting up and managing Azure AI Search infrastructure for the GitHub Issue Labeler triage bot. This tool creates search indexes, retrieves GitHub issues/documents, and configures the knowledge agent used for intelligent issue triaging.

## Overview

SearchIndexCreator is responsible for:
- Fetching issues and documentation from GitHub repositories
- Uploading content to Azure Blob Storage
- Creating/updating Azure AI Search indexes with vector and semantic search capabilities
- Configuring skillsets for text chunking and embedding generation
- Managing Azure AI Search knowledge agents for intelligent retrieval

## Prerequisites

- .NET 8.0 SDK
- Azure subscription with the following resources:
  - Azure AI Search service
  - Azure OpenAI service (with embedding and chat models deployed)
  - Azure Blob Storage account
  - Azure App Configuration (for production deployment)
- GitHub Personal Access Token with `repo` permissions
- Appropriate Azure RBAC roles:
  - `Search Index Data Contributor` on Azure AI Search
  - `Storage Blob Data Contributor` on Azure Storage
  - `Cognitive Services OpenAI User` on Azure OpenAI

## Configuration

Create a `local.settings.json` file in the SearchIndexCreator project directory:

```json
{
  "IsEncrypted": false,
  "Values": {
    "SearchEndpoint": "https://<your-search-service>.search.windows.net",
    "OpenAIEndpoint": "https://<your-openai-service>.openai.azure.com",
    "IndexName": "<index-name>",
    "StorageName": "<storage-account-name>",
    "ContainerName": "<container-name-prefix>",
    "KnowledgeAgentModelName": "gpt-4.1-mini",
    "KnowledgeAgentName": "<knowledge-agent-name>",
    "BlobConnectionString": "ResourceId=/subscriptions/<sub-id>/resourceGroups/<rg-name>/providers/Microsoft.Storage/storageAccounts/<storage-name>;",
    "GithubKey": "<github-pat>",
    "OpenAIModelName": "<openai-model>",
    "EmbeddingModelName": "<text-embedding-model>",
    "IssueStorageName": "<storage-account-name>",
    "repo": "<repository-name>",
    "RepositoryNamesForLabels": "azure-sdk-for-net;azure-sdk-for-js;azure-sdk-for-python",
    "ConfigurationEndpoint": "https://<app-config>.azconfig.io",
    "McpRepositoryForLabels": "mcp",
    "RerankerThreshold": "1.0"
  }
}
```

### Configuration Fields

| Field | Description | Example |
|-------|-------------|---------|
| `SearchEndpoint` | Azure AI Search service endpoint | `https://my-search.search.windows.net` |
| `OpenAIEndpoint` | Azure OpenAI service endpoint | `https://my-openai.openai.azure.com` |
| `IndexName` | Name for the search index | `issue-triage-index` |
| `StorageName` | Azure Storage account name for blob storage | `mystorageaccount` |
| `ContainerName` | Prefix for blob container (suffix `-blob` added automatically) | `issue-triage` |
| `KnowledgeAgentModelName` | Azure OpenAI chat model deployment name | `gpt-4.1-mini` |
| `KnowledgeAgentName` | Name for the knowledge agent | `issue-triage-agent` |
| `BlobConnectionString` | Resource ID connection string for managed identity | See Azure Portal → Storage Account → Access Keys |
| `GithubKey` | GitHub Personal Access Token | `github_pat_...` |
| `OpenAIModelName` | Azure OpenAI model for chat completions | `gpt-4.1-mini` |
| `EmbeddingModelName` | Azure OpenAI embedding model deployment | `text-embedding-ada-002` or `text-embedding-3-large` |
| `IssueStorageName` | Storage account for label retrieval | Same as `StorageName` |
| `repo` | Target repository name | `azure-sdk-for-net` or `mcp` |
| `RepositoryNamesForLabels` | Semicolon-separated list of Azure SDK repos | `azure-sdk-for-net;azure-sdk-for-js` |
| `ConfigurationEndpoint` | Azure App Configuration endpoint (optional) | `https://my-config.azconfig.io` |
| `McpRepositoryForLabels` | MCP repository name | `mcp` |
| `RerankerThreshold` | Semantic reranker score threshold (0.0-1.0) | `1.0` (default), `0.7` (for MCP) |

### Embedding Model Notes

- **`text-embedding-3-small`**: 1536 dimensions (default for Azure SDK repos)
- **`text-embedding-3-large`**: 3072 dimensions (used for MCP repository)

The vector dimensions are automatically calculated based on the embedding model name.

### Repository-Specific Settings

**For Azure SDK repositories:**
- `repo`: Set to specific SDK repo (e.g., `azure-sdk-for-net`)
- Uses Service/Category labels (colors: `e99695` for service, `ffeb77` for category)
- Chunk size: 1000 characters, overlap: 100 characters
- `RerankerThreshold`: `1.0` (default)

**For MCP repository:**
- `repo`: Set to `mcp`
- Uses Server/Tool labels (`server-*`, `tools-*`, `remote-mcp` prefixes)
- Chunk size: 2200 characters, overlap: 250 characters
- `RerankerThreshold`: `0.7` (more permissive for broader matches)

## Usage

Run the application:

```powershell
dotnet run
```

You'll be presented with a menu:

```
Select an option:
1. Process Search Content
2. Process Issue Examples
3. Process Demo
4. Create or Refresh Labels
5. Create or Update Knowledge Agent
6. Delete Knowledge Agent
```

### Option 1: Process Search Content

**Purpose:** Complete end-to-end setup/refresh of the search infrastructure.

**What it does:**
1. Fetches all closed, labeled issues from the configured GitHub repository
2. Retrieves documentation files from the repository (for Azure SDK repos)
3. Uploads content to Azure Blob Storage as JSON documents
4. Creates or updates the Azure AI Search index with:
   - Vector search (HNSW algorithm with binary quantization)
   - Semantic search configuration
   - Appropriate fields for the repository type
5. Creates or updates the data source connection to Blob Storage
6. Creates or updates the skillset:
   - Text splitting skill (chunking)
   - Azure OpenAI embedding skill
7. Creates or updates the indexer with daily schedule

**When to use:**
- Initial setup of the search infrastructure
- Refreshing the knowledge base with latest issues
- After modifying index schema or skillset configuration

**Expected time:** 5-30 minutes depending on repository size and number of issues

### Option 2: Process Issue Examples

**Purpose:** Extract sample issues for testing.

**What it does:**
- Retrieves the last 7 days of closed issues with `customer-reported` and `issue-addressed` labels
- Saves to `issues.json` in the current directory

**When to use:**
- Creating test datasets
- Manual testing of labeler predictions

### Option 3: Process Demo

**Purpose:** Upload recent issues to a private demo repository.

**What it does:**
- Retrieves issues from the last 14 days
- Creates duplicate issues in a private repository (requires `GithubKeyPrivate` configuration)

**When to use:**
- Setting up demo environments
- Testing without affecting production repositories

### Option 4: Create or Refresh Labels

**Purpose:** Extract and store label definitions from GitHub repositories.

**What it does:**
- Fetches labels from configured repositories
- Filters labels by type:
  - Azure SDK: Service labels (`e99695` color) and Category labels (`ffeb77` color)
  - MCP: Server labels (`server-*`), Tool labels (`tools-*`, `remote-mcp`)
- Uploads label definitions to Azure Blob Storage (`labels` container)

**When to use:**
- Initial setup
- After adding/modifying labels in GitHub
- Periodically to sync label changes

### Option 5: Create or Update Knowledge Agent

**Purpose:** Configure the Azure AI Search knowledge agent.

**What it does:**
- Creates or updates a knowledge agent that references the search index
- Configures the chat model (`KnowledgeAgentModelName`)
- Sets the semantic reranker threshold (`RerankerThreshold`)

**When to use:**
- After creating/updating the search index
- When changing reranker threshold settings
- After modifying knowledge agent configuration
- **To verify changes after code refactoring** (quickest validation method)

**Expected time:** < 1 minute

### Option 6: Delete Knowledge Agent

**Purpose:** Remove the knowledge agent.

**What it does:**
- Deletes the knowledge agent from Azure AI Search
- Does not affect the search index or data

**When to use:**
- Cleanup operations
- Before recreating with different configuration

## Architecture

### Data Flow

```
GitHub Issues/Docs
    ↓
IssueTriageContentRetrieval
    ↓
Azure Blob Storage (JSON documents)
    ↓
Azure Search Indexer (scheduled)
    ↓
Skillset (chunking + embeddings)
    ↓
Azure Search Index (vector + semantic)
    ↓
Knowledge Agent (retrieval interface)
```

### Search Index Features

- **Vector Search**: HNSW algorithm with binary quantization for efficient similarity search
- **Semantic Search**: L2 semantic ranker for improved relevance
- **Hybrid Search**: Combines vector, semantic, and keyword search
- **Built-in Vectorizer**: Automatic embedding generation using Azure OpenAI

### Field Schema

**Common Fields:**
- `ChunkId` (key): Unique identifier for each chunk
- `ParentId`: Original issue/document ID
- `Chunk`: Text content (chunked)
- `TextVector`: Embedding vector (1536 or 3072 dimensions)
- `Title`: Issue/document title
- `Author`: GitHub username
- `Repository`: Repository name
- `CreatedAt`: Creation timestamp
- `Url`: GitHub issue/document URL
- `CodeOwner`: Code owner identifier (1 or 0)
- `DocumentType`: `Issue`, `Document`, or `Comment`

**Azure SDK Specific:**
- `Service`: Service label (filterable)
- `Category`: Category label (filterable)

**MCP Specific:**
- `Server`: Server label(s) (filterable)
- `Tool`: Tool label(s) (filterable)

## Troubleshooting

### Authentication Errors

**Issue:** `Unauthorized` or `Forbidden` errors

**Solution:**
- Ensure you're authenticated with Azure CLI: `az login`
- Verify RBAC role assignments on Azure resources
- Check that managed identity or service principal has correct permissions

### Indexer Failures

**Issue:** Indexer shows failed status in Azure Portal

**Solution:**
- Check indexer execution history for detailed error messages
- Verify blob storage contains valid JSON documents
- Ensure skillset configuration matches your Azure OpenAI deployment names
- Confirm embedding model deployment exists and is accessible

### Missing Issues in Search Results

**Issue:** Search doesn't return expected issues

**Solution:**
- Verify issues have required labels (Service/Category or Server/Tool)
- Check if issues are closed (only closed issues are indexed)
- Wait for indexer to complete processing (check Azure Portal)
- Verify `RerankerThreshold` isn't too strict (lower for more results)

### Embedding Dimension Mismatch

**Issue:** `Vector dimensions do not match` error

**Solution:**
- Verify `EmbeddingModelName` in configuration matches your deployment
- If changing models, delete and recreate the index (or change `IndexName`)
- Ensure consistency: `text-embedding-ada-002` = 1536 dims, `text-embedding-3-large` = 3072 dims

### Testing Changes

Full refresh workflow:
1. Run **Option 1** (Process Search Content) - 5-30 minutes
2. Monitor indexer status in Azure Portal
3. Run **Option 4** to refresh labels
4. Run **Option 5** (Update Knowledge Agent)
