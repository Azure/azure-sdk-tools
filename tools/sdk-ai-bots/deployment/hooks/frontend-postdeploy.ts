/**
 * frontend — postdeploy hook
 *
 * Runs as part of `azd deploy frontend`, after azd has pushed the container
 * image and repointed the App Service (see frontend-predeploy.ts). It:
 *   1. Health-checks the bot front-end (reads the site URL from azd outputs), and
 *   2. Drives Teams Toolkit with the azd-owned environment: `teamsapp provision
 *      --env azd` (and, when TEAMS_PUBLISH=1, `teamsapp publish --env azd`), and
 *   3. Installs the Teams app into the configured team through Microsoft Graph.
 *
 * The `.env.azd` file is generated during `azd provision`
 * (deployment/hooks/lib/sync-teams-env.ts) from whichever logical environment
 * azd targeted (dev / preview / prod), so the whole flow is just:
 *   azd provision       →  writes env/.env.azd
 *   azd deploy frontend →  builds/pushes image + `teamsapp ... --env azd`
 *
 * Azure resource provisioning is owned by azd (the former teamsapp `arm/deploy`
 * step was removed). Teams Toolkit creates/updates the app registration and
 * manifest; Microsoft Graph establishes its team-scoped installation.
 */

import { execSync } from "child_process";
import { existsSync, readFileSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

import { getEnvSuiteValue } from "./lib/env-suite.js";
import {
  getTeamsAppInstallationStatus,
  installTeamsAppInTeam,
} from "./lib/install-teams-app.js";

const BOT_URL =
  process.env.SERVICE_FRONTEND_URI ??
  process.env.BOT_URL ??
  (process.env.BOT_DOMAIN ? `https://${process.env.BOT_DOMAIN}` : "");
// This file lives in deployment/hooks/, so the Teams project (with teamsapp.yml
// + env/) is a sibling of deployment/, two levels up. Resolving from the module
// path rather than process.cwd() keeps this correct regardless of the directory
// azd runs the hook from.
const TEAMS_PROJECT_ROOT =
  process.env.TEAMS_PROJECT_ROOT ??
  resolve(dirname(fileURLToPath(import.meta.url)), "../../azure-sdk-qa-bot");
const TEAMS_ENV = "azd";

function log(msg: string): void {
  console.log(`[frontend:postdeploy] ${msg}`);
}

async function healthCheck(): Promise<void> {
  if (!BOT_URL) {
    log("BOT_URL / SERVICE_FRONTEND_URI / BOT_DOMAIN not set — skipping health check.");
    return;
  }
  log(`Probing ${BOT_URL}/health`);
  for (let i = 0; i < 12; i++) {
    try {
      const res = await fetch(`${BOT_URL}/health`);
      if (res.ok) {
        log(`  ✓ healthy (HTTP ${res.status}) after ${i * 10}s`);
        return;
      }
      log(`  attempt ${i + 1}: HTTP ${res.status}`);
    } catch (err) {
      log(`  attempt ${i + 1}: ${(err as Error).message}`);
    }
    await new Promise((r) => setTimeout(r, 10_000));
  }
  // Non-fatal: a freshly repointed container can take a while to come up, and a
  // smoke-check timeout should not fail an otherwise-successful deploy (nor
  // block the teamsapp step). Warn and continue.
  log(
    "WARNING: health check did not return 200 within 2 minutes. The new container " +
      "may still be starting; verify manually at the URL above.",
  );
}

/** Run `teamsapp <command> --env azd` from the Teams project root. */
function runTeamsapp(command: string): void {
  log(`Running \`teamsapp ${command} --env ${TEAMS_ENV}\`...`);
  // The `teamsapp` binary is provided by the `@microsoft/teamsapp-cli` package
  // — invoke it by package name so `npx` resolves it even when it is not a
  // local dependency (a bare `npx teamsapp` looks for a package literally named
  // `teamsapp`, which does not exist on npm).
  //
  // `--interactive true` forces the CLI to open the Microsoft 365 sign-in in a
  // browser and wait for it. Without it, teamsapp auto-detects that azd runs the
  // hook with a non-TTY stdout and silently switches to non-interactive mode,
  // where it can't sign in and fails instead of prompting. The browser flow uses
  // a localhost redirect (not stdin), so it still works under a piped stdout.
  execSync(`npx --yes @microsoft/teamsapp-cli ${command} --env ${TEAMS_ENV} --interactive true`, {
    stdio: "inherit",
    cwd: TEAMS_PROJECT_ROOT,
    // Clear CI markers so the CLI doesn't force non-interactive mode and skip
    // the browser login prompt.
    env: { ...process.env, CI: "", TF_BUILD: "", TEAMSFX_INTERACTIVE: "true" },
  });
}

function readEnvValue(envFile: string, key: string): string | undefined {
  const pattern = new RegExp(`^\\s*${key}\\s*=\\s*(.*)$`);
  for (const line of readFileSync(envFile, "utf8").split(/\r?\n/)) {
    const match = pattern.exec(line);
    if (!match) continue;

    let value = match[1].trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    return value || undefined;
  }
  return undefined;
}

interface GeneratedTeamsManifest {
  id?: string;
  version?: string;
  bots?: Array<{ botId?: string }>;
}

function readGeneratedManifest(): {
  externalId: string;
  version: string;
  botId: string;
} {
  const manifestFile = resolve(
    TEAMS_PROJECT_ROOT,
    "appPackage",
    "build",
    `manifest.${TEAMS_ENV}.json`,
  );
  if (!existsSync(manifestFile)) {
    throw new Error(
      `Generated Teams manifest '${manifestFile}' not found after teamsapp provision.`,
    );
  }

  const manifest = JSON.parse(
    readFileSync(manifestFile, "utf8"),
  ) as GeneratedTeamsManifest;
  const externalId = manifest.id?.trim();
  const version = manifest.version?.trim();
  const botId = manifest.bots?.[0]?.botId?.trim();
  if (!externalId || !version || !botId) {
    throw new Error(
      `Generated Teams manifest '${manifestFile}' is missing id, version, or bots[0].botId.`,
    );
  }

  const deployedBotId = process.env.BOT_ID?.trim();
  if (deployedBotId && deployedBotId.toLowerCase() !== botId.toLowerCase()) {
    throw new Error(
      `Generated Teams manifest bot '${botId}' does not match deployed BOT_ID ` +
        `'${deployedBotId}'.`,
    );
  }

  return { externalId, version, botId };
}

async function reconcileTeamsApp(envFile: string): Promise<void> {
  const environmentName = process.env.AZURE_ENV_NAME ?? "dev";
  const teamId =
    process.env.TEAMS_GROUP_ID?.trim() ||
    getEnvSuiteValue(environmentName, "teamsGroupId");
  const tenantId =
    process.env.TEAMS_APP_TENANT_ID?.trim() ||
    readEnvValue(envFile, "TEAMS_APP_TENANT_ID") ||
    getEnvSuiteValue(environmentName, "teamsAppTenantId");
  const manifest = readGeneratedManifest();

  if (!teamId || !tenantId) {
    throw new Error(
      "Cannot reconcile the Teams app: TEAMS_GROUP_ID or TEAMS_APP_TENANT_ID is missing.",
    );
  }

  const options = {
    teamId,
    teamsAppExternalId: manifest.externalId,
    tenantId,
    expectedBotId: manifest.botId,
    expectedVersion: manifest.version,
    log,
  };
  const status = await getTeamsAppInstallationStatus(options);
  const explicitPublish = process.env.TEAMS_PUBLISH === "1";
  const driftRequiresPublish = status.state !== "current";

  if (status.state === "drifted") {
    log(
      `Installed Teams app drift detected: version ` +
        `'${status.installedVersion ?? "unknown"}' / bot ` +
        `'${status.installedBotId ?? "unknown"}'; expected version ` +
        `'${manifest.version}' / bot '${manifest.botId}'.`,
    );
  } else if (status.state === "not-installed") {
    log(`Teams app '${manifest.externalId}' is not installed in team '${teamId}'.`);
  }

  if (explicitPublish || driftRequiresPublish) {
    log(
      explicitPublish
        ? "TEAMS_PUBLISH=1 - publishing the Teams app package."
        : "Publishing the Teams app package because the team installation is absent or stale.",
    );
    runTeamsapp("publish");
  }

  await installTeamsAppInTeam({
    ...options,
  });
}

/**
 * Sync the Teams app registration + manifest via Teams Toolkit, then install
 * it into the configured team through Microsoft Graph.
 *
 * `teamsapp provision` requires an interactive Microsoft 365 sign-in. When that
 * can't complete, or Graph permissions/catalog publication are unavailable,
 * it should NOT fail the whole `azd deploy frontend`: the container image and
 * App Service have already been deployed successfully by the predeploy hook.
 * Set TEAMS_PROVISION_STRICT=1 when incomplete Teams setup must fail deployment.
 *
 * Controls:
 *   AZD_SKIP_TEAMS_PROVISION=1  — skip the teamsapp step entirely.
 *   TEAMS_PROVISION_STRICT=1    — treat teamsapp failures as fatal (e.g. CI that
 *                                 pre-authenticates M365 and wants a hard fail).
 *   TEAMS_PUBLISH=1             — publish even when the installed definition is
 *                                 already current. Drift publishes automatically.
 */
async function provisionTeamsApp(): Promise<void> {
  if (process.env.AZD_SKIP_TEAMS_PROVISION === "1") {
    log("AZD_SKIP_TEAMS_PROVISION=1 — skipping teamsapp provision/publish.");
    return;
  }

  const strict = process.env.TEAMS_PROVISION_STRICT === "1";
  try {
    const envFile = resolve(TEAMS_PROJECT_ROOT, "env", `.env.${TEAMS_ENV}`);
    if (!existsSync(envFile)) {
      throw new Error(
        `Teams env file '${envFile}' not found. Run \`azd provision\` first so its ` +
          `frontend hook generates env/.env.${TEAMS_ENV}.`,
      );
    }
    runTeamsapp("provision");
    await reconcileTeamsApp(envFile);
  } catch (err) {
    if (strict) throw err;
    log("");
    log("WARNING: Teams app setup did not complete (the container image + App");
    log("Service were still deployed successfully). This usually means the");
    log("Microsoft 365 sign-in, app-catalog publication, or Graph permissions");
    log("are incomplete. Retry Teams Toolkit provisioning manually with:");
    log(`  (cd ${TEAMS_PROJECT_ROOT} && npx @microsoft/teamsapp-cli provision --env ${TEAMS_ENV})`);
    log("Set TEAMS_PROVISION_STRICT=1 to make this a hard failure instead.");
    log(`  reason: ${(err as Error).message}`);
  }
}

(async () => {
  log("Starting frontend postdeploy");
  // Run the interactive teamsapp step first so its Microsoft 365 browser
  // sign-in prompt appears immediately and is never blocked by a slow-starting
  // container in the (non-fatal) health check below.
  await provisionTeamsApp();
  await healthCheck();
  log("Done.");
})().catch((err) => {
  console.error(`[frontend:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
