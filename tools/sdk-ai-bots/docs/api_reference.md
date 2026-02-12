# Azure SDK Knowledge Base API Integration Guide

This guide helps developers integrate the Azure SDK Knowledge Base API into their products and tools to leverage Azure SDK domain knowledge for Q&A capabilities.

## Overview

The Azure SDK Knowledge Base API provides AI-powered Q&A capabilities for Azure SDK-related topics, including:

- **TypeSpec authoring guidance** - Best practices and patterns for TypeSpec API definitions
- **Azure SDK development** - SDK generation, usage patterns, and troubleshooting
- **API design guidelines** - Azure REST API design principles and conventions
- **Language-specific guidance** - Python, .NET, Java, JavaScript, and Go SDK specifics

### Use Cases

- MCP (Model Context Protocol) tools for IDE integration
- Custom chatbots and assistants
- CLI tools for developer productivity
- Automated documentation assistants

## Getting Started

### Prerequisites

- Microsoft corporate account with appropriate permissions
- Azure CLI installed for authentication

### API Endpoint

| Environment | Endpoint | Auth Scope |
|-------------|----------|------------|
| Production | `https://azuresdkbot-dqh7g6btekbfa3hh.eastasia-01.azurewebsites.net` | `api://azure-sdk-qa-bot/.default` |

### Authentication

The API uses Azure AD Bearer token authentication.

#### For Programmatic Integration (Recommended)

Use the **Azure Identity SDK** with `DefaultAzureCredential`, which automatically handles various authentication scenarios including managed identity, Visual Studio credentials, Azure CLI, and interactive browser login.

> **Note**: The authentication scope varies by environment:
>
> - Production: `api://azure-sdk-qa-bot/.default`
> - Preview: `api://azure-sdk-qa-bot-test/.default`
> - Dev: `api://azure-sdk-qa-bot-dev/.default`
>
> The `/.default` suffix requests all statically configured permissions for the application.

**C# Example:**

```csharp
using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;

const string scope = "api://azure-sdk-qa-bot/.default"; // Use api://azure-sdk-qa-bot-dev/.default for dev

var credential = new DefaultAzureCredential();
var token = await credential.GetTokenAsync(
    new TokenRequestContext(new[] { scope }), 
    CancellationToken.None
);

// Assumes httpClient is an existing HttpClient instance
httpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token.Token);
```

**Python Example:**

```python
from azure.identity import DefaultAzureCredential

scope = "api://azure-sdk-qa-bot/.default"  # Use api://azure-sdk-qa-bot-dev/.default for dev

credential = DefaultAzureCredential()
token = credential.get_token(scope)

headers = {"Authorization": f"Bearer {token.token}"}
```

**JavaScript/TypeScript Example:**

```typescript
import { DefaultAzureCredential } from "@azure/identity";

const scope = "api://azure-sdk-qa-bot/.default"; // Use api://azure-sdk-qa-bot-dev/.default for dev

const credential = new DefaultAzureCredential();
const token = await credential.getToken(scope);

const headers = { Authorization: `Bearer ${token.token}` };
```

## API Reference: Completion Endpoint

### Endpoint

```http
POST /completion
```

### Request Schema

```json
{
  "tenant_id": "azure_sdk_qa_bot",
  "message": {
    "role": "user",
    "content": "How do I define a REST API using TypeSpec?"
  },
  "history": [],
  "sources": [],
  "top_k": 10,
  "with_full_context": false,
  "with_preprocess": false,
  "additional_infos": [],
  "intention": null
}
```

#### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `tenant_id` | string | Yes | The knowledge domain to query. See [Available Tenants](#available-tenants). |
| `message` | object | Yes | The user's question with `role` ("user") and `content` (the question text). |
| `history` | array | No | Previous messages for conversation context. Each message has `role` and `content`. |
| `sources` | array | No | Limit search to specific knowledge sources. Default: all sources. |
| `top_k` | integer | No | Number of documents to retrieve. Default: 10. |
| `with_full_context` | boolean | No | Return the full RAG context in response. Default: false. |
| `with_preprocess` | boolean | No | Preprocess the message before sending to the agent. Default: false. |
| `additional_infos` | array | No | Additional information (links, images) to provide context. Each item has `type`, `content`, and `link`. |
| `intention` | object | No | Override automatic intent detection with explicit intention fields. |

### Response Schema

```json
{
  "id": "completion-uuid",
  "answer": "To define a REST API using TypeSpec, you can...",
  "has_result": true,
  "references": [
    {
      "title": "TypeSpec REST API Tutorial",
      "source": "typespec_docs",
      "link": "https://typespec.io/docs/...",
      "content": "Relevant excerpt..."
    }
  ],
  "intention": {
    "question": "How do I define a REST API using TypeSpec?",
    "category": "typespec_authoring",
    "needs_rag_processing": true
  },
  "route_tenant": "azure_sdk_qa_bot"
}
```

#### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier for this completion. |
| `answer` | string | The AI-generated answer. |
| `has_result` | boolean | Whether the agent found relevant information. |
| `references` | array | Documents used to generate the answer (title, source, link, content). |
| `intention` | object | Analyzed intent of the question. |
| `route_tenant` | string | The tenant that processed the request (may differ if routing occurred). |
| `full_context` | string | Full RAG context (only if `with_full_context: true`). |

## Generating Client SDKs with TypeSpec

The API is defined using TypeSpec, enabling automatic client SDK generation for multiple languages.

### TypeSpec Definition Location

The TypeSpec definitions are located at:

```text
tools/sdk-ai-bots/azure-sdk-qa-bot-backend/tsp/
├── main.tsp        # Service definition
├── model.tsp       # Data models
├── route.tsp       # API routes
├── package.json    # Dependencies
└── tspconfig.yaml  # Emitter configuration
```

### Available Client Emitters

| Emitter Package | Language | Status |
|-----------------|----------|--------|
| `@azure-tools/typespec-ts` | JavaScript/TypeScript | Preview |
| `@azure-tools/typespec-python` | Python | Preview |
| `@azure-tools/typespec-java` | Java | Preview |
| `@azure-tools/typespec-csharp` | C# | Preview |

### Generating a Client SDK

1. **Clone the repository and navigate to the TypeSpec folder:**

   ```bash
   cd tools/sdk-ai-bots/azure-sdk-qa-bot-backend/tsp
   ```

2. **Add desired emitter to `package.json`:**

   ```json
   {
     "dependencies": {
       "@azure-tools/typespec-python": "latest"
     }
   }
   ```

3. **Configure the emitter in `tspconfig.yaml`:**

   ```yaml
   emit:
     - "@azure-tools/typespec-python"
   options:
     "@azure-tools/typespec-python":
       emitter-output-dir: "{project-root}/clients/python"
   ```

4. **Install dependencies and generate the client:**

   ```bash
   tsp install
   tsp compile .
   ```

For complete documentation on TypeSpec client emitters, see: [TypeSpec Client Emitters](https://typespec.io/docs/emitters/clients/introduction/)

## Available Tenants

| Tenant ID | Description | Best For |
|-----------|-------------|----------|
| `azure_sdk_qa_bot` | General Azure SDK Q&A | General SDK questions |
| `typespec_extension` | TypeSpec language and tooling | TypeSpec syntax, emitters |
| `azure_typespec_authoring` | Azure TypeSpec authoring | Azure API design with TypeSpec |
| `python_channel_qa_bot` | Python SDK specific | Azure SDK for Python |
| `dotnet_channel_qa_bot` | .NET SDK specific | Azure SDK for .NET |
| `java_channel_qa_bot` | Java SDK specific | Azure SDK for Java |
| `javascript_channel_qa_bot` | JavaScript SDK specific | Azure SDK for JavaScript |
| `golang_channel_qa_bot` | Go SDK specific | Azure SDK for Go |
| `azure_sdk_onboarding` | SDK onboarding guidance | New service onboarding |
| `api_spec_review_bot` | API specification review | REST API design review |
| `general_qa_bot` | General questions | Broad Azure SDK topics |

## Best Practices

### 1. Use Conversation History

For follow-up questions, include the conversation history to maintain context:

```json
{
  "tenant_id": "azure_sdk_qa_bot",
  "message": { "role": "user", "content": "Can you show me an example?" },
  "history": [
    { "role": "user", "content": "How do I define a REST API using TypeSpec?" },
    { "role": "assistant", "content": "To define a REST API..." }
  ]
}
```

### 2. Choose the Right Tenant

Select the most specific tenant for your domain to get better answers:

- Use `python_channel_qa_bot` for Python SDK questions
- Use `azure_typespec_authoring` for TypeSpec API design
- Use `azure_sdk_qa_bot` for general questions

## Real-World Integration Example

For a complete example of integrating this API, see the **TypeSpec Authoring MCP Tool** implementation:

- **Pull Request**: [#13122 - azsdk_typespec_consult MCP tool](https://github.com/Azure/azure-sdk-tools/pull/13122)
- **Location**: `tools/azsdk-cli/Azure.Sdk.Tools.Cli/`

This implementation demonstrates:

- C# client models for the Completion API
- Azure Identity authentication flow
- Response handling and error management
- Integration with MCP (Model Context Protocol) for IDE tooling

## Support

For questions or issues:

- **GitHub Issues**: [azure-sdk-tools](https://github.com/Azure/azure-sdk-tools/issues)
