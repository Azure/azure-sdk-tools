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

- **Azure TypeSpec Style Guide**: Style conventions and coding standards specific to writing TypeSpec for Azure services, ensuring consistency across Azure service definitions. See [Azure TypeSpec Style Guide](https://azure.github.io/typespec-azure/docs/reference/azure-style-guide)

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

### Architecture

This spec provides AI-powered TypeSpec authoring assistance through a **GitHub Copilot Skill** named `azure-typespec-author`, rather than a standalone custom agent. 

![Azure TypeSpec author skill architecture diagram](azure-typespec-author-architecture.png)

The design has two cooperating components:

1. **Azure TypeSpec Author Skill (Runtime)**: The request-time workflow executed by GitHub Copilot. It performs project analysis, intake, plan generation, code edits, validation, and reference output.
1. **Skill Self-Evolve Agent (Offline)**: An offline optimization loop that ingests telemetry and benchmark results, improves prompts/workflows/reference links, reruns benchmarks, and proposes updates via pull request.

### Skill: `azure-typespec-author`

The skill is a Markdown-defined capability (`SKILL.md` plus a set of `references/` files) that GitHub Copilot invokes automatically whenever a task involves creating or modifying TypeSpec (`.tsp`) files (except `client.tsp`). 

**Triggers** — the skill activates for any TypeSpec change: adding, bumping, or promoting API versions (preview, stable); adding or modifying resources, operations, models, properties, or decorators; changing visibility, constraints, LRO patterns, or suppressions; defining `operationId`, spread models, or extension resources; and post-Swagger-conversion edits. It does **not** activate for SDK generation, package release, or single MCP tool calls.

**Prerequisite** — the `azure-sdk-mcp` server (provided by `azsdk-cli`) must be running to serve the skill's MCP tools.


#### Skill Workflow

The runtime skill enforces a fixed six-step workflow defined in `SKILL.md`. Every `.tsp` edit runs the full workflow — even a seemingly trivial change (for example, making a property optional with `?`) can be breaking — and steps are never skipped. All edits are minimal and scoped to the request, and the plan produced in Step 3 is the single source of truth for Step 4.

| Step | Name | Reference | Summary |
|------|------|-----------|---------|
| 1 | Analyze Project | `references/analyze-project.md` | Collect project root, `tspconfig.yaml`, service type (ARM / data-plane), existing and latest API versions, intent, target resource/interface, and constraints. |
| 2 | Intake | `references/intake.md` | Collect requirements and context, identify the case (Add Resource Type, Add Resource Operations, API Version Evolution), and gather case-specific inputs to drive search. |
| 3 | Generate Authoring Plan | `references/authoring-plan.md` | Build a grounded plan using search toolings: primary agentic search tooling first, then the backup MCP planning path only when search is insufficient. |
| 4 | Apply Changes | — | Make minimal `.tsp` edits that follow the Step 3 plan exactly; confirm uncertainties with the user first. |
| 5 | Validate | `references/validation.md` | Run `azsdk_run_typespec_validation` and `tsp compile .` (always), plus example verification for API-version evolution. |
| 6 | Output Reference Links | — | Emit all documentation URLs referenced in Step 3 so the user can trace the guidance behind each change. |

#### Search Toolings (Step 3)

Step 3 builds the authoring plan in order: use agentic search first, and fall back to MCP planning only when search cannot produce sufficient grounded guidance.

1. **Agentic search**: follow `references/agentic-search.md`: select the relevant URLs from `references/reference-document-links.md`, `web_fetch` each into Markdown, search for content matching a query derived from the request and Step 1 result, iterate until sufficient, and synthesize the extracted guidance into a concrete plan.
2. **Authoring plan MCP tool (backup)**: call `azsdk_typespec_generate_authoring_plan` with the verbatim user request, the Step 1–2 context, and the project root. The tool returns a RAG-grounded plan from the Azure SDK Knowledge Base.

##### Agentic search

The runtime executes agentic search with a deterministic sub-flow:

1. Classify the request into a concrete case (resource type/operations/version evolution/guideline-fix).
1. Select candidate links from `reference-document-links.md` for that case.
1. Fetch and normalize the selected pages into markdown content.
1. Retrieve the most relevant snippets using request-aware semantic/keyword queries.
1. Build a candidate plan with explicit citations for each non-trivial recommendation.
1. If confidence or coverage is insufficient, expand the search scope and retry within bounded iterations.
1. Hand the grounded plan and references to Step 4 and Step 6.

Primary benefits:

1. Keeps authoring guidance close to authoritative docs.
1. Reduces hallucinated decorators/templates by constraining generation to retrieved evidence.
1. Makes plan decisions auditable by users and reviewers through explicit reference links.

##### Authoring plan MCP Tool

The MCP planner is a resilience path, not the default path. The runtime should invoke it when one or more conditions are true:

1. Search retrieval has low confidence or conflicting guidance.
1. The request spans multiple coupled concerns (for example, version evolution + route hierarchy + breaking-change decorators).
1. Critical references are missing from the link store for the detected case.
1. Bounded search iterations are exhausted without a complete executable plan.

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
  "operation_status": "Succeeded",
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
    "question": "<analyzed-question>",
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
    "question": "<analyzed-question>",
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
- Uses interactive browser flow for authentication (MSAL `AcquireTokenInteractive`)
- Access tokens are cached in memory for the lifetime of the process and are not persisted to disk by default
- The tool uses the Azure Knowledge Base service and is configured with a default service by default. If you want to use a different Azure Knowledge Base service instead of the default one, set following environment variables to override:
  - `AZURE_SDK_KB_ENDPOINT`: Service endpoint
  - `AZURE_SDK_KB_CLIENT_ID`: Application (client) ID of the service
  - `AZURE_SDK_KB_SCOPE`: Authentication scope

**Integration**:
- TypeSpec authoring tool sends structured queries to the knowledge base
- Knowledge base returns contextual solutions with references
- Runtime merges the response with local project analysis and either executes directly or requests user clarification

#### Validation Tooling (Step 5)

**Name (MCP)**: `azsdk_run_typespec_validation`

**Purpose**: Validate a TypeSpec project and surface errors and warnings so the skill can fix issues before completing a task.

**Usage in the skill (Step 5)**:

1. Invoke `azsdk_run_typespec_validation` with the project root. On failure, fix the reported issues and re-run; limit to 3 retry attempts, then stop and report the remaining errors to the user.
1. Run `tsp compile .` from the project root and verify the OpenAPI `.json` output under the path configured by the `@azure-tools/typespec-autorest` entry in `tspconfig.yaml`.
1. For API-version evolution only, verify that `examples/` exists for the target version with the correct `api-version`, and that example folders for removed versions are deleted.

#### Reference Files

`SKILL.md` delegates step detail to the following reference files, keeping the entry point small and each concern independently maintainable:

| File | Purpose |
|------|---------|
| `references/analyze-project.md` | Step 1: project analysis inputs and output template |
| `references/intake.md` | Step 2: general + case-specific intake and confirmation |
| `references/authoring-plan.md` | Step 3: build the authoring plan (MCP tool + agentic search) |
| `references/agentic-search.md` | Procedure: select URLs → fetch → extract guidance |
| `references/reference-document-links.md` | Catalog of external Azure / TypeSpec guide URLs |
| `references/validation.md` | Step 5: validate → compile → verify examples |

#### Why a Skill (Design Rationale)

- **Transparent activation** — the host agent selects the skill from declarative triggers, so users do not need to know when to switch to a dedicated agent.
- **Reproducible behavior** — the fixed workflow and the plan-as-single-source-of-truth rule reduce model-to-model variance and hallucinated decorators.
- **Search-first grounding** — agentic retrieval over curated references and fetched docs is the primary planning path, improving traceability and minimizing speculative output.
- **Controlled fallback** — the Knowledge Base MCP tool acts as a backup planner when search is insufficient, preserving robustness without making the workflow opaque.
- **Maintainability** — a Markdown `SKILL.md` plus `references/` requires no compiled agent host and can be reviewed and evolved like documentation.

- **Continuous improvement by design** — the self-evolution agent continuously improves prompts, tools, and references from telemetry and benchmarks.

---


### Skill Self-Evolve Agent

The Skill Self-Evolve Agent runs outside the request-time workflow and continuously improves solution quality.

#### Objectives

1. Identify failure patterns across benchmark and real-world authoring sessions.
1. Improve retrieval coverage and freshness of reference links.
1. Improve workflow instructions and prompts to reduce ambiguity and retries.
1. Keep improvements reviewable and safe through benchmark gates and PR-based updates.

#### Loop Stages

1. **Analyze telemetry and benchmark results** from WorkIQ and benchmark pipelines.
1. **Generate summary and insights** including frequent failure clusters and citation gaps.
1. **Update skill assets** such as `SKILL.md`, references, decision rules, and prompt wording.
1. **Update reference knowledge** by adding new links, pruning stale links, and improving case-to-link mapping.
1. **Rerun benchmarks** to validate that updates improve objective metrics.
1. **Create PR** with a clear delta summary and benchmark evidence for human review and merge.

#### Guardrails

1. No direct production rollout: all updates flow through pull requests.
1. No benchmark regression accepted without explicit human approval.
1. Every generated workflow change includes rationale and linked evidence.

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

- **Search-First Coverage and Traceability**:
  - At least 70% of benchmark scenarios are resolved without MCP fallback
  - Plans include citations to references selected or fetched during Step 3
  - Fallback invocations include a recorded reason category (coverage gap, ambiguity, low confidence, or multi-concern complexity)

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

- **Self-Evolution Efficacy**:
  - The self-evolution loop produces periodic PRs with measurable benchmark gains
  - Reference-link freshness is maintained (stale/broken links below defined threshold)
  - Prompt/workflow changes are accompanied by before-vs-after benchmark evidence

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
1. Runs search tooling first (link-store selection + fetch + retrieval) to produce a grounded plan
1. Calls `azsdk_typespec_generate_authoring_plan` only if search is insufficient
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
1. Runs search tooling first (link-store selection + fetch + retrieval) to produce a grounded plan
1. Calls `azsdk_typespec_generate_authoring_plan` only if search is insufficient
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
1. Runs search tooling first (link-store selection + fetch + retrieval) to produce a grounded plan
1. Calls `azsdk_typespec_generate_authoring_plan` only if search is insufficient
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
Option '--request' is required.

Description:
  Generate a solution or execution plan for defining and updating a TypeSpec-based API specification for an Azure 
  service.

Usage:
  azsdk tsp generate-authoring-plan [options]

Options:
  --request (REQUIRED)      The TypeSpec‑related task or user request sent to an AI agent to produce a proposed 
                            solution or execution plan with references.
  --additional-information  The additional information to consider for the TypeSpec project.
  --typespec-project        The root path of the TypeSpec project
  -h, --help                Show help and usage information
```

---

