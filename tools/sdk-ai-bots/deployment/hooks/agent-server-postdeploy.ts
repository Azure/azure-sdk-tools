import { execSync } from "child_process";

import { ensureAgentServerAuthorization } from "./lib/ensure-agent-server-authorization.js";

function log(message: string): void {
  console.log(`[agent-server:postdeploy] ${message}`);
}

function loadAzdEnv(): void {
  try {
    const raw = execSync("azd env get-values", { encoding: "utf8" });
    for (const line of raw.split(/\r?\n/)) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith("#")) continue;
      const separator = trimmed.indexOf("=");
      if (separator === -1) continue;
      const key = trimmed.slice(0, separator).trim();
      let value = trimmed.slice(separator + 1).trim();
      if (value.startsWith('"') && value.endsWith('"')) {
        value = value.slice(1, -1).replace(/\\"/g, '"').replace(/\\\\/g, "\\").replace(/\\n/g, "\n");
      }
      if (!process.env[key]) process.env[key] = value;
    }
  } catch {
    log("Unable to load azd environment values; using the current process environment.");
  }
}

try {
  loadAzdEnv();
  ensureAgentServerAuthorization(process.env, log);
} catch (error) {
  console.error(`[agent-server:postdeploy] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}