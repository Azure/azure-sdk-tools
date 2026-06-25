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

## Approach Tradeoffs

API Review Hub is intentionally a skinny workflow and state service. GitHub remains the review surface, Azure DevOps remains the release pipeline entry point, and API Review Hub coordinates the state and automation that would otherwise be spread across GitHub webhooks, Azure DevOps work items, and pipeline scripts.

The main reason to consider this service approach is that the earlier serverless plan depended on GitHub Actions connecting to Azure DevOps through OIDC, and that connection model is considered insecure for this workflow. Because that was a foundational requirement for keeping the implementation entirely in GitHub and Azure DevOps automation, API Review Hub provides a narrower service boundary where repository automation can use the GitHub App and managed identity instead.

| Benefit | Impact |
|---|---|
| Centralized workflow state | Review PR status, approval state, package version state, release gate decisions, and webhook processing state can be queried from one service instead of inferred from GitHub, Azure DevOps, and pipeline history. |
| Simpler pipelines and scripts | Language-specific API review complexity can move into API Review Hub, allowing Azure DevOps pipelines and repository scripts to become fewer, smaller, and more consistent. |
| GitHub-first review experience | Architects and service teams continue to review in GitHub, while API Review Hub stores the durable state projection needed for automation and release gating. |
| Better automation boundary | GitHub App operations, webhook validation, branch synchronization, review PR cleanup, and webhook secret rotation are owned by one service instead of scattered through scripts. |
| Reduced pipeline credential risk | Repository automation can use the GitHub App and managed service identity rather than expanding Azure DevOps OIDC-based access patterns across many language-specific pipelines. |
| Future extensibility | Additional agents, dashboards, or release planner integrations can query API Review Hub through explicit APIs rather than scraping GitHub or Azure DevOps state. |

| Cost or Risk | Impact |
|---|---|
| Continued service maintenance | API Review Hub still requires hosting, deployment, monitoring, incident response, backup strategy, and long-term ownership, similar to APIView today. |
| Azure resource ownership | The team that owns API Review Hub must also own the App Service, Cosmos DB, Key Vault, App Configuration, Application Insights, managed identities, and deployment configuration. |
| Central team ownership | API review workflow ownership remains concentrated in one service team instead of being fully dispersed to individual language teams. |
| Additional persisted state | The service must keep its Cosmos DB projection consistent with GitHub events, release pipeline calls, and any migrated Azure DevOps metadata. |
| Failure modes remain service-centered | If API Review Hub is unavailable or stale, review PR creation, release gates, or cleanup automation may be blocked or require fallback behavior, as with APIView-owned workflows today. |

The alternative is to keep the workflow distributed across GitHub, Azure DevOps, and pipeline scripts. That avoids introducing a new hosted service, but it pushes coordination logic into less discoverable places, makes approval state harder to query consistently, and makes future agent or dashboard scenarios depend on scraping or reimplementing workflow logic.

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

### Endpoint Summary

The proposed TypeSpec contract defines the following service endpoints under `/api`.

The following endpoints are required for the end-to-end review and release process.

| Endpoint | Primary User | Purpose |
|---|---|---|
| `POST /api/github/webhook-events` | GitHub | Accepts GitHub webhook deliveries so API Review Hub can respond to and synchronize with GitHub changes, including review activity, pushed commits, pull request lifecycle changes, and other workflow events. |
| `POST /api/review-prs` | User/Agent | Requests creation of a GitHub API review pull request for a package API change. This is an LRO that returns an operation ID. |
| `GET /api/review-prs/operations/{operationId}` | User/Agent | Gets the status of an async review PR creation operation and returns the created review pull request when the operation succeeds. |
| `GET /api/releases/check-gate` | ADO | Evaluates whether a package version and API hash have the approval needed for release. |
| `POST /api/releases/mark-released` | ADO | Marks a package version as released after the release pipeline succeeds. Returns only a status code on success. |

The following endpoints are secondary, but useful for agentic queries and for a simple dashboard that preserves some of the grouping functionality that the legacy APIView provides today.

| Endpoint | Primary User | Purpose |
|---|---|---|
| `GET /api/review-prs` | User/Agent | Lists review pull requests known to API Review Hub, optionally filtered by package language or working branch. |
| `GET /api/review-prs/{reviewPullRequestId}` | User/Agent | Gets a review pull request by API Review Hub identifier. |
| `POST /api/review-prs/resolve` | User/Agent | Functionally similar to `GET /api/review-prs/{reviewPullRequestId}`, but resolves a review pull request using values callers are more likely to know, such as a review PR URL or package coordinates, instead of an opaque API Review Hub ID. |
| `GET /api/services` | User/Agent | Lists service groupings known to API Review Hub. |
| `GET /api/services/{serviceId}` | User/Agent | Gets service metadata and associated packages. |
| `GET /api/packages` | User/Agent | Lists package records known to API Review Hub, optionally filtered by language or service. |
| `GET /api/packages/{packageId}` | User/Agent | Gets package metadata by package identifier. |
| `GET /api/packages/{packageId}/versions` | User/Agent | Lists version records for a package. |
| `POST /api/packages/resolve` | User/Agent | Functionally similar to getting a package or package version by API Review Hub identifiers, but resolves using values callers are more likely to know, such as package coordinates or a review PR URL, instead of opaque API Review Hub IDs. Returns a wrapper object with a `kind` field indicating whether the resolved resource is a package or package version. |
| `GET /api/health` | User/Agent | Returns service health for probes and operational checks. |

### Workflow Scenarios

#### Create Review Pull Request

1. A user or automation requests a review pull request with `POST /api/review-prs`.
2. API Review Hub accepts the request and returns an operation ID.
3. API Review Hub uses the `azure-sdk-automation` GitHub App to create the required synthetic branches and open the review pull request.
4. API Review Hub assigns the appropriate architect reviewers and applies the `architecture-review-needed` label.
5. API Review Hub records the review pull request, associates it with the package version, and initializes approval state as `pending`.
6. The caller polls `GET /api/review-prs/operations/{operationId}` to retrieve the created review pull request when the operation succeeds.

#### Architect Review

1. The architect submits their review in GitHub.
2. GitHub sends a webhook delivery to `POST /api/github/webhook-events`.
3. API Review Hub validates the webhook signature, delivery metadata, repository, and actor.
4. API Review Hub determines whether the review represents an approval, rejection, revocation, or other supported review state change.
5. API Review Hub updates the approval record associated with the review pull request and package version.

#### Updates Pushed

1. GitHub sends a webhook delivery when a new commit is pushed.
2. API Review Hub checks whether the pushed branch is a working branch for any open review pull request.
3. When the branch matches a known review workflow, API Review Hub uses the GitHub App to synchronize changes from the working branch to the review branch.
4. API Review Hub updates review pull request state as needed so later lookup and release-gate calls observe the current review state.

#### Release

1. The service team triggers the release pipeline in Azure DevOps.
2. The release pipeline calls `GET /api/releases/check-gate` with package language, package name, version, and API hash.
3. API Review Hub checks whether the package version has a matching approved API hash and no later state has made the approval stale.
4. If the gate allows release, the pipeline proceeds.
5. After a successful release, the pipeline calls `POST /api/releases/mark-released`.
6. API Review Hub records the package version as released and uses the GitHub App to close the associated review pull request when appropriate.

#### Review Pull Request Cleanup

1. A service team or API Review Hub closes or merges the review pull request in GitHub.
2. GitHub sends a webhook delivery to `POST /api/github/webhook-events`.
3. API Review Hub updates the review pull request lifecycle status to `closed` or `merged`.
4. API Review Hub uses the GitHub App to delete synthetic base and review branches when cleanup is safe.
5. The review pull request remains available in GitHub, and API Review Hub preserves the historical review record.

#### Webhook Secret Rotation

1. On a regular schedule, API Review Hub enumerates repository registrations whose `rotationDate` has passed or is due soon.
2. For each linked repository, API Review Hub generates a new webhook secret.
3. API Review Hub copies the current secret value from the stable `webhookSecretKey` Key Vault reference to the stable `lastWebhookSecretKey` Key Vault reference.
4. API Review Hub stores the new webhook secret value in Key Vault at the existing `webhookSecretKey` reference for that repository.
5. API Review Hub sets the Key Vault expiration on the current secret value, sets the Key Vault expiration on the last secret value to the end of the old-secret grace period, and updates `rotationDate` to the next time API Review Hub should rotate the current secret before it expires.
6. During the overlap window, API Review Hub accepts webhook deliveries signed with either the current or last webhook secret value for that repository, as long as the corresponding Key Vault secret value has not expired.
7. API Review Hub uses the GitHub App or GitHub webhook configuration APIs to update the repository webhook configuration to use the new webhook secret value.
8. After the overlap window expires and webhook delivery has stabilized on the new secret, API Review Hub stops accepting deliveries signed with the old secret and clears the secret value at the `lastWebhookSecretKey` reference.
9. API Review Hub repeats this process for every linked repository.
10. This workflow is an internal operational process and does not require a new public service API.

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
| `reviewPullRequests` | Review pull request records associated with package versions, including GitHub PR identity, branch metadata, approval state, and lifecycle status. |
| `webhookEvents` | GitHub delivery IDs, event metadata, processing status, retry state, and failure details. |
| `repositoryRegistrations` | Linked repository records, including stable Key Vault references for current and previous webhook secret values, the registration update time, and the next API Review Hub rotation date. Secret expiration is enforced by Key Vault secret attributes. |

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