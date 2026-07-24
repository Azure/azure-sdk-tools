/**
 * Standalone quota checker — runs the same verification as
 * hooks/preprovision.ts without requiring `azd provision`.
 *
 * Usage (from tools/sdk-ai-bots/deployment/):
 *
 *   npx tsx scripts/check-quotas.ts
 *   npx tsx scripts/check-quotas.ts --subscription <id> --location <region> [--env <name>]
 *   npm run check-quotas
 *
 * Defaults: reads AZURE_SUBSCRIPTION_ID / AZURE_LOCATION / AZURE_ENV_NAME from
 * the process env (populated by `azd env select` locally or by the pipeline).
 *
 * Exit codes:
 *   0  — all quotas below limit (warnings may be printed)
 *   1  — at least one quota exhausted, or required inputs missing
 */

import { execSync } from "child_process";

import { runQuotaCheck } from "../hooks/lib/quota-check.ts";

type Args = {
  subscription: string;
  location: string;
  env: string;
};

/**
 * Reads the currently-selected `azd` environment's variables (stored in
 * .azure/<env>/.env) so this CLI works the same as an azd hook without
 * requiring the caller to `source` any file. Returns an empty record if
 * azd isn't installed or no env is selected.
 */
function loadAzdEnvValues(): Record<string, string> {
  try {
    const raw = execSync("azd env get-values", {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    const out: Record<string, string> = {};
    for (const line of raw.split(/\r?\n/)) {
      const m = /^([A-Z0-9_]+)="?(.*?)"?$/.exec(line.trim());
      if (m) out[m[1]] = m[2];
    }
    return out;
  } catch {
    return {};
  }
}

function parseArgs(argv: string[]): Args {
  const azdEnv = loadAzdEnvValues();
  const args: Args = {
    subscription:
      process.env.AZURE_SUBSCRIPTION_ID ?? azdEnv.AZURE_SUBSCRIPTION_ID ?? "",
    location: process.env.AZURE_LOCATION ?? azdEnv.AZURE_LOCATION ?? "",
    env: process.env.AZURE_ENV_NAME ?? azdEnv.AZURE_ENV_NAME ?? "",
  };
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    const next = argv[i + 1];
    switch (flag) {
      case "--subscription":
      case "-s":
        args.subscription = next ?? "";
        i++;
        break;
      case "--location":
      case "-l":
        args.location = next ?? "";
        i++;
        break;
      case "--env":
      case "-e":
        args.env = next ?? "";
        i++;
        break;
      case "--help":
      case "-h":
        printUsageAndExit(0);
    }
  }
  return args;
}

function printUsageAndExit(code: number): never {
  const usage = [
    "Usage: npx tsx scripts/check-quotas.ts [options]",
    "",
    "Reads defaults from process env, then falls back to `azd env get-values`",
    "for the currently-selected azd environment.",
    "",
    "Options:",
    "  -s, --subscription <id>   Azure subscription ID (default: $AZURE_SUBSCRIPTION_ID | azd env)",
    "  -l, --location <region>   Azure region (default: $AZURE_LOCATION | azd env)",
    "  -e, --env <name>          Environment name for the retarget hint (default: $AZURE_ENV_NAME | azd env)",
    "  -h, --help                Show this help",
    "",
    "Exit codes: 0 = ok, 1 = quota exhausted / missing input.",
  ].join("\n");
  console.log(usage);
  process.exit(code);
}

const { subscription, location, env } = parseArgs(process.argv.slice(2));

if (!subscription) {
  console.error("[check-quotas] AZURE_SUBSCRIPTION_ID not set (or --subscription not provided).");
  process.exit(1);
}
if (!location) {
  console.error("[check-quotas] AZURE_LOCATION not set (or --location not provided).");
  process.exit(1);
}

console.log(`[check-quotas] subscription=${subscription} location=${location} env=${env || "(none)"}`);
console.log(`[check-quotas] Checking resource quotas...`);

const result = runQuotaCheck({ subscriptionId: subscription, location, envName: env });

for (const p of result.unreachable) {
  console.log(`  ${p} @ ${location}: unable to query usages (skipped)`);
}
for (const w of result.warnings) {
  console.log(`  ⚠ ${w}`);
}

if (result.ok) {
  console.log("[check-quotas] ✓ quota check passed");
  process.exit(0);
}

console.error("[check-quotas] " + result.message);
process.exit(1);
