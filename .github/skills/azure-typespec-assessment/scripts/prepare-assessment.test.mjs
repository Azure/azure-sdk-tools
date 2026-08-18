import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  discoverProjectRoots,
  listChangedFiles,
  parseArgs,
  parseSourceHunks,
  prepareAssessment,
  resolveBaseline,
} from "./prepare-assessment.mjs";

function git(repo, ...args) {
  return execFileSync("git", args, { cwd: repo, encoding: "utf8" }).trim();
}

function createRepository() {
  const repo = mkdtempSync(join(tmpdir(), "typespec-assessment-test-"));
  git(repo, "init", "-b", "main");
  git(repo, "config", "user.email", "test@example.com");
  git(repo, "config", "user.name", "Test User");
  mkdirSync(join(repo, "spec"), { recursive: true });
  writeFileSync(join(repo, "spec", "tspconfig.yaml"), "emit: []\n");
  writeFileSync(
    join(repo, "spec", "main.tsp"),
    "model Widget {\n  name?: string;\n}\n",
  );
  git(repo, "add", ".");
  git(repo, "commit", "-m", "base");
  const baseCommit = git(repo, "rev-parse", "HEAD");
  git(repo, "branch", "-M", "main");
  git(repo, "update-ref", "refs/remotes/origin/main", baseCommit);
  git(
    repo,
    "symbolic-ref",
    "refs/remotes/origin/HEAD",
    "refs/remotes/origin/main",
  );
  git(repo, "remote", "add", "origin", "https://github.com/Azure/example.git");
  git(repo, "switch", "-c", "feature");
  return { repo, baseCommit };
}

test("parseArgs accepts overrides", () => {
  assert.deepEqual(
    parseArgs([
      "--repo",
      "r",
      "--base",
      "main",
      "--output",
      "o",
      "--skip-compile",
    ]),
    {
      repo: "r",
      base: "main",
      output: "o",
      skipCompile: true,
    },
  );
});

test("resolveBaseline uses default remote head", () => {
  const { repo, baseCommit } = createRepository();
  try {
    assert.deepEqual(resolveBaseline(repo), {
      baseRef: "origin/main",
      baseCommit,
    });
  } finally {
    rmSync(repo, { recursive: true, force: true });
  }
});

test("changed files include tracked and untracked work", () => {
  const { repo, baseCommit } = createRepository();
  try {
    writeFileSync(
      join(repo, "spec", "main.tsp"),
      "model Widget {\n  name: string;\n}\n",
    );
    writeFileSync(join(repo, "spec", "extra.tsp"), "model Extra {}\n");
    assert.deepEqual(listChangedFiles(repo, baseCommit), [
      "spec/extra.tsp",
      "spec/main.tsp",
    ]);
    assert.deepEqual(
      discoverProjectRoots(repo, listChangedFiles(repo, baseCommit)),
      ["spec"],
    );
  } finally {
    rmSync(repo, { recursive: true, force: true });
  }
});

test("source hunks map additions and deletions to the correct revision", () => {
  const diff = [
    "diff --git a/spec/main.tsp b/spec/main.tsp",
    "--- a/spec/main.tsp",
    "+++ b/spec/main.tsp",
    "@@ -2,1 +2,2 @@",
    "@@ -8,3 +9,0 @@",
  ].join("\n");
  const references = parseSourceHunks(
    diff,
    "head",
    "abc123",
    "https://github.com/Azure/example.git",
  );
  assert.equal(references[0].revision, "head");
  assert.equal(references[0].link, "spec/main.tsp#L2-L3");
  assert.equal(references[1].revision, "base");
  assert.equal(
    references[1].link,
    "https://github.com/Azure/example/blob/abc123/spec/main.tsp#L8-L10",
  );
});

test("prepareAssessment creates source evidence without compilation", () => {
  const { repo } = createRepository();
  const output = join(repo, "..", `${repo.split(/[\\/]/).at(-1)}-output`);
  try {
    writeFileSync(
      join(repo, "spec", "main.tsp"),
      'model Widget {\n  name?: string = "widget";\n}\n',
    );
    const evidence = prepareAssessment({
      repo,
      base: "origin/main",
      output,
      skipCompile: true,
    });
    assert.equal(evidence.projects[0].path, "spec");
    assert.equal(evidence.compileSkipped, true);
    assert.equal(evidence.sourceReferences[0].path, "spec/main.tsp");
    assert.equal(
      JSON.parse(readFileSync(join(output, "evidence.json"), "utf8"))
        .schemaVersion,
      1,
    );
  } finally {
    rmSync(repo, { recursive: true, force: true });
    rmSync(output, { recursive: true, force: true });
  }
});

test("recent PR evidence contains ten source-linked cases", () => {
  const cases = JSON.parse(
    readFileSync(
      new URL("./fixtures/recent-pr-cases.json", import.meta.url),
      "utf8",
    ),
  );
  assert.equal(cases.length, 10);
  assert.deepEqual(
    cases.map((item) => item.pr),
    [43308, 44742, 43745, 42853, 44200, 44454, 44882, 45536, 42435, 45348],
  );
  assert.ok(
    cases.every(
      (item) =>
        item.sourceReferences.length > 0 &&
        item.sourceReferences.every(
          (source) =>
            source.path.endsWith(".tsp") &&
            source.link.includes(
              `/blob/${source.revision === "base" ? item.baseCommit : item.headCommit}/`,
            ) &&
            source.link.includes("#L"),
        ),
    ),
  );
  assert.ok(cases.some((item) => item.restFindings.length > 0));
  assert.ok(cases.some((item) => item.downstreamFindings.length > 0));
  assert.ok(cases.some((item) => item.errors.length > 0));
});

test("recent PR evidence includes LRO and paging operation cases", () => {
  const operations = JSON.parse(
    readFileSync(
      new URL("./fixtures/recent-pr-operations.json", import.meta.url),
      "utf8",
    ),
  );
  const allOperations = Object.values(operations).flat();
  assert.ok(
    allOperations.some((operation) => operation.lro.isLongRunning),
    "expected an LRO operation",
  );
  assert.ok(
    allOperations.some((operation) => operation.paging.isPaged),
    "expected a paging operation",
  );
  assert.ok(
    operations["42435"].some(
      (operation) => operation.lro.isLongRunning && operation.paging.isPaged,
    ),
    "expected PR 42435 to exercise combined LRO and paging behavior",
  );
});
