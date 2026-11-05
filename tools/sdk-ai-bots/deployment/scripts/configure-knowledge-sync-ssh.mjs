import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const PRIVATE_KEY_PATTERN =
  /^-----BEGIN (OPENSSH|RSA|EC|DSA|ENCRYPTED)? ?PRIVATE KEY-----/;

export function normalizePrivateKey(value) {
  const trimmed = value.trim();
  const key = trimmed.startsWith("-----BEGIN ")
    ? trimmed
    : Buffer.from(trimmed, "base64").toString("utf8").trim();
  if (!PRIVATE_KEY_PATTERN.test(key)) {
    throw new Error("Invalid SSH private key format");
  }
  return Buffer.from(`${key}\n`).toString("base64");
}

export function configureKnowledgeSyncSsh(
  env = process.env,
  dependencies = {},
) {
  const run = dependencies.execFileSync ?? execFileSync;
  const log = dependencies.log ?? console.log;
  const sshKeyFile = env.SSH_KEY_FILE?.trim();
  const appName = env.APP_NAME?.trim();
  const resourceGroup = env.RESOURCE_GROUP?.trim();
  if (!sshKeyFile || !appName || !resourceGroup) {
    throw new Error(
      "SSH_KEY_FILE, APP_NAME, and RESOURCE_GROUP are required",
    );
  }

  let directory;
  try {
    const encodedKey = normalizePrivateKey(readFileSync(sshKeyFile, "utf8"));
    directory = mkdtempSync(join(tmpdir(), "knowledge-sync-ssh-"));
    const settingsPath = join(directory, "settings.json");
    writeFileSync(
      settingsPath,
      JSON.stringify({ SSH_PRIVATE_KEY: encodedKey }),
      { mode: 0o600 },
    );
    run(
      "az",
      [
        "webapp",
        "config",
        "appsettings",
        "set",
        "--name",
        appName,
        "--resource-group",
        resourceGroup,
        "--settings",
        `@${settingsPath}`,
        "--only-show-errors",
        "--output",
        "none",
      ],
      {
        stdio: ["ignore", "pipe", "pipe"],
        timeout: 120_000,
      },
    );
    log("Knowledge sync SSH setting configured.");
  } catch {
    throw new Error(
      "SSH setting configuration failed; check the secure file and App Service permissions.",
    );
  } finally {
    if (directory) rmSync(directory, { recursive: true, force: true });
  }
}

if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(resolve(process.argv[1])).href
) {
  try {
    configureKnowledgeSyncSsh();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
