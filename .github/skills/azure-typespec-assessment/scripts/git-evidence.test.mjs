import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";
import { collectChanges, deriveServiceRoot, resolveComparison } from "./git-evidence.mjs";

function git(repo, ...args) {
  const result = spawnSync("git", ["-C", repo, ...args], { encoding: "utf8" });
  assert.equal(result.status, 0, result.stderr);
  return result.stdout.trim();
}

test("collectChanges combines committed, staged, unstaged, and untracked TypeSpec", () => {
  const repo = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-git-"));
  git(repo, "init", "-q");
  git(repo, "config", "user.email", "test@example.com");
  git(repo, "config", "user.name", "Test");
  fs.mkdirSync(path.join(repo, "specification", "widget"), { recursive: true });
  fs.writeFileSync(path.join(repo, "specification", "widget", "main.tsp"), "model A {}\n");
  fs.writeFileSync(path.join(repo, "specification", "widget", "staged.tsp"), "model B {}\n");
  git(repo, "add", ".");
  git(repo, "commit", "-qm", "base");
  const base = git(repo, "rev-parse", "HEAD");

  fs.appendFileSync(path.join(repo, "specification", "widget", "main.tsp"), "model C {}\n");
  fs.appendFileSync(path.join(repo, "specification", "widget", "staged.tsp"), "model D {}\n");
  git(repo, "add", "specification/widget/staged.tsp");
  fs.writeFileSync(path.join(repo, "specification", "widget", "new.tsp"), "model E {}\n");

  const comparison = resolveComparison(repo, base);
  const changes = collectChanges(repo, comparison.mergeBaseCommit, "specification/widget");
  assert.deepEqual(
    changes.map((item) => [item.path, item.origins]),
    [
      ["specification/widget/main.tsp", ["unstaged"]],
      ["specification/widget/new.tsp", ["untracked"]],
      ["specification/widget/staged.tsp", ["staged"]],
    ],
  );
});

test("deriveServiceRoot rejects paths outside specification", () => {
  assert.equal(deriveServiceRoot("specification"), "specification");
  assert.equal(
    deriveServiceRoot("specification/widget/resource-manager/Widget"),
    "specification/widget",
  );
  assert.throws(() => deriveServiceRoot("tools/widget"), /specification/);
});
