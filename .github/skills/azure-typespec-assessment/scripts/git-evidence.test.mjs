import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";
import {
  collectChanges,
  createSparseWorktree,
  deriveServiceRoot,
  normalizeSparseRoots,
  resolveComparison,
} from "./git-evidence.mjs";

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

test("normalizes explicit sparse roots without collapsing them", () => {
  assert.deepEqual(
    normalizeSparseRoots(
      [
        "specification/recoveryservicesbackup/",
        "specification\\recoveryservices",
        "specification/recoveryservices",
      ],
      "specification",
    ),
    ["specification/recoveryservices", "specification/recoveryservicesbackup"],
  );
  assert.deepEqual(
    normalizeSparseRoots(
      undefined,
      "specification/widget/resource-manager/Widget",
    ),
    ["specification/widget"],
  );
});

test("creates a worktree with multiple sparse roots", () => {
  const repo = fs.mkdtempSync(
    path.join(os.tmpdir(), "typespec-sparse-source-"),
  );
  const worktreeRoot = fs.mkdtempSync(
    path.join(os.tmpdir(), "typespec-sparse-worktree-"),
  );
  const destination = path.join(worktreeRoot, "checkout");
  try {
    git(repo, "init", "-q");
    git(repo, "config", "user.email", "test@example.com");
    git(repo, "config", "user.name", "Test");
    for (const service of ["one", "two", "excluded"]) {
      const directory = path.join(repo, "specification", service);
      fs.mkdirSync(directory, { recursive: true });
      fs.writeFileSync(
        path.join(directory, "main.tsp"),
        `namespace ${service};\n`,
      );
    }
    git(repo, "add", ".");
    git(repo, "commit", "-qm", "base");
    const commit = git(repo, "rev-parse", "HEAD");

    const roots = createSparseWorktree(
      repo,
      commit,
      ["specification/one", "specification/two"],
      destination,
    );

    assert.deepEqual(roots, ["specification/one", "specification/two"]);
    assert.equal(
      fs.existsSync(path.join(destination, "specification", "one", "main.tsp")),
      true,
    );
    assert.equal(
      fs.existsSync(path.join(destination, "specification", "two", "main.tsp")),
      true,
    );
    assert.equal(
      fs.existsSync(
        path.join(destination, "specification", "excluded", "main.tsp"),
      ),
      false,
    );
  } finally {
    spawnSync("git", [
      "-C",
      repo,
      "worktree",
      "remove",
      "--force",
      destination,
    ]);
    fs.rmSync(repo, { recursive: true, force: true });
    fs.rmSync(worktreeRoot, { recursive: true, force: true });
  }
});
