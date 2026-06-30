/**
 * frontend — postdeploy hook
 *
 * Health-check the bot front-end after `azd deploy frontend`. Reads the
 * site URL from azd outputs.
 */

const BOT_URL = process.env.SERVICE_FRONTEND_URI ?? process.env.BOT_URL ?? "";

function log(msg: string): void {
  console.log(`[frontend:postdeploy] ${msg}`);
}

async function healthCheck(): Promise<void> {
  if (!BOT_URL) {
    log("BOT_URL / SERVICE_FRONTEND_URI not set — skipping health check.");
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
  throw new Error("Frontend health check did not succeed within 2 minutes.");
}

(async () => {
  log("Starting frontend postdeploy");
  await healthCheck();
  log("Done.");
})().catch((err) => {
  console.error(`[frontend:postdeploy] FAILED: ${err.message}`);
  process.exit(1);
});

export {};
