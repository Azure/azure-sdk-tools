/**
 * deploy-logic-app — apply the Logic App workflow definition & parameters
 * to an existing environment via ARM PATCH, independently of
 * `azd deploy function-app`.
 *
 * Usage (from the deployment/ folder):
 *   npm run deploy:logic-app -- -e dev
 *   npm run deploy:logic-app -- --env preview
 *
 * The script resolves the target azd environment, loads its outputs with
 * `azd env get-values`, exports them into process.env, and calls the
 * shared patchWorkflow() helper. Requires that:
 *   - `azd provision` has been run for the environment (populates the .env).
 *   - `azd deploy function-app` has been run at least once (so the Function
 *     App host can validate the workflow's `function.id` reference).
 */

import { execSync } from "child_process";

import { patchWorkflow } from "../hooks/lib/patch-workflow.js";

function log(msg: string): void {
  console.log(`[deploy-logic-app] ${msg}`);
}

function parseArgs(argv: string[]): { envName: string | undefined } {
  let envName: string | undefined;
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "-e" || a === "--env" || a === "--environment") {
      envName = argv[++i];
    } else if (a?.startsWith("--env=")) {
      envName = a.slice("--env=".length);
    } else if (a === "-h" || a === "--help") {
      console.log("Usage: deploy-logic-app [-e <env-name>]");
      console.log("Applies the Logic App workflow definition to the target azd environment.");
      process.exit(0);
    }
  }
  return { envName };
}

/**
 * Load `azd env get-values -e <env>` into process.env. Values are printed
 * as KEY="value" lines; we strip surrounding quotes and unescape \n.
 */
function loadAzdEnvValues(envName?: string): void {
  const envFlag = envName ? ` -e "${envName}"` : "";
  const output = execSync(`azd env get-values${envFlag}`, { encoding: "utf8" });

  for (const rawLine of output.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) continue;
    const eq = line.indexOf("=");
    if (eq === -1) continue;

    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if (
      value.length >= 2 &&
      value.startsWith('"') &&
      value.endsWith('"')
    ) {
      value = value
        .slice(1, -1)
        .replace(/\\"/g, '"')
        .replace(/\\\\/g, "\\")
        .replace(/\\n/g, "\n");
    }
    if (!process.env[key]) {
      process.env[key] = value;
    }
  }
}

(async () => {
  const { envName } = parseArgs(process.argv.slice(2));
  const resolvedEnv = envName ?? process.env.AZURE_ENV_NAME;
  if (resolvedEnv) {
    log(`Target environment: ${resolvedEnv}`);
  } else {
    log("No environment specified — using azd default (see .azure/config.json).");
  }

  log("Loading azd env values...");
  loadAzdEnvValues(envName);

  await patchWorkflow({ logPrefix: "[deploy-logic-app]" });
  log("Done.");
})().catch((err) => {
  console.error(`[deploy-logic-app] FAILED: ${err.message}`);
  process.exit(1);
});
