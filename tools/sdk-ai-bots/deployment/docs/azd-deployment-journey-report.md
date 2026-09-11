<!-- cspell:words appconfig appservice postdeploy postprovision predeploy preprovision qiaozha -->

# SDK AI Bot azd Deployment Journey Report

- **Date:** September 9, 2026
- **Pull request:** [Azure/azure-sdk-tools#16357](https://github.com/Azure/azure-sdk-tools/pull/16357)
- **Branch:** `qiaozha/sdk-ai-bots-deployment`

## Scope

This report reviews the end-to-end experience and evolution of deploying the
SDK AI bot with Azure Developer CLI (`azd`). It correlates the implementation in
PR #16357 with all 12 `Azure/azure-dev` issues authored by `qiaozha` and
distinguishes native capabilities, retained workarounds, and current risks.

PR #16357 replaces fragmented deployment machinery with a seven-layer `azd`
architecture, workload identity federation, explicit dependencies, and a
source-controlled environment contract. The result is coherent, but it remains
a hybrid: `azd` owns orchestration and several native deployments, while
TypeScript hooks own substantial image, RBAC, Foundry, Search, Teams,
configuration, and Logic App state.

## Findings and Remediation

### 1. Resolved: frontend deployment ownership

The frontend previously combined a TypeScript App Service declaration with a
container site and a custom image-building predeploy hook. It now uses an
explicit native Docker service in [`azure.yaml`](../../azure.yaml), and its
Dockerfile performs the TypeScript build inside the remote ACR build.

The custom frontend predeploy hook was removed. Package, publish, and deployment
now have one owner: azd's native App Service container lifecycle.

### 2. Resolved: hosted-agent image verification

[`agent-postdeploy.ts`](../hooks/agent-postdeploy.ts) now resolves the target
image independently from the ACR endpoint, environment name, and
`AZD_IMAGE_TAG`. Reconciliation fails closed when those authoritative deployment
inputs are unavailable; unproduced image override aliases were removed.

Focused tests cover explicit, derived, and missing target-image inputs. The hook
can now detect image drift while injecting `AZURE_APPCONFIG_ENDPOINT`.

### 3. Resolved: native Function App container deployment

The Function App now uses `host: function`, `language: docker`, and native
remote build in [`azure.yaml`](../../azure.yaml). Its duplicate ACR build and
direct container-repoint predeploy hook were removed.

This adopts the container-aware Function host delivered by
[Azure/azure-dev#9284](https://github.com/Azure/azure-dev/pull/9284) and removes
the lifecycle ordering ambiguity.

### 4. Resolved: principal-aware frontend role assignments

All four frontend grants are declared in
[`frontend/main.bicep`](../infra/layers/frontend/main.bicep). Their deterministic
names include the assignment scope, frontend managed identity resource ID, and
role definition ID, preventing collisions between different frontend
identities.

The assignments cover Monitoring Metrics Publisher, ACR Pull, Storage Blob Data
Contributor, and Storage Table Data Contributor. The prior broad Storage
Account Contributor grant was removed because frontend code does not retrieve
keys or perform management-plane storage operations. Existing environments need
a one-time approved cleanup of assignments created with the earlier GUID
formula before applying the new Bicep names. The known out-of-band Foundry
assignment remains the only tuple-aware ensure-hook case.

### 5. Resolved: rollout contract matches active behavior

Inactive slot, rollout-strategy, success-rate, and latency metadata was removed
from [`environment-suite.yaml`](../infra/environments/environment-suite.yaml)
and its pipeline loader. The contract now declares only values consumed by the
active deployment.

[`smoke-test.ts`](../scripts/smoke-test.ts) now resolves the resource group,
component app name, and health path from the suite while preserving explicit
overrides. Resolution is testable without contacting Azure.

### 6. Resolved: frontend Bicep action-group schema

The required action-group `properties.enabled` value was added in
[`frontend/main.bicep`](../infra/layers/frontend/main.bicep). The layer compiles
successfully; only the two known non-blocking type-provider warnings for
2025-05-01 Web resources remain.

## Deployment Evolution

### June 30 through July 17: establish the deployment system

The initial scaffold grew into layered Bicep, environment-specific naming,
Azure Deployment Environments experiments, custom image hooks, identity setup,
and data-plane reconciliation. Early development repeatedly exposed boundaries
between `azd`, ARM, and service-specific configuration.

### July 7 through July 22: turn deployment failures into upstream feedback

The first eleven `azure-dev` issues came directly from preview, provisioning,
agent, App Service, Function App, hook, and RBAC failures. Together they document
a shift from treating hooks as incidental helpers to recognizing that they had
become part of the deployment's desired-state system.

### July 24 through August 4: adopt fixes and remove obsolete workarounds

Upstream fixes preserved agent service fields and App Service container
configuration. Existing named-slot support was discovered, adopted, and then
made irrelevant when the agent server moved to a dedicated production App
Service. The branch removed obsolete agent/global-hook and ACR identity-repair
logic. Cognitive Services model deployments were serialized with
`@batchSize(1)`.

### August 20 through September 9: consolidate the operational model

The branch consolidated seven dependency-ordered infrastructure layers, removed
the ADE scaffolding and redundant pipelines, and added workload identity
federation, Search, Teams consent, evolution settings, configurable image tags,
and separate developer and deployment identities. A single component-aware
orchestrator became the deployment entry point.

Issue #9904 records the remaining architectural gap: the custom orchestrator is
still required because native `azd up <service>` cannot provision a service's
infrastructure dependency closure and deploy that service as one operation.

## Authored Azure Developer CLI Issues

The source query was
`repo:Azure/azure-dev is:issue author:qiaozha sort:created-asc`. It returned 12
issues: five closed and seven open.

| Issue and title | Created | State | Upstream and final-branch outcome |
| --- | --- | --- | --- |
| [#9011: Lack of clear error details](https://github.com/Azure/azure-dev/issues/9011) | Jul 7 | Closed | Fixed by [#9324](https://github.com/Azure/azure-dev/pull/9324) in 1.30. The project inherits the fix through its `azd >= 1.32` requirement. |
| [#9152: Agent host strips hooks and image templating](https://github.com/Azure/azure-dev/issues/9152) | Jul 15 | Closed | Fixed by [#9211](https://github.com/Azure/azure-dev/pull/9211) in 1.29. The final service-scoped agent hook is preserved. |
| [#9246: Named App Service slot deployment](https://github.com/Azure/azure-dev/issues/9246) | Jul 22 | Closed | Existing `AZD_DEPLOY_<SERVICE>_SLOT_NAME` support was confirmed and briefly adopted. The later dedicated-site topology no longer needs it. |
| [#9247: Agent environment variables are not persisted](https://github.com/Azure/azure-dev/issues/9247) | Jul 22 | Open | The postdeploy hook clones the latest Foundry version to inject App Configuration and now validates it against an independently resolved target image. |
| [#9248: Resolve image interpolation after predeploy](https://github.com/Azure/azure-dev/issues/9248) | Jul 22 | Open | Frontend and Function custom image hooks were removed in favor of native Docker services with a tag selected before azd starts. |
| [#9249: App Service container settings are overwritten](https://github.com/Azure/azure-dev/issues/9249) | Jul 22 | Closed | Fixed by [#9281](https://github.com/Azure/azure-dev/pull/9281) in 1.29. The old ACR identity-repair helper was removed. |
| [#9250: Container Function deployment hangs](https://github.com/Azure/azure-dev/issues/9250) | Jul 22 | Closed | Fixed by [#9284](https://github.com/Azure/azure-dev/pull/9284) in 1.30. The branch now adopts native `host: function` container deployment. |
| [#9251: Expose target service to global hooks](https://github.com/Azure/azure-dev/issues/9251) | Jul 22 | Open | Largely avoided through service-scoped hooks. The remaining global predeploy hook only enforces deployment policy. |
| [#9252: Preview omits hook effects](https://github.com/Azure/azure-dev/issues/9252) | Jul 22 | Open | Pipeline preview manually runs preprovision only. RBAC and postprovision data-plane changes remain absent. |
| [#9253: Idempotent role-assignment ensure semantics](https://github.com/Azure/azure-dev/issues/9253) | Jul 22 | Open | The known out-of-band Foundry grant retains tuple-aware ensure reconciliation. Frontend grants remain declarative in Bicep with principal-aware names and require a one-time legacy GUID migration in existing environments. |
| [#9398: Cognitive Services RequestConflict guidance](https://github.com/Azure/azure-dev/issues/9398) | Aug 3 | Open | Locally mitigated by serializing model deployment through `@batchSize(1)`. |
| [#9904: Support azd up for a service](https://github.com/Azure/azure-dev/issues/9904) | Sep 8 | Open | The pipeline implements an equivalent using targeted provision and deploy stages plus repeated environment refreshes. |

## End-to-End Experience

### Pipeline deployment

The active pipeline follows this sequence:

1. Load the selected environment from `environment-suite.yaml`.
2. Authenticate through the environment's workload identity service connection.
3. Validate the environment contract and compile relevant Bicep.
4. Refresh prior layer outputs and run `azd provision --preview`.
5. Present the preview at a manual approval gate.
6. Apply the seven dependency-ordered infrastructure layers.
7. Deploy agent server, Function App, chat agent, the production-only evolution
   agent, and frontend in dependency order.
8. Reconcile App Configuration, Key Vault, Search, Teams, Foundry RBAC and
   settings, and the Logic App workflow through hooks.

This is operationally stronger than the original fragmented process, but the
preview covers only the ARM/Bicep portion plus manually invoked preprovision
checks. It does not plan the substantial postprovision data-plane changes.

### Local deployment

Local deployment is intentionally supported only for development. Production is
pipeline-only, and local preview and production bootstrapping are disabled. A
developer can provision and deploy individual services, but must supply the
required identities, environment values, consent, and existing shared-resource
state.

Local and pipeline image deployment now share the native azd lifecycle. The
selected `AZD_IMAGE_TAG` must be present before azd loads the project; no service
hook computes a second image after package or publish.

### First-state and recovery boundaries

A brand-new preview or production environment cannot follow the normal layered
preview path because later layers require outputs from existing state. It needs
a separately approved bootstrap process.

Reprovisioning can also restore placeholder images or an empty Logic App shell.
Application deployment and reconciliation hooks must run afterward. Slot swaps,
blocking smoke tests, automated rollback, and health-threshold enforcement are
not currently part of the active path.

## Pull Request Review State

The pull request is open and non-draft. Several unresolved review threads target
files or paths that were deleted or superseded during the branch's evolution.
For example, the environment-suite indentation and drift-detection regex
comments no longer describe the current implementations.

The smoke-test naming concern was resolved by suite-based target lookup. The
Logic App workflow still has an open alignment request against
[Azure/azure-sdk-tools#16865](https://github.com/Azure/azure-sdk-tools/pull/16865)
and
[Azure/azure-sdk-tools#16911](https://github.com/Azure/azure-sdk-tools/pull/16911).

## Validation

### Maintainer Tooling Consolidation

The remaining PowerShell files were standalone operator utilities inherited
from the earlier deployment scaffold. Because they sat outside the TypeScript
hook package, they had accumulated separate argument conventions, YAML parsing,
process handling, and no direct unit-test surface.

Environment validation, local azd synchronization, endpoint smoke testing, and
layered drift detection now use TypeScript behind four npm commands. A typed
`yaml`-based suite module is shared with preprovision drift checks, so local
mapping logic has one owner. The unreferenced frontend source-copy PowerShell
helper was removed instead of ported.

A producer-consumer audit then removed dead aliases, unused Bicep outputs and
app settings, and unsupported suite metadata. Intentional pairs remain only
where external contracts differ, such as resource name versus data-plane
endpoint and desired `*_OVERRIDE` input versus refreshed layer output. The
audit also fixed Function storage's unproduced environment key and
agent-server's nonstandard Application Insights key.

Pipeline templates continue to use Bash and `yq` during their early setup phase;
installing package-local TypeScript dependencies there would add work without
removing a deployment behavior. The preview-operation parser also remains plain
JavaScript because those pipelines invoke it directly with Node. There are no
PowerShell files or PowerShell utility commands left under `tools/sdk-ai-bots`.

The following checks were run from `tools/sdk-ai-bots/deployment` on September
9, 2026:

- `npm run typecheck`: passed.
- `npm test`: 57 tests passed, 0 failed.
- Frontend build and tests: 86 tests passed.
- Function App build and tests: one focused test passed.
- All seven Bicep parameter entry points compiled.
- Isolated azd 1.32.0 project parsing and all-service packaging passed with the
   pinned agent extension.
- Frontend provider what-if passed with 48 Deploy/Ignore results, including all
   four Bicep role assignments, using `ProviderNoRbac`; standard preview was
   blocked only by the caller's missing delete-lock write permission.
- VS Code diagnostics for `azure.yaml`: no errors.
- Frontend Bicep compilation: passed with two unavailable Web type-provider
   warnings.
- Suite-based smoke target resolution passed for all three components.

The deployment package now exposes `test` and `typecheck` scripts. Docker was
unavailable, so no local image build ran. No live Azure deployment or resource
mutation was performed.

## Recommendation

The six implementation findings are resolved. The remaining preview,
first-state bootstrap, and active rollout-gate limitations should remain
explicit operational boundaries or follow-up work.