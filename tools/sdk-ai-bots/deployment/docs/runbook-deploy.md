# Deploy Runbook

> First-time setup of a new ADO project / subscription is covered in
> [manual-setup.md](manual-setup.md). This runbook covers ongoing operations.

## Routine deployment (dev)

1. Verify the env-suite is current:
    ```pwsh
    pwsh deployment/scripts/validate-env-suite.ps1
    ```
2. Run the dev CI pipeline for whichever component you changed. It publishes
   a new image tag.
3. Trigger the matching dev CD pipeline, passing `imageTag=<tag-from-CI>`.

## Promotion to preview

1. Confirm dev has been stable for at least 1 hour with the candidate tag.
2. Trigger `<component>.cd.yml` with `environment=preview` and the same
   `imageTag`. Approval gate fires.
3. Pipeline deploys to staging slot, smoke-tests, swaps to production slot,
   smoke-tests again.

## Promotion to prod

1. Operational readiness checklist (`docs/operational-readiness-checklist.md`)
   must be signed off.
2. Trigger `pipelines/orchestrators/deploy-all-prod.yml` with all four image
   tags. The orchestrator runs:
    - Preflight (`bicep what-if` + readiness reminder), waits for approval.
    - Backend CD → 10-min watch → function-app → agent → frontend.
3. Each stage records `Deployment:<component>:LastKnownGoodTag` in App
   Configuration on success.

## One-time manual steps

These cannot be automated via Bicep and must be done once per environment by
an operator with appropriate permissions:

1. **Teams + Azure Blob OAuth consent** (Logic App). After the `logic-app`
   layer is deployed, sign in to the portal and complete OAuth consent for
   each managed-API connection. Connection names are printed by
   `hooks/postprovision.ts`.
2. **Teams App publish.** The Teams app manifest is built in CI; first-time
   publish into the tenant catalog is still done via Teams Toolkit
   (`teamsapp publish`). Subsequent updates flow through `teamsapp/update`.
3. **Enable Storage blob versioning** on the shared storage account
   (already in Bicep but verify after the first apply).
4. **Bot Service channel registration.** Verify in the portal that the
   Teams channel is bound to the bot resource (Bicep wires it, but the
   portal flag for "Microsoft 365 channel" must be flipped on once).

## Seed Key Vault secrets

```bash
az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name GitHubAppPrivateKey      --file github-app.pem
az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name TeamsWebhookUrl          --value "<url>"
az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name CosmosDbConnectionString --value "<conn>"
```

Refer to `hooks/postprovision.ts` `seedKeyVaultSecrets()` for the full
inventory.

## Local developer flow (dev only)

```bash
cd tools/sdk-ai-bots/deployment
npm install
azd auth login
azd env select dev
azd provision --environment dev --no-prompt
azd deploy frontend --environment dev --no-prompt
```

Local prod deploy is blocked by `hooks/preprovision.ts`.
