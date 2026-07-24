/**
 * seedKeyVaultSecrets — populate the Key Vault secrets the backend reads at
 * startup. The Go backend's `config.InitSecrets()` fatally exits (container
 * crash / 503) if these secrets are missing, so they must exist before the
 * backend container can start:
 *
 *   - AI-SEARCH-APIKEY               ← AI Search service admin (primary) key
 *   - AOAI-CHAT-COMPLETIONS-API-KEY  ← Azure OpenAI / AI Services account key
 *
 * Both source keys are read from the provisioned resources via the control
 * plane, then written to the app's own Key Vault. Safe to run on every
 * provision (secret set is create-or-new-version).
 *
 * Requires the deploying principal to have `Key Vault Secrets Officer` on the
 * vault; this grants it idempotently (self-healing) in case bicep's role
 * assignment doesn't cover the current deployer.
 */

import { execSync } from "child_process";
import { randomUUID } from "crypto";
import * as fs from "fs";
import * as os from "os";
import * as path from "path";

export interface SeedKeyVaultSecretsOptions {
  /** App Key Vault name (KEY_VAULT_NAME output). */
  keyVaultName: string;
  /** Resource group of the source resources. */
  resourceGroup: string;
  /** AI Search service name (SEARCH_SERVICE_NAME output). */
  searchServiceName: string;
  /** AI Services / Azure OpenAI account name (AI_RESOURCE_NAME output). */
  aiResourceName: string;
  /** Environment values — normally `process.env`. */
  env: NodeJS.ProcessEnv;
  log?: (msg: string) => void;
}

/** Key Vault Secrets Officer built-in role definition ID. */
const KEY_VAULT_SECRETS_OFFICER_ROLE_ID = "b86a8fe4-44ce-4948-aee5-eccb2c155cd7";

function run(cmd: string): string {
  return execSync(cmd, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

/**
 * Ensure the deploying principal holds `Key Vault Secrets Officer` on the vault
 * so it can write secrets (the vault uses RBAC auth).
 */
function ensureSecretsOfficerRole(
  keyVaultName: string,
  env: NodeJS.ProcessEnv,
  log: (msg: string) => void,
): void {
  const subscriptionId = env.AZURE_SUBSCRIPTION_ID?.trim();
  const resourceGroup = env.AZURE_RESOURCE_GROUP?.trim();
  if (!subscriptionId || !resourceGroup) {
    log("  AZURE_SUBSCRIPTION_ID / AZURE_RESOURCE_GROUP not set — cannot ensure RBAC; relying on existing access.");
    return;
  }

  let principalId = env.DEVELOPER_PRINCIPAL_ID?.trim();
  let principalType = env.DEVELOPER_PRINCIPAL_TYPE?.trim() || "User";
  if (!principalId) {
    try {
      principalId = run(`az ad signed-in-user show --query id --output tsv`);
      principalType = "User";
    } catch {
      log("  Could not resolve deployer principal id — relying on existing access.");
      return;
    }
  }
  if (!principalId) {
    log("  No principal id available — relying on existing access.");
    return;
  }

  const scope =
    `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}` +
    `/providers/Microsoft.KeyVault/vaults/${keyVaultName}`;

  try {
    run(
      `az role assignment create --assignee-object-id "${principalId}" ` +
        `--assignee-principal-type "${principalType}" ` +
        `--role "${KEY_VAULT_SECRETS_OFFICER_ROLE_ID}" --scope "${scope}" --output none`,
    );
    log(`  ✓ granted Key Vault Secrets Officer to ${principalId} (${principalType})`);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    if (/RoleAssignmentExists|already exists/i.test(msg)) {
      log(`  ✓ Key Vault Secrets Officer already assigned to ${principalId}`);
      return;
    }
    log(`  ⚠ could not create role assignment (${msg.split("\n")[0]}); continuing.`);
  }
}

/**
 * Seed the two backend secrets from the provisioned resources.
 */
export function seedKeyVaultSecrets(opts: SeedKeyVaultSecretsOptions): void {
  const { keyVaultName, resourceGroup, searchServiceName, aiResourceName, env } = opts;
  const log = opts.log ?? ((m: string) => console.log(`[seed-key-vault] ${m}`));

  if (!keyVaultName) {
    log("KEY_VAULT_NAME not set — skipping Key Vault secret seeding.");
    return;
  }
  if (!searchServiceName || !aiResourceName) {
    log("SEARCH_SERVICE_NAME / AI_RESOURCE_NAME not set — skipping Key Vault secret seeding.");
    return;
  }

  ensureSecretsOfficerRole(keyVaultName, env, log);

  // Read the source keys from the provisioned resources (control plane).
  const searchApiKey = run(
    `az search admin-key show --service-name "${searchServiceName}" --resource-group "${resourceGroup}" --query primaryKey --output tsv`,
  );
  const aoaiApiKey = run(
    `az cognitiveservices account keys list --name "${aiResourceName}" --resource-group "${resourceGroup}" --query key1 --output tsv`,
  );

  const sleep = (seconds: number): void => {
    execSync(`sleep ${seconds}`, { stdio: "ignore" });
  };

  // Write the value via a temp file so the secret never appears in the process
  // args / shell history.
  const setSecretOnce = (name: string, value: string): void => {
    const tmp = path.join(os.tmpdir(), `kv-${randomUUID()}`);
    fs.writeFileSync(tmp, value, { mode: 0o600 });
    try {
      run(
        `az keyvault secret set --vault-name "${keyVaultName}" --name "${name}" ` +
          `--file "${tmp}" --output none`,
      );
    } finally {
      try {
        fs.unlinkSync(tmp);
      } catch {
        /* best-effort cleanup */
      }
    }
  };

  const setSecretWithRetry = (name: string, value: string): void => {
    // Key Vault RBAC can take a few minutes to propagate after the role
    // assignment is created, so retry on Forbidden/authorization errors.
    const delaysSeconds = [0, 15, 30, 30, 45, 60, 60, 60, 60];
    for (let attempt = 0; attempt < delaysSeconds.length; attempt++) {
      try {
        setSecretOnce(name, value);
        return;
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        const isAuthDelay = /Forbidden|AuthenticationFailure|not authorized|403|S2S/i.test(msg);
        const isLast = attempt === delaysSeconds.length - 1;
        if (!isAuthDelay || isLast) throw err;
        const wait = delaysSeconds[attempt + 1];
        log(`  … secret write forbidden (RBAC still propagating); retrying in ${wait}s`);
        sleep(wait);
      }
    }
  };

  log(`Seeding 2 secret(s) into '${keyVaultName}'...`);
  setSecretWithRetry("AI-SEARCH-APIKEY", searchApiKey);
  log("  ✓ AI-SEARCH-APIKEY");
  setSecretWithRetry("AOAI-CHAT-COMPLETIONS-API-KEY", aoaiApiKey);
  log("  ✓ AOAI-CHAT-COMPLETIONS-API-KEY");
  log(`Key Vault '${keyVaultName}' seeded.`);
}
