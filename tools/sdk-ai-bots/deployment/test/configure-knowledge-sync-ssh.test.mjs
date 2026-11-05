import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  configureKnowledgeSyncSsh,
  normalizePrivateKey,
} from "../scripts/configure-knowledge-sync-ssh.mjs";

const key = [
  "-----BEGIN OPENSSH PRIVATE KEY-----",
  "fixture",
  "-----END OPENSSH PRIVATE KEY-----",
].join("\n");

test("normalizes raw and base64 SSH private keys", () => {
  const expected = Buffer.from(`${key}\n`).toString("base64");
  assert.equal(normalizePrivateKey(key), expected);
  assert.equal(
    normalizePrivateKey(Buffer.from(key).toString("base64")),
    expected,
  );
});

test("passes the SSH setting through a private settings file", () => {
  const root = mkdtempSync(join(tmpdir(), "knowledge-sync-ssh-test-"));
  const keyPath = join(root, "key");
  writeFileSync(keyPath, key, { mode: 0o600 });
  let command = [];
  let settings = {};
  try {
    configureKnowledgeSyncSsh(
      {
        SSH_KEY_FILE: keyPath,
        APP_NAME: "backend",
        RESOURCE_GROUP: "group",
      },
      {
        execFileSync: (_executable, args) => {
          command = args;
          const settingsArg = args[args.indexOf("--settings") + 1];
          settings = JSON.parse(readFileSync(settingsArg.slice(1), "utf8"));
        },
        log: () => {},
      },
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }

  assert.equal(
    settings.SSH_PRIVATE_KEY,
    Buffer.from(`${key}\n`).toString("base64"),
  );
  assert.match(command[command.indexOf("--settings") + 1], /^@/);
  assert.doesNotMatch(JSON.stringify(command), /fixture/);
});
