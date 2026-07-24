# Component Dependency Graph

```mermaid
graph TD
    subgraph "Shared Infrastructure (Layer 1)"
        SR[shared-resources<br/>ACR, Storage, Cosmos, Key Vault,<br/>App Config, Search, Log Analytics]
    end

    subgraph "AI Platform (Layer 2)"
        AGP[agent-platform<br/>Foundry project, AI Services,<br/>model deployments]
    end

    subgraph "Identity (Layer 3)"
        FE_ID[frontend identity<br/>bot user-assigned MI]
    end

    subgraph "Compute (Layer 4-5)"
        BE[backend<br/>App Service + agent slot]
        FN[function-app<br/>Elastic Premium plan]
    end

    subgraph "Integration (Layer 6)"
        LA[logic-app<br/>Teams + Blob + Cosmos workflow]
    end

    subgraph "Application Code (azd services)"
        FE_APP[frontend app<br/>Teams bot container]
        FN_APP[function-app app<br/>queue handler container]
        AG_APP[hosted agent<br/>Foundry container agent]
        AG_SRV[agent-server<br/>backend agent slot container]
    end

    subgraph "Data Maintenance"
        KS[knowledge-sync<br/>scheduled ADO job]
    end

    SR --> AGP
    SR --> FE_ID
    FE_ID --> BE
    SR --> BE
    AGP --> BE
    SR --> FN
    BE --> LA
    FN --> LA
    FE_ID --> LA

    BE --> AG_SRV
    AGP --> AG_APP
    BE --> FE_APP
    SR --> FN_APP
    SR --> KS
```

## Deploy order

Encoded by `dependsOn` in `infra/main.bicep` and by `INFRA_LAYERS` in
`hooks/lib/layers.ts`:

1. `shared-resources` — every other layer references its outputs.
2. `agent-platform` — depends on managed identity + storage from layer 1.
3. `frontend identity` — `backend` slot configuration references this MI.
4. `backend` — needs `agent.outputs.aiResourceName`, `aiProjectName`, and
   the frontend MI.
5. `function-app` — independent of backend; shares shared-resources.
6. `logic-app` — last; references both backend and function-app outputs.

After provisioning, `azd deploy` runs application-code services in this
order (driven by the `services:` block in `azure.yaml`):

1. `function-app` (no dependents)
2. `agent` (hosted agent) — depends on agent-platform layer
3. `frontend` (Teams bot) — must come last so a deploy-time channel rebind
   does not race with the others

`knowledge-sync` runs on a cron schedule independent of any deploy.
