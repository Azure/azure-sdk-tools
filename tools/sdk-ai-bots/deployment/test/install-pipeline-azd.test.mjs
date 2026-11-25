import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const fakeYq = String.raw`#!/usr/bin/env node
console.log(process.env.AZD_CONSTRAINT);
`;
const fakeCli = String.raw`#!/usr/bin/env node
console.log("azd version " + process.env.DOWNLOADED_VERSION + " (test)");
`;
const fakeInstaller = String.raw`#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$INSTALL_LOG"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --install-folder) directory="$2"; shift 2 ;;
    *) shift ;;
  esac
done
cat > "$directory/azd" <<'CLI'
#!/usr/bin/env node
console.log("azd version " + process.env.DOWNLOADED_VERSION + " (test)");
CLI
chmod 755 "$directory/azd"
`;
const fakeCurl = `#!/usr/bin/env node
const fs = require("node:fs");
fs.appendFileSync(process.env.CURL_LOG, JSON.stringify(process.argv.slice(2)) + "\\n");
if (process.env.FAIL_DOWNLOAD === "1") {
  console.error("HTTP 504");
  process.exit(22);
}
process.stdout.write(${JSON.stringify(fakeInstaller)});
`;

function runInstaller({ existingVersion, ...extraEnv } = {}) {
  const temporary = mkdtempSync(join(tmpdir(), "pipeline-azd-test-"));
  const tooling = join(temporary, "tooling");
  const installLog = join(temporary, "install.log");
  const curlLog = join(temporary, "curl.log");
  mkdirSync(tooling);
  writeFileSync(join(temporary, "yq"), fakeYq, { mode: 0o700 });
  writeFileSync(join(temporary, "curl"), fakeCurl, { mode: 0o700 });
  writeFileSync(installLog, "");
  writeFileSync(curlLog, "");
  if (existingVersion) {
    writeFileSync(join(tooling, "azd"), fakeCli.replace("process.env.DOWNLOADED_VERSION", JSON.stringify(existingVersion)), { mode: 0o700 });
  }
  try {
    const result = spawnSync("bash", [
      resolve(root, "scripts/install-pipeline-azd.sh"), join(temporary, "azure.yaml"), tooling,
    ], {
      encoding: "utf8",
      env: {
        PATH: `${temporary}:${process.env.PATH}`,
        AZD_CONSTRAINT: "=1.37.3",
        DOWNLOADED_VERSION: "1.37.3",
        INSTALL_LOG: installLog,
        CURL_LOG: curlLog,
        TF_BUILD: "True",
        ...extraEnv,
      },
    });
    if (result.error) throw result.error;
    return {
      ...result,
      tooling,
      installArgs: readFileSync(installLog, "utf8"),
      downloaded: readFileSync(curlLog, "utf8").trim() !== "",
      hasBinary: existsSync(join(tooling, "azd")),
    };
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

test("installs the exact CLI in the job directory and propagates its PATH", () => {
  const result = runInstaller();
  assert.equal(result.status, 0, result.stderr);
  assert.ok(result.downloaded);
  assert.ok(result.hasBinary);
  assert.ok(result.installArgs.includes("--version 1.37.3"));
  assert.ok(result.installArgs.includes(`--install-folder ${result.tooling}`));
  assert.ok(result.installArgs.includes(`--symlink-folder ${result.tooling}`));
  assert.ok(result.stdout.includes(`##vso[task.prependpath]${result.tooling}`));
});

test("reuses a matching job-local CLI without downloading", () => {
  const result = runInstaller({ existingVersion: "1.37.3" });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.downloaded, false);
  assert.ok(result.stdout.includes("azd version 1.37.3"));
});

for (const version of ["1.34.2", "1.38.0"]) {
  test(`replaces job-local ${version} instead of ignoring the exact pin`, () => {
    const result = runInstaller({ existingVersion: version });
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.downloaded);
    assert.ok(result.stdout.includes("azd version 1.37.3"));
  });
}

test("rejects a non-exact CLI requirement before downloading", () => {
  const result = runInstaller({ AZD_CONSTRAINT: ">=1.37.0" });
  assert.notEqual(result.status, 0);
  assert.equal(result.downloaded, false);
});

test("download failures do not fall back to an older job-local CLI", () => {
  const result = runInstaller({ existingVersion: "1.34.2", FAIL_DOWNLOAD: "1" });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /HTTP 504/);
  assert.ok(!result.stdout.includes("task.prependpath"));
});

test("unexpected installed versions fail before PATH propagation", () => {
  const result = runInstaller({ DOWNLOADED_VERSION: "1.37.2" });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Expected azd 1\.37\.3.*got '1\.37\.2'/);
  assert.ok(!result.stdout.includes("task.prependpath"));
});
