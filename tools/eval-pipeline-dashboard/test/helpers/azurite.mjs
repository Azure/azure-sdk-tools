import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

export async function startAzurite(t) {
  const directory = await mkdtemp(join(tmpdir(), "vally-azurite-"));
  const account = "dashboardtest";
  const key = randomBytes(32).toString("base64");
  const env = { ...process.env, AZURITE_ACCOUNTS: `${account}:${key}` };
  // Do not let a non-test service inherit Node's internal test-runner context.
  delete env.NODE_TEST_CONTEXT;
  const child = spawn(process.execPath, [resolve(import.meta.dirname, "azurite-server.mjs"), directory], {
    stdio: ["ignore", "pipe", "pipe", "ipc"],
    env,
  });
  const exited = new Promise((done) => child.once("exit", done));
  t.after(async () => {
    if (child.exitCode === null) {
      child.send("shutdown", () => {});
      const force = setTimeout(() => child.kill(), 5000);
      force.unref();
      await exited;
      clearTimeout(force);
    }
    await rm(directory, { recursive: true, force: true });
  });
  let output = "";
  const endpoints = await new Promise((ready, reject) => {
    // The pinned emulator has a large dependency tree; cold Windows loads can
    // exceed one minute. Bound startup separately from individual storage calls.
    const timeout = setTimeout(() => reject(new Error(`Azurite startup timed out: ${output.slice(-3000)}`)), 120_000);
    const fail = () => { clearTimeout(timeout); reject(new Error(`Azurite exited before startup: ${output.slice(-3000)}`)); };
    child.once("error", reject);
    child.once("exit", fail);
    child.stderr.on("data", (data) => { output += data; });
    child.stdout.on("data", (data) => { output += data; });
    child.on("message", (message) => {
      if (message.phase) output += `phase=${message.phase}\n`;
      if (message.error) { clearTimeout(timeout); reject(new Error(message.error)); }
      if (message.ready) {
        clearTimeout(timeout); child.off("exit", fail); ready({ blob: message.blob, queue: message.queue });
      }
    });
  });
  return { ...endpoints, account, key };
}