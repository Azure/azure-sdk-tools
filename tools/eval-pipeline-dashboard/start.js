// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Startup bootstrap for Azure App Service (Linux, Oryx).
//
// Oryx compresses the built dependencies into `node_modules.tar.gz` and, at
// runtime, extracts them to a shared location referenced via NODE_PATH. That
// works for CommonJS `require()` but **ESM `import` ignores NODE_PATH**, so this
// ESM app ("type":"module") would fail with ERR_MODULE_NOT_FOUND before any of
// our code runs.
//
// To fix it deterministically, this tiny bootstrap — which imports only Node
// built-ins and therefore loads even when node_modules is empty — extracts the
// tarball *in place* into ./node_modules (where ESM resolution looks) the first
// time, then dynamically imports the real server. The extraction lands on the
// App Service persistent share (/home), so subsequent restarts skip it.
//
// Locally (after `npm install`) node_modules already exists, so this is a no-op.
import { existsSync, mkdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const marker = resolve(
  __dirname,
  "node_modules/@microsoft/vally-server/package.json",
);
const tarball = resolve(__dirname, "node_modules.tar.gz");

if (!existsSync(marker) && existsSync(tarball)) {
  const dest = resolve(__dirname, "node_modules");
  mkdirSync(dest, { recursive: true });
  console.error(
    "[bootstrap] node_modules is compressed; extracting node_modules.tar.gz " +
      "in place so ESM imports resolve...",
  );
  execFileSync("tar", ["-xzf", tarball, "-C", dest], { stdio: "inherit" });
  console.error("[bootstrap] node_modules ready.");
}

// Ensure the vally-server performance patch is applied even if node_modules was
// compressed by Oryx before the postinstall hook ran. Idempotent no-op locally.
await import("./scripts/patch-vally.mjs");

await import("./server.js");
