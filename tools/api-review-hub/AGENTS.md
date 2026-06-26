# Agent Instructions - API Review Hub

## Project Overview

This is a new TypeScript web application that coordinates Azure SDK API review workflows on GitHub. GitHub remains the review surface, Azure DevOps remains the release pipeline entry point, and API Review Hub owns the workflow state, webhook processing, repository automation, and release-gating metadata needed to connect those systems.

The service must stay independent from the existing APIView application and the Python `apiview-copilot` package. Do not reuse APIView or APIView Copilot runtime code, data stores, deployment infrastructure, or service-specific assumptions. Existing systems may call this service through explicit APIs.

Use [./proposal-api-review-hub-webapp.md](./proposal-api-review-hub-webapp.md) as the source of truth for product and architecture intent, and [./main.tsp](./main.tsp) as the source of truth for the proposed API contract until this folder has more complete implementation docs.

## Tech Stack

- **Runtime:** Node.js 24+.
- **Language:** TypeScript.
- **Server:** Node.js built-in HTTP APIs to start; add frameworks only when there is a concrete implementation need.
- **Persistence:** Azure Cosmos DB for workflow metadata, idempotency records, release-gating lookup data, and webhook processing state.
- **Configuration:** Azure App Configuration for non-secret runtime settings and feature switches.
- **Secrets:** Azure Key Vault for GitHub App key material, webhook secrets, and other sensitive configuration.
- **Auth:** Microsoft Entra ID for service APIs where appropriate; GitHub webhook signatures for webhook calls.
- **Azure resource access:** Managed identities. Avoid static keys unless an approved migration exception exists.
- **GitHub API:** Use the existing `azure-sdk-automation` GitHub App and repository-scoped installation tokens for repository actions.
- **Telemetry:** Application Insights for requests, dependencies, traces, exceptions, and operational events.
- **Tests:** Use a Node/TypeScript test runner selected during scaffold, with unit tests for routing, auth, webhook validation, persistence mapping, and idempotency.

## Initial File Structure

The scaffold may evolve, but keep responsibilities separated along these lines:

```text
src/
	server.ts                  # Entry point, HTTP server setup, startup checks
	config/                    # App Configuration and environment loading
	routes/                    # /api route registration and handlers
	services/                  # Workflow orchestration and business logic
	github/                    # GitHub App auth, webhook validation, repository actions
	azure/                     # Cosmos DB, Key Vault, App Configuration, identity clients
	models/                    # Shared request, response, and persistence types
	telemetry/                 # Application Insights setup and structured event helpers
    scripts/                   # Scripts used for standing up Azure infrastructure
public/                      # Optional minimal dashboard assets, if added
tests/                       # Unit and focused integration tests
```

All service endpoints must live under `/api`. Keep the GitHub webhook route stable once chosen, with `POST /api/github/webhook-events` as the preferred route from the proposal.

## Coding Conventions

### TypeScript and Modules

- Use ESM-style `import`/`export` consistently.
- Use Node built-ins with the `node:` prefix, for example `import crypto from "node:crypto"`.
- Prefer explicit exported types for service boundaries, persistence records, and API contracts.
- Keep route handlers thin; put workflow decisions in services that are easy to unit test.
- Avoid broad dependency additions. Add routing, validation, OpenAPI, queue, or auth libraries only when the scaffold has a concrete need.

### HTTP APIs

- Keep all API routes under `/api`.
- Validate method, path, headers, content type, and request body before mutating state.
- Return sanitized error responses without stack traces.
- Use stable response shapes for agent-facing endpoints.
- Set timeouts on outbound calls.

### GitHub Webhooks

- Validate GitHub signature, delivery ID, event type, repository metadata, and supported caller before doing any workflow work.
- Deduplicate deliveries using GitHub delivery IDs persisted in Cosmos DB.
- Process webhook events idempotently and tolerate retries.
- Return quickly after durable acceptance; longer work should happen through an internal processing path.
- Emit structured telemetry with event type, delivery ID, repository, processing result, and failure reason.
- Support current and previous webhook secrets during an intentional rotation grace period.

### GitHub Repository Actions

- Use the `azure-sdk-automation` GitHub App, not user tokens or PATs.
- Use installation tokens scoped to the target repository.
- Limit repository writes to repositories where the app is installed and authorized; add an internal allowlist if needed.
- Keep branch creation, review PR creation, labels, reviewer assignment, comments, and cleanup behind a focused GitHub service abstraction.

### Azure Resource Access

- Use `DefaultAzureCredential` or an equivalent managed identity based credential path for Azure resources.
- Cosmos DB access should use Microsoft Entra ID and managed identity rather than account keys.
- Key Vault and App Configuration access must use managed identity.
- Keep non-secret config in App Configuration; keep secrets and key material in Key Vault.

### Persistence

- Cosmos DB should store only coordination data needed for workflow state, idempotency, release gates, and diagnostics.
- Suggested logical containers: `services`, `packages`, `packageVersions`, `reviewPullRequests`, `webhookEvents`, and `repositoryRegistrations`.
- Choose partition keys around access patterns: webhook correlation, package lookup by language and name, release-gating lookup by package version and API hash, and inspection by delivery ID.
- Make state transitions retry-safe and resilient to duplicate or out-of-order GitHub events.

### Security

- Never log secrets, tokens, GitHub signatures, private key material, or raw webhook secret values.
- Custom endpoints that mutate state or expose non-public metadata must use Entra ID authentication unless a documented exception exists.
- Validate and sanitize all user, GitHub, Azure DevOps, and persisted data before rendering or returning it.
- Keep authentication and authorization decisions explicit for GitHub callers, trusted automation, and human administrative callers.

### Testing

- Add focused tests with each behavior change.
- Mock GitHub, Azure Identity, Cosmos DB, Key Vault, App Configuration, and Application Insights clients.
- Cover webhook signature validation, delivery deduplication, idempotent state transitions, release-gate decisions, and sanitized error handling.
- Keep integration tests opt-in when they require live Azure or GitHub resources.

### Variable Naming

- Use descriptive names: `reviewPullRequest`, `packageVersion`, `repositoryRegistration`, `webhookEvent`, `operationId`.
- Common abbreviations are fine for domain concepts: `api`, `pr`, `url`, `id`, `auth`, `config`.

## Required Endpoints

Implement the required endpoints from the proposal before adding secondary dashboard or query surfaces:

| Endpoint | Primary Caller | Purpose |
| --- | --- | --- |
| `POST /api/github/webhook-events` | GitHub | Accepts GitHub webhook deliveries and synchronizes review workflow state. |
| `POST /api/review-prs` | User/Agent | Requests creation of an API review pull request; returns an operation ID. |
| `GET /api/review-prs/operations/{operationId}` | User/Agent | Gets async review PR creation status. |
| `GET /api/releases/check-gate` | Azure DevOps | Evaluates whether a package version and API hash are approved for release. |
| `POST /api/releases/mark-released` | Azure DevOps | Marks a package version as released after the release pipeline succeeds. |

Secondary endpoints for querying review PRs, services, packages, package versions, resolve operations, and health checks should remain consistent with the proposal.

## Environment Configuration

Exact names may be finalized during scaffold, but the app needs configuration for:

| Setting | Purpose |
| --- | --- |
| Environment name | Production, backup, staging, local, or test mode. |
| App Configuration endpoint | Non-secret runtime configuration. |
| Key Vault name or URI | GitHub App key material, webhook secrets, and sensitive settings. |
| Cosmos DB account, database, and containers | Workflow state and idempotency persistence. |
| Application Insights connection | Request, dependency, trace, and exception telemetry. |
| GitHub App ID and installation settings | Repository automation through `azure-sdk-automation`. |
| Allowed repository owners or allowlist | Bounds for repository webhook processing and writes. |
| Azure DevOps caller configuration | Release-gate and mark-released caller authorization. |

## Key Design Decisions

1. **Independent service boundary:** Do not couple this app to APIView or APIView Copilot runtime code, databases, or deployment assumptions.
2. **Skinny workflow service:** Keep GitHub as the review UI and Azure DevOps as the release pipeline entry point; store only the durable state needed to coordinate them.
3. **Built-in Node HTTP first:** Start without Express or another framework unless the implementation proves the need.
4. **Managed identity by default:** Use managed identities for Azure resources and avoid static secrets for Azure service access.
5. **GitHub App automation:** Repository writes happen through `azure-sdk-automation` installation tokens.
6. **Durable webhook acceptance:** Validate, deduplicate, persist, and process webhooks idempotently.
7. **Release gate by API hash:** Release checks must verify the package version has a matching approved API hash and has not become stale.
8. **Separate environment stamps:** Production, backup, and staging must not casually share mutable runtime state.

## Common Tasks

### Adding a new API endpoint

1. Confirm the endpoint belongs under `/api` and document its caller and auth model.
2. Add request validation and sanitized error responses.
3. Put workflow logic in a service module rather than directly in the route handler.
4. Add telemetry for success, validation failure, auth failure, and dependency failure.
5. Add tests for allowed methods, auth, validation, success response, and failure response.

### Handling a new GitHub webhook event

1. Confirm the event is required for API review workflow state.
2. Validate signature, delivery ID, repository metadata, and event-specific payload shape.
3. Persist receipt and deduplicate by delivery ID.
4. Implement idempotent state transitions in a service module.
5. Add tests for duplicate delivery, retry, unsupported event, and out-of-order event behavior.

### Adding a Cosmos DB record type

1. Define the TypeScript model and partition key strategy.
2. Add a focused data access abstraction for queries and mutations.
3. Avoid storing secrets or raw GitHub payloads unless there is an explicit diagnostic need.
4. Add tests for serialization, key construction, and expected query shape.

### Running locally

The exact commands should be updated when the scaffold lands. The expected flow is:

```bash
npm install
npm run build
npm test
npm start
```
