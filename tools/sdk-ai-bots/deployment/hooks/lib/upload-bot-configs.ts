/**
 * Upload per-env bot-configs blobs (channel.yaml, tenant.yaml, etc.).
 *
 * Called from hooks/postprovision.ts. Reads YAML/JSON files from
 *   deployment/config/<AZURE_ENV_NAME>/
 * and uploads each to the shared storage account's `bot-configs` container
 * (created by qaBotSharedResources/sharedResources.bicep).
 *
 * Files may reference azd env vars via `${VAR}` placeholders (e.g. the
 * frontend's channel.yaml references `${SERVER_BASE_URL}` for the RAG
 * backend endpoint). Placeholders are substituted from process.env at
 * upload time so a single source-controlled template drives every
 * environment/subscription — no per-env URL rewrites required. Missing
 * placeholders are fatal (we'd rather fail the provision than ship a
 * literal `${...}` string to the blob).
 *
 * Files are env-specific because they embed the set of Teams channels the
 * bot is registered against. If the env-specific config directory is
 * missing or empty, the upload is skipped so first-time provisions on a
 * new environment don't fail — the operator can then place the files
 * under config/<env>/ and rerun `azd provision` (or invoke this helper
 * directly).
 */

import { execSync } from "child_process";
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from "fs";
import { tmpdir } from "os";
import { resolve, join, relative } from "path";

const CONTAINER = "bot-configs";
const CONFIG_ROOT_ENV = "BOT_CONFIGS_SOURCE_DIR";
const DEFAULT_CONFIG_ROOT = "config";
// Files whose contents get env-var expansion applied. Blobs outside this list
// (e.g. binary assets) are uploaded verbatim.
const TEMPLATED_EXTENSIONS = new Set([".yaml", ".yml", ".json"]);
// `${VAR}` with a strict identifier — matches shell/bicep interpolation.
const PLACEHOLDER = /\$\{([A-Z_][A-Z0-9_]*)\}/g;

type UploadOptions = {
  envName: string;
  storageAccountName: string;
  /** Optional override for the source directory root (defaults to `config/`). */
  configRoot?: string;
  /**
   * Optional env-var map for `${VAR}` expansion inside templated files.
   * Defaults to `process.env`. Missing keys are fatal.
   */
  env?: NodeJS.ProcessEnv;
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

/** Return the file extension (lower-cased, with leading dot) or "". */
function extOf(path: string): string {
  const i = path.lastIndexOf(".");
  return i < 0 ? "" : path.slice(i).toLowerCase();
}

/**
 * Substitute `${VAR}` placeholders using values from `env`. Throws if any
 * placeholder in the source has no corresponding env var — this catches
 * missing outputs at provision time rather than shipping a literal
 * `${SERVER_BASE_URL}` to blob storage.
 */
function expandPlaceholders(source: string, env: NodeJS.ProcessEnv): string {
  const missing = new Set<string>();
  const expanded = source.replace(PLACEHOLDER, (_match, name: string) => {
    const value = env[name];
    if (value === undefined || value === "") {
      missing.add(name);
      return `\${${name}}`;
    }
    return value;
  });
  if (missing.size > 0) {
    throw new Error(
      `bot-configs: missing env var(s) for placeholder expansion: ${[...missing].join(", ")}. ` +
        `Ensure the corresponding bicep output(s) are exposed and azd has persisted them to .azure/<env>/.env.`
    );
  }
  return expanded;
}

export function uploadBotConfigs(opts: UploadOptions): void {
  const log = opts.log ?? defaultLog;
  const { envName, storageAccountName } = opts;
  const env = opts.env ?? process.env;

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

    // Expand `${VAR}` placeholders in known text formats. If the file has
    // no placeholders the expanded output equals the source, so we can
    // still upload the original path. If it does, write a temp copy so
    // `az storage blob upload --file` sees the substituted content.
    let uploadPath = absPath;
    let tempPath: string | undefined;
    if (TEMPLATED_EXTENSIONS.has(extOf(relPath))) {
      const source = readFileSync(absPath, "utf8");
      if (PLACEHOLDER.test(source)) {
        PLACEHOLDER.lastIndex = 0; // reset after `.test`
        const expanded = expandPlaceholders(source, env);
        tempPath = join(
          tmpdir(),
          `bot-configs-${envName}-${blobName.replace(/[\\/]/g, "_")}`
        );
        writeFileSync(tempPath, expanded, "utf8");
        uploadPath = tempPath;
      }
    }

    execSync(
      `az storage blob upload ` +
        `--account-name "${storageAccountName}" ` +
        `--container-name "${CONTAINER}" ` +
        `--name "${blobName}" ` +
        `--file "${uploadPath}" ` +
        `--auth-mode login ` +
        `--overwrite ` +
        `--only-show-errors`,
      { stdio: "inherit" }
    );
    log(`  ✓ ${blobName}${tempPath ? " (env-expanded)" : ""}`);
  }
}
