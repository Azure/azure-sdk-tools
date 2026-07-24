---
marp: true
theme: default
paginate: true
size: 16:9
title: Deploying the Azure SDK Q&A Bot — a reflection on azd
---

<!-- _class: lead -->

# Deploying the Azure SDK Q&A Bot

## What `azd` gave us, where it fought us, and what the _ideal_ experience looks like

_A reflection on the developer's mental model of "deploy a service"_

---

# 1 — Chatbot architecture

A multi-component **AI/chat** service — several runtimes, one product:

| Component        | Runtime                                                                  | Purpose                                        |
| ---------------- | ------------------------------------------------------------------------ | ---------------------------------------------- |
| **frontend**     | App Service (Node)                                                       | Teams bot `/api/messages`, health, config      |
| **agent-server** | App Service `agent` **slot** (Python FastAPI)                            | `/agent/chat`, `/ping`, RAG orchestration, Cosmos, Search |
| **agent**        | Foundry hosted agent (`azure.ai.agent`)                                  | Model reasoning / responses                    |
| **function-app** | Function App (TS, container)                                             | `AdoTokenRefresh` timer, hooks                 |
| **logic-app**    | Standard Logic App                                                       | Teams event routing                            |
| **shared**       | ACR, Key Vault, App Config, Storage, Cosmos, AI Search, AI Foundry, UAMI | Platform                                       |

Plus **Teams app registration + manifest** (`teamsapp` CLI), an **Entra app registration** for the server audience, and a **Bot channel registration**.

---

# 1 — Chatbot architecture (data flow)

```
        Teams client
            │  /api/messages
            ▼
     ┌─────────────┐                          ┌────────────────────┐
     │  frontend   │────────────────────────► │  agent-server slot │
     │ (Node,      │          /agent/chat     │ (Python FastAPI,   │
     │  Teams bot) │                          │  RAG orchestr.)    │
     └─────────────┘                          └─────────┬──────────┘
            ▲                              ┌────────────┼────────────┐
            │                       Cosmos / AI Search  │   Foundry hosted agent
            │                                           │    (model reasoning)
     ┌──────┴───────┐                          ┌────────┴─────────────────┐
     │  logic-app   │◄──── function-app ──────►│  App Config / Key Vault  │
     │ (Teams route)│      (timer, hooks)      │  ACR / Storage / UAMI    │
     └──────────────┘                          └──────────────────────────┘
```

Five deployable services + a shared platform layer — **all of which must be provisioned, wired, and secured together.**

---

# 2 — The deployment problem

Before `azd`, standing this up meant:

- **Manual, fragile** deployments — a mix of `az` CLI scripts, portal clicks, and hand-edited settings per environment.
- **No single source of truth** — dev / preview / prod drifted; `.env` files, ARM templates, and pipeline YAML each held partial truth.
- **Secrets & RBAC** applied out-of-band — repeat provisions kept overwriting or duplicating role assignments.
- **No repeatable local dev loop** — engineers couldn't stand up their own copy without tribal knowledge.
- **Container images** deployed ad-hoc — no traceability between an App Service and the tag it runs.
- **Cross-team handoffs** (Teams Toolkit, Foundry hosted agents, Logic Apps) each had their own tool — nothing tied them together.

> The goal: **make provisioning a solved problem, so the interesting work is the app — not the plumbing.**

---

# 3 — The `azd` current workflow

`azd` gave us **one command per intent** and Bicep-as-infra:

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
           ▼                                                   │
 main.bicep (5 modules)                                        ▼
   sharedResources → qaBotAgent →                       azd core deploy
   qaBotFrontend → qaBotFunctionApp →                          │
   qaBotLogicApp                                               ▼
           │                                          <service>-postdeploy.ts
           ▼                                            • re-pin ACR identity
 postprovision.ts (global)                              • re-seed KV / health
   • outputs → env, RBAC, KV seed,                           │
     App Config, Teams env sync                              ▼
                                                     postdeploy.ts (global)
                                                       • agent RBAC + Foundry ver
                                                       • patchWorkflow (Logic App)
```

Same commands locally and in CI — the pipeline just sets `AZURE_ENV_NAME` + `AZD_SKIP_IMAGE_BUILD=1`.

---

# 3 — Our mental flow: fitting a service into `azd`

Every time we onboard a piece of the product, we run the **same three-step thought process**:

```
 ① ENUMERATE resources        ② MAP deployable components     ③ INJECT hooks to align
    (→ one provision pass)        (→ azd host types)              (→ data flow ↔ azd phases)
 ─────────────────────────    ──────────────────────────     ─────────────────────────────
 ACR, Key Vault, App Config   frontend    → host: appservice   image built AFTER provision?
 Storage, Cosmos, AI Search   agent-server→ host: appservice     → predeploy: az acr build
 AI Foundry, UAMI             agent       → azure.ai.agent      env needed BEFORE agent boot?
 App Services + slots         function-app→ host: appservice      → postdeploy: seed App Config
 Function App, Logic App      logic-app   → (no host — hook)    RBAC needed BEFORE app runs?
 Entra app, Bot, Teams conn   knowledge-  → (no host — pipeline)  → postprovision: ensure role
                              sync                              workflow refs a live Function?
     ⇓ extract into Bicep         ⇓ tag azd-service-name          → postdeploy: patch workflow
   azd provision (all at once)  azd deploy <service>            (pre/post × provision/deploy)
```

**Step ①** — list *every* Azure resource the service touches, extract them into Bicep modules so **`azd provision` stands them all up in one pass**.
**Step ②** — decide which runnable components map to an `azd` **host type**; tag them `azd-service-name` so **`azd deploy` pushes code/images**.
**Step ③** — wherever a component's real **data flow** (image → endpoint → workflow, env → boot, RBAC → run) doesn't line up with `azd`'s fixed **provision → deploy** order, **inject a hook** to bridge the gap.

> The workflow we saw on the previous slide is the *output* of this thinking. The **hooks in step ③ are exactly where the service's data flow refuses to fit `azd`'s two phases.**

---

# 4 — Challenges we met

Eight gaps, each closed by **one workaround**:

| # | Challenge | Why it happened | Where we fixed it |
| - | --------- | --------------- | ----------------- |
| 1 | Role assignments **not idempotent** | ARM allows one tuple; Bicep can't "create-if-not-exists" | `ensure-role-assignment.ts` |
| 2 | Hosted agent **env vars not injected** | `azure.ai.agents` step is a no-op | `agent-postdeploy` → Foundry data-plane API |
| 3 | Can't deploy to a **named slot** | `azd` only models the prod slot | `agent-server-predeploy.ts` does the deploy |
| 4 | Container **Function App** deploy hangs | `host: function` did a zip deploy | switched to `host: appservice, docker` |
| 5 | Teams **OAuth connection** re-authorized | Bicep re-`PUT`s the connection each run | conditional create + preprovision probe |
| 6 | Teams **`.env` clobbered** | base-env sync overwrites per-env Teams app | env-suite is the source of truth |
| 7 | `azd deploy` **wipes App Service settings** | deploy re-serializes container config | `repinAcrPullIdentity()` postdeploy |
| 8 | Logic App references a **not-yet-existing Function** | workflow validated on create | two-phase shell + `patchWorkflow` |

> **≈ 1000 lines of TypeScript hooks** exist purely to bridge these gaps.

---

# 4 — Challenges we met (the shape of them)

The eight challenges cluster into **four root causes**:

- **3× Bicep / ARM can't express the intent** — idempotent grants, conditional OAuth resources, deferred workflow definition.
- **3× `azd` can't reach a resource type** — named App Service slots, hosted-agent env vars, container Function App on `host: function`.
- **1× `azd deploy` re-serializes what it doesn't own** — App Service container config (ACR pull identity wiped).
- **1× tools don't share an env schema** — Teams Toolkit `.env` vs. `azd` env.

Plus **service-platform constraints** no tool can paper over:

- Globally-unique resource names (soft-delete collisions) → probe-and-increment in `preprovision`.
- Quota is invisible until you hit a wall → pre-flight `az cognitiveservices` / `az ml quota` checks.
- No standard management API shape → **five** different health-poll implementations for five services.

---

# 4 — Deep dive: the function-app ↔ logic-app **two-phase problem**

A **circular, cross-phase dependency** that no single `azd` phase can satisfy:

```
   logic-app workflow  ──needs──►  function-app endpoint  ──needs──►  container image
   (validated on PUT)             (/admin/host/status=200)          (built at DEPLOY time)
        ▲                                                                    │
        └──────────────── but the workflow is declared at PROVISION ────────┘
```

- **Provision time** (`main.bicep`): the Logic App workflow is validated the moment it's `PUT`.
  If its definition references the Function App, ARM calls that Function — which **doesn't exist yet**
  (its container image is built and pushed only during `azd deploy`). → first-ever provision **fails**.
- **Deploy time** (`azd deploy function-app`): the image is finally built and pushed — but by now the
  Logic App has already been declared with an **empty/stale** definition.

**Today's workaround — split it across two phases, glued by a global hook:**

| Phase | Where | What it does |
| ----- | ----- | ------------ |
| 1. shell | `main.bicep` | create the workflow with an **empty** definition (passes validation) |
| 2. patch | global `postdeploy.ts` → `patch-workflow.ts` | `GET` workflow → merge real `workflowDefinition.json` → `PUT` back — **gated on** Function App `/admin/host/status = 200` |

> The workflow's _real_ definition lives in a **hook that fires from a global phase**, disconnected from
> the `logic-app` it belongs to — and there is no `logic-app` service in `azure.yaml` at all.

---

# 4 — The two-phase problem, the way we _want_ to write it

The fix isn't more hooks — it's letting **the service that owns the workflow own its whole lifecycle**, so the "patch" is just the next idempotent line, not a global-phase hook:

```ts
functionApp() {                 // build + push the image FIRST
  prepareEnvVars();
  functionAppProvision();
  deployFunctionApp();          // az acr build → container set → endpoint is live
  persistFunctionEndpoints();   // SERVICE_FUNCTION_APP_NAME available downstream
}

logicApp() {                    // runs AFTER functionApp() — dependency is the await order
  prepareEnvVars();
  logicAppProvision();          // provision + apply the REAL workflow definition in ONE
  applyLogicAppWorkflow();      //   idempotent step — no empty shell, no global postdeploy hook
  persistLogicAppEndpoints();
}

await functionApp();            // image exists → endpoint responds
await logicApp();               // workflow validates against a Function that is already up
```

- **The circular dependency becomes a linear one** — `await functionApp()` before `await logicApp()`.
- **No empty shell + later patch** — the workflow is declared once, correctly, because its dependency is already live.
- **The patch stops being a disconnected global hook** — it's folded into the service that owns it.

---

# 5 — Pros / Cons of the current workflow

### ✅ Pros

- **One CLI unifies provision + deploy + env** — `azd up` / `azd provision` / `azd deploy` / `azd down`.
- **Infra lives with the code** — Bicep is versioned, reviewed, diffable.
- **Per-environment isolation** — `.azure/<env>/.env` holds outputs; no more drift between dev/preview/prod.
- **Idempotent & repeatable** — a second `azd provision` converges instead of duplicating.
- **Same commands locally and in CI** — no separate deployment script to maintain.
- **All fixes in one place** — the `deployment/hooks/` layer, not scattered across pipelines and READMEs.

### ❌ Cons

- **~1000 lines of glue** — imperative, un-typed, order-dependent hooks.
- **Hooks don't show up in `--preview`** — the dry-run never reflects reality.
- **Silent contract** — nothing in `azure.yaml` says `azd deploy frontend` runs 4 hook files.
- **Provision/deploy boundary is blurred** — deploy hooks mutate infrastructure.
- **A hook throw fails the whole command** — even when the image already deployed fine.

---

# 6 — Thinking back from the journey

Every hook we wrote was us saying: _"`azd`, that's not quite what I meant."_

Step back and ask: **what is the developer's mental model when they say "deploy a service"?**

> "Here is my **service** — its parts, how they connect, what they need. Make the cloud look like _this_. If it already looks like this, do nothing."

That model has three properties:

1. **The service is the noun.** I think in terms of _frontend talks to agent-server talks to agent_, not _host types, hook phases, and env-var glue_.
2. **Desired state, not steps.** I describe the _end state_ once; the tool figures out the _path_ (create, update, skip).
3. **One artifact I can read.** The architecture should be legible in one place — not reconstructed by tracing which hook mutates which resource in which order.

We spent the journey **modeling `azd`** — host types, hook order, `.env` glue — instead of **modeling the service.**

---

# 6 — A "service" is more than an `azd` host type

`azd` thinks a service is one of a fixed set of **host types**: `appservice`, `function`, `containerapp`, `staticwebapp`, `aks`, `azure.ai.agent`. But our _product_ contains parts that are real, first-class services — yet map to **none** of them.

### Example: the **knowledge-sync** task

The Q&A bot is only useful because its **AI Search index is kept fresh**. That freshness is a service:

```
   repos (docs, TypeSpec spector cases)
            │  clone + process markdown
            ▼
   knowledge-sync (TS batch job)  ──►  Blob Storage  ──►  Azure AI Search index
            ▲                                                      │
     scheduled daily (ADO pipeline cron)              the agent's search_knowledge_base
                                                       tool reads THIS every request
```

- It's **not** a long-running host — it's a **scheduled batch job** (`sync_knowledge.yml`, daily).
- It has **no `azd-service-name` tag**, no entry in `azure.yaml` — `azd` has no host that fits "cron job that seeds a search index."
- Yet it is **load-bearing**: without it the agent answers from stale/empty knowledge. It shares the _same_ ACR, App Config, Storage, and AI Search as the deployed services.

> A "service" is the **whole unit of value** — long-running apps **and** the scheduled/data-plane
> tasks that keep them correct. `azd`'s host-type list is a subset of what the developer means by "my service."

---

# 6 — Why that gap matters

Because `knowledge-sync` doesn't fit an `azd` host type, it lives **outside** the deployment model entirely:

| Aspect | Deployed services (`azure.yaml`) | knowledge-sync task |
| ------ | -------------------------------- | ------------------- |
| Declared in | `azure.yaml` + Bicep | a **separate** ADO pipeline YAML |
| Provisioned by | `azd provision` | not `azd` at all |
| Shares infra (ACR, App Config, Search) | yes | yes — but wired **by hand** |
| Environment config | `.azure/<env>/.env` | duplicated `AZURE_APPCONFIG_ENDPOINT` |
| Visible in one place | partially | **no** — invisible to `azd up` |

The developer's mental model says _"the knowledge-sync is part of my bot."_
The tool's model says _"that's not a host type I know, so it isn't part of the deployment."_

> Every part of the product that `azd` has no host for becomes **another artifact in another
> place** — the opposite of "one service, one place." The service is bigger than the tool's vocabulary.

---

# 7 — The deeper reason it isn't intuitive

The friction isn't one missing feature. **The mental model `azd` forces on us doesn't match the thing we're building.**

### The pattern doesn't align with the developer's mental model

- We describe **how `azd` should orchestrate** — which host, which hook fires when, which env var carries state — instead of **what the service _is_.**
- The service architecture becomes an **emergent property** of config + hooks, not something you can read in one place.
- `azd` splits the world into **provision** (infra) and **deploy** (code) — but real services don't cleave there. Pinning an image, wiring an ACR-pull identity, seeding config a running app needs — infra or code? The boundary is arbitrary, so we mutate resources in _deploy_ hooks to stitch back together what the tool insisted on separating.

---

# 7 — The deeper reason it isn't intuitive (hooks)

### Hooks scattered everywhere feel disconnected from the deployment

- **Order is convention, not declaration** — `preprovision → main.bicep → postprovision`, `predeploy → <svc>-predeploy → core → <svc>-postdeploy → postdeploy`. Nothing in `azure.yaml` states this; you learn it by reading every hook.
- **Hooks don't run in preview** — `azd provision --preview` skips them entirely, so the preview never reflects reality (RBAC, KV seeding, env injection, Logic App patch are all invisible).
- **State passes through `.env` strings** — `AGENT_DEPLOYED_IMAGE`, `CREATE_TEAMS_CONNECTION`, `AGENT_BASED_IMAGE_REPOSITORY`. Untyped, order-dependent, silently broken by a typo.
- **"Single source of truth" is maintained by hooks** — the _real_ desired state is Bicep **+ ~1000 lines that re-read and re-mutate live Azure**. A truth reconstructed imperatively on every run isn't a source of truth — it's a recipe.

> The tool splits the problem where _it_ is easy to build, not where _our service_ naturally divides.

---

# 8 — The ideal developer experience

> **One service = one function that owns its whole lifecycle**, authored in the same imperative language as everything else. The developer writes the _workflow_; `azd` runs it.

```ts
// one service = one function that owns its lifecycle
functionApp() {                 // TS Function App (container)
  prepareEnvVars();             // process.env — CONTAINER_REGISTRY_NAME, FUNCTION_APP_NAME
  functionAppProvision();       // define provision details via provisioning libraries
  deployFunctionApp();          // az acr build → az functionapp config container set
  persistFunctionEndpoints();   // SERVICE_FUNCTION_APP_IMAGE_NAME, SERVICE_FUNCTION_APP_NAME
}

logicApp() {                    // Standard Logic App — NO `logic-app` service in azure.yaml today
  prepareEnvVars();             // process.env — TEAMS_CONNECTION_NAME, CREATE_TEAMS_CONNECTION
  logicAppProvision();          // define provision details via provisioning libraries
  applyLogicAppWorkflow();      // was a global-postdeploy hook (patchWorkflow) — now folded
                                // into logicAppProvision() as ONE idempotent operation
  persistLogicAppEndpoints();   // LOGIC_APP_NAME, TEAMS_CONNECTION_NAME
}
```

The `logicApp ↔ functionApp` two-phase problem disappears: the workflow patch is no longer a
disconnected hook in a global phase — it's just the next line **inside the service that owns it.**

---

# 8 — The ideal experience (one ordered workflow)

The whole service is **one readable workflow** — dependencies expressed as plain `await` order, not an implicit hook sequence:

```ts
// deploy the whole service, in order
await shared();       // platform: ACR, Key Vault, App Config
await agent();        // hosted Foundry agent
await agentServer();  // FastAPI on the agent slot
await functionApp();  // TS Function App
await logicApp();     // Teams workflow
await frontend();     // Teams bot
```

The developer then deploys with `azd` — provision, deploy, hooks and identities all under **one command per service**:

```bash
azd auth login
azd env new my-dev
azd up <service>   # provision + deploy + hooks + identities
```

Why this matches the mental model:

- **The service is the noun.** Each function _is_ a component; the file reads top-to-bottom as the architecture.
- **Ordering is declared, not memorized.** `await shared() → … → frontend()` _is_ the dependency graph.
- **No phase glue.** `prepare → provision → deploy → persist` lives together, per service — not split across `pre/postprovision` + `pre/postdeploy` hooks joined by `.env` strings.
- **Idempotent by construction.** `applyLogicAppWorkflow()` folds into `logicAppProvision()` — one converging operation, not a separate global hook.

---

# 8 — The ideal experience (turn hooks back into infrastructure)

Every hook we own is a **temporary bridge**. The end state:

| Today (hook)                                    | Ideal (declarative, typed)                                                    |
| ----------------------------------------------- | ------------------------------------------------------------------------------ |
| `ensure-role-assignment.ts`                     | Typed `ensure*` RBAC primitive with idempotency built in                       |
| `agent-postdeploy` → new Foundry version w/ env | Host injects `agent.yaml environment_variables` natively                        |
| `agent-server-predeploy` (slot deploy)          | First-class named-slot support                                                 |
| `repinAcrPullIdentity`                          | Deploy preserves site container config                                         |
| `patch-workflow` (Logic App PUT)                | Deferred / two-phase resource is a first-class construct                       |
| `sync-teams-env` (rewrite `.env.azd`)           | Shared env schema across tools; no cross-tool copying                          |
| `preprovision.ensureEntraApp`                   | Microsoft Graph provider for Entra apps                                        |

> **Rule of thumb:** if a hook does something IaC _should_ express, that hook is a **bug report against the platform.**

---

# 9 — What this means for `azd` to support

Two capabilities would collapse most of our glue.

## A. Multi-stage provision / deployment — the way Aspire does it

Aspire models a distributed app as a **single app model** in code (`AppHost`): you declare each resource (`AddProject`, `AddContainer`, `AddAzureKeyVault`, …) and **wire dependencies with `.WithReference(...)`**. From that one model, Aspire derives:

- **Ordering from the dependency graph** — not from a hook sequence you memorize. A resource that references another is provisioned/started after it.
- **Env & connection wiring is automatic** — `.WithReference(kv)` injects the endpoint into the consumer. No `.env` string glue between phases.
- **One artifact you can read** — the topology (who talks to whom) is legible in the app model, not reconstructed from six hook files.
- **Provision and deploy are stages of one graph** — not two disconnected commands with hooks stitching them together.

If `azd` let us express **stages + inter-resource references** natively, the ordering and env-injection that lives in `preprovision`/`postprovision`/`postdeploy` today would become **edges in a graph** the tool understands.

---

# 9 — What this means for `azd` to support (imperative language)

## B. Imperative language support for infra

Bicep is a separate DSL that **can't express** idempotent RBAC, conditional OAuth resources, deferred workflows, or "ensure-if-not-exists" — so each leaks into a TypeScript hook. Two languages, two mental models, one gap between them.

**Author infra in the same language as the hooks.** With [`js-provisioning-lib`](https://github.com/Azure/js-provisioning-lib), the stack is TypeScript, compiled to Bicep, consumed by `azd` unchanged:

```ts
const identity = new UserAssignedIdentity(rg, { name: 'qabot-identity' });
const kv = new KeyVault(rg, { properties: { tenantId: fn.subscription().tenantId, /* … */ } });

// RBAC — typed constant, idempotent, no `az` CLI string
new RoleAssignment(kv, {
  properties: {
    principalId: identity.properties.principalId,
    principalType: 'ServicePrincipal',
    roleDefinitionId: KV_SECRETS_OFFICER_ROLE_ID,
  },
});

// Typed output — no `az deployment sub show | jq`
stack.outputs.add('AZURE_APPCONFIG_ENDPOINT', 'string', appCfg.properties.endpoint);
```

- **One language, one repo, one type system** — typos become compile errors, not runtime 503s.
- **Composable, testable** — the stack is a plain value; `serialize(stack)` is pure and snapshot-testable.
- **Hooks shrink or disappear** — KV seeding, App Config seeding, RBAC, output plumbing become declarative constructs beside the resources.

> **Imperative authoring + a dependency-graph model = hooks turned back into infrastructure.**

---

# 9 — Putting it together

```
  Today:  Bicep (DSL)  +  ~1000 lines of imperative hooks  +  .env string glue
             │                      │                              │
        can't express         mutate live Azure             untyped state
        the intent            out of band                   between phases

  Ideal:  One typed app model  →  dependency graph  →  staged provision + deploy
             │                         │                      │
       service is the noun      ordering derived        env injected by
       (readable in one place)  from references         reference, not string
```

- **Multi-stage / dependency graph** (Aspire-style) removes the _ordering_ and _env-glue_ hooks.
- **Imperative infra language** (`js-provisioning-lib`) removes the _expressiveness_ hooks.
- **`azd` still consumes the emitted Bicep** — no change to `azure.yaml infra: { provider: bicep }`.

**The moment each capability lands, we delete a hook.**

---

<!-- _class: lead -->

# Recap

- **Architecture:** five services + a shared platform, deployed as one product.
- **Problem:** manual, drift-prone, no single source of truth.
- **`azd` today:** one CLI, Bicep-as-infra — but ~1000 lines of hooks bridge the gaps.
- **Why it isn't intuitive:** we model `azd`, not the service; hooks feel disconnected from the deployment.
- **Ideal:** describe the service once, in one typed language, converge to it.
- **Asks of `azd`:** **multi-stage / dependency-graph provisioning** (Aspire-style) + **imperative infra language** — together they turn hooks back into infrastructure.

**IaC + `azd` isn't a silver bullet — it's the smallest surface where the remaining gaps are all _someone else's bug_.**

---

# References

| Category | Link | Description |
| -------- | ---- | ----------- |
| Feature | [azure-dev#9246](https://github.com/Azure/azure-dev/issues/9246) | Deploy to a named App Service **slot** |
| Feature | [azure-dev#9248](https://github.com/Azure/azure-dev/issues/9248) | Late-resolve `${VAR}` in `image:` after predeploy |
| Feature | [azure-dev#9251](https://github.com/Azure/azure-dev/issues/9251) | Expose **target service name** to global hooks |
| Feature | [azure-dev#9252](https://github.com/Azure/azure-dev/issues/9252) | `azd provision --preview` omits hook effects |
| Feature | [azure-dev#9253](https://github.com/Azure/azure-dev/issues/9253) | First-class **idempotent RBAC** semantics |
| Bug | [azure-dev#9247](https://github.com/Azure/azure-dev/issues/9247) | `azure.ai.agent` doesn't persist `agent.yaml` env vars |
| Bug | [azure-dev#9249](https://github.com/Azure/azure-dev/issues/9249) | `azd deploy` wipes unknown App Service settings |
| Bug | [azure-dev#9250](https://github.com/Azure/azure-dev/issues/9250) | Container Function App on `host: function` hangs |
| Bug | [azure-dev#9152](https://github.com/Azure/azure-dev/issues/9152) | `azure.ai.agent` strips service-level hooks |
| Related | [Azure/js-provisioning-lib](https://github.com/Azure/js-provisioning-lib) | Infra as TypeScript, compiled to Bicep |
| Related | [azure-sdk-tools#16357](https://github.com/Azure/azure-sdk-tools/pull/16357) | Chatbot deployment PoC |

---

<!-- _class: lead -->

# Questions / Discussion

Repo: `Azure/azure-sdk-tools` → `tools/sdk-ai-bots/deployment/`

- Hooks: `deployment/hooks/`
- Bicep: `deployment/infra/modules/`
- Env contract: `deployment/infra/environments/environment-suite.yaml`
- Full journey deck: `deployment/docs/azd-deployment-journey.slides.md`
- Runbook: `deployment/docs/runbook-deploy.md`
