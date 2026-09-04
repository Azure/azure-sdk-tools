import assert from "node:assert/strict";
import {
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { execFileSync } from "node:child_process";
import test from "node:test";
import { fileURLToPath, pathToFileURL } from "node:url";

import {
  loadCase,
  materializeWorkspaces,
  parseArgs,
  validateSparseRoots,
} from "./materialize-workspaces.mjs";

const evalRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const testRoot = join(evalRoot, ".workspaces");

function git(cwd, args) {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

function createFixture() {
  mkdirSync(testRoot, { recursive: true });
  const root = mkdtempSync(join(testRoot, "materializer-test-"));
  const repository = join(root, "remote");
  mkdirSync(repository);
  git(repository, ["init"]);
  git(repository, ["config", "user.email", "eval@example.invalid"]);
  git(repository, ["config", "user.name", "Eval Test"]);
  git(repository, ["config", "uploadpack.allowFilter", "true"]);
  git(repository, ["config", "uploadpack.allowAnySHA1InWant", "true"]);
  const sparseRoot =
    "specification/network/resource-manager/Microsoft.Network/Network";
  const project = `${sparseRoot}/Network`;
  const requiredPackages = [
    "@typespec/compiler",
    "@typespec/openapi3",
    "@azure-tools/typespec-autorest",
    "@azure-tools/typespec-azure-resource-manager",
    "@azure-tools/typespec-client-generator-core",
  ];
  mkdirSync(join(repository, project), { recursive: true });
  mkdirSync(join(repository, "unrelated"), { recursive: true });
  writeFileSync(join(repository, ".gitignore"), "node_modules/\n");
  writeFileSync(
    join(repository, "package.json"),
    `${JSON.stringify({ private: true, engines: { node: "*" } }, null, 2)}\n`,
  );
  writeFileSync(
    join(repository, "package-lock.json"),
    `${JSON.stringify(
      {
        name: "sparse-pilot-fixture",
        lockfileVersion: 3,
        packages: Object.fromEntries([
          ["", { name: "sparse-pilot-fixture" }],
          ...requiredPackages.map((name) => [
            `node_modules/${name}`,
            { version: "1.0.0" },
          ]),
        ]),
      },
      null,
      2,
    )}\n`,
  );
  writeFileSync(join(repository, project, "main.tsp"), "model Widget {}\n");
  writeFileSync(join(repository, "unrelated", "large.txt"), "not sparse\n");
  git(repository, ["add", "."]);
  git(repository, ["commit", "-m", "base"]);
  const baseCommit = git(repository, ["rev-parse", "HEAD"]);
  writeFileSync(
    join(repository, project, "main.tsp"),
    "model Widget {\n  name: string;\n}\n",
  );
  git(repository, ["add", "."]);
  git(repository, ["commit", "-m", "head"]);
  const headCommit = git(repository, ["rev-parse", "HEAD"]);
  git(repository, ["update-ref", "refs/pull/44988/head", headCommit]);
  const casesPath = join(root, "cases.json");
  writeFileSync(
    casesPath,
    `${JSON.stringify(
      [
        {
          pr: 44988,
          title: "fixture",
          baseCommit,
          headCommit,
          sparseRoots: [sparseRoot],
          projects: [project],
          expected: {},
        },
      ],
      null,
      2,
    )}\n`,
  );
  return {
    root,
    repositoryPath: repository,
    repository: pathToFileURL(repository).href,
    output: join(root, "output"),
    casesPath,
    sparseRoot,
    project,
    baseCommit,
    headCommit,
  };
}

test("requires one explicit known case", () => {
  assert.throws(() => parseArgs([]), /--case is required/);
  assert.throws(() => loadCase(undefined), /--case is required/);
  assert.throws(() => loadCase("999999"), /Unknown case/);
  assert.throws(() => loadCase("all"), /one explicit --case/);
});

test("falls back to the exact head when the pull ref moved", () => {
  const fixture = createFixture();
  try {
    git(fixture.repositoryPath, [
      "update-ref",
      "refs/pull/44988/head",
      fixture.baseCommit,
    ]);
    const result = materializeWorkspaces({
      case: 44988,
      repository: fixture.repository,
      output: fixture.output,
      casesPath: fixture.casesPath,
    });
    assert.equal(
      result.phases.fetch.headSource,
      "commit-after-pull-ref-mismatch",
    );
    assert.equal(
      git(result.layout.head, ["rev-parse", "HEAD"]),
      fixture.headCommit,
    );
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});

test("validates project containment within sparse roots", () => {
  assert.deepEqual(
    validateSparseRoots({
      pr: 1,
      sparseRoots: ["specification/network"],
      projects: ["specification/network/Network"],
    }),
    {
      sparseRoots: ["specification/network"],
      projects: ["specification/network/Network"],
    },
  );
  assert.throws(
    () =>
      validateSparseRoots({
        pr: 1,
        sparseRoots: ["specification/network"],
        projects: ["specification/storage"],
      }),
    /outside its declared sparse roots/,
  );
});

test("declares sparse roots for every historical case", () => {
  for (const pr of [
    42435, 42853, 43308, 43745, 44200, 44454, 44742, 44882, 44988, 45348, 45536,
  ]) {
    const testCase = loadCase(String(pr));
    assert.ok(testCase.sparseRoots.length > 0);
    validateSparseRoots(testCase);
  }
});

test("materializes detached base and head sparse workspaces and safely reuses them", () => {
  const fixture = createFixture();
  try {
    const first = materializeWorkspaces({
      case: 44988,
      repository: fixture.repository,
      output: fixture.output,
      casesPath: fixture.casesPath,
    });
    assert.equal(
      git(dirname(first.layout.source), [
        "--git-dir",
        first.layout.source,
        "rev-parse",
        "--is-bare-repository",
      ]),
      "true",
    );
    assert.equal(
      git(dirname(first.layout.source), [
        "--git-dir",
        first.layout.source,
        "config",
        "--get",
        "remote.origin.partialclonefilter",
      ]),
      "blob:none",
    );
    assert.equal(
      git(dirname(first.layout.source), [
        "--git-dir",
        first.layout.source,
        "tag",
        "--list",
      ]),
      "",
    );
    assert.equal(
      git(first.layout.base, ["rev-parse", "HEAD"]),
      fixture.baseCommit,
    );
    assert.equal(
      git(first.layout.head, ["rev-parse", "HEAD"]),
      fixture.headCommit,
    );
    assert.equal(
      git(first.layout.head, [
        "merge-base",
        fixture.baseCommit,
        fixture.headCommit,
      ]),
      fixture.baseCommit,
    );
    assert.equal(first.phases.fetch.mergeBase, fixture.baseCommit);
    assert.equal(first.phases.fetch.historyDeepened, true);
    assert.equal(git(first.layout.base, ["branch", "--show-current"]), "");
    assert.equal(git(first.layout.head, ["branch", "--show-current"]), "");
    assert.equal(
      git(first.layout.head, ["sparse-checkout", "list"]),
      fixture.sparseRoot,
    );
    assert.equal(
      git(first.layout.head, [
        "diff",
        "--name-only",
        fixture.baseCommit,
        fixture.headCommit,
        "--",
        "package.json",
        "package-lock.json",
      ]),
      "",
    );
    assert.equal(
      readFileSync(join(first.layout.base, "package-lock.json"), "utf8"),
      readFileSync(join(first.layout.head, "package-lock.json"), "utf8"),
    );
    assert.equal(existsSync(join(first.layout.base, "node_modules")), false);
    assert.equal(existsSync(join(first.layout.head, "node_modules")), false);
    assert.deepEqual(
      first.packageFiles.map(({ path }) => path),
      ["package.json", "package-lock.json"],
    );
    for (const file of first.packageFiles) {
      assert.ok(file.blob);
      assert.ok(existsSync(file.basePath));
      assert.ok(existsSync(file.headPath));
    }
    assert.ok(existsSync(join(first.layout.head, fixture.project, "main.tsp")));
    assert.equal(
      existsSync(join(first.layout.head, "unrelated", "large.txt")),
      false,
    );
    assert.equal(first.status, "succeeded");
    assert.ok(first.storage.totalBytes > 0);
    assert.ok(first.phases.sourceInit.storageBytes > 0);
    assert.ok(first.phases.fetch.storageBytes > 0);
    assert.ok(first.phases.baseWorkspace.storageBytes > 0);
    assert.ok(first.phases.headWorkspace.storageBytes > 0);

    const second = materializeWorkspaces({
      case: 44988,
      repository: fixture.repository,
      output: fixture.output,
      casesPath: fixture.casesPath,
    });
    assert.equal(second.phases.sourceInit.reused, true);
    assert.equal(second.phases.fetch.reused, true);
    assert.equal(second.phases.baseWorkspace.reused, true);
    assert.equal(second.phases.headWorkspace.reused, true);

    git(second.layout.head, ["sparse-checkout", "set", "--cone", "unrelated"]);
    assert.throws(
      () =>
        materializeWorkspaces({
          case: 44988,
          repository: fixture.repository,
          output: fixture.output,
          casesPath: fixture.casesPath,
        }),
      /sparse roots are/,
    );
    const failed = JSON.parse(readFileSync(second.layout.manifest, "utf8"));
    assert.equal(failed.phases.headWorkspace.status, "failed");
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});
