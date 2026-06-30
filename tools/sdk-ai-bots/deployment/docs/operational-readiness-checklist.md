# Operational Readiness Checklist

Sign off all items below before promoting a release to **prod**. Anything
unchecked must be filed as a follow-up issue with a date and owner.

## Per-component

For each of `frontend`, `backend`, `function-app`, `agent-server`,
`hosted-agent`, `logic-app`, `knowledge-sync`:

- [ ] DRI named (and a backup)
- [ ] On-call rotation includes this component
- [ ] CI pipeline is green on `main`
- [ ] CD pipeline last deployed `preview` is healthy for > 24 hours
- [ ] `Deployment:<component>:LastKnownGoodTag` in App Config matches
      the candidate tag's predecessor
- [ ] Rollback verified at least once in preview (slot swap back) within
      the last 90 days

## Infrastructure

- [ ] `scripts/validate-env-suite.ps1` passes for prod
- [ ] `scripts/detect-drift.ps1 -Environment prod` shows zero drift
- [ ] `az deployment sub what-if --parameters @prod.parameters.json` is
      reviewed by at least one other engineer
- [ ] Bicep changes merged via PR with at least one CODEOWNER approval

## Identity / RBAC

- [ ] All app-to-app calls use managed identity (no client secrets)
- [ ] Pipeline service-connection `azuresdkqabot-prod` has only
      `Contributor` on the prod RG (no broader scope)
- [ ] Developers have NO Contributor on prod RG
- [ ] Bot Service production endpoint requires AAD (EasyAuth on agent-server slot)

## Secrets

- [ ] All Key Vault secrets seeded (see `runbook-deploy.md` §"Seed secrets")
- [ ] No secrets present in any pipeline YAML or repo file
- [ ] Key Vault soft-delete enabled (90-day retention)

## Observability

- [ ] Application Insights connected for each App Service / Function App
- [ ] Availability tests configured for `/health` and `/ping`
- [ ] Alerts wired to the shared action group: - 5xx rate > 1 % over 5 min - p95 latency > 3000 ms over 5 min - Function App invocation failure > 5 % over 5 min
- [ ] Dashboard (or saved KQL bookmark) link present in `docs/`

## Resource locks

- [ ] `CanNotDelete` lock present on prod RG
- [ ] Cosmos DB backup policy set to ≥ 7 days continuous

## Cost / capacity

- [ ] Quota for AI Services models (`gpt-4.1`, `gpt-4.1-mini`,
      `text-embedding`) confirmed for the prod region
- [ ] Elastic Premium plan SKU sized for expected QPS
- [ ] ACR retention policy in place (don't fill on every CI build)

## Process

- [ ] Approval gate active on `sdk-ai-bots-prod` ADO environment
- [ ] Approvers include both a senior engineer and the DRI
- [ ] Operational readiness checklist itself is reviewed annually

---

Sign-off block (one row per release):

| Date | Release tag | Signed off by | Notes |
| ---- | ----------- | ------------- | ----- |
|      |             |               |       |
