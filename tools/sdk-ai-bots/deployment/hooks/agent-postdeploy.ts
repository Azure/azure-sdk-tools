/**
 * agent — postdeploy hook
 *
 * Sanity-check the hosted Foundry agent by pinging its Responses endpoint.
 */

const AGENT_BASE_URL = process.env.AGENT_BASE_URL ?? "";

function log(msg: string): void {
  console.log(`[agent:postdeploy] ${msg}`);
}

(async () => {
  if (!AGENT_BASE_URL) {
    log("AGENT_BASE_URL not set — skipping ping.");
    return;
  }
  log(`Pinging ${AGENT_BASE_URL}/ping`);
  const res = await fetch(`${AGENT_BASE_URL}/ping`);
  log(`  HTTP ${res.status}`);
  if (!res.ok) throw new Error(`Agent ping failed: HTTP ${res.status}`);
})().catch((err) => {
  console.error(`[agent:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
