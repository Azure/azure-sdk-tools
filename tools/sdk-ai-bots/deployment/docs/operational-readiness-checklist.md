# Operational Readiness Checklist

Complete this checklist before approving a production deployment. Record any
exception with an owner and due date.

## Source and Configuration

- [ ] The candidate source revision is recorded and all relevant build and test
      checks are green.
- [ ] `validate-env-suite.ps1 -Environment prod` passes.
- [ ] No selected production value contains `REPLACE_WITH_*`.
- [ ] Production tenant/channel YAML matches the environment suite.
- [ ] Resource names and `bicepOverrides` identify the intended existing
      production resources.
- [ ] The production preview has been reviewed for Create, Modify, and Delete
      operations by someone other than the requester.

## Pipeline Controls

- [ ] Production and candidate-dev service connections use workload identity
      federation and are authorized only for the required definitions.
- [ ] Production service-connection approval and branch-control checks are
      active.
- [ ] The pipeline manual preview-to-apply gate has designated approvers.

## Identity and Access

- [ ] The deployment principal can provision resources and role assignments at
      the intended scope; no human developer identity is used by automation.
- [ ] Frontend Azure Bot authentication uses the declared user-assigned managed
      identity and the same Entra tenant as Teams.
- [ ] Agent-server Easy Auth uses the expected backend application client ID and
      Application ID URI.
- [ ] Runtime identities have only the required App Configuration, Key Vault,
      Storage, Search, Cosmos DB, ACR, and AI Services roles.
- [ ] The evolution-agent identity has production access plus only the required
      candidate-dev access.
- [ ] No client secrets or credentials appear in repository or pipeline YAML.

## Runtime Configuration and Integrations

- [ ] Postprovision completed App Configuration, Search reconciliation,
      `AI-SEARCH-APIKEY`, and bot-config upload without skipped required values.
- [ ] `WEB_FETCH_ALLOWED_DOMAINS` contains the approved production domains.
- [ ] The configured GitHub App private-key secret exists and runtime identities
      can read it.
- [ ] The Teams managed-API connection reports `Connected`.
- [ ] The Azure Bot `MsTeamsChannel` exists.
- [ ] The approved Teams app version is published in the tenant catalog.
- [ ] `TeamsWebhookUrl` exists when deployment notifications are required.

## Data and Recovery

- [ ] Knowledge sync and wiki build definitions have completed successfully and
      both Search indexer triggers were accepted.
- [ ] Cosmos DB continuous backup is enabled and its restore procedure is known.
- [ ] Blob versioning is enabled if blob-version rollback is part of the
      recovery plan; it is disabled by the current Bicep configuration.
- [ ] A known-good source revision is recorded for each deployed component.
- [ ] Operators understand that rollback is a redeployment; no automated
      slot/revision rollback pipeline exists.

## Observability and Verification

- [ ] Application Insights and diagnostic settings are connected for each
      service.
- [ ] Frontend availability test and deployment alerts report healthy.
- [ ] Alert destinations and on-call ownership have been exercised.
- [ ] Operators can verify frontend `/health`, agent-server `/ping`, and
      Function App `/api/health` after deployment.
- [ ] The Teams message path and Logic App trigger have been tested in the
      target tenant.
- [ ] Dashboard or saved-query links are recorded in the team's operational
      system.

## Ownership and Capacity

- [ ] Every component and data job has a primary and backup DRI.
- [ ] Model quota covers the capacities declared in the agent Bicep layer.
- [ ] App Service, Functions, Search, Cosmos DB, and ACR capacity are reviewed
      for expected production load.
- [ ] The release owner and production approver sign off below.

| Date | Source revision | Release owner | Approver | Exceptions |
| --- | --- | --- | --- | --- |
| | | | | |
