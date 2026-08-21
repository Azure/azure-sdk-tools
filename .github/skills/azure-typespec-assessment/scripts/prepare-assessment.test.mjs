import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  realpathSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";

import {
  canonicalTempDirectory,
  discoverProjectRoots,
  ensureNodeWithNpm,
  findTspCommand,
  findTypeSpecValidationCommand,
  linkDependencies,
  listChangedFiles,
  parseArgs,
  parseSourceHunks,
  preflightToolchain,
  prepareAssessment,
  readEmitterOptions,
  resolveBaseline,
  satisfiesNodeEngine,
  summarizeCompilerFailure,
  summarizeValidationFailure,
  sourceLink,
  untrackedReferences,
  writeEmitterConfig,
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
      "--skip-validation",
    ]),
    {
      repo: "r",
      base: "main",
      output: "o",
      skipCompile: true,
      skipValidation: true,
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
      discoverProjectRoots(
        repo,
        listChangedFiles(repo, baseCommit),
        baseCommit,
      ),
      ["spec"],
    );
  } finally {
    rmSync(repo, { recursive: true, force: true });
  }
});

test("project discovery ignores parent configs without main.tsp", () => {
  const { repo, baseCommit } = createRepository();
  try {
    mkdirSync(join(repo, "container", "child"), { recursive: true });
    writeFileSync(join(repo, "container", "tspconfig.yaml"), "emit: []\n");
    writeFileSync(
      join(repo, "container", "child", "tspconfig.yaml"),
      "emit: []\n",
    );
    writeFileSync(
      join(repo, "container", "child", "main.tsp"),
      "model Child {}\n",
    );
    writeFileSync(
      join(repo, "container", "child", "change.tsp"),
      "model Change {}\n",
    );
    assert.deepEqual(
      discoverProjectRoots(
        repo,
        listChangedFiles(repo, baseCommit),
        baseCommit,
      ),
      ["container/child"],
    );
  } finally {
    rmSync(repo, { recursive: true, force: true });
  }
});

test("project discovery retains a baseline project when main.tsp is deleted", () => {
  const { repo, baseCommit } = createRepository();
  try {
    rmSync(join(repo, "spec", "main.tsp"));
    assert.deepEqual(
      discoverProjectRoots(
        repo,
        listChangedFiles(repo, baseCommit),
        baseCommit,
      ),
      ["spec"],
    );
  } finally {
    rmSync(repo, { recursive: true, force: true });
  }
});

test("Node engine checks support the repository range forms", () => {
  assert.equal(satisfiesNodeEngine("v24.1.0", ">=20.0.0 <25.0.0"), true);
  assert.equal(satisfiesNodeEngine("v18.20.0", ">=20.0.0"), false);
  assert.equal(satisfiesNodeEngine("v24.1.0", "^24.0.0"), true);
  assert.equal(satisfiesNodeEngine("v20.1.0", "20.x"), true);
  assert.equal(satisfiesNodeEngine("v22.0.0", "20 - 24"), true);
  assert.equal(satisfiesNodeEngine("v25.0.0", "20 - 24"), false);
  assert.equal(satisfiesNodeEngine("v24.8.0", "~24"), true);
  assert.equal(satisfiesNodeEngine("v24.2.0", "=24"), true);
});

test("toolchain preflight validates the TCGC emitter version", () => {
  const root = mkdtempSync(join(tmpdir(), "typespec-assessment-preflight-"));
  try {
    writeFileSync(
      join(root, "package.json"),
      JSON.stringify({ engines: { node: "*" } }),
    );
    writeFileSync(
      join(root, "package-lock.json"),
      JSON.stringify({
        packages: {
          "node_modules/@azure-tools/typespec-client-generator-core": {
            version: "1.0.0",
          },
        },
      }),
    );
    assert.throws(
      () => preflightToolchain(root),
      /@azure-tools\/typespec-client-generator-core@1\.0\.0 is not installed/,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("TypeSpec CLI runs through the active Node executable", () => {
  const root = mkdtempSync(join(tmpdir(), "typespec-assessment-cli-"));
  const compiler = join(root, "node_modules", "@typespec", "compiler");
  try {
    mkdirSync(join(compiler, "cmd"), { recursive: true });
    writeFileSync(
      join(compiler, "package.json"),
      JSON.stringify({ bin: { tsp: "cmd/tsp.js" }, version: "1.0.0" }),
    );
    writeFileSync(join(compiler, "cmd", "tsp.js"), "");
    const command = findTspCommand(root, root);
    assert.equal(command.command, process.execPath);
    assert.deepEqual(command.prefix, [join(compiler, "cmd", "tsp.js")]);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("dependency linking is limited to the checkout root", () => {
  const root = mkdtempSync(join(tmpdir(), "typespec-assessment-dependencies-"));
  const source = join(root, "source");
  const checkout = join(root, "checkout");
  try {
    mkdirSync(join(source, "node_modules"), { recursive: true });
    mkdirSync(checkout, { recursive: true });
    linkDependencies(source, checkout);
    assert.ok(existsSync(join(checkout, "node_modules")));
    assert.equal(
      existsSync(join(checkout, "specification", "service", "node_modules")),
      false,
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("assessment temp paths use the canonical Windows path", () => {
  assert.equal(
    canonicalTempDirectory(),
    process.platform === "win32" ? realpathSync.native(tmpdir()) : tmpdir(),
  );
});

test("repository TypeSpec Validation CLI runs through the active Node executable", () => {
  const root = mkdtempSync(join(tmpdir(), "typespec-assessment-tsv-"));
  try {
    const script = join(
      root,
      "eng",
      "tools",
      "typespec-validation",
      "cmd",
      "tsv.js",
    );
    mkdirSync(join(root, "eng", "tools", "typespec-validation", "cmd"), {
      recursive: true,
    });
    writeFileSync(script, "");
    assert.deepEqual(findTypeSpecValidationCommand(root), {
      command: process.execPath,
      prefix: [script],
    });
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("standalone Node runtimes receive the active npm installation", () => {
  const root = mkdtempSync(join(tmpdir(), "typespec-assessment-node-runtime-"));
  try {
    const sourceNode = join(root, "source", "node.exe");
    const npmCli = join(root, "npm", "bin", "npm-cli.js");
    mkdirSync(dirname(sourceNode), { recursive: true });
    mkdirSync(dirname(npmCli), { recursive: true });
    writeFileSync(sourceNode, "node");
    writeFileSync(npmCli, "npm");
    const originalExecPath = process.execPath;
    Object.defineProperty(process, "execPath", {
      configurable: true,
      value: sourceNode,
    });
    try {
      const runtimeNode = ensureNodeWithNpm(join(root, "temp"), npmCli);
      assert.ok(existsSync(runtimeNode));
      assert.ok(
        existsSync(
          join(
            dirname(runtimeNode),
            "node_modules",
            "npm",
            "bin",
            "npm-cli.js",
          ),
        ),
      );
    } finally {
      Object.defineProperty(process, "execPath", {
        configurable: true,
        value: originalExecPath,
      });
    }
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("compiler failure summaries include first diagnostic, count, and log", () => {
  const summary = summarizeCompilerFailure(
    [
      "spec/main.tsp:4:3 - error invalid-type: First diagnostic",
      "spec/main.tsp:8:3 - error invalid-type: Second diagnostic",
      "Found 2 errors.",
    ].join("\n"),
    "compile-logs/spec-head-autorest.log",
  );
  assert.equal(
    summary,
    "spec/main.tsp:4:3 - error invalid-type: First diagnostic (2 errors; log: compile-logs/spec-head-autorest.log)",
  );
});

test("repository validation failure summaries retain the diagnostic and log", () => {
  assert.equal(
    summarizeValidationFailure(
      [
        "Executing rule: Compile",
        "spec/main.tsp:4:3 - warning no-legacy-usage: Legacy usage is not allowed.",
        "Rule Compile failed",
      ].join("\n"),
      "validation-logs/spec-head.log",
    ),
    "spec/main.tsp:4:3 - warning no-legacy-usage: Legacy usage is not allowed. (log: validation-logs/spec-head.log)",
  );
});

test("generated emitter configs preserve project options and safe overrides", () => {
  const root = mkdtempSync(join(tmpdir(), "typespec-assessment-config-"));
  const original = join(root, "tspconfig.yaml");
  const generated = join(root, "generated.yaml");
  try {
    writeFileSync(
      original,
      [
        "emit:",
        '  - "@azure-tools/typespec-autorest"',
        "options:",
        '  "@azure-tools/typespec-autorest":',
        "    output-splitting: legacy-feature-files",
        '    output-file: "old.json"',
        "    omit-unreachable-types: true",
        "",
      ].join("\n"),
    );
    assert.deepEqual(
      readEmitterOptions(
        readFileSync(original, "utf8"),
        "@azure-tools/typespec-autorest",
      ).map((entry) => entry.key),
      ["output-splitting", "output-file", "omit-unreachable-types"],
    );
    writeEmitterConfig(
      generated,
      original,
      "@azure-tools/typespec-autorest",
      join(root, "artifacts"),
    );
    const config = readFileSync(generated, "utf8");
    assert.match(config, /output-splitting: legacy-feature-files/);
    assert.match(config, /omit-unreachable-types: true/);
    assert.match(
      config,
      /output-file: "\{version-status\}\/\{version\}\/\{feature\}\.json"/,
    );
    assert.doesNotMatch(config, /old\.json|service-yaml/);

    writeEmitterConfig(
      generated,
      original,
      "@azure-tools/typespec-client-generator-core",
      join(root, "tcgc"),
    );
    assert.match(readFileSync(generated, "utf8"), /api-version: "all"/);
  } finally {
    rmSync(root, { recursive: true, force: true });
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
    "def456",
  );
  assert.equal(references[0].revision, "head");
  assert.equal(
    references[0].link,
    "https://github.com/Azure/example/blob/def456/spec/main.tsp#L2-L3",
  );
  assert.equal(references[1].revision, "base");
  assert.equal(
    references[1].link,
    "https://github.com/Azure/example/blob/abc123/spec/main.tsp#L8-L10",
  );
});

test("source links fall back locally without a GitHub remote", () => {
  assert.equal(
    sourceLink("spec/main.tsp", "head", "def456", "", 2, 3),
    "spec/main.tsp#L2-L3",
  );
  assert.equal(
    sourceLink("spec/main.tsp", "base", "abc123", "", 8, 10),
    "abc123:spec/main.tsp#L8-L10",
  );
});

test("untracked source links use the head commit", () => {
  const { repo } = createRepository();
  try {
    writeFileSync(join(repo, "spec", "new.tsp"), "model NewModel {}\n");
    const references = untrackedReferences(
      repo,
      ["spec/new.tsp"],
      "def456",
      "https://github.com/Azure/example.git",
    );
    assert.equal(
      references[0].link,
      "https://github.com/Azure/example/blob/def456/spec/new.tsp#L1-L2",
    );
  } finally {
    rmSync(repo, { recursive: true, force: true });
  }
});

test("prepareAssessment creates source evidence without compilation", async () => {
  const { repo } = createRepository();
  const output = join(repo, "..", `${repo.split(/[\\/]/).at(-1)}-output`);
  try {
    writeFileSync(
      join(repo, "spec", "main.tsp"),
      'model Widget {\n  name?: string = "widget";\n}\n',
    );
    const evidence = await prepareAssessment({
      repo,
      base: "origin/main",
      output,
      skipCompile: true,
    });
    assert.equal(evidence.projects[0].path, "spec");
    assert.equal(evidence.compileSkipped, true);
    assert.equal(evidence.sourceReferences[0].path, "spec/main.tsp");
    assert.ok(evidence.durationMs >= 0);
    assert.equal(
      evidence.complianceAssessment.agenticSearchProcedure,
      ".github/skills/azure-typespec-author/references/agentic-search.md",
    );
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
