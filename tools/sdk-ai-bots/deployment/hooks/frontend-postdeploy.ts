/**
 * frontend — postdeploy hook
 *
 * Runs as part of `azd deploy frontend`, after azd has pushed the container
 * image and repointed the App Service (see frontend-predeploy.ts). It:
 *   1. Health-checks the bot front-end (reads the site URL from azd outputs), and
 *   2. Drives Teams Toolkit with the azd-owned environment: `teamsapp provision
 *      --env azd` (and, when TEAMS_PUBLISH=1, `teamsapp publish --env azd`).
 *
 * The `.env.azd` file is generated during `azd provision`
 * (deployment/hooks/lib/sync-teams-env.ts) from whichever logical environment
 * azd targeted (dev / preview / prod), so the whole flow is just:
 *   azd provision       →  writes env/.env.azd
 *   azd deploy frontend →  builds/pushes image + `teamsapp ... --env azd`
 *
 * Azure resource provisioning is owned by azd (the former teamsapp `arm/deploy`
 * step was removed), so `teamsapp provision` here only creates/updates the Teams
 * app registration + manifest in the Developer Portal.
 */

import { execSync } from "child_process";
import { existsSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

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

/**
 * Sync the Teams app registration + manifest via Teams Toolkit using the
 * azd-owned env file.
 *
 * `teamsapp provision` requires an interactive Microsoft 365 sign-in. When that
 * can't complete (e.g. running unattended, or the account is blocked by a
 * conditional-access policy) it should NOT fail the whole `azd deploy frontend`
 * — the container image + App Service have already been deployed successfully
 * by the predeploy hook. So teamsapp failures are logged as a warning with the
 * manual command to run, and the deploy still succeeds.
 *
 * Controls:
 *   AZD_SKIP_TEAMS_PROVISION=1  — skip the teamsapp step entirely.
 *   TEAMS_PROVISION_STRICT=1    — treat teamsapp failures as fatal (e.g. CI that
 *                                 pre-authenticates M365 and wants a hard fail).
 *   TEAMS_PUBLISH=1             — also run `teamsapp publish` (higher-impact).
 */
function provisionTeamsApp(): void {
  if (process.env.AZD_SKIP_TEAMS_PROVISION === "1") {
    log("AZD_SKIP_TEAMS_PROVISION=1 — skipping teamsapp provision/publish.");
    return;
  }

  const envFile = resolve(TEAMS_PROJECT_ROOT, "env", `.env.${TEAMS_ENV}`);
  if (!existsSync(envFile)) {
    throw new Error(
      `Teams env file '${envFile}' not found. Run \`azd provision\` first so its ` +
        `postprovision hook generates env/.env.${TEAMS_ENV}.`,
    );
  }

  const strict = process.env.TEAMS_PROVISION_STRICT === "1";
  try {
    runTeamsapp("provision");
    // Publishing to the Teams Admin Center is a separate, higher-impact step;
    // opt in with TEAMS_PUBLISH=1 (e.g. for prod rollouts).
    if (process.env.TEAMS_PUBLISH === "1") {
      runTeamsapp("publish");
    }
  } catch (err) {
    if (strict) throw err;
    log("");
    log("WARNING: teamsapp step did not complete (the container image + App");
    log("Service were still deployed successfully). This usually means the");
    log("interactive Microsoft 365 sign-in could not complete. Once you can sign");
    log("in, finish the Teams app registration manually with:");
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
  provisionTeamsApp();
  await healthCheck();
  log("Done.");
})().catch((err) => {
  console.error(`[frontend:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
