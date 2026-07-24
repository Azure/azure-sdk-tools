/**
 * syncTeamsEnv — generate the azd-owned Teams Toolkit environment file
 * (azure-sdk-qa-bot/env/.env.azd) from the current azd environment.
 *
 * Called from hooks/postprovision.ts after `azd provision` has persisted the
 * bicep outputs into the azd environment. azd decides which logical
 * environment is being provisioned (AZURE_ENV_NAME = dev / preview / prod);
 * this helper takes the matching committed Teams env file as the static base
 * (display names, GitHub App, storage config, TEAMS_APP_ID, ...) and overlays
 * the fresh azd / bicep outputs (BOT_* resource identifiers, subscription /
 * resource group), writing the merged result to a single `.env.azd` file.
 *
 * `teamsapp provision/deploy/publish --env azd` then consumes that one file, so
 * the frontend no longer needs its own `arm/deploy` step and the committed
 * per-env files (env/.env.dev|preprod|prod) stay free of provisioning churn.
 */

import { existsSync, readFileSync, writeFileSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

/** Teams Toolkit env name for the generated azd-owned file. */
const AZD_TEAMS_ENV = "azd";

// This file lives in deployment/hooks/lib/, so the Teams project (a sibling of
// deployment/) is three levels up. Resolving from the module path rather than
// process.cwd() keeps this correct whether the caller is the global
// postprovision hook (cwd = azure.yaml dir) or a per-service hook (cwd =
// deployment/).
const DEFAULT_TEAMS_PROJECT_ROOT = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../azure-sdk-qa-bot",
);

export interface SyncTeamsEnvOptions {
  /** azd environment name (AZURE_ENV_NAME), e.g. `dev` / `preview` / `prod`. */
  azdEnvName: string;
  /** Environment values — normally `process.env` after azd env is loaded. */
  env: NodeJS.ProcessEnv;
  /**
   * Teams app registration for this environment. Sourced from
   * `infra/environments/environment-suite.yaml`
   * (environments.<env>.teamsAppId / .teamsAppTenantId) by the caller.
   *
   * Each env has its OWN Teams app; these MUST NOT be copied from the
   * committed base file (e.g. .env.dev), which would repoint the azd env to a
   * foreign app on every provision. When empty or set to a `REPLACE_WITH_*`
   * placeholder, the keys are dropped from the seed so `teamsapp provision
   * --env azd` mints a fresh registration on its first run — copy the
   * resulting GUID back into environment-suite.yaml.
   */
  teamsAppId?: string;
  teamsAppTenantId?: string;
  /**
   * Optional override for the Teams project root (folder containing `env/`).
   * Defaults to `../azure-sdk-qa-bot` relative to the deployment/ cwd azd uses
   * when running the hook.
   */
  teamsProjectRoot?: string;
  log?: (msg: string) => void;
}

const defaultLog = (m: string): void => console.log(`[sync-teams-env] ${m}`);

/**
 * Map an azd environment name to the committed Teams Toolkit env used as the
 * static base. They match for dev / prod; azd calls the middle ring `preview`
 * while Teams calls it `preprod` (env/.env.preprod), so translate that one.
 */
function toTeamsEnvName(azdEnvName: string): string {
  return azdEnvName === "preview" ? "preprod" : azdEnvName;
}

/**
 * azd values overlaid onto the static base. Each `target` key is upserted into
 * `.env.azd` from the azd environment variable named by `source`. Keys marked
 * `required` fail the sync when absent (they are bicep outputs that must exist
 * after a successful `azd provision`); optional ones are skipped if unset.
 */
const AZD_OVERRIDES: { target: string; source: string; required: boolean }[] = [
  { target: "BOT_AZURE_APP_SERVICE_RESOURCE_ID", source: "BOT_AZURE_APP_SERVICE_RESOURCE_ID", required: true },
  { target: "BOT_DOMAIN", source: "BOT_DOMAIN", required: true },
  { target: "BOT_ID", source: "BOT_ID", required: true },
  { target: "BOT_TENANT_ID", source: "BOT_TENANT_ID", required: true },
  { target: "AZURE_SUBSCRIPTION_ID", source: "AZURE_SUBSCRIPTION_ID", required: false },
  { target: "AZURE_RESOURCE_GROUP_NAME", source: "AZURE_RESOURCE_GROUP", required: false },
];

/**
 * Keys Teams Toolkit writes back into the per-env file when it registers the
 * Teams app. Each Teams Toolkit environment has its OWN registration (different
 * TEAMS_APP_ID per env), so these must never leak from the committed base file
 * (e.g. .env.dev) into the azd-owned .env.azd — doing so would repoint the azd
 * env to a foreign app on every provision. The authoritative value comes from
 * environment-suite.yaml, passed in via SyncTeamsEnvOptions.
 */
const TEAMSAPP_OWNED_KEYS = ["TEAMS_APP_ID", "TEAMS_APP_TENANT_ID"] as const;

/** Remove any lines assigning one of `keys`. */
function stripEnvLines(lines: string[], keys: readonly string[]): string[] {
  const patterns = keys.map((k) => new RegExp(`^\\s*${k}\\s*=`));
  return lines.filter((line) => !patterns.some((p) => p.test(line)));
}

/** Upsert `KEY=value` into the lines of an env file, preserving everything else. */
function upsertEnvLines(lines: string[], key: string, value: string): string[] {
  const assignment = `${key}=${value}`;
  const pattern = new RegExp(`^\\s*${key}\\s*=`);
  const idx = lines.findIndex((line) => pattern.test(line));
  if (idx === -1) {
    return [...lines, assignment];
  }
  const next = [...lines];
  next[idx] = assignment;
  return next;
}

/** True for empty / whitespace / `REPLACE_WITH_*` placeholders from the suite file. */
function isPlaceholder(value: string | undefined): boolean {
  const v = value?.trim() ?? "";
  return v === "" || v.startsWith("REPLACE_WITH_");
}

export function syncTeamsEnv(options: SyncTeamsEnvOptions): void {
  const log = options.log ?? defaultLog;
  const baseEnvName = toTeamsEnvName(options.azdEnvName);

  const projectRoot = options.teamsProjectRoot ?? DEFAULT_TEAMS_PROJECT_ROOT;
  const baseFile = resolve(projectRoot, "env", `.env.${baseEnvName}`);
  const azdFile = resolve(projectRoot, "env", `.env.${AZD_TEAMS_ENV}`);

  // Seed from the committed per-env file so the generated .env.azd is
  // self-contained (display names, GitHub App, storage, ...). Always strip
  // teamsapp-owned keys — they come from environment-suite.yaml, not from
  // the base file (see TEAMSAPP_OWNED_KEYS).
  let lines: string[];
  if (existsSync(baseFile)) {
    lines = stripEnvLines(readFileSync(baseFile, "utf8").split("\n"), TEAMSAPP_OWNED_KEYS);
    log(`Base Teams env: .env.${baseEnvName} (azd env '${options.azdEnvName}')`);
  } else {
    lines = [
      "# Generated by azd (deployment/hooks/lib/sync-teams-env.ts). Do not edit by hand.",
    ];
    log(
      `WARNING: base Teams env '.env.${baseEnvName}' not found — writing .env.azd ` +
        `from azd outputs only; static Teams values may be missing.`,
    );
  }

  const missing: string[] = [];
  for (const { target, source, required } of AZD_OVERRIDES) {
    const value = options.env[source]?.trim();
    if (!value) {
      if (required) missing.push(source);
      continue;
    }
    lines = upsertEnvLines(lines, target, value);
    log(`  set ${target}`);
  }

  if (missing.length > 0) {
    throw new Error(
      `Cannot generate .env.azd: missing azd outputs [${missing.join(", ")}]. ` +
        `Ensure 'azd provision' completed and persisted the bicep outputs.`,
    );
  }

  // Teams Toolkit resolves the env from the file suffix; this file is `.env.azd`
  // so its TEAMSFX_ENV (used e.g. for the app-package zip name) must be `azd`.
  lines = upsertEnvLines(lines, "TEAMSFX_ENV", AZD_TEAMS_ENV);

  // Apply the per-env Teams app registration from environment-suite.yaml.
  // Skip when the value is missing / a placeholder so `teamsapp provision
  // --env azd` mints a fresh registration on its first run; the resulting
  // GUID should then be committed back to environment-suite.yaml.
  const teamsappValues: Record<(typeof TEAMSAPP_OWNED_KEYS)[number], string | undefined> = {
    TEAMS_APP_ID: options.teamsAppId,
    TEAMS_APP_TENANT_ID: options.teamsAppTenantId,
  };
  for (const key of TEAMSAPP_OWNED_KEYS) {
    const value = teamsappValues[key];
    if (isPlaceholder(value)) {
      log(
        `  ${key} not set in environment-suite.yaml for '${options.azdEnvName}' — ` +
          `omitting; teamsapp will create one on first provision.`,
      );
      continue;
    }
    lines = upsertEnvLines(lines, key, value!.trim());
    log(`  set ${key} (from environment-suite.yaml)`);
  }

  writeFileSync(azdFile, lines.join("\n"), "utf8");
  log(`Wrote ${azdFile} — run \`teamsapp <cmd> --env ${AZD_TEAMS_ENV}\`.`);
}
