# Proposal: API Review Hub

## Objective

Create API Review Hub, a new TypeScript web application that coordinates API review workflows on GitHub.

This service must be independent from the existing APIView application and the Python `apiview-copilot` package. It must not reuse APIView or APIView Copilot runtime code, data stores, deployment infrastructure, or service-specific assumptions. Existing systems may call the new service through explicit APIs, but the new web app owns its own hosting, configuration, persistence, authentication, and operational model.

The new service will:

- Accept GitHub webhook events from all repositories subscribed through the GitHub App or webhook configuration, then filter and correlate the events needed for API review workflows.
- Validate all incoming GitHub webhook requests before taking any other actions.
- Expose a small set of required APIs and allow additional custom API endpoints to be added over time.
- Store API review workflow metadata in Cosmos DB.
- Use the existing `azure-sdk-automation` GitHub App for repository actions.
- Use Entra ID authentication where appropriate.
- Use managed identities to access Azure resources.
- Run as separate production, backup, and staging resource stamps.

---

## Key Architecture Decision

The GitHub API review workflow should be implemented as a new TypeScript service rather than as a continuation of APIView or APIView Copilot.

This keeps the new GitHub-centered workflow from inheriting APIView UI-era assumptions, parser-specific implementation details, Copilot review pipeline behavior, or existing deployment coupling. It also avoids binding the new TypeScript service to the legacy APIView and APIView Copilot language stacks. APIView and APIView Copilot can remain independent systems while this service focuses on subscribed GitHub event ingestion, workflow state, release-gating metadata, and repository automation. That separation creates a clean path to retire the legacy APIView and APIView Copilot systems once the GitHub workflow has replaced their required responsibilities.

---

## Technology Stack

- Runtime: Node.js on Azure App Service.
- Language: TypeScript.
- HTTP server: Node.js built-in HTTP APIs.
- Persistence: Azure Cosmos DB.
- Configuration: Azure App Configuration.
- Secrets and GitHub App private key material: Azure Key Vault.
- Telemetry: Application Insights.
- Authentication: Microsoft Entra ID for service APIs where appropriate.
- Azure resource access: Managed identities.

The implementation should start with the built-in Node.js HTTP APIs rather than introducing an application framework. Every dependency adds potential vulnerability, maintenance, and supply-chain risk, so the service should use the minimum dependency set needed for the required behavior. Additional routing, middleware, validation, OpenAPI, or authentication libraries should require a concrete implementation need.

---

## Azure Resource Model

Each environment stamp must include the following resources:

| Resource | Purpose |
|---|---|
| App Service Plan | Dedicated compute plan for the web app. |
| Web App | Hosts the TypeScript service. |
| Key Vault | Stores secrets, GitHub App key material or signing keys, webhook secrets, and other sensitive configuration. |
| App Configuration | Stores non-secret runtime configuration, feature switches, environment settings, and endpoint configuration. |
| Cosmos DB | Stores workflow metadata, webhook processing state, custom endpoint metadata, release-gating lookup records, and idempotency records. |
| Application Insights | Stores request telemetry, dependency telemetry, traces, exceptions, and operational events. |

### Environment Stamps

The service must be deployed as three separate copies:

| Environment | Purpose |
|---|---|
| Production | Primary live service for API review GitHub workflows. The production Web App must use deployment slots, including a staging slot used by the deployment pipeline before swap. |
| Backup | Standby copy for production recovery. It should have its own resource stamp and configuration, and it should be ready to receive traffic after an explicit failover decision. |
| Staging | Pre-production validation environment for integration tests, deployment verification, and GitHub workflow testing. |

Production, backup, and staging should not share mutable runtime state unless that sharing is explicitly part of the failover design. Configuration and secrets should be replicated or promoted intentionally, not edited independently by hand.

The production staging slot is an App Service deployment slot, not the separate staging environment stamp. The pipeline should deploy production builds to the staging slot first, validate the slot, and require an explicit slot swap to move the build into production. Keeping the previous production slot available after swap provides a fast rollback path to the last known-good application and configuration if the new deployment fails.

---

## Required Service Endpoints

All API Review Hub HTTP endpoints must live under the `/api` URL group so service API traffic is consistently identifiable by path.

### GitHub Webhook Endpoint

The service must provide a GitHub webhook endpoint. The exact route can be finalized during implementation, but the service must reserve a stable endpoint such as:

```http
POST /api/github/webhook-events
```

The endpoint must:

- Validate every incoming GitHub webhook request, including signature, delivery ID, event type, and repository metadata.
- Support GitHub App event delivery and repository webhook delivery if both are required.
- Receive events at the subscribed repository granularity and decide in service code which events apply to API review workflows.
- Deduplicate deliveries using GitHub delivery IDs.
- Persist webhook receipt and processing state in Cosmos DB.
- Process events idempotently.
- Return quickly after durable acceptance, with longer work delegated to an internal processing path.
- Produce structured telemetry for event type, delivery ID, repository, processing result, and failure reason.

Webhook secret rotation must be operationalized through Key Vault and GitHub webhook or GitHub App configuration APIs where possible. During rotation, the service should support validating both the current and next secret for a short grace period.

### Custom API Endpoints

The service must allow custom API endpoints to be added without changing the GitHub webhook contract.

Custom endpoints should be added for specific workflow actions or queries, such as:

- Requesting creation of API review pull requests.
- Returning release-gating decisions.

Custom endpoints that mutate state or expose non-public metadata must use Entra ID authentication unless there is a specific documented reason to use another authentication model.

---

## GitHub App Integration

The service will coordinate with the existing `azure-sdk-automation` GitHub App for repository actions.

Examples include:

- Creating or updating branches used for API review artifacts.
- Opening or updating API review pull requests.
- Posting comments or status summaries.
- Applying labels.
- Requesting reviewers.
- Closing or updating review PRs in response to lifecycle events.

The service should use GitHub App installation tokens scoped to the target repository. GitHub App identity, installation metadata, and key material should be stored or referenced through App Configuration and Key Vault. Where practical, private key material should remain in Key Vault and signing should use Key Vault-backed cryptography rather than loading long-lived private keys into application memory.

Repository operations must be limited to repositories where the GitHub App is installed and authorized. By default, the service can modify any repository where the `azure-sdk-automation` GitHub App is installed and has the required permissions. The service may also enforce an internal repository allowlist, in which case repository modifications are limited to repositories that need API review workflows, such as Azure SDK language repositories and possibly `azure-rest-api-specs`.

---

## Authentication and Authorization

- The web app must use managed identity for Azure resource access.
- Cosmos DB access should use Microsoft Entra ID and managed identity, not static account keys, unless a temporary migration exception is approved.
- Key Vault access must use managed identity.
- App Configuration access must use managed identity.
- Application Insights ingestion should use managed identity or platform-supported connection configuration.
- Custom service APIs must use Entra ID authentication where appropriate.
- GitHub webhook requests must be authenticated by validating GitHub signatures.
- GitHub repository actions must use `azure-sdk-automation` GitHub App installation tokens.

The service should distinguish between trusted automation, GitHub webhook callers, and human administrative callers. Each API surface should document its expected caller and authorization model.

---

## Cosmos DB Data Model Sketch

Cosmos DB should store only the data needed for workflow coordination, idempotency, release-gating lookup, and diagnostics.

Suggested logical containers include:

| Container | Purpose |
|---|---|
| `services` | Service records that group related language packages, such as Azure Storage Blobs. |
| `packages` | Package records keyed by language and package name, with service association. |
| `packageVersions` | Version records with release status, API hash, approval state, and review PR links. |
| `webhookEvents` | GitHub delivery IDs, event metadata, processing status, retry state, and failure details. |
| `repositoryInstallations` | GitHub App installation metadata and repository capability flags. |

The final partitioning strategy should be chosen around the most common access patterns: webhook correlation, package lookup by language and package name, release-gating lookup by package version and API hash, and operational inspection by delivery ID.

---

## Configuration and Secret Management

App Configuration should hold non-secret settings such as:

- Environment name.
- GitHub App ID and allowed repository owner settings.
- Feature switches.
- Custom endpoint enablement.
- Cosmos DB database and container names.
- Backup and failover mode indicators.

Key Vault should hold secrets and sensitive values such as:

- GitHub webhook secrets.
- GitHub App private key material or Key Vault key references.
- Emergency break-glass secrets, if any are approved.
- Any external service credentials that cannot use managed identity.

Configuration should be deployed consistently across production, backup, and staging. Manual portal edits should be reserved for emergency operations and captured afterward as source-controlled configuration changes.

---

## Reliability and Operations

- Webhook processing must be retry-safe and idempotent.
- Duplicate GitHub deliveries must not create duplicate state transitions.
- Out-of-order GitHub events must not corrupt workflow state.
- Production and backup must have independent App Service, App Configuration, Key Vault, Cosmos DB, and Application Insights resources.
- Backup must have a documented activation process and configuration parity checks.
- Staging must be able to validate GitHub webhook delivery and GitHub App operations without affecting production repositories. Staging repository write access should either follow the repositories where `azure-sdk-automation` is installed or be narrowed by an internal allowlist to repositories that need API review workflows, such as Azure SDK language repositories and possibly `azure-rest-api-specs`.
- Application Insights must capture enough telemetry to explain webhook failures, GitHub App failures, authentication failures, and release-gating lookup failures.
- Operational dashboards should show webhook volume, processing failures, GitHub API failures, Cosmos DB throttling, and endpoint latency.

---

## Deployment Requirements

- Infrastructure should be source controlled.
- Deployments should be repeatable for production, backup, and staging.
- The build should compile TypeScript, run unit tests, and produce a deployable app artifact.
- The release process should deploy the staging environment first, then deploy the production build to the production Web App staging slot, validate it, and require an explicit slot swap before it serves production traffic.
- The release process should preserve the previous production slot as the last known-good version so rollback can be performed quickly by swapping slots back if the new deployment fails.
- Backup should be deployed or updated after production, or through a documented backup parity process.
- Environment-specific values should come from App Configuration, Key Vault, managed identities, and deployment parameters rather than hard-coded application values.

---

## Non-Goals

- Reusing APIView runtime code or data stores.
- Reusing APIView Copilot runtime code or data stores.
- Recreating APIView's historical review UI.
- Implementing AI review generation in this service.
- Replacing the `azure-sdk-automation` GitHub App.
- Storing long-lived GitHub user tokens.

---

## Open Questions

- Should backup be warm standby with replicated Cosmos DB data, or an independently restorable stamp with documented recovery steps?
- Should failover be manual, DNS-based, or handled by a future traffic management resource?
- Which custom endpoints are required for the first milestone beyond GitHub webhook ingestion?
- What retention policy should apply to webhook event records and operational diagnostics?
- Which release-gating lookup contract must be supported in the first version?

---

## Success Criteria

- The TypeScript web app is deployed independently from APIView and APIView Copilot.
- Production, backup, and staging resource stamps exist with the required Azure resources.
- The service accepts and validates GitHub webhooks.
- Webhook processing is idempotent and observable.
- The service can authenticate to Azure resources using managed identity.
- Custom service APIs can use Entra ID authentication.
- Repository actions are performed through `azure-sdk-automation` GitHub App installation tokens.
- Cosmos DB stores review workflow state and webhook processing records needed for reliable operation.