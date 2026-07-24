---
marp: true
theme: default
paginate: true
size: 16:9
title: The azd Journey — Deploying the Azure SDK Q&A Bot
---

<!-- _class: lead -->

# The `azd` Journey
## Deploying the Azure SDK Q&A Bot end-to-end

*From ad-hoc scripts → Infrastructure-as-Code + repeatable service delivery*

---

# What we are actually deploying

A multi-component **AI/chat** service:

| Component | Runtime | Purpose |
|---|---|---|
| **frontend** | App Service (Node) | Teams bot `/api/messages`, health, config |
| **backend** | App Service (Go) | RAG orchestration, Cosmos, Search |
| **agent-server** | App Service `agent` **slot** (Python FastAPI) | `/agent/chat`, `/ping` |
| **agent** | Foundry hosted agent (`azure.ai.agent`) | Model reasoning / responses |
| **function-app** | Function App (TS, container) | `AdoTokenRefresh` timer, hooks |
| **logic-app** | Standard Logic App | Teams event routing |
| **shared** | ACR, Key Vault, App Config, Storage, Cosmos, AI Search, AI Foundry, UAMI | Platform |

Plus: **Teams app registration + manifest** (via `teamsapp` CLI), Entra app registration for the server audience, Bot channel registration.

---

# The problem we set out to solve

Before `azd`:

- **Manual, fragile** deployments — a mix of `az` CLI scripts, portal clicks, and hand-edited settings per environment.
- **No single source of truth**: dev / preview / prod drifted; `.env` files, ARM templates, and pipeline YAML each held partial truth.
- **Secrets & RBAC** applied out-of-band — repeat provisions kept overwriting or duplicating role assignments.
- **No repeatable local dev loop** — engineers couldn’t stand up their own copy without tribal knowledge.
- **Container images** deployed ad-hoc; no traceability between an App Service and the tag it runs.
- **Cross-team handoffs** (Teams Toolkit, Foundry hosted agents, Logic Apps) each had their own tools — nothing tied them together.

---

# Why we chose `azd`

`azd` gives us:

1. **One command per intent**: `azd provision` (infra) / `azd deploy` (code) / `azd down` (teardown).
2. **First-class Bicep integration** — infra lives with the code, declaratively.
3. **Per-environment isolation** — `.azure/<env>/.env` holds outputs; `azure.yaml` binds services to modules.
4. **Extensible hooks** at every phase: pre/post provision, pre/post deploy — global and per-service.
5. **Teams / Foundry hosts already modeled** (`azure.ai.agent`, `appservice`, `function`, …).
6. **Same commands locally and in CI** — the pipeline just sets `AZURE_ENV_NAME` + `AZD_SKIP_IMAGE_BUILD=1`.

The goal: **make provisioning a solved problem, and make the interesting work be the app, not the plumbing.**

---

# Where we are today — high-level flow

```
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────────────┐
│ preprovision.ts  │──►│  main.bicep      │──►│ postprovision.ts         │
│  quotas, Entra   │   │  6 module layers │   │  outputs → env, RBAC,    │
│  drift, guards   │   │  (shared→agent→  │   │  KV seed, App Config,    │
│                  │   │   backend→…)     │   │  Teams env sync          │
└──────────────────┘   └──────────────────┘   └──────────────────────────┘
                              │
                              ▼
        ┌───────── azd deploy <service> ─────────┐
        │  predeploy (global) → service predeploy │
        │           ↓                              │
        │  azd core deploy (container / package)   │
        │           ↓                              │
        │  service postdeploy → postdeploy global  │
        │           ↓                              │
        │  patchWorkflow (Logic App live)          │
        └──────────────────────────────────────────┘
```

Every service converges on the **same global hooks**, so agent RBAC + Logic App update happen no matter which service you deployed.

---

# Challenge 1 — Bicep role assignments are **not** idempotent

**Symptom:** `azd provision` failed with `RoleAssignmentExists` because a matching (principal, role, scope) tuple already existed under a different name (from a lock-protected out-of-band grant).

**Why:** ARM allows only ONE role assignment per tuple, and Bicep can’t do *create-if-not-exists*.

**Resolution:**

- Removed the offending `roleAssignments` from Bicep.
- Added `hooks/lib/ensure-role-assignment.ts` — thin wrapper over `az role assignment create` that treats `RoleAssignmentExists` as success.
- Called from `postprovision.ts` (`ensureAgentAccountRoleAssignments`) and `agent-postdeploy.ts` (`ensureAgentIdentityRbac`).

**Lesson:** for any grant that may already exist out-of-band (especially in locked RGs), do it in a hook — not in Bicep.

---

# Challenge 2 — Hosted Foundry agent env vars don’t get injected

**Symptom:** hosted agent crashed on boot with `AZURE_APPCONFIG_ENDPOINT env var is required` → `424 session_not_ready` → `/agent/chat` returns 500.

**Root cause:**

- `azd` (`azure.ai.agents` beta.5) has a *“Registering agent environment variables”* step — it’s effectively a **no-op**: env from `agent.yaml` is not embedded into the deployed version’s definition.
- `azure.yaml` `${VAR}` interpolation is resolved at project-load — *before* predeploy — so we can’t point `image:` at the freshly-built immutable tag either.

**Resolution:** `hooks/agent-postdeploy.ts` → `ensureAgentAppConfigEnv()`

1. `GET .../agents/<name>/versions/@latest`.
2. If image ≠ `AGENT_DEPLOYED_IMAGE` **or** `environment_variables.AZURE_APPCONFIG_ENDPOINT` missing → `POST` a new version cloning definition, pinning image, embedding env.
3. Idempotent — no-op when already correct.

**Lesson:** `azd` cannot inject hosted-agent env vars; the only working path is the Foundry data-plane API.

---

# Challenge 3 — `azd` can’t deploy to a named App Service **slot**

**Symptom:** `azd deploy agent-server` failed at core step: *“unable to find a resource tagged with `azd-service-name: agent-server`”*.

**Why:** the agent server (`server.py`) runs in the `agent` **deployment slot** of the backend site. `azd` only models the production slot.

**Resolution:** `hooks/agent-server-predeploy.ts` **does the deploy**:

- `az acr build` → immutable tag `dev-N.0.0`.
- `az webapp config container set --slot agent`.
- Writes `AGENT_BASED_IMAGE_REPOSITORY` back to `.env` so the next `azd provision` re-pins the slot to that immutable tag (Bicep default was `:dev` which drifted).

The `azd deploy agent-server` core step is essentially a no-op we tolerate — the hook is where the work happens.

---

# Challenge 4 — Function App on `host: function` timed out at deploy

**Symptom:** `azd deploy function-app` hung at *“Uploading deployment package”* for 20 min, exited 1. Global `postdeploy` was skipped → Logic App workflow never updated.

**Root cause:** Function App is **container-based** (`linuxFxVersion=DOCKER|…`). `azure.yaml` had `host: function, language: ts` → `azd` did a code/zip package deploy → incompatible → hang.

**Resolution:**

- Changed `azure.yaml` → `host: appservice, language: docker, remoteBuild: true`.
- `function-predeploy.ts` builds via `az acr build`, sets `SERVICE_FUNCTION_APP_IMAGE_NAME`, and `az functionapp config container set` to repoint.
- Deploy now finishes in ~7 min; global `postdeploy` (Logic App update) runs.

---

# Challenge 5 — Teams OAuth connection re-authorized every provision

**Symptom:** Bicep re-`PUT` the Teams `Microsoft.Web/connections` resource unconditionally → an already-authorized (“Connected”) OAuth connection can lose its bound token.

**Resolution — conditional-create + preprovision probe:**

- Added `createTeamsConnection` param to `qaBotLogicApp/logicAppResources.bicep`; wrapped `teamsConnection` in `if (createTeamsConnection)`.
- Workflow `$connections` block computes the connection resourceId manually (symbolic ref would fail when the condition is false).
- `preprovision.ts` → `ensureTeamsConnectionFlag()` probes the live resource: `status == "Connected"` → set `CREATE_TEAMS_CONNECTION=false`; else `true`.

**Lesson:** treat OAuth-consented resources as **owned by the tenant, not by IaC** — only create the shell once.

---

# Challenge 6 — Teams `.env` clobbered by base-env sync

**Symptom:** every `azd provision` copied `TEAMS_APP_ID` from committed `.env.<baseEnv>` into `.env.azd` → wrong Teams app for the azd env.

**Why:** each Teams Toolkit env has its **own** Teams app registration — inheriting from a base env silently repoints to a foreign app.

**Resolution — env-suite is the source of truth:**

- Added `teamsAppId` / `teamsAppTenantId` **per environment** to `deployment/infra/environments/environment-suite.yaml`.
- New `hooks/lib/env-suite.ts` → `getEnvSuiteValue(envName, key)`.
- `sync-teams-env.ts` **never** preserves those keys from the existing `.env.azd`; strips them from any seed file.
- Placeholder / unknown → keys omitted → `teamsapp provision --env azd` mints a fresh registration on first run.

---

# Challenge 7 — `azd deploy` wipes App Service settings

**Symptom:** `azd deploy backend` overwrote the site container config → `acrUserManagedIdentityID` cleared → platform fell back to a nonexistent system-assigned identity → 503 on image pull.

**Resolution:** `backend-postdeploy.ts` → `repinAcrPullIdentity()`

- Re-applies `acrUseManagedIdentityCreds=true`, `acrUserManagedIdentityID=<MI clientId>`.
- Restarts the site.
- Idempotent; runs on every `azd deploy backend`.

**Meta-lesson:** any App Service setting `azd` doesn’t know about must be **re-applied in a postdeploy hook**, because `azd deploy` re-serializes the container config.

---

# Challenge 8 — Logic App references a Function that doesn’t exist yet

**Symptom:** first-ever provision failed — Logic App workflow validation calls the Function App on create; the Function App container image isn’t pushed yet.

**Resolution:** two-phase workflow:

1. `main.bicep` creates the workflow **shell** with an empty definition.
2. Global `postdeploy.ts` → `lib/patch-workflow.ts`:
   - `GET` the workflow.
   - Mutate `properties.definition` / `properties.parameters` from `workflowDefinition.json`.
   - `PUT` it back (Logic Apps don’t accept `PATCH` on `properties`).
   - Gated on Function App `/admin/host/status = 200`.
- Fires after **any** service deploy (skip with `POSTDEPLOY_SKIP_LOGIC_APP=1`).

---

# The current workflow, in one picture

```
    azd provision                                    azd deploy <service>
    ─────────────                                    ──────────────────────
 preprovision.ts (global)                         predeploy.ts (global)
   • env-suite validation                            • prod guardrail
   • quota check                                     • if agent: agent-predeploy
   • Entra app for SERVER_AUDIENCE                       az acr build
   • developer principal                          <service>-predeploy.ts
   • Teams-connection probe                            • az acr build → dev-N.0.0
           │                                           • repoint slot / app
           ▼                                           • azd env set immutable tag
 main.bicep (6 modules)                                     │
   sharedResources → qaBotAgent →                           ▼
   qaBotBackend → qaBotFrontend →                    azd core deploy
   qaBotFunctionApp → qaBotLogicApp                         │
           │                                                ▼
           ▼                                        <service>-postdeploy.ts
 postprovision.ts (global)                            • re-pin ACR identity
   • persistBicepOutputs → azd env                    • re-seed KV secrets
   • ensureRole (Foundry User)                        • Teams provision (frontend)
   • uploadBotConfigs → storage                       • health check
   • seedKeyVault / seedAppConfig                            │
   • syncTeamsEnv → env/.env.azd                             ▼
                                                    postdeploy.ts (global)
                                                       • agent-postdeploy
                                                          (RBAC + new Foundry ver)
                                                       • patchWorkflow (Logic App)
```

---

# What this journey cost us

Every one of those 8 challenges was **one workaround per gap** in the platform:

- 3× *because Bicep / ARM couldn’t express the intent* — idempotent grants, conditional OAuth resources, deferred workflow definition.
- 3× *because `azd` can’t reach a resource type* — named App Service slots, hosted-agent env vars, container Function App on `host: function`.
- 1× *because `azd deploy` re-serializes what it doesn’t own* — App Service container config (ACR pull identity wiped).
- 1× *because tools don’t share an env schema* — Teams Toolkit `.env` vs. azd env.

**≈ 1000 lines of TypeScript hooks** are dedicated to bridging those gaps. That is a lot of glue — glue we control, in one place — but it is still glue.

---

# Why hooks break the workflow's integrity

The declarative promise of `azd`:

```
azd provision   →   all infra correct, as described in Bicep
azd deploy      →   services running the right image, done
```

The actual flow — every ⚡ is a hook we own:

```
  preprovision ⚡  →  main.bicep  →  postprovision ⚡
                                        │
                          fixes what Bicep couldn't express:
                          RBAC, KV seed, App Config, Teams env sync,
                          re-reads live Azure state via `az` CLI

  predeploy ⚡  →  <svc>-predeploy ⚡  →  azd core deploy  →  <svc>-postdeploy ⚡  →  postdeploy ⚡
                                                                                        │
                                                                       re-applies things azd
                                                                       just wiped, PUTs the
                                                                       Logic App workflow, etc.
```

Each ⚡ is a hidden concern:

- **Imperative** — reason about order, retries, partial failure by hand.
- **Non-idempotent by default** — every hook has to guard itself.
- **No plan / no diff / no dry-run** — the hook mutates live Azure with `az` CLI.
- **Language sprawl** — TypeScript hooks shell out to Bash-quoted `az` invocations that mutate the same resources Bicep declared.
- **Silent contract** — nothing in `azure.yaml` tells a reader that `azd deploy frontend` actually runs 4 hook files.
- **Un-typed** — `process.env.AGENT_BASED_IMAGE_REPOSITORY` is a string; a typo becomes a silent break at run time.
- **Cross-cutting failure modes** — a hook throw fails the whole `azd deploy`, even when the container image already deployed successfully (we hit this repeatedly — see the Function App timeout / Teams provision cases).

> Every hook is a place where `azd`’s two-command story stops being true.

The more hooks we add, the less `azd provision` and `azd deploy` mean what they say. That is the real cost.

---

# What we plan to improve

## Short term
- Move remaining `az` CLI calls in hooks behind small, tested helper libs (some already: `ensure-role-assignment`, `ensure-entra-app`, `env-suite`, `acr-tags`).
- Bring the currently-skipped layer pipeline (`DEPLOY_LAYER=<name>`) back for targeted incremental provisions.
- Cover hooks with unit + integration tests (fake `az` / Foundry via nock).
- Kill remaining `value: ''` App Service settings; require explicit params or omit.

## Medium term
- **Move infra from Bicep to TypeScript** using [`js-provisioning-lib`](https://github.com/Azure/js-provisioning-lib) — same declarative model as Bicep, but authored in the same language as the hooks, so types flow end-to-end. `azd` still consumes the emitted Bicep as usual.
- Split `main.bicep` explicitly into provision **layers** with parallel deploy in `postprovision` (the layer pipeline exists but is dormant).
- Publish a `deployment` npm package so hooks are shared code, not per-repo copies.

---

# Where we want to work more closely with `azd`

Concrete feature asks / RFCs to open with the `azd` team:

1. **Native slot deploys** for `host: appservice` — model the `agent` slot; kill `agent-server-predeploy.ts`.
2. **Hosted agent env injection** (`azure.ai.agent`) — actually persist `agent.yaml environment_variables` into the deployed version (would kill `ensureAgentAppConfigEnv`).
3. **Per-service hooks not stripped by `azure.ai.agent`** — GitHub issue [azure-dev#9152](https://github.com/Azure/azure-dev/issues/9152) is the tracker; today we route agent hooks through the global `pre/postdeploy`.
4. **Late-resolved `${VAR}` in `azure.yaml`** for `image:` — resolve *after* predeploy so we can point at the freshly built immutable tag.
5. **Preserve unknown App Service settings** on `azd deploy` (or expose a “merge, don’t replace” option) — would kill `repinAcrPullIdentity`.
6. **Conditional / idempotent RBAC** — first-class “ensure” semantics for role assignments; today Bicep has none.
7. **Standard `--service <name>` env var** everywhere so the global `predeploy` can branch without our `AZD_DEPLOY_SERVICE` convention.

---

# How IaC — done properly — should resolve most of this

Every hook we own is a **temporary bridge**. The end state is:

| Today (hook) | Tomorrow (IaC + `azd`) |
|---|---|
| `ensure-role-assignment.ts` | Typed `ensure*` primitives in the provisioning lib — declarative RBAC with idempotency built in |
| `agent-postdeploy` → new Foundry version w/ env | `azd`’s `azure.ai.agent` host injects `agent.yaml environment_variables` natively |
| `agent-server-predeploy` (slot deploy) | `azd` first-class named-slot support in `appservice` host |
| `repinAcrPullIdentity` | `azd` preserves site container config on deploy |
| `patch-workflow` (Logic App PUT) | Deferred / two-phase resources are a first-class construct in the provisioning lib |
| `sync-teams-env` (rewrite `.env.azd`) | `azd` × Teams Toolkit share an env schema; no cross-tool env copying |
| `preprovision.ensureEntraApp` | Microsoft Graph provider for Entra apps, called from the provisioning lib |

**Rule of thumb:** if a hook is doing something IaC *should* be able to express, that hook is a bug report against the platform.

---

# IaC via [`js-provisioning-lib`](https://github.com/Azure/js-provisioning-lib) — infra as *TypeScript*, compiled to Bicep, consumed by `azd`

Instead of authoring Bicep (a separate DSL) plus hooks (TypeScript), we author *both* in TypeScript. `js-provisioning-lib` compiles our stacks to `.bicep`; `azd` reads those files exactly as it does today.

```ts
// deployment/infra/main.ts
import { Stack, ResourceGroup, fn, az } from "js-provisioning-lib/core";
import { UserAssignedIdentity } from "js-provisioning-lib/managedidentity";
import { Vault as KeyVault } from "js-provisioning-lib/keyvault";
import { StorageAccount } from "js-provisioning-lib/storage";
import { RoleAssignment } from "js-provisioning-lib/authorization";
import { ConfigurationStore } from "js-provisioning-lib/appconfiguration";
import { Registry as ContainerRegistry } from "js-provisioning-lib/container-registry";
import { serialize } from "js-provisioning-lib/serialization";
import { renderBicepFiles } from "js-provisioning-lib/bicep";

const stack = new Stack("qabot", { targetScope: "subscription" });
const rg    = new ResourceGroup(stack, { location: "eastus" });

const identity = new UserAssignedIdentity(rg, { name: "qabot-identity" });
const acr      = new ContainerRegistry(rg, { name: "qabotacr", sku: { name: "Basic" } });
const kv       = new KeyVault(rg, { properties: { tenantId: fn.subscription().tenantId, sku: { name: "standard", family: "A" } } });
const storage  = new StorageAccount(rg, { sku: { name: "Standard_LRS" }, kind: "StorageV2" });
const appCfg   = new ConfigurationStore(rg, { sku: { name: "standard" } });

// RBAC — typed constants, no more az CLI strings
new RoleAssignment(kv, { properties: {
  principalId: identity.properties.principalId,
  principalType: "ServicePrincipal",
  roleDefinitionId: KV_SECRETS_OFFICER_ROLE_ID,   // exported from a constants module
}});

// Outputs — typed, no `az deployment sub show | jq` needed
stack.outputs.add("MANAGED_IDENTITY_CLIENT_ID", "string", identity.properties.clientId);
stack.outputs.add("AZURE_APPCONFIG_ENDPOINT",   "string", appCfg.properties.endpoint);

// azd reads the emitted .bicep files (azure.yaml → infra: { provider: bicep }).
for (const f of renderBicepFiles(serialize(stack))) writeFileSync(f.path, f.contents);
```

What this changes:

- **One language, one repo, one type system** — env-suite values, resource IDs, RBAC targets are all typed handles; typos become compile errors, not runtime 503s.
- **Composable & testable** — the stack is a plain value. `serialize(stack)` is a pure function; snapshot-test in CI.
- **Reusable `Component`s** — `class QaBotAgentPlatform extends Component { ... }` collapses today’s `qaBotAgent/component.bicep` + `postprovision` grants + KV seeding into one class per concern.
- **`azd` consumes the emitted Bicep** — no change to `azure.yaml infra: { provider: bicep }`; the library just replaces the *authoring* surface.
- **Hooks shrink or disappear** — KV seeding, App Config seeding, RBAC ensuring, output plumbing all become declarative constructs living beside the resources.

> **`js-provisioning-lib` turns hooks back into infrastructure.**

---

# What that unlocks on the `azd` side

- **`azd` extensions API** — turn what remains of our hooks into reusable, versioned extensions rather than repo-local scripts.
- **`azd` composability** — each service (frontend, backend, agent, function-app) becomes its own composable unit; a new environment is `azd env new` + a config file, not a 400-line runbook.
- **First-class hosts** for the resource types we struggle with today: named App Service slots, Logic App workflows, hosted Foundry agents with env injection.
- **Bicep provider ecosystem** feeds into `js-provisioning-lib` — Microsoft Graph for Entra apps, Foundry for agent versions — each new provider becomes a new resource package in the library.

**The moment each of those lands, we delete a hook.**

---

# The stretch goal

> A brand new developer clones the repo, runs three commands, and has a working Q&A bot in their own subscription — with no manual steps, no portal clicks, no tribal knowledge, and no `az` CLI invocations outside of what `azd` does for us.

```bash
azd auth login
azd env new my-dev
azd up          # provision + deploy in one go
```

Every hook we keep is measured against that goal.

---

<!-- _class: lead -->

# Recap

- We picked `azd` because it unifies **provision + deploy + env** in one CLI.
- The journey exposed real gaps in Bicep, ARM, and `azd`’s host coverage.
- We resolved every gap in **one place** — the `deployment/hooks/` layer — instead of scattering fixes across pipelines and READMEs.
- The current workflow is **idempotent, environment-scoped, and repeatable**.
- Our roadmap is to **shrink the glue** by working with the `azd` and Bicep teams, adopting `js-provisioning-lib`, and turning our hooks into extensions.

**IaC + `azd` is not a silver bullet — it’s the smallest surface area we know of where the remaining gaps are all *someone else’s bug*.**

---

<!-- _class: lead -->

# Questions / Discussion

Repo: `Azure/azure-sdk-tools` → `tools/sdk-ai-bots/deployment/`

- Hooks: `deployment/hooks/`
- Bicep: `deployment/infra/modules/`
- Env contract: `deployment/infra/environments/environment-suite.yaml`
- Runbook: `deployment/docs/runbook-deploy.md`
