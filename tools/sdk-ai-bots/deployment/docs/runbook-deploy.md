# Deploy Runbook

> First-time setup of a new ADO project / subscription is covered in
> [manual-setup.md](manual-setup.md). This runbook covers ongoing operations.

## Routine deployment (dev)

1. Verify the env-suite is current:
    ```pwsh
    pwsh deployment/scripts/validate-env-suite.ps1
    ```
2. Run the dev CI pipeline for the changed component to validate the source.
3. Trigger the matching component orchestrator, review its infrastructure
   preview, and approve the apply stage.
4. `azd deploy` remotely builds the component from the selected source revision
   and deploys it.

## Promotion to preview

1. Confirm dev has been stable for at least 1 hour with the candidate commit.
2. Trigger `pipelines/orchestrators/<component>/<component>.yml` with
   `environment=preview` from the candidate commit. Review the infrastructure
   preview and approve the apply stage. The pipeline then performs a native ACR
   remote build.
3. The pipeline deploys to staging slot, smoke-tests, swaps to production slot,
   smoke-tests again.

## Promotion to prod

1. Operational readiness checklist (`docs/operational-readiness-checklist.md`)
   must be signed off.
2. Trigger `pipelines/orchestrators/qa-bot-all.yml` with `environment=prod`
   from the approved commit. The orchestrator runs:
    - Preflight (`bicep what-if` + readiness reminder).
      - Manual approval.
      - Provision (`azd provision`).
      - Agent-server → 10-min watch → function-app → agent → frontend.
   The prod service connection's configured approval and branch-control checks
   protect Azure operations in these stages.
3. Record the successful source revision and resulting App Service or Foundry
   revision in the deployment record.

## One-time manual steps

These cannot be automated via Bicep and must be done once per environment by
an operator with appropriate permissions:

1. **Teams + Azure Blob OAuth consent** (Logic App). After the `logic-app`
   layer is deployed, the pipeline pauses when Teams consent is missing. Open
   the supplied portal link, authorize with the Teams service account, save,
   and resume. The pipeline verifies `Connected` before continuing. Complete
   any other managed-API consent in the portal as required.
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
