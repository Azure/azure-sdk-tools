/**
 * Upload per-env bot-configs blobs (channel.yaml, tenant.yaml, etc.).
 *
 * Called from hooks/postprovision.ts. Reads YAML/JSON files from
 *   deployment/config/<AZURE_ENV_NAME>/
 * and uploads each to the shared storage account's `bot-configs` container
 * (created by qaBotSharedResources/sharedResources.bicep).
 *
 * Files are env-specific because they embed the backend/agent web app
 * endpoint URL and the set of Teams channels the bot is registered against.
 * If the env-specific config directory is missing or empty, the upload is
 * skipped so first-time provisions on a new environment don't fail — the
 * operator can then place the files under config/<env>/ and rerun
 * `azd provision` (or invoke this helper directly).
 */

import { execSync } from "child_process";
import { existsSync, readdirSync, statSync } from "fs";
import { resolve, join, relative } from "path";

const CONTAINER = "bot-configs";
const CONFIG_ROOT_ENV = "BOT_CONFIGS_SOURCE_DIR";
const DEFAULT_CONFIG_ROOT = "config";

type UploadOptions = {
  envName: string;
  storageAccountName: string;
  /** Optional override for the source directory root (defaults to `config/`). */
  configRoot?: string;
  log?: (msg: string) => void;
};

const defaultLog = (m: string): void => console.log(`[bot-configs] ${m}`);

/** Recursively collect every file under `dir` (returns paths relative to dir). */
function collectFiles(dir: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(dir)) {
    const abs = join(dir, entry);
    if (statSync(abs).isDirectory()) {
      for (const nested of collectFiles(abs)) {
        out.push(join(entry, nested));
      }
    } else {
      out.push(entry);
    }
  }
  return out;
}

export function uploadBotConfigs(opts: UploadOptions): void {
  const log = opts.log ?? defaultLog;
  const { envName, storageAccountName } = opts;

  if (!storageAccountName) {
    log("STORAGE_ACCOUNT_NAME is empty — skipping bot-configs upload.");
    return;
  }
  if (!envName) {
    log("AZURE_ENV_NAME is empty — skipping bot-configs upload.");
    return;
  }

  const configRoot = process.env[CONFIG_ROOT_ENV] ?? opts.configRoot ?? DEFAULT_CONFIG_ROOT;
  const envDir = resolve(process.cwd(), configRoot, envName);
  if (!existsSync(envDir)) {
    log(`No source directory at '${relative(process.cwd(), envDir)}' — skipping upload.`);
    return;
  }

  const files = collectFiles(envDir);
  if (files.length === 0) {
    log(`'${relative(process.cwd(), envDir)}' is empty — nothing to upload.`);
    return;
  }

  log(
    `Uploading ${files.length} file(s) from '${relative(process.cwd(), envDir)}' → ` +
      `${storageAccountName}/${CONTAINER}`
  );

  for (const relPath of files) {
    const absPath = join(envDir, relPath);
    // Preserve directory structure inside the container by using the relative
    // path as the blob name (forward slashes required by Azure).
    const blobName = relPath.split(/[\\/]/).join("/");
    execSync(
      `az storage blob upload ` +
        `--account-name "${storageAccountName}" ` +
        `--container-name "${CONTAINER}" ` +
        `--name "${blobName}" ` +
        `--file "${absPath}" ` +
        `--auth-mode login ` +
        `--overwrite ` +
        `--only-show-errors`,
      { stdio: "inherit" }
    );
    log(`  ✓ ${blobName}`);
  }
}
