# chatbot-azd-bicep-deployment-transformer

## Description

Transform an existing chatbot project into a production-grade, repeatable, auditable, and reversible Azure deployment system using:

- Azure Developer CLI (`azd`) as the developer-facing and pipeline-invoked workflow layer.
- Bicep as the Azure infrastructure-as-code layer.
- Azure DevOps or GitHub Actions pipelines as the execution/orchestration layer.
- A shared environment-suite definition used consistently by all components.
- Separate provisioning, CI, and CD pipelines per deployable component.
- Progressive rollout, health validation, monitoring, and rollback guardrails.

Use this skill when the user asks to transform, modernize, restructure, or standardize an existing chatbot project's deployment, provisioning, CI/CD, rollout, rollback, or environment management model.

The target project is a multi-component chatbot system. It may include, but is not limited to:

- Teams bot frontend
- Backend App Service, Container App, or AKS workload
- Function App
- Logic App
- AI Foundry project, agent, or model deployment resources
- Knowledge sync job or ingestion pipeline
- Prompt/routing/tool configuration
- App Configuration
- Key Vault
- ACR / container registry
- Storage
- Cosmos DB or other persistence
- Azure AI Search / vector store
- Application Insights / Log Analytics / alerts
- Managed identities and RBAC relationships
- External channel mappings, tenant IDs, service connections, connector resources, or callback URLs

The goal is to migrate from manual, ad-hoc, portal/script-based deployment into a structured code-first deployment system.

---

## Core Principles

### 1. Separate responsibilities clearly

Use the following boundary model:

- **Provisioning** creates and configures infrastructure and platform resources.
- **CI** builds, tests, scans, and publishes immutable artifacts.
- **CD** deploys immutable artifacts into existing environments and coordinates rollout.
- **Rollout** controls traffic/user exposure after deployment.
- **Rollback** restores a previously known-good version or traffic route.
- **Runtime configuration** must be versioned, validated, and promoted like code.
- **Manual Portal changes are not the source of truth.**

Do not mix responsibilities unless there is a deliberate, documented exception.

### 2. Use build-once, promote-many

The same artifact must be promoted across environments.

Correct:

```text
Commit
  -> CI builds image/package once
  -> publish artifact
  -> deploy same artifact to dev
  -> promote same artifact to test/stage/prod
```

Incorrect:

```text
Build separately in each environment
Run pip install or npm install during production CD
Manually rebuild on rollback
```

For containerized components:

- `docker build` belongs to CI.
- `docker push` belongs to CI.
- `azd deploy`, Helm, slot swap, or equivalent deployment belongs to CD.
- Dependency installation such as `pip install`, `npm install`, or compilation should happen in CI/image build, not CD.
- CD must consume versioned artifacts, not create them.

### 3. Treat database and data migrations as controlled CD state evolution

If the chatbot contains databases, indexes, vector stores, knowledge sources, prompts, routing rules, or persisted state:

- Treat schema/data/index/prompt/routing migration as a controlled CD step.
- Do not run production migrations automatically from application startup.
- Prefer explicit pre-deployment or post-deployment migration jobs.
- Require idempotency where possible.
- Use expand/contract or backward-compatible migration patterns when rollback matters.
- Record migration version and connect it to the app artifact version.
- Never silently mutate production state from an app process unless explicitly approved.

### 4. Use Git as the source of truth for environments

All components must share the same environment suite.

Define environments once and have every component pipeline read from that definition.

Example environment suite:

```yaml
environments:
  dev:
    subscription: "<dev-subscription-id>"
    resourceGroupPrefix: "rg-chatbot-dev"
    regions:
      - name: "eastus"
        enabled: true
    approvalRequired: false
    prodDeployOnlyFromPipeline: false

  test:
    subscription: "<test-subscription-id>"
    resourceGroupPrefix: "rg-chatbot-test"
    regions:
      - name: "eastus"
        enabled: true
    approvalRequired: false
    prodDeployOnlyFromPipeline: false

  stage:
    subscription: "<stage-subscription-id>"
    resourceGroupPrefix: "rg-chatbot-stage"
    regions:
      - name: "eastus"
        enabled: true
      - name: "westus"
        enabled: true
    approvalRequired: true
    prodDeployOnlyFromPipeline: false

  prod:
    subscription: "<prod-subscription-id>"
    resourceGroupPrefix: "rg-chatbot-prod"
    regions:
      - name: "eastus"
        ring: "canary"
        enabled: true
      - name: "westus"
        ring: "pilot"
        enabled: true
      - name: "westeurope"
        ring: "broad"
        enabled: true
    approvalRequired: true
    prodDeployOnlyFromPipeline: true
```

Rules:

- Pipelines must not hardcode environment lists or region lists.
- Pipelines must read from the shared environment-suite file.
- Bicep parameters must be generated or selected from the same environment source.
- Any environment change must go through PR review.
- Add drift detection / `what-if` validation before applying infrastructure changes.
- Avoid separate environment definitions per component unless they are derived from the shared environment suite.

### 5. Each component owns its own pipelines but shares common environment contracts

Every component should have:

1. **Provision pipeline**
   - Validates Bicep.
   - Runs `what-if`.
   - Applies infrastructure through `azd provision` or Bicep invoked by azd.
   - Uses environment-suite definitions.
   - Runs only when infra/config changes or manually triggered.
   - Requires approvals for shared/prod infrastructure.

2. **CI pipeline**
   - Restores dependencies.
   - Builds code.
   - Runs unit tests.
   - Runs lint/static analysis/security scans.
   - Builds container/package artifact.
   - Publishes immutable artifact to ACR/artifact store.
   - Tags with commit SHA and build number.

3. **CD pipeline**
   - Selects an existing immutable artifact.
   - Uses `azd deploy` internally where possible.
   - Deploys to one environment/region/ring at a time.
   - Performs smoke tests and health checks.
   - Supports canary, blue-green, slot swap, or ring-based rollout.
   - Supports rollback or traffic reversal.
   - Does not rebuild the application.

Use reusable templates so every component follows the same pattern, but keep component-specific configuration isolated.

---

## Required Repository Transformation

When transforming a project, create or propose this structure unless the repo already has a better equivalent:

```text
/
├─ azure.yaml
├─ infra/
│  ├─ main.bicep
│  ├─ modules/
│  │  ├─ shared/
│  │  ├─ identity/
│  │  ├─ networking/
│  │  ├─ observability/
│  │  ├─ appservice/
│  │  ├─ containerapp/
│  │  ├─ functionapp/
│  │  ├─ logicapp/
│  │  ├─ ai-foundry/
│  │  ├─ search/
│  │  ├─ storage/
│  │  ├─ cosmos/
│  │  └─ keyvault/
│  ├─ environments/
│  │  ├─ environment-suite.yaml
│  │  ├─ dev.parameters.json
│  │  ├─ test.parameters.json
│  │  ├─ stage.parameters.json
│  │  └─ prod.parameters.json
│  └─ README.md
├─ components/
│  ├─ frontend/
│  │  ├─ src/
│  │  ├─ Dockerfile
│  │  ├─ azure.yaml
│  │  └─ pipelines/
│  │     ├─ frontend.provision.yml
│  │     ├─ frontend.ci.yml
│  │     └─ frontend.cd.yml
│  ├─ backend/
│  │  ├─ src/
│  │  ├─ Dockerfile
│  │  ├─ azure.yaml
│  │  └─ pipelines/
│  │     ├─ backend.provision.yml
│  │     ├─ backend.ci.yml
│  │     └─ backend.cd.yml
│  ├─ function-app/
│  │  └─ pipelines/
│  ├─ logic-app/
│  │  └─ pipelines/
│  ├─ agent/
│  │  └─ pipelines/
│  └─ knowledge-sync/
│     └─ pipelines/
├─ pipelines/
│  ├─ templates/
│  │  ├─ azd-auth.yml
│  │  ├─ load-environment-suite.yml
│  │  ├─ bicep-validate.yml
│  │  ├─ bicep-what-if.yml
│  │  ├─ azd-provision.yml
│  │  ├─ container-build.yml
│  │  ├─ azd-deploy.yml
│  │  ├─ smoke-test.yml
│  │  ├─ rollout-canary.yml
│  │  ├─ swap-slot.yml
│  │  ├─ rollback.yml
│  │  └─ notify.yml
│  ├─ orchestrators/
│  │  ├─ deploy-all-dev.yml
│  │  ├─ deploy-all-stage.yml
│  │  └─ deploy-all-prod.yml
│  └─ README.md
├─ config/
│  ├─ appsettings.shared.json
│  ├─ appsettings.dev.json
│  ├─ appsettings.test.json
│  ├─ appsettings.stage.json
│  └─ appsettings.prod.json
├─ docs/
│  ├─ deployment-architecture.md
│  ├─ runbook-deploy.md
│  ├─ runbook-rollback.md
│  ├─ environment-contract.md
│  ├─ component-dependency-graph.md
│  └─ operational-readiness-checklist.md
└─ scripts/
   ├─ validate-env-suite.ps1
   ├─ detect-drift.ps1
   ├─ smoke-test.ps1
   └─ rollback.ps1
```

If the project already has a different structure, preserve it where possible but add equivalent concepts.

---

## Required Transformation Behavior

### Step 1: Discover the current architecture

Inspect the project and identify:

- Components
- Runtime type per component
- Existing deployment scripts
- Existing Bicep/ARM/Terraform files
- Existing `azure.yaml`
- Existing Azure DevOps or GitHub Actions pipelines
- Existing environment definitions
- Existing manual steps documented in README, issues, scripts, or docs
- Identity and RBAC wiring
- Secrets and Key Vault usage
- App Configuration usage
- Monitoring/alerts/logging setup
- Known rollback paths
- Data/stateful dependencies
- Traffic switching mechanisms
- Region/ring/stage model
- Deployment ordering dependencies

Output an architecture inventory table.

### Step 2: Classify actions

Classify each discovered action into provisioning, CI, CD, rollout, rollback, or operational validation.

| Action | Category |
|---|---|
| Create resource group | Provisioning |
| Create App Service / Container App / Function App / Logic App | Provisioning |
| Create Key Vault / Storage / Cosmos DB / AI Search / ACR | Provisioning |
| Configure managed identity / RBAC | Provisioning |
| Configure App Insights / Log Analytics / alerts | Provisioning |
| Build app binary/container | CI |
| Run unit/integration tests before artifact | CI |
| Build Docker image | CI |
| Push image to ACR | CI |
| Deploy versioned image/package | CD |
| Run DB schema migration | CD state evolution |
| Run AI Search index migration | CD state evolution |
| Deploy prompt/routing/tool config | CD config deployment |
| Run smoke test | CD validation |
| Switch slot/backend/traffic weight | Rollout |
| Enable feature flag | Rollout |
| Revert to old artifact | Rollback |
| Switch traffic back | Rollback |
| Disable feature flag | Rollback |
| Restore DB backup | Data recovery, not normal rollback |

If an existing script mixes categories, split it.

### Step 3: Design shared environment-suite contract

Create or update a single shared environment-suite file.

The environment suite must include:

- Environment names
- Subscription IDs or symbolic subscription references
- Tenant IDs when necessary
- Resource group naming convention
- Region list
- Ring/stamp information
- Approval requirement
- Deployment permission model
- Whether local deploy is allowed
- Whether prod deploy is pipeline-only
- Shared resources vs per-environment resources
- Shared resources vs per-region/stamp resources
- Secrets/Key Vault mapping
- Traffic entry point mapping
- Observability workspace mapping

Make every component pipeline consume this contract.

### Step 4: Convert provisioning to Bicep + azd

For each component:

- Create or update Bicep modules.
- Use parameters, outputs, and modules for reuse.
- Prefer managed identity over secrets.
- Store secrets in Key Vault.
- Configure diagnostics and monitoring by default.
- Expose outputs needed by dependent components.
- Avoid resource names hardcoded in multiple places.
- Avoid Portal-only resource configuration.
- Use `azd provision` internally when possible.
- Use Bicep validation and `what-if` before applying.
- Ensure provisioning is idempotent.
- Separate shared infrastructure from component-specific infrastructure.

Recommended provisioning pipeline flow:

```text
load environment suite
  -> select env + regions + component
  -> azd auth/login with pipeline identity
  -> bicep lint/build
  -> what-if / validation
  -> approval if protected env
  -> azd provision --environment <env> --no-prompt
  -> publish provision outputs
```

### Step 5: Convert CI per component

For each buildable component:

- Restore dependencies.
- Build.
- Run tests.
- Run lint/static analysis/security scans.
- Build Docker image or package.
- Push artifact to ACR/artifact store.
- Tag artifacts with commit SHA, build ID, semantic version if available.
- Publish test reports and SBOM if applicable.
- Do not deploy from CI except optional dev preview if explicitly configured.

Recommended CI flow:

```text
checkout
  -> restore
  -> lint
  -> test
  -> build
  -> package
  -> docker build
  -> docker push
  -> publish artifact metadata
```

### Step 6: Convert CD per component

For each deployable component:

- Consume artifact produced by CI.
- Resolve environment and region from environment-suite.
- Use `azd deploy` internally where possible.
- Do not rebuild artifact.
- Deploy to dev automatically if policy allows.
- Deploy to test/stage/prod via promotion gates.
- Include health checks and smoke tests.
- Include rollout and rollback hooks.
- Output deployed version and endpoint.

Recommended CD flow:

```text
select artifact
  -> load environment suite
  -> deploy to env/region/ring
  -> smoke test
  -> health validation
  -> rollout step
  -> post-deployment validation
  -> notify
```

### Step 7: Add rollout support

Support one or more rollout patterns based on hosting platform.

#### App Service

- Use deployment slots when available.
- Deploy to staging slot first.
- Run smoke test against staging slot.
- Swap staging into production.
- Rollback by swapping back or redeploying previous package.

#### Azure Container Apps / AKS

- Use revision traffic weights, Kubernetes rollout, service selector, ingress/controller, service mesh, or tools such as Argo Rollouts/Flagger if already present.
- Support canary: 5% -> 25% -> 50% -> 100%.
- Gate each step on metrics and smoke tests.

#### Front Door / Application Gateway / Traffic Manager

- Use backend weights or routing rules for regional/ring rollout.
- Use region-by-region rollout for production.

#### Feature flags

- Separate deployment from release.
- Deploy code dark.
- Enable feature to internal users, canary users, specific regions, then all users.
- Rollback by disabling flag without redeploying.

### Step 8: Add rollback support

Each CD pipeline must specify its rollback strategy.

Support at least:

- Redeploy previous artifact.
- Slot swap back.
- Traffic weight back to previous version.
- Disable feature flag.
- Re-apply previous Bicep state for infrastructure changes where safe.
- Run explicit data recovery only when approved and documented.

Rollback must not rely on rebuilding old code.

Rollback metadata must include:

- Last known good artifact
- Last deployed version
- Environment
- Region/ring
- Deployment time
- Health validation status
- Rollback command or pipeline stage

Add `runbook-rollback.md`.

### Step 9: Add operational readiness checks

Before production rollout, verify:

- Managed identities exist and have least privilege.
- Key Vault access works.
- App Configuration values exist.
- Health endpoints work.
- Application Insights connected.
- Alerts exist.
- Logs and traces are correlated.
- Dashboards exist or are linked.
- Each component has a DRI/owner.
- Each component has rollback path.
- Each stateful migration is backward compatible or documented as non-reversible.
- Prod deployment is pipeline-only.
- Manual Portal drift detection is available.

### Step 10: Protect production

Enforce:

- Developers can use `azd deploy` locally only for dev/sandbox.
- Production deployment identity is only available to the pipeline.
- Branch protection required.
- Approval gates for stage/prod.
- No local prod credentials.
- Environment checks in pipeline.
- `azd --environment prod` must fail outside pipeline unless explicitly authorized.
- Service connections use least privilege.
- Secrets are not stored in repo or pipeline logs.

---

## Expected Output Format

When transforming a repo, produce the following sections.

### 1. Executive Summary

Summarize:

- Current state
- Target state
- Major risks
- Migration strategy
- Expected benefits

### 2. Current Architecture Inventory

Provide a table:

| Component | Runtime | Current Provisioning | Current CI | Current CD | Manual Steps | Risks |
|---|---|---|---|---|---|---|

### 3. Target Architecture

Describe:

- Shared environment-suite
- Bicep module model
- azd usage model
- Pipeline model
- Rollout model
- Rollback model
- Security model

### 4. Proposed Repository Changes

Provide a file tree and explain major files.

### 5. Environment-Suite Contract

Generate or update the environment-suite YAML.

### 6. Component Pipeline Plan

For each component, output:

```text
Component: <name>

Provision pipeline:
- Purpose:
- Trigger:
- Input:
- azd command:
- Bicep files:
- Validation:
- Approval:
- Output:

CI pipeline:
- Purpose:
- Trigger:
- Steps:
- Artifact:
- Tags:
- Scans:

CD pipeline:
- Purpose:
- Trigger:
- Artifact input:
- azd command:
- Rollout:
- Validation:
- Rollback:
```

### 7. Deployment Ordering

Generate a dependency-aware sequence.

Example:

```text
1. shared infrastructure
2. identity and Key Vault
3. data stores
4. AI resources / Foundry / model resources
5. agent configuration
6. backend
7. function app
8. logic app
9. frontend / Teams bot
10. knowledge sync
11. traffic/feature rollout
```

Adjust based on discovered dependencies.

### 8. Rollout Plan

Include:

- Default rollout strategy
- Canary/ring model
- Regional sequencing
- Health gates
- Manual approvals
- Automatic rollback conditions

### 9. Rollback Plan

Include:

- Per-component rollback strategy
- Cross-component rollback strategy
- Data/state migration cautions
- Last-known-good artifact strategy
- Emergency manual steps only if unavoidable

### 10. Validation Checklist

Include:

- Build validation
- Bicep validation
- What-if validation
- Security validation
- Runtime validation
- Smoke tests
- Health probes
- Observability checks
- Rollback test

### 11. Generated Artifacts

Generate actual proposed file contents when possible, especially:

- `azure.yaml`
- `infra/main.bicep`
- Bicep module stubs
- `infra/environments/environment-suite.yaml`
- reusable pipeline templates
- component provision/CI/CD pipeline YAML
- runbooks

If full generation is too large, generate the highest-value files first and clearly mark the rest as stubs.

---

## Implementation Rules

### azd usage

Use azd internally in pipelines as the standard workflow layer.

Use:

```bash
azd provision --environment <env> --no-prompt
azd deploy <service-name> --environment <env> --no-prompt
```

or equivalent when the project requires service-specific deployment.

Avoid using `azd up` directly in production pipelines unless it is intentionally wrapping both provisioning and deployment and the team accepts that coupling.

Prefer:

- `azd provision` in provision pipelines.
- `azd deploy` in CD pipelines.
- `azd pipeline config` during setup/bootstrap, not as every deployment action.

### Bicep usage

Use Bicep for Azure resources.

Bicep must be:

- Modular
- Parameterized
- Validated
- Reviewed
- Used with `what-if`
- Connected to environment-suite parameters
- Free of manually copied portal state when possible

### Pipeline usage

Pipelines must:

- Be pipeline-as-code.
- Use reusable templates.
- Consume shared environment-suite.
- Use least-privileged service connections.
- Run validation before mutation.
- Apply approvals for protected environments.
- Publish outputs and deployment metadata.
- Include rollback path.

### Manual scripts

Manual scripts are allowed only when:

- Used for local dev/test convenience, or
- Wrapped by a pipeline with logs, approvals, identity, and auditability, or
- Documented as temporary migration steps.

Do not leave production deployment dependent on untracked manual scripts.

### Security

Enforce:

- Managed identity by default.
- Key Vault for secrets.
- No secrets in repo.
- No broad contributor permissions unless justified.
- Production deploy only from pipeline identity.
- PR review for infra changes.
- Approval gates for stage/prod.
- Dependency and container scanning in CI.
- RBAC included in Bicep where safe and stable.

### Observability

Every component must expose or configure:

- Health endpoint
- Liveness/readiness if applicable
- Logs
- Metrics
- Traces where applicable
- Dashboard or query links
- Alerts for availability/error rate/latency
- Post-deploy smoke test

### Rollout and traffic

Do not assume DevOps itself switches traffic.

The pipeline orchestrates traffic changes through the hosting/routing layer:

- App Service slots
- Container Apps revisions
- AKS services/ingress/service mesh
- Front Door/Application Gateway weights
- Feature flags

### Rollback safety

Rollback must be planned before deployment.

Every component CD pipeline must identify:

- Previous artifact
- Previous route/slot/revision
- Rollback command/stage
- Whether data/schema migration blocks rollback
- How to disable new feature behavior quickly

---

## Quality Bar

The transformation is successful only if:

- Existing manual steps are either removed, automated, or explicitly documented.
- Every component has provision, CI, and CD ownership.
- All components share the same environment suite.
- All pipelines can derive env/region/ring from one source of truth.
- The same artifact is promoted across environments.
- `azd` is used consistently as the workflow abstraction.
- Bicep is the source of truth for Azure infrastructure.
- Rollout is gradual or slot/ring based for production.
- Rollback is tested or at least explicitly runnable.
- Prod deployment cannot be triggered accidentally from local developer machines.
- The final repo is understandable to application developers who are not deployment experts.

---

## Final Response Style

Be direct and practical.

When presenting results:

- Prefer tables for inventories and decisions.
- Prefer file trees for repo structure.
- Prefer YAML/Bicep code blocks for generated artifacts.
- Call out assumptions explicitly.
- Call out risks and required human decisions.
- Do not hide gaps.
- Do not claim something is implemented unless you generated or found the file.
- If replacing manual steps, show exactly which manual step is replaced by which pipeline step.
