# Spec: TypeSpec Authoring - AI-Powered TypeSpec Authoring Assistance Tool

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals](#goals)
- [Design Proposal](#design-proposal)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)

---

## Definitions

- **TypeSpec**: A language for describing cloud service APIs and generating other API description languages, client and service code, documentation, and other assets. TypeSpec provides highly extensible core language primitives that can describe API shapes common among REST, OpenAPI, GraphQL, gRPC, and other protocols. See [TypeSpec official documentation](https://typespec.io)

- **Azure SDK Design Principles**: The foundational design principles that guide the development of Azure SDKs across all languages, ensuring consistency, usability, and adherence to Azure standards. See [Azure SDK design principles](https://azure.github.io/azure-sdk/general_introduction.html)

- **Azure REST API Guidelines**: Standards and best practices for designing REST APIs in Azure, covering naming conventions, error handling, versioning, and resource modeling. See [Azure REST API guidelines](https://github.com/microsoft/api-guidelines/tree/vNext/azure)

- **ARM (Azure Resource Manager) Guidelines**: Specifications that define how Azure Resource Manager resource providers should be designed and implemented, including resource lifecycle, operations, and compliance requirements. See [ARM guidelines](https://github.com/cloud-and-ai-microsoft/resource-provider-contract/tree/master/v1.0)

- **ARM API Best Practices**: Design patterns and recommendations for creating consistent, high-quality ARM APIs, including guidance on resource modeling, operation patterns, and API versioning. See [ARM API Best Practices](https://armwiki.azurewebsites.net/api_contracts/best_practices.html)

- **Azure TypeSpec Style Guide**: Style conventions and coding standards specific to writing TypeSpec for Azure services, ensuring consistency across Azure service definitions. See [Azure TypeSpec Style Guide](https://azure.github.io/typespec-azure/docs/style-guide)

- **ARM TypeSpec Best Practices**: Recommended patterns for using TypeSpec operation templates and interface templates when defining ARM resource types and operations. See [ARM Resource Operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type)

- **RAG (Retrieval-Augmented Generation)**: An AI pattern that enhances language model responses by retrieving relevant context from a knowledge base before generating output, improving accuracy and grounding responses in authoritative sources

- **Azure SDK Knowledge Base**: A backend service that provides RAG-powered solutions for Azure SDK and TypeSpec authoring tasks. It indexes and retrieves relevant information from Azure SDK documentation, guidelines, and best practices to generate context-aware recommendations

- **AI Hallucination**: When an AI model generates plausible-sounding but incorrect or fabricated information, such as inventing non-existent decorators or APIs


---

## Background / Problem Statement

TypeSpec is the foundation of the Azure SDK ecosystem, and well-crafted TypeSpec contributes to producing high-quality SDKs. However, Azure API developers face significant challenges when authoring TypeSpec.

### Current State

Users are facing various problems during TypeSpec authoring, where agent like GitHub Copilot with frontier models cannot provide effective help. We categorize these problems into three main types. For more cases, please refer to this [project](https://github.com/haolingdong-msft/innerloop-typespec-authoring-benchmark) to understand more cases that agent cannot provide effective help.

> **Note**: The AI-generated outputs shown in the examples below represent the current state of generic AI assistance (as of the time of writing). AI models continuously evolve, and the specific outputs demonstrated here are for illustrative purposes to highlight the gap between generic AI and domain-specific, Azure-aware AI assistance.

**Problem 1: Writing TypeSpec that follows Azure guidelines and fixing non-compliant code**
- Azure API developers want to add new resources, operations, or other components to Azure services following ARM/DP/SDK/TypeSpec guidelines
- Generic AI (like standard GitHub Copilot) cannot provide effective help because it lacks domain-specific knowledge about Azure TypeSpec patterns and standards

**Example**: When a user asks to create an ARM resource named 'Asset' with CRUD operations, generic AI generates incorrect code that doesn't follow Azure Resource Manager patterns.

**Prompt:** Add an ARM resource named 'Asset' with CRUD operations.

Currently, GitHub Copilot generates code like the following, which invents non-existent decorators and produces incorrect code due to AI hallucination: 
```typespec
/**
 * The Asset ARM resource.
 */
@armResource("assets", "asset")
model AssetResource extends ResourceBase<AssetProperties> {}

/**
 * Create or update an Asset resource.
 */
@armResourceOperation("createOrUpdate", AssetResource)
op createOrUpdateAsset(
  resource: AssetResource
): AssetResource;

// other operations
```

According to the ARM resource [guideline](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/#child-resource), the expected code should use pre-defined templates like `TrackedResource` ,`ArmResourceRead` and decorators like `@armResourceOperations`:
```typespec
model Asset is TrackedResource<AssetProperties> {
  ...ResourceNameParameter<Asset>;
}

@armResourceOperations
interface Assets {
  get is ArmResourceRead<Asset>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Asset>;
  update is ArmResourcePatchAsync<Asset, AssetProperties>;
  delete is ArmResourceDeleteWithoutOkAsync<Asset>;
  listByResourceGroup is ArmResourceListByParent<Asset>;
  listBySubscription is ArmListBySubscription<Asset>;
}
```
 
**Problem 2: Updating TypeSpec for expected compilation outputs**
- Azure API developers need to update TypeSpec to achieve expected outputs after compilation (e.g., correct API paths in generated OpenAPI)
- Generic AI cannot provide effective help for these domain-specific challenges

**Example**: After compiling TypeSpec, developers notice that the generated paths in `openapi.json` are incorrect. For instance, the TypeSpec below outputs the path `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Widget/assets/{assetName}`. However, since "assets" belong to an "employee," the expected path should include `employees/{employeeName}` before `assets/{assetName}`. Generic AI cannot guide developers on how to properly fix this.

```typespec assets.tsp
model Asset is TrackedResource<AssetProperties> {
  ...ResourceNameParameter<Asset>;
}

@armResourceOperations
interface Assets {
  get is ArmResourceRead<Asset>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Asset>;
  update is ArmResourcePatchAsync<Asset, AssetProperties>;
  delete is ArmResourceDeleteWithoutOkAsync<Asset>;
  listByResourceGroup is ArmResourceListByParent<Asset>;
  listBySubscription is ArmListBySubscription<Asset>;
}
```


**Prompt:** Change the route for interface Assets from `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Widget/assets/{assetName}` to `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Widget/employees/{employeeName}/assets/{assetName}`

Currently, GitHub Copilot simply adds `@route('/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Widget/employees/{employeeName}/assets/{assetName}')`, which does not follow our guidelines:
```typespec assets.tsp
model Asset is TrackedResource<AssetProperties> {
  ...ResourceNameParameter<Asset>;
}

@route('/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Widget/employees/{employeeName}/assets/{assetName}')
@armResourceOperations
interface Assets {
  get is ArmResourceRead<Asset>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Asset>;
  update is ArmResourcePatchAsync<Asset, AssetProperties>;
  delete is ArmResourceDeleteWithoutOkAsync<Asset>;
  listByParent is ArmResourceListByParent<Asset>;
}
```

According to the ARM resource [guideline](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/#child-resource), the expected code is:
```typespec assets.tsp
// Use @parentResource to modify the path
@parentResource(Employee)
@route('/employees/{employeeName}/assets/{assetName}')
model Asset is TrackedResource<AssetProperties> {
  @path
  employeeName: string;
  
  @path
  assetName: string;
}

// Output Swagger path:
// /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Widget/employees/{employeeName}/assets/{assetName}
```
 
**Problem 3: Adding a New version following Azure versioning guidelines**
- TypeSpec versioning is intricate, involving decorators such as `@added`, `@removed`, `@useDependency` to manage preview vs stable versions. These rules are nuanced and tied to Azure’s breaking-change policies, making them hard for generic AI to infer without domain-specific context.
- Generic AI currently cannot reliably provide effective guidance for scenarios requiring integrated knowledge of TypeSpec versioning decorators and Azure-specific conversion and breaking-change policies.

**Example**: When a user asks to "add a new preview version", generic AI may add a new version without replacing the older one.

**Prompt:** add a new preview version 2025-10-01-preview for service widget

Current AI only simply adds a new api version enum option in versions enum

```typespec main.tsp
/** The available API versions. */
enum Versions {
  /** 2021-11-01 version */
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  v2021_11_01: "2021-11-01",

  /** 2022-10-01-preview version */
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  v2022_10_01_preview: "2022-10-01-preview",

  /** 2025-10-01-preview version */
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  v2025_10_01_preview: "2025-10-01-preview",
}
```

According to the ARM versioning guideline and best practices, the expected behavior should:
1. Rename the latest preview version to match the new preview version, in all instances in the spec. e.g. change the `Versions` enum.
    ```typespec main.tsp
    /** The available API versions. */
    enum Versions {
      /** 2021-11-01 version */
      @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
      v2021_11_01: "2021-11-01",

      /** 2025-10-01-preview version */
      @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
      @previewVersion
      v2025_10_01_preview: "2025-10-01-preview",
    }
    ```
1. Change the name of the `examples` version folder for the latest preview to match the new preview version
1. Make changes to the API description based on how the API has changed
  - If any type that was introduced in the latest preview is _not_ in the new preview, simply remove the type
  - If any other types are removed in this preview (unlikely) mark these with an `@removed` decorator referencing the new version
  - If any types are added, renamed, or otherwise modified in the new version, mark them with the appropriate versioning decorator
1. Add and modify examples to match the API changes
  

### Why This Matters

**Impact on Service Development Experience:**
- TypeSpec authoring is critical to the inner loop experience for service teams
- Poor TypeSpec quality leads to incorrect SDK generation, requiring multiple iterations and delays
- The current workflow requires deep expertise in TypeSpec syntax and Azure-specific patterns, creating a steep learning curve

**Cost of Not Solving This:**
- **Increased Review Efforts**: Reviewers spend significant time identifying and correcting TypeSpec issues that don't follow Azure standards
- **Development Delays**: Service teams struggle with trial-and-error approaches to get TypeSpec right, slowing down the entire SDK generation pipeline (Author TypeSpec → Generate SDK → Validate SDK → Create PR → Release SDK)
- **Quality Issues**: Incorrect TypeSpec leads to malformed SDKs that need to be regenerated, wasting engineering resources
- **Knowledge Barrier**: Teams must constantly reference documentation and guidelines without intelligent assistance, reducing productivity

**User Experience Friction:**
- Developers currently have to switch between generic AI assistance and manual documentation lookup
- The lack of context-aware guidance means even experienced developers make mistakes
- New team members face an especially steep learning curve without AI assistance that understands Azure patterns

---

## Goals

- **AI Pair Programming for TypeSpec**: Enable GitHub Copilot to provide intelligent, context-aware assistance for TypeSpec authoring by integrating Azure SDK RAG (Retrieval-Augmented Generation) knowledge base
- **Guide Users Through Intent-Driven Development**: Allow users to describe their intent in natural language (e.g., "I need to add a new API version to my Widget service" or "I want to add an ARM resource named 'Asset' with CRUD operations"), and have the AI guide them through the correct TypeSpec implementation
- **Generate TypeSpec Following ARM/DP/SDK/TypeSpec Guidelines**: Ensure that generated TypeSpec code adheres to Azure Resource Manager (ARM) patterns, Data Plane (DP) standards, SDK guidelines, and TypeSpec best practices
- **Provide Contextual References**: When generating or suggesting TypeSpec, include references to relevant documentation (e.g., links to TypeSpec Azure guidelines for versioning, ARM resource types, routing patterns)
- **Save Review Efforts**: Reduce the time reviewers spend identifying standards violations by ensuring TypeSpec code follows standards from the start
- **Improve Developer Learning**: Help service teams learn TypeSpec syntax and Azure patterns through interactive guidance, increasing their confidence in making code changes
- **Accelerate Inner Loop Development**: Speed up the iterative process of authoring TypeSpec, compiling, validating, and adjusting to achieve expected SDK outputs

---

## Design Proposal

This spec proposes two approaches for providing AI-powered TypeSpec authoring assistance. Each approach has different tradeoffs in terms of implementation complexity, maintenance overhead, and specialization capability.

### Approach 1: Custom Agent-Based Design

Build a custom agent `azure-typespec-author-agent` that assists users in defining or updating TypeSpec API specifications and handling other TypeSpec‑related tasks.
The agent adopts Azure KB to provide solution for user request:

Based on the request, the agent will invoke an Azure KB tool to retrieve the solution for user request and then apply the solution to edit TypeSpec files.

- Leverage the existing APIs for Azure SDK knowledge base to deliver solutions aligned with Azure guidelines and best practices. 
- Implement a TypeSpec solution MCP tool that consults the Azure SDK RAG service to generate these solutions.

#### Overview

```code
┌──────┐          ┌─────────────┐          ┌───────────┐          ┌───────────┐
│ User │          │ custom agent│          │ azsdk     │          │ Knowledge │
│      │          │ Copilot     │          │ MCP       │          │ base      │
└──┬───┘          └──────┬──────┘          └─────┬─────┘          └─────┬─────┘
   │                     │                       │                      │
   │ "Add new preview    │                       │                      │
   │  version 2025-12-09 │                       │                      │
   │  to project widget" │                       │                      │
   │────────────────────>│                       │                      │
   │                     │────┐                  │                      │
   │                     │    │ retrieve required│                      │
   │                     │<───┘ information      │                      │
   │                     │                       │                      │
   │                     │ Request versioning    │                      │
   │                     │ info                  │                      │
   │                     │──────────────────────>│                      │
   │                     │                       │                      │
   │                     │                       │ Search request       │
   │                     │                       │─────────────────────>│
   │                     │                       │                      │
   │                     │                       │ Versioning solution  │
   │                     │                       │<─────────────────────│
   │                     │                       │                      │
   │                     │ Versioning solution   │                      │
   │                     │<──────────────────────│                      │
   │                     │────┐                  │                      │
   │                     │    │ Make edits       │                      │
   │                     │<───┘                  │                      │
   │                     │                       │                      │
   │ "Changes made"      │                       │                      │
   │<────────────────────│                       │                      │
   │                     │                       │                      │
```

#### Detailed Design

The TypeSpec authoring workflow follows a streamlined process where the user interacts with Custom agent `azure-typespec-author-agent` using natural language, and agent leverages the TypeSpec Solution Tool (MCP) to generate standards-compliant solutions. The architecture diagram above illustrates this end-to-end flow:

1. **User prompts GitHub Copilot** with a TypeSpec task (e.g., "Add new preview version 2025-12-09 to project widget")
1. **Agent collect required information** for this task (e.g. the namespace, version, current project structure)
1. **Agent invokes the TypeSpec Solution Tool** (MCP: `azsdk_typespec_generate_authoring_plan`) with the user's request and any additional context
1. The `azsdk_typespec_generate_authoring_plan` Tool queries the Azure SDK Knowledge Base with a structured request containing the user's intent and project context
1. The Knowledge Base returns a RAG-powered solution with step-by-step guidance and documentation references
1. **Agent applies the solution** to update TypeSpec files and presents the changes to the user with explanations and reference links

This design ensures that generated TypeSpec code adheres to Azure Resource Manager (ARM) patterns, Data Plane (DP) standards, SDK guidelines, and TypeSpec best practices by grounding every solution in authoritative Azure documentation.

##### Component 1: TypeSpec Solution Tool

**Name (CLI)**: `azsdk typespec generate-authoring-plan`

**Name (MCP)**: `azsdk_typespec_generate_authoring_plan`

**Purpose**: Provide a solution to define or edit TypeSpec API specifications for TypeSpec-related tasks.

**Input Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `--request` | string | Yes | N/A | The TypeSpec-related task or user request sent to an AI agent to produce a proposed solution or execution plan with references |
| `--additional-information` | string | No | "" | Additional information to consider for the TypeSpec project |
| `--typespec-project` | string | No | Current directory | The root path of the TypeSpec project |

**Output Format**:

```json
{
  "operation_status": "succeeded",
  "typespec_project": "./tsp",
  "solution": "<solution-for-the-typespec-task>",
  "references": [
    {
      "title": "How to define a preview version",
      "source": "typespec_azure_docs",
      "link": "https://azure.github.io/typespec-azure/docs/howtos/versioning/preview-version",
      "snippet": "To define a preview version..."
    }
  ],
  "full_context": "<full-context-used-to-generate-solution>",
  "reasoning": "<llm-reasoning-process>",
  "query_intention": {
    "category": "versioning",
    "question_scope": "branded",
    "service_type": "management-plane"
  }
}
```

**Workflow**:

1. **Validate Input**: Check that request parameter is not empty
1. **Build Completion Request**: Create structured request with:
   - Tenant ID set to `azure_typespec_authoring`
   - User message containing the request
   - Optional additional information as text attachment
1. **Authenticate**: Retrieve access token using MSAL public client authentication with device code flow
1. **Query Knowledge Base**: Send POST request to Azure SDK Knowledge Base `/completion` endpoint
1. **Process Response**: Extract solution, references, reasoning, and query intention from response
1. **Format Output**: Present solution with references and metadata to the user

##### Component 2: Azure SDK Knowledge Base

**Purpose**: Backend service that provides RAG-powered solutions for Azure SDK and TypeSpec authoring tasks.

**API Endpoint**: `https://<knowledge-base-service-endpoint>/completion`

**Request Structure**:

```json
{
  "tenant_id": "azure_typespec_authoring",
  "message": {
    "role": "user",
    "content": "<user-request>"
  },
  "additional_infos": [
    {
      "type": "text",
      "content": "<additional-context>"
    }
  ]
}
```

**Response Structure**:

```json
{
  "id": "<completion-id>",
  "answer": "<generated-solution>",
  "has_result": true,
  "references": [
    {
      "title": "<document-title>",
      "source": "<document-source>",
      "link": "<document-url>",
      "content": "<relevant-content>"
    }
  ],
  "full_context": "<context-used>",
  "reasoning": "<llm-reasoning>",
  "intention": {
    "category": "<detected-category>",
    "question_scope": "<branded|unbranded|unknown>",
    "service_type": "<management-plane|data-plane|unknown>"
  }
}
```

**Capabilities**:
- Indexes Azure SDK documentation, guidelines, and best practices
- Retrieves relevant context based on user request
- Generates solutions aligned with Azure standards
- Provides authoritative documentation references

**Authentication**:
- The TypeSpec authoring tool authenticates with the Knowledge Base using Microsoft Authentication Library (MSAL) public client authentication
- Uses device code flow for interactive authentication
- Access tokens are cached persistently to minimize re-authentication
- The tool uses the Azure Knowledge Base service and is configured with a default service by default. If you want to use a different Azure Knowledge Base service instead of the default one, set following environment variables to override:
  - `AZURE_SDK_KB_ENDPOINT`: Service endpoint
  - `AZURE_SDK_KB_CLIENT_ID`: Application (client) ID of the service
  - `AZURE_SDK_KB_SCOPE`: Authentication scope

**Integration**:
- TypeSpec authoring tool sends structured queries to the knowledge base
- Knowledge base returns contextual solutions with references
- Tool formats and presents results to the user

---

#### Future Enhancement for Approach 1

##### Multiple Skills

**Description:**

Enhance Custom agent `azure-typespec-author-agent` capability with multiple Skills. Agent will choose skill according to the request scenario.

```code
┌──────┐          ┌─────────────┐          ┌───────────┐          ┌───────────┐          ┌────────────────┐
│ User │          │ custom agent│          │ azsdk     │          │ Knowledge │          │ llms.txt       │ 
│      │          │ Copilot     │          │ MCP       │          │ base (bot)│          │ web fetch tool │
└──┬───┘          └──────┬──────┘          └─────┬─────┘          └─────┬─────┘          └──────┬─────────┘
   │                     │                       │                      │                        │
   │ "Add new preview    │                       │                      │                        │
   │  version 2025-12-09 │                       │                      │                        │
   │  to my project"     │                       │                      │                        │
   │────────────────────>│                       │                      │                        │
   │                     │────┐                  │                      │
   │                     │    │ retrieve required│                      │
   │                     │<───┘ information      │                      │
   │                     │                       │                      │                        │
   │                     │────┐                  │                      │                        │
   │                     │    │ detect scenario  │                      │                        │
   │                     │<───┘                  │                      │                        │
   │                     │                       │                      │                        │
   │                     │ (if: normal authoring)│                      │                        │
   │                     │ Request versioning    │                      │                        │
   │                     │ info                  │                      │                        │
   │                     │──────────────────────>│                      │                        │
   │                     │                       │                      │                        │
   │                     │                       │ Search request       │                        │
   │                     │                       │─────────────────────>│                        │
   │                     │                       │                      │                        │
   │                     │                       │ Versioning solution  │                        │
   │                     │                       │<─────────────────────│                        │
   │                     │                       │                      │                        │
   │                     │ Versioning solution   │                      │                        │
   │                     │<──────────────────────│                      │                        │
   │                     │────┐                  │                      │                        │
   │                     │    │ Make edits       │                      │                        │
   │                     │<───┘                  │                      │                        │
   │                     │                       │                      │                        │
   │ "Changes made"      │                       │                      │                        │
   │<────────────────────│                       │                      │                        │
   │                     │                       │                      │                        │
   │                     │                       │                      │                        │
   │ "rename model name  │                       │                      │                        │
   │  for dotnet sdk"    │                       │                      │                        │
   │────────────────────>│                       │                      │                        │
   │                     │                       │                      │                        │
   │                     │────┐                  │                      │
   │                     │    │ retrieve required│                      │
   │                     │<───┘ information      │                      │
   │                     │────┐                  │                      │                        │
   │                     │    │ detect scenario  │                      │                        │
   │                     │<───┘                  │                      │                        │
   │                     │                       │                      │                        │
   │                     │ (if: customization)   │                      │                        │
   │                     │ Request customization │                      │                        │
   │                     │ request               │                      │                        │
   │                     │───────────────────────│──────────────────────│───────────────────────>│
   │                     │                       │                      │                        │
   │                     │ customization context │                      │                        │
   │                     │<──────────────────────│──────────────────────│────────────────────────│
   │                     │                       │                      │                        │
   │                     │────┐                  │                      │                        │
   │                     │    │generate solution │                      │                        │
   │                     │<───┘  (llm call)      │                      │                        │
   │                     │────┐                  │                      │                        │
   │                     │    │ Make edits       │                      │                        │
   │                     │<───┘                  │                      │                        │

```

##### Call AI Search instead of Azure Knowledge Base

The custom agent will invoke an AI search to retrieve the relevant TypeSpec information, then use that knowledge as context to generate the final response.

This approach cleanly decouples knowledge retrieval from final responses, enabling the custom agent to leverage the VS Code model to generate solutions. The custom agent can also craft tailored prompts to produce responses that are well‑suited for authoring tasks.

---

### Approach 2: Skills-Only Approach (Without Custom Agent)

Instead of building a custom agent, leverage GitHub Copilot's Skills framework to provide TypeSpec authoring assistance directly through MCP tools. This approach allows the existing Copilot agent to intelligently invoke specialized TypeSpec skills based on user context and requests.

#### Overview

```code
┌──────┐          ┌─────────────┐          ┌───────────┐          ┌───────────┐
│ User │          │ TypeSpec    │          │ azsdk     │          │ Knowledge │
│      │          │ Authoring   │          │ MCP       │          │ base      │
│      │          │ Skills      │          │           │          │           │
└──┬───┘          └──────┬──────┘          └─────┬─────┘          └─────┬─────┘
   │                     │                       │                      │
   │ "Add new preview    │                       │                      │
   │  version 2025-12-09 │                       │                      │
   │  to project widget" │                       │                      │
   │────────────────────>│                       │                      │
   │                     │────┐                  │                      │
   │                     │    │ detect TypeSpec  │                      │
   │                     │    │ context & choose │                      │
   │                     │<───┘ appropriate skill│                      │
   │                     │                       │                      │
   │                     │ Request versioning    │                      │
   │                     │ info                  │                      │
   │                     │──────────────────────>│                      │
   │                     │                       │                      │
   │                     │                       │ Search request       │
   │                     │                       │─────────────────────>│
   │                     │                       │                      │
   │                     │                       │ Versioning solution  │
   │                     │                       │<─────────────────────│
   │                     │                       │                      │
   │                     │ Versioning solution   │                      │
   │                     │<──────────────────────│                      │
   │                     │────┐                  │                      │
   │                     │    │ Make edits       │                      │
   │                     │<───┘                  │                      │
   │                     │                       │                      │
   │ "Changes made"      │                       │                      │
   │<────────────────────│                       │                      │
   │                     │                       │                      │
```

#### Detailed Design

1. **User prompts** with a TypeSpec task (e.g., "Add new preview version 2025-12-09 to project widget")
1. **Skill detects TypeSpec context** based on:
   - User's query and case to resolve
   - File extensions (`.tsp`)
   - Project structure (presence of `tspconfig.yaml`, `package.json` with TypeSpec dependencies)
   - Active file content and imports
   - Workspace-indexed TypeSpec files
1. **Skill automatically invokes the appropriate MCP tool** (`azsdk_typespec_generate_authoring_plan`) without requiring custom agent routing logic
1. The MCP tool queries the Azure SDK Knowledge Base with the user's request and project context
1. The Knowledge Base returns a RAG-powered solution with step-by-step guidance
1. **Agent processes the solution** and applies appropriate file edits, presenting changes to the user with explanations

---

### Approach Comparison Summary

Both approaches leverage the same Azure SDK Knowledge Base backend and provide similar core functionality. The key differences are:

- **Approach 1 (Custom Agent)**: Does NOT redirect every TypeSpec question to the Azure SDK Knowledge Base backend, making it more lightweight. However, customers need to explicitly know when to switch to use the custom agent. 

- **Approach 2 (Skills-Only)**: Redirects every TypeSpec question to the Azure SDK Knowledge Base backend, which acts like a proxy and may be heavier in processing. And calling Azure SDK Knowledge Base backend multiple times may potentially have performance issue. However, it's transparent to customers who by default use the TypeSpec Authoring tool without needing to explicitly switch context. 

---
## Cross-Language Considerations

TypeSpec authoring is language-agnostic. The generated SDKs target specific languages, but the TypeSpec authoring experience with AI assistance applies uniformly across all target SDK languages. Language-specific considerations come into play during SDK generation validation, not during TypeSpec authoring.
  
---

## Success Criteria

This feature/tool is complete when:

- **Benchmark Test Suite**: A curated set of 50+ real-world TypeSpec authoring scenarios is established, covering the most commonly encountered cases:
  - ARM resource creation with CRUD operations (15+ variants)
  - Parent-child resource hierarchy and routing (10+ variants)
  - API versioning scenarios (preview, stable, deprecated) (10+ variants)
  - Complex decorator usage (`@armResourceOperations`, `@parentResource`, `@route`, `@added`, `@removed`) (10+ variants)
  - Common anti-patterns and their corrections (5+ variants)

- **Agent Output Accuracy**: When tested against the benchmark suite:
  - Generated TypeSpec code passes compilation without errors
  - Generated code follows Azure ARM/DP/SDK guidelines (validated by automated linter/validator)
  - Generated code matches expected patterns for resource hierarchy and routing
  - Generated code includes proper syntax, e.g. decorators, templates (no hallucinated decorators like `@armResource` or `@armResourceOperation`; the correct decorator is `@armResourceOperations`)

- **Documentation Reference Quality**: For each agent response:
  - Responses include relevant documentation links (e.g., TypeSpec Azure guidelines)
  - Documentation links are accurate and point to the correct section
  - References are contextually appropriate to the user's question

- **User Intent Recognition**: Agent correctly interprets and responds to:
  - Natural language requests for adding ARM resources
  - Path correction requests based on compiled OpenAPI output
  - Versioning change requests (adding preview/stable versions)
  - Request to fix non-compliant code patterns

- **Comparative Improvement**: Compared to generic GitHub Copilot baseline:
  - Reduction in decorator hallucinations
  - Improvement in correct usage of `@parentResource` and `@route` for hierarchical resources
  - Improvement in adherence to Azure versioning guidelines

- **Review Effort Reduction**: Measurable impact on TypeSpec PR reviews:
  - Reduction in reviewer comments related to TypeSpec standards violations
  - Reduction in PR iterations required to achieve compliant TypeSpec
  - Track via telemetry over 3-month period post-deployment

---

## Agent Prompts


### Scenario 1: Add a new resource type (a simple case)

**Prompt:**

```text
add a new ARM resource type named 'Asset' with CRUD operations
```

**Expected Agent Activity:**

1. Analyzes current TypeSpec project structure and namespace
1. Clarifies resource characteristics with user:
   - Is this a top-level resource or a child resource?
   - If child resource, identify the parent resource
   - What properties should the resource have?
   - Should operations be synchronous or asynchronous/LRO?
1. Calls `azsdk_typespec_generate_authoring_plan` tool with the request and collected information
1. Apply changes according to the retrieved solution:
   - Create resource model extending appropriate base (`TrackedResource`/`ProxyResource`)
   - Add resource name parameter
   - Define resource properties model
   - Create interface with `@armResourceOperations` decorator
   - Implement CRUD operations using appropriate templates (`ArmResourceRead`, `ArmResourceCreateOrReplaceAsync`, etc.)
   - For child resources, apply `@parentResource` decorator
1. Compile the TypeSpec to validate generated OpenAPI paths
1. Summarize all actions taken and display reference documentation

### Scenario 2: Add a new preview API version (an e2e user story that contains multiple cases)

**Prompt:**

```text
add a new preview API version 2025-10-01-preview for service widget resource management
```

**Expected Agent Activity:**

1. Analyzes current TypeSpec project to identify namespace and version
1. Calls `azsdk_typespec_generate_authoring_plan` tool with the request and collected information
1. Apply version related changes according to the retrieved solution
   - Replace an existing preview with the new preview version if latest version is preview, otherwise, just add the new preview version.
   - Update examples according to API changes
1. Ask for features to add or update to this version. e.g.
   - Add new resources
   - Add new operations to an existing resource
   - Add new models, unions, or enums
   - Update existing resources
   - Update existing operations
   - Update existing models, unions, or enums
   - Remove resources, operations, or models
1. For each feature, the agent actions are similar to Scenario 1.

### Scenario 3: Add a new stable API version  (an e2e user story that contains multiple cases)

**Prompt:**

```text
add a new stable API version 2025-10-01 for service widget resource management
```

**Expected Agent Activity:**

1. Analyzes current TypeSpec project to identify namespace and version
1. Calls `azsdk_typespec_generate_authoring_plan` tool with the request and collected information
1. Apply changes according to the retrieved solution:
   - Remove preview resources, operations, models, unions, or enums that are not carried over to the stable version
   - Update examples according to API changes
1. Ask for features to add or update to this version. e.g.
   - Add new resources
   - Add new operations to an existing resource
   - Add new models, unions, or enums
   - Update existing resources
   - Update existing operations
   - Update existing models, unions, or enums
   - Remove resources, operations, or models
1. For each feature, the agent actions are similar to Scenario 1.

### Scenario 4: Update TypeSpec to follow Azure guidelines

**Prompt:**

```text
update the TypeSpec code to follow Azure guidelines for service widget resource management
```

**Expected Agent Activity:**

1. Validate the TypeSpec code and display a list of code snippets that violates Azure guidelines, and the suggested fix
1. Let user confirm which one to fix
1. Apply the code fix
1. Compile the fixed TypeSpec code and let user validate the output

---

## CLI Commands

### typespec generate authoring plan

**Command:**

```bash
azsdk typespec generate-authoring-plan --request <typespec-request> [--additional-information <additional context>] [--typespec-project <project-path>]
```

**Options:**

- `--request <value>`: (Required) The TypeSpec-related task or request to generate a solution for
- `--additional-information <value>`: Additional information, such as context about the TypeSpec project (optional)
- `--typespec-project <value>`: The root path of the TypeSpec project (optional, defaults to current directory)

**Expected Output:**

```text
TypeSpec project: ./tsp
**Solution:** To add a new API version '2025-10-10' for your service 'widget' in TypeSpec, you need to update your version enum and ensure all changes are tracked with versioning decorators.

**Step-by-step guidance:**
1. Update the Versions enum in your versioned namespace to include the new version. Each version string should follow the YYYY-MM-DD format, and if it's a preview, use a '-preview' suffix and decorate @previewVersion on the enum.
2. Add an example folder for this version and copy the relative examples.

**References:**
- **How to define a preview version** (typespec_azure_docs)
  https://azure.github.io/typespec-azure/docs/howtos/versioning/preview-version
  Snippet: To define a preview version...

**Query Analysis:**
- Category: versioning
- Scope: branded
- Service Type: management-plane
```

**Error Cases:**

```text

✗ Error: Required argument missing for command: 'generate-authoring-plan'
  
Usage: azsdk typespec generate-authoring-plan
```

---

