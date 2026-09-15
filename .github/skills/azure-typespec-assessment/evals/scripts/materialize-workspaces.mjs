#!/usr/bin/env node

import {
  existsSync,
  lstatSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const evidenceRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultRepository = "https://github.com/Azure/azure-rest-api-specs.git";

function git(cwd, args) {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

function gitBare(repository, args) {
  return git(dirname(repository), ["--git-dir", repository, ...args]);
}

function requiredValue(argv, index, argument) {
  const value = argv[index + 1];
  if (!value || value.startsWith("--")) {
    throw new Error(`${argument} requires a value`);
  }
  return value;
}

export function parseArgs(argv) {
  const options = {
    repository: defaultRepository,
    output: join(evidenceRoot, ".workspaces"),
  };
  for (let index = 0; index < argv.length; index += 1) {
    if (argv[index] === "--repository") {
      options.repository = requiredValue(argv, index, argv[index]);
      index += 1;
    } else if (argv[index] === "--case") {
      options.case = requiredValue(argv, index, argv[index]);
      index += 1;
    } else if (argv[index] === "--output") {
      options.output = resolve(requiredValue(argv, index, argv[index]));
      index += 1;
    } else {
      throw new Error(`Unknown argument: ${argv[index]}`);
    }
  }
  if (!options.case) {
    throw new Error("--case is required during the sparse workspace pilot");
  }
  return options;
}

function normalizeRepository(repository) {
  if (/^[a-z][a-z\d+.-]*:\/\//i.test(repository)) return repository;
  return resolve(repository);
}

function normalizeRepoPath(value, description) {
  if (typeof value !== "string" || value.length === 0) {
    throw new Error(`${description} must be a non-empty repository path`);
  }
  const normalized = value.replaceAll("\\", "/").replace(/\/+$/, "");
  const segments = normalized.split("/");
  if (
    normalized.startsWith("/") ||
    /^[a-z]:\//i.test(normalized) ||
    segments.some(
      (segment) => segment === "" || segment === "." || segment === "..",
    )
  ) {
    throw new Error(
      `${description} must be a normalized relative repository path: ${value}`,
    );
  }
  return normalized;
}

export function validateSparseRoots(testCase) {
  if (
    !Array.isArray(testCase.sparseRoots) ||
    testCase.sparseRoots.length === 0
  ) {
    throw new Error(
      `PR ${testCase.pr} must declare at least one sparseRoots entry`,
    );
  }
  if (!Array.isArray(testCase.projects) || testCase.projects.length === 0) {
    throw new Error(`PR ${testCase.pr} must declare at least one project`);
  }
  const sparseRoots = testCase.sparseRoots.map((root, index) =>
    normalizeRepoPath(root, `PR ${testCase.pr} sparseRoots[${index}]`),
  );
  if (new Set(sparseRoots).size !== sparseRoots.length) {
    throw new Error(`PR ${testCase.pr} contains duplicate sparse roots`);
  }
  const projects = testCase.projects.map((project, index) =>
    normalizeRepoPath(project, `PR ${testCase.pr} projects[${index}]`),
  );
  for (const project of projects) {
    if (
      !sparseRoots.some(
        (root) => project === root || project.startsWith(`${root}/`),
      )
    ) {
      throw new Error(
        `PR ${testCase.pr} project ${project} is outside its declared sparse roots`,
      );
    }
  }
  return { sparseRoots, projects };
}

export function loadCase(caseId, casesPath = join(evidenceRoot, "cases.json")) {
  if (caseId === undefined || caseId === null || String(caseId).length === 0) {
    throw new Error("--case is required during the sparse workspace pilot");
  }
  if (String(caseId) === "all") {
    throw new Error(
      "The sparse workspace pilot requires one explicit --case; 'all' is not supported",
    );
  }
  const cases = JSON.parse(readFileSync(resolve(casesPath), "utf8"));
  const selected = cases.filter(({ pr }) => String(pr) === String(caseId));
  if (selected.length === 0) throw new Error(`Unknown case: ${caseId}`);
  if (selected.length > 1) throw new Error(`Duplicate case: ${caseId}`);
  const testCase = selected[0];
  const { sparseRoots, projects } = validateSparseRoots(testCase);
  return { ...testCase, sparseRoots, projects };
}

function requireDirectory(path, description) {
  if (!lstatSync(path).isDirectory()) {
    throw new Error(`${description} is not a directory: ${path}`);
  }
}

function gitConfig(cwd, name) {
  try {
    return gitBare(cwd, ["config", "--get", name]);
  } catch {
    return undefined;
  }
}

function readRef(source, ref) {
  try {
    return gitBare(source, ["show-ref", "--verify", "--hash", ref]);
  } catch {
    return undefined;
  }
}

function verifyRef(source, ref, expectedCommit) {
  return readRef(source, ref) === expectedCommit;
}

function initializeSource(source, repository) {
  if (existsSync(source)) {
    requireDirectory(source, "Partial object store");
    if (gitBare(source, ["rev-parse", "--is-bare-repository"]) !== "true") {
      throw new Error(`Existing partial object store is not bare: ${source}`);
    }
    const configuredRepository = gitConfig(source, "remote.origin.url");
    if (configuredRepository !== repository) {
      throw new Error(
        `${source} uses remote ${configuredRepository ?? "<missing>"}; expected ${repository}`,
      );
    }
    if (
      gitConfig(source, "remote.origin.promisor") !== "true" ||
      gitConfig(source, "remote.origin.partialclonefilter") !== "blob:none"
    ) {
      throw new Error(
        `${source} is not configured as a blob:none promisor remote`,
      );
    }
    return { reused: true };
  }

  git(dirname(source), ["init", "--bare", source]);
  gitBare(source, ["remote", "add", "origin", repository]);
  gitBare(source, ["config", "remote.origin.promisor", "true"]);
  gitBare(source, ["config", "remote.origin.partialclonefilter", "blob:none"]);
  return { reused: false };
}

function fetchCase(source, testCase) {
  const baseRef = `refs/eval/${testCase.pr}/base`;
  const headRef = `refs/eval/${testCase.pr}/head`;
  const existingBase = verifyRef(source, baseRef, testCase.baseCommit);
  const existingHead = verifyRef(source, headRef, testCase.headCommit);
  let headSource = "existing";
  let historyDeepened = false;

  if (!existingBase) {
    gitBare(source, [
      "fetch",
      "--depth=1",
      "--no-tags",
      "--no-write-fetch-head",
      "--filter=blob:none",
      "origin",
      `+${testCase.baseCommit}:${baseRef}`,
    ]);
  }
  if (!existingHead) {
    try {
      gitBare(source, [
        "fetch",
        "--depth=1",
        "--no-tags",
        "--no-write-fetch-head",
        "--filter=blob:none",
        "origin",
        `+refs/pull/${testCase.pr}/head:${headRef}`,
      ]);
      headSource = "pull-ref";
      if (readRef(source, headRef) !== testCase.headCommit) {
        gitBare(source, [
          "fetch",
          "--depth=1",
          "--no-tags",
          "--no-write-fetch-head",
          "--filter=blob:none",
          "origin",
          `+${testCase.headCommit}:${headRef}`,
        ]);
        headSource = "commit-after-pull-ref-mismatch";
      }
    } catch {
      gitBare(source, [
        "fetch",
        "--depth=1",
        "--no-tags",
        "--no-write-fetch-head",
        "--filter=blob:none",
        "origin",
        `+${testCase.headCommit}:${headRef}`,
      ]);
      headSource = "commit";
    }
  }

  for (const [ref, expected] of [
    [baseRef, testCase.baseCommit],
    [headRef, testCase.headCommit],
  ]) {
    const actual = readRef(source, ref);
    if (actual !== expected) {
      throw new Error(
        `${ref} resolved to ${actual ?? "<missing>"}; expected ${expected}`,
      );
    }
    gitBare(source, ["cat-file", "-e", `${expected}^{commit}`]);
  }
  let mergeBase;
  for (const deepenBy of [64, 256, 1024]) {
    try {
      mergeBase = gitBare(source, ["merge-base", headRef, baseRef]);
      break;
    } catch {
      gitBare(source, [
        "fetch",
        `--deepen=${deepenBy}`,
        "--no-tags",
        "--no-write-fetch-head",
        "--filter=blob:none",
        "origin",
        `+${testCase.baseCommit}:${baseRef}`,
        `+${testCase.headCommit}:${headRef}`,
      ]);
      historyDeepened = true;
    }
  }
  if (!mergeBase) {
    try {
      mergeBase = gitBare(source, ["merge-base", headRef, baseRef]);
    } catch {
      throw new Error(
        `Unable to resolve a merge base between ${testCase.baseCommit} and ${testCase.headCommit} after deepening sparse history`,
      );
    }
  }
  return {
    reused: Boolean(existingBase && existingHead && !historyDeepened),
    baseRef,
    headRef,
    headSource,
    mergeBase,
    historyDeepened,
  };
}

function sparseList(workspace) {
  return git(workspace, ["sparse-checkout", "list"])
    .split(/\r?\n/)
    .filter(Boolean)
    .map((root) => root.replaceAll("\\", "/"))
    .sort();
}

function sameValues(left, right) {
  return (
    left.length === right.length &&
    left.every((value, index) => value === right[index])
  );
}

function verifyWorkspace(workspace, source, commit, sparseRoots) {
  requireDirectory(workspace, "Sparse workspace");
  const actualCommit = git(workspace, ["rev-parse", "HEAD"]);
  if (actualCommit !== commit) {
    throw new Error(`${workspace} is at ${actualCommit}; expected ${commit}`);
  }
  const expectedRoots = [...sparseRoots].sort();
  const actualRoots = sparseList(workspace);
  if (!sameValues(actualRoots, expectedRoots)) {
    throw new Error(
      `${workspace} sparse roots are ${JSON.stringify(actualRoots)}; expected ${JSON.stringify(expectedRoots)}`,
    );
  }
  const commonDirectory = resolve(
    workspace,
    git(workspace, ["rev-parse", "--git-common-dir"]),
  );
  if (resolve(commonDirectory) !== resolve(source)) {
    throw new Error(`${workspace} is not attached to ${source}`);
  }
  const status = git(workspace, ["status", "--porcelain"]);
  if (status) {
    throw new Error(`${workspace} contains unexpected working tree changes`);
  }
}

function verifySharedPackageFiles(baseWorkspace, headWorkspace) {
  const files = ["package.json", "package-lock.json"];
  return files.map((file) => {
    const basePath = join(baseWorkspace, file);
    const headPath = join(headWorkspace, file);
    if (!existsSync(basePath) || !existsSync(headPath)) {
      throw new Error(
        `Sparse pilot requires ${file} at the root of both base and head workspaces`,
      );
    }
    const baseBlob = git(baseWorkspace, ["rev-parse", `HEAD:${file}`]);
    const headBlob = git(headWorkspace, ["rev-parse", `HEAD:${file}`]);
    if (baseBlob !== headBlob) {
      throw new Error(
        `${file} differs between base and head and would contaminate the assessed diff`,
      );
    }
    return { path: file, blob: baseBlob };
  });
}

function materializeWorkspace(workspace, source, ref, commit, sparseRoots) {
  if (existsSync(workspace)) {
    verifyWorkspace(workspace, source, commit, sparseRoots);
    return { reused: true, path: workspace, commit, sparseRoots };
  }
  gitBare(source, [
    "worktree",
    "add",
    "--detach",
    "--no-checkout",
    workspace,
    ref,
  ]);
  git(workspace, ["sparse-checkout", "set", "--cone", ...sparseRoots]);
  git(workspace, ["checkout", "--detach", commit]);
  verifyWorkspace(workspace, source, commit, sparseRoots);
  return { reused: false, path: workspace, commit, sparseRoots };
}

function directoryBytes(path) {
  let total = 0;
  for (const entry of readdirSync(path, { withFileTypes: true })) {
    const entryPath = join(path, entry.name);
    if (entry.isDirectory()) {
      total += directoryBytes(entryPath);
    } else {
      total += statSync(entryPath).size;
    }
  }
  return total;
}

function readManifest(path) {
  if (!existsSync(path)) return undefined;
  return JSON.parse(readFileSync(path, "utf8"));
}

function writeManifest(path, manifest) {
  manifest.updatedAt = new Date().toISOString();
  writeFileSync(path, `${JSON.stringify(manifest, null, 2)}\n`);
}

function verifyManifest(manifest, expected) {
  if (!manifest) return;
  for (const [name, value] of Object.entries(expected)) {
    if (JSON.stringify(manifest[name]) !== JSON.stringify(value)) {
      throw new Error(
        `Existing materialization manifest has unexpected ${name}: ${JSON.stringify(manifest[name])}`,
      );
    }
  }
}

function runPhase(manifest, manifestPath, name, action) {
  const startedAt = process.hrtime.bigint();
  manifest.phases[name] = { status: "running" };
  writeManifest(manifestPath, manifest);
  try {
    const result = action();
    manifest.phases[name] = {
      status: "succeeded",
      elapsedMs: Math.round(
        Number(process.hrtime.bigint() - startedAt) / 1_000_000,
      ),
      ...result,
    };
    writeManifest(manifestPath, manifest);
    return result;
  } catch (error) {
    manifest.phases[name] = {
      status: "failed",
      elapsedMs: Math.round(
        Number(process.hrtime.bigint() - startedAt) / 1_000_000,
      ),
      error: error.message,
    };
    writeManifest(manifestPath, manifest);
    throw error;
  }
}

export function materializeWorkspaces(options) {
  const testCase = loadCase(options.case, options.casesPath);
  const repository = normalizeRepository(
    options.repository ?? defaultRepository,
  );
  const output = resolve(options.output ?? join(evidenceRoot, ".workspaces"));
  const caseRoot = join(output, String(testCase.pr));
  const source = join(caseRoot, "source.git");
  const baseWorkspace = join(caseRoot, "base");
  const headWorkspace = join(caseRoot, "head");
  const manifestPath = join(caseRoot, "materialization-manifest.json");
  mkdirSync(caseRoot, { recursive: true });

  const identity = {
    pr: testCase.pr,
    repository,
    baseCommit: testCase.baseCommit,
    headCommit: testCase.headCommit,
    sparseRoots: testCase.sparseRoots,
    projects: testCase.projects,
  };
  verifyManifest(readManifest(manifestPath), identity);
  const manifest = {
    schemaVersion: 1,
    ...identity,
    resumable: true,
    layout: {
      root: caseRoot,
      source,
      base: baseWorkspace,
      head: headWorkspace,
      manifest: manifestPath,
    },
    phases: {},
  };
  writeManifest(manifestPath, manifest);

  runPhase(manifest, manifestPath, "sourceInit", () => ({
    ...initializeSource(source, repository),
    storageBytes: directoryBytes(source),
  }));
  const fetched = runPhase(manifest, manifestPath, "fetch", () => ({
    ...fetchCase(source, testCase),
    storageBytes: directoryBytes(source),
  }));
  runPhase(manifest, manifestPath, "baseWorkspace", () => {
    const workspace = materializeWorkspace(
      baseWorkspace,
      source,
      fetched.baseRef,
      testCase.baseCommit,
      testCase.sparseRoots,
    );
    return {
      ...workspace,
      storageBytes: directoryBytes(baseWorkspace),
    };
  });
  runPhase(manifest, manifestPath, "headWorkspace", () => {
    const workspace = materializeWorkspace(
      headWorkspace,
      source,
      fetched.headRef,
      testCase.headCommit,
      testCase.sparseRoots,
    );
    return {
      ...workspace,
      storageBytes: directoryBytes(headWorkspace),
    };
  });
  manifest.packageFiles = runPhase(
    manifest,
    manifestPath,
    "packageVerification",
    () => ({
      files: verifySharedPackageFiles(baseWorkspace, headWorkspace).map(
        (file) => ({
          ...file,
          basePath: join(baseWorkspace, file.path),
          headPath: join(headWorkspace, file.path),
        }),
      ),
    }),
  ).files;

  manifest.storage = {
    measuredAt: new Date().toISOString(),
    sourceBytes: directoryBytes(source),
    baseWorkspaceBytes: directoryBytes(baseWorkspace),
    headWorkspaceBytes: directoryBytes(headWorkspace),
  };
  manifest.storage.totalBytes =
    manifest.storage.sourceBytes +
    manifest.storage.baseWorkspaceBytes +
    manifest.storage.headWorkspaceBytes;
  manifest.status = "succeeded";
  writeManifest(manifestPath, manifest);
  return manifest;
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  try {
    const manifest = materializeWorkspaces(parseArgs(process.argv.slice(2)));
    process.stdout.write(`${JSON.stringify(manifest, null, 2)}\n`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
