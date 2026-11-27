import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { getPinnedPnpmVersion } from "./install-pnpm.ts";

test("reads pnpm's version from the separate specs checkout, including integrity suffixes", (t) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "pnpm-spec-pin-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const packageFile = path.join(root, "package.json");
  fs.writeFileSync(packageFile, JSON.stringify({ packageManager: "pnpm@12.6.0+sha512.test" }));
  assert.equal(getPinnedPnpmVersion(root), "12.6.0");
  for (const packageManager of [undefined, "npm@12.6.0", "pnpm@", "pnpm@12.6.0 & echo invalid"]) {
    fs.writeFileSync(packageFile, JSON.stringify({ packageManager }));
    assert.throws(() => getPinnedPnpmVersion(root), /Expected pnpm packageManager/);
  }
});
