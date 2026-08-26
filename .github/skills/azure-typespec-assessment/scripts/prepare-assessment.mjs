#!/usr/bin/env node

import { spawn, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  cpSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  realpathSync,
  readFileSync,
  readdirSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import {
  basename,
  dirname,
  isAbsolute,
  join,
  relative,
  resolve,
} from "node:path";
import { fileURLToPath } from "node:url";

import {
  parseTypeSpecDiffHunks,
  untrackedTypeSpecDiffHunk,
} from "./typespec-diff-hunks.mjs";
import { analyzeArtifacts } from "./analyze-artifacts.mjs";

const TYPE_SPEC_FILES = /\.(tsp)$/i;
const PROJECT_FILES = new Set([
  "tspconfig.yaml",
  "tspconfig.yml",
  "package.json",
  "package-lock.json",
  "pnpm-lock.yaml",
  "yarn.lock",
]);
const REQUIRED_PACKAGES = [
  "@typespec/compiler",
  "@typespec/openapi3",
  "@azure-tools/typespec-autorest",
  "@azure-tools/typespec-azure-resource-manager",
  "@azure-tools/typespec-client-generator-core",
];
const EMITTERS = [
  {
    name: "@azure-tools/typespec-autorest",
    id: "autorest",
  },
  {
    name: "@azure-tools/typespec-client-generator-core",
    id: "tcgc",
  },
];
const ARTIFACT_CACHE_VERSION = 1;

function elapsedMs(startedAt) {
  return Math.round(Number(process.hrtime.bigint() - startedAt) / 1_000_000);
}

export function artifactCacheKey({
  projectTree,
  lockHash,
  node,
  emitter,
  config,
}) {
  return createHash("sha256")
    .update(
      JSON.stringify({
        version: ARTIFACT_CACHE_VERSION,
        projectTree,
        lockHash,
        node,
        emitter,
        config,
      }),
    )
    .digest("hex");
}

export function restoreArtifactCache(cachePath, outputPath) {
  const cachedArtifacts = join(cachePath, "artifacts");
  if (!existsSync(cachedArtifacts)) return false;
  rmSync(outputPath, { recursive: true, force: true });
  cpSync(cachedArtifacts, outputPath, { recursive: true });
  return true;
}

export function storeArtifactCache(cachePath, outputPath) {
  const cachedArtifacts = join(cachePath, "artifacts");
  mkdirSync(cachePath, { recursive: true });
  rmSync(cachedArtifacts, { recursive: true, force: true });
  cpSync(outputPath, cachedArtifacts, { recursive: true });
}

function progress(message) {
  process.stderr.write(
    `[prepare-assessment ${new Date().toISOString()}] ${message}\n`,
  );
}

async function timedProgress(label, operation) {
  const startedAt = process.hrtime.bigint();
  progress(`${label} started`);
  try {
    const result = await operation();
    progress(
      `${label} completed in ${(elapsedMs(startedAt) / 1000).toFixed(1)}s`,
    );
    return result;
  } catch (error) {
    progress(`${label} failed in ${(elapsedMs(startedAt) / 1000).toFixed(1)}s`);
    throw error;
  }
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: options.cwd,
    env: options.env,
    encoding: "utf8",
    input: options.input,
    shell: process.platform === "win32" && /\.(cmd|bat)$/i.test(command),
  });
  if (!options.allowFailure && result.status !== 0) {
    const error = new Error(
      `${command} ${args.join(" ")} failed with exit code ${result.status}`,
    );
    error.stdout = result.stdout;
    error.stderr = result.stderr;
    throw error;
  }
  return result;
}

function runAsync(command, args, options = {}) {
  return new Promise((resolvePromise, reject) => {
    const child = spawn(command, args, {
      cwd: options.cwd,
      env: options.env,
      shell: false,
      windowsHide: true,
    });
    let stdout = "";
    let stderr = "";
    child.stdout?.setEncoding("utf8");
    child.stderr?.setEncoding("utf8");
    child.stdout?.on("data", (data) => {
      stdout += data;
    });
    child.stderr?.on("data", (data) => {
      stderr += data;
    });
    child.on("error", reject);
    child.on("close", (status) => {
      const result = { status, stdout, stderr };
      if (!options.allowFailure && status !== 0) {
        const error = new Error(
          `${command} ${args.join(" ")} failed with exit code ${status}`,
        );
        error.stdout = stdout;
        error.stderr = stderr;
        reject(error);
      } else {
        resolvePromise(result);
      }
    });
  });
}

function git(repoRoot, args, options = {}) {
  return run("git", args, { cwd: repoRoot, ...options });
}

function gitText(repoRoot, args, options = {}) {
  return git(repoRoot, args, options).stdout.trim();
}

export function parseArgs(argv) {
  const args = {
    repo: ".",
    output: ".typespec-assessment",
    artifactCache: join(
      canonicalTempDirectory(),
      "typespec-assessment-artifact-cache",
    ),
    checkoutCache: undefined,
    rawArtifactDiffs: false,
    skipCompile: false,
  };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === "--skip-compile") {
      args.skipCompile = true;
    } else if (value === "--raw-artifact-diffs") {
      args.rawArtifactDiffs = true;
    } else if (
      [
        "--repo",
        "--output",
        "--base",
        "--artifact-cache",
        "--checkout-cache",
      ].includes(value)
    ) {
      const next = argv[index + 1];
      if (!next) throw new Error(`${value} requires a value`);
      const key =
        value === "--artifact-cache"
          ? "artifactCache"
          : value === "--checkout-cache"
            ? "checkoutCache"
            : value.slice(2);
      args[key] = next;
      index += 1;
    } else {
      throw new Error(`Unknown argument: ${value}`);
    }
  }
  return args;
}

export function resolveBaseline(repoRoot, explicitBase) {
  let baseRef = explicitBase;
  if (!baseRef) {
    const upstream = git(
      repoRoot,
      ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"],
      { allowFailure: true },
    );
    if (upstream.status === 0 && upstream.stdout.trim()) {
      baseRef = upstream.stdout.trim();
    }
  }

  if (!baseRef) {
    const remotes = gitText(repoRoot, ["remote"])
      .split(/\r?\n/)
      .filter(Boolean);
    const ordered = [...new Set(["upstream", "origin", ...remotes])];
    for (const remote of ordered) {
      if (!remotes.includes(remote)) continue;
      const head = git(
        repoRoot,
        ["symbolic-ref", "--quiet", "--short", `refs/remotes/${remote}/HEAD`],
        { allowFailure: true },
      );
      if (head.status === 0 && head.stdout.trim()) {
        baseRef = head.stdout.trim();
        break;
      }
    }
  }

  if (!baseRef)
    throw new Error(
      "Unable to detect a tracked/default remote branch; pass --base.",
    );
  const baseCommit = gitText(repoRoot, ["merge-base", "HEAD", baseRef]);
  return { baseRef, baseCommit };
}

export function listChangedFiles(repoRoot, baseCommit) {
  const tracked = gitText(repoRoot, [
    "diff",
    "--name-only",
    "--diff-filter=ACMRD",
    baseCommit,
    "--",
  ])
    .split(/\r?\n/)
    .filter(Boolean);
  const untracked = gitText(repoRoot, [
    "ls-files",
    "--others",
    "--exclude-standard",
  ])
    .split(/\r?\n/)
    .filter(Boolean);
  return [...new Set([...tracked, ...untracked])]
    .filter((path) => !path.split("/").includes("node_modules"))
    .sort();
}

function isTypeSpecRelated(path) {
  return TYPE_SPEC_FILES.test(path) || PROJECT_FILES.has(basename(path));
}

export function discoverProjectRoots(repoRoot, changedFiles, baseCommit) {
  const roots = new Set();
  const candidates = new Set();
  for (const changedFile of changedFiles.filter(isTypeSpecRelated)) {
    let current = resolve(repoRoot, dirname(changedFile));
    while (current.startsWith(repoRoot)) {
      for (const name of ["tspconfig.yaml", "tspconfig.yml", "main.tsp"]) {
        candidates.add(
          relative(repoRoot, join(current, name)).replaceAll("\\", "/"),
        );
      }
      if (current === repoRoot) break;
      current = dirname(current);
    }
  }
  const candidatePaths = [...candidates].sort();
  const baselineFiles = new Set();
  if (baseCommit && candidatePaths.length > 0) {
    const result = git(repoRoot, ["cat-file", "--batch-check"], {
      input: candidatePaths
        .map((path) => `${baseCommit}:${path}`)
        .join("\n")
        .concat("\n"),
    });
    result.stdout.split(/\r?\n/).forEach((line, index) => {
      if (line.includes(" blob ")) baselineFiles.add(candidatePaths[index]);
    });
  }
  function baselineFileExists(path) {
    return baselineFiles.has(relative(repoRoot, path).replaceAll("\\", "/"));
  }
  for (const changedFile of changedFiles.filter(isTypeSpecRelated)) {
    let current = resolve(repoRoot, dirname(changedFile));
    if (PROJECT_FILES.has(basename(changedFile)))
      current = resolve(repoRoot, dirname(changedFile));
    while (current.startsWith(repoRoot)) {
      const headProject =
        (existsSync(join(current, "tspconfig.yaml")) ||
          existsSync(join(current, "tspconfig.yml"))) &&
        existsSync(join(current, "main.tsp"));
      const baseProject =
        (baselineFileExists(join(current, "tspconfig.yaml")) ||
          baselineFileExists(join(current, "tspconfig.yml"))) &&
        baselineFileExists(join(current, "main.tsp"));
      if (headProject || baseProject) {
        roots.add((relative(repoRoot, current) || ".").replaceAll("\\", "/"));
        break;
      }
      if (current === repoRoot) break;
      current = dirname(current);
    }
  }
  return [...roots].sort();
}

export function parseSourceHunks(
  diffText,
  revision,
  baseCommit,
  remoteUrl,
  headCommit,
  hasLocalTypeSpecChanges = false,
) {
  const references = [];
  let path;
  for (const line of diffText.split(/\r?\n/)) {
    if (line.startsWith("--- ")) {
      const value = line.slice(4).trim();
      if (value !== "/dev/null") path = value.replace(/^a\//, "");
      continue;
    }
    if (line.startsWith("+++ ")) {
      const value = line.slice(4).trim();
      if (value !== "/dev/null") path = value.replace(/^b\//, "");
      continue;
    }
    if (!line.startsWith("@@ ") || !path?.endsWith(".tsp")) continue;
    const match = line.match(/@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@/);
    if (!match) continue;
    const oldStart = Number(match[1]);
    const oldCount = Number(match[2] ?? 1);
    const newStart = Number(match[3]);
    const newCount = Number(match[4] ?? 1);
    const useBase = newCount === 0;
    const startLine = useBase ? oldStart : newStart;
    const count = useBase ? oldCount : newCount;
    const sourceRevision = useBase ? "base" : revision;
    const sourceCommit = useBase ? baseCommit : headCommit;
    references.push({
      path,
      revision: sourceRevision,
      startLine,
      endLine: Math.max(startLine, startLine + count - 1),
      link: sourceLink(
        path,
        sourceRevision,
        sourceCommit,
        remoteUrl,
        startLine,
        Math.max(startLine, startLine + count - 1),
        hasLocalTypeSpecChanges,
      ),
    });
  }
  return references;
}

function normalizeRemoteUrl(remoteUrl) {
  if (!remoteUrl) return undefined;
  const ssh = remoteUrl.match(/^git@github\.com:(.+?)(?:\.git)?$/);
  if (ssh) return `https://github.com/${ssh[1].replace(/\.git$/, "")}`;
  if (remoteUrl.startsWith("https://github.com/"))
    return remoteUrl.replace(/\.git$/, "");
  return undefined;
}

export function sourceLink(
  path,
  revision,
  commit,
  remoteUrl,
  startLine,
  endLine,
  hasLocalTypeSpecChanges = false,
) {
  const fragment = `#L${startLine}-L${endLine}`;
  if (revision === "head" && hasLocalTypeSpecChanges) {
    return `${path}${fragment}`;
  }
  const github = normalizeRemoteUrl(remoteUrl);
  if (github && commit) {
    return `${github}/blob/${commit}/${path}${fragment}`;
  }
  return revision === "head"
    ? `${path}${fragment}`
    : `${commit}:${path}${fragment}`;
}

export function untrackedReferences(
  repoRoot,
  changedFiles,
  headCommit,
  remoteUrl,
  untrackedFiles = untrackedTypeSpecFiles(repoRoot, changedFiles),
) {
  return untrackedFiles.map((path) => {
    const lines = readFileSync(join(repoRoot, path), "utf8").split(
      /\r?\n/,
    ).length;
    return {
      path,
      revision: "head",
      startLine: 1,
      endLine: lines,
      link: sourceLink(
        path,
        "head",
        headCommit,
        remoteUrl,
        1,
        lines,
        true,
      ),
    };
  });
}

function untrackedTypeSpecFiles(repoRoot, changedFiles) {
  const typeSpecFiles = changedFiles.filter((path) => path.endsWith(".tsp"));
  if (typeSpecFiles.length === 0) return [];
  const tracked = new Set(
    gitText(repoRoot, ["ls-files", "--", ...typeSpecFiles])
      .split(/\r?\n/)
      .filter(Boolean),
  );
  return typeSpecFiles.filter((path) => !tracked.has(path));
}

function safeProjectId(projectRoot) {
  return projectRoot === "."
    ? "repository-root"
    : projectRoot.replace(/[\\/]/g, "__");
}

function yamlKey(line) {
  const match = line.match(/^\s*(?:"([^"]+)"|'([^']+)'|([^:#]+))\s*:/);
  return match?.[1] ?? match?.[2] ?? match?.[3]?.trim();
}

export function readEmitterOptions(configText, emitter) {
  const lines = configText.split(/\r?\n/);
  const optionsIndex = lines.findIndex((line) =>
    /^\s*options\s*:\s*(?:#.*)?$/.test(line),
  );
  if (optionsIndex < 0) return [];
  const optionsIndent = lines[optionsIndex].match(/^\s*/)[0].length;
  let emitterIndex = -1;
  let emitterIndent;
  for (let index = optionsIndex + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (!line.trim() || line.trimStart().startsWith("#")) continue;
    const indent = line.match(/^\s*/)[0].length;
    if (indent <= optionsIndent) break;
    if (yamlKey(line) === emitter) {
      emitterIndex = index;
      emitterIndent = indent;
      break;
    }
  }
  if (emitterIndex < 0) return [];

  const entries = [];
  let current;
  for (let index = emitterIndex + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (!line.trim()) {
      if (current) current.lines.push("");
      continue;
    }
    const indent = line.match(/^\s*/)[0].length;
    if (indent <= emitterIndent) break;
    if (indent === emitterIndent + 2 && yamlKey(line)) {
      current = { key: yamlKey(line), lines: [line.trimStart()] };
      entries.push(current);
    } else if (current) {
      current.lines.push(line.slice(Math.min(line.length, emitterIndent + 2)));
    }
  }
  return entries;
}

export function writeEmitterConfig(
  configPath,
  originalConfig,
  emitter,
  emitterOutputDir,
) {
  const originalText = readFileSync(originalConfig, "utf8");
  const overrides = new Set([
    "emitter-output-dir",
    "output-file",
    "service-yaml",
  ]);
  const preserved = readEmitterOptions(originalText, emitter).filter(
    (entry) => !overrides.has(entry.key),
  );
  const normalizedOriginal = originalConfig.replaceAll("\\", "/");
  const normalizedOutput = emitterOutputDir.replaceAll("\\", "/");
  const lines = [
    `extends: ${JSON.stringify(normalizedOriginal)}`,
    "emit:",
    `  - ${JSON.stringify(emitter)}`,
    "options:",
    `  ${JSON.stringify(emitter)}:`,
  ];
  for (const entry of preserved) {
    for (const line of entry.lines) {
      lines.push(line ? `    ${line}` : "");
    }
  }
  lines.push(`    emitter-output-dir: ${JSON.stringify(normalizedOutput)}`);
  if (emitter === "@azure-tools/typespec-autorest") {
    lines.push('    output-file: "{version-status}/{version}/{feature}.json"');
  } else {
    lines.push('    emitter-name: "generic"');
    lines.push('    api-version: "all"');
  }
  writeFileSync(configPath, `${lines.join("\n")}\n`);
}

export function linkDependencies(sourceRoot, targetRoot) {
  const source = join(sourceRoot, "node_modules");
  const target = join(targetRoot, "node_modules");
  if (!existsSync(source) || existsSync(target)) return;
  symlinkSync(
    source,
    target,
    process.platform === "win32" ? "junction" : "dir",
  );
}

export function canonicalTempDirectory() {
  return process.platform === "win32"
    ? realpathSync.native(tmpdir())
    : tmpdir();
}

function packagePath(packageName) {
  return packageName.split("/");
}

function findInstalledPackage(repoRoot, projectRoot, packageName) {
  for (const root of [repoRoot, projectRoot]) {
    const packageJson = join(
      root,
      "node_modules",
      ...packagePath(packageName),
      "package.json",
    );
    if (existsSync(packageJson)) return packageJson;
  }
  return undefined;
}

function versionParts(version) {
  const match = String(version).match(/^v?(\d+)\.(\d+)\.(\d+)/);
  return match ? match.slice(1).map(Number) : undefined;
}

function compareVersions(left, right) {
  const leftParts = versionParts(left);
  const rightParts = versionParts(right);
  if (!leftParts || !rightParts) return Number.NaN;
  for (let index = 0; index < 3; index += 1) {
    if (leftParts[index] !== rightParts[index])
      return leftParts[index] - rightParts[index];
  }
  return 0;
}

export function satisfiesNodeEngine(version, range) {
  if (!range || range === "*") return true;
  const actual = versionParts(version);
  if (!actual) return false;

  function partialBounds(value) {
    const parts = value.replace(/^v/, "").split(".");
    const numeric = [];
    let wildcard = false;
    for (const part of parts) {
      if (/^(?:x|X|\*)$/.test(part)) {
        wildcard = true;
        break;
      }
      if (!/^\d+$/.test(part)) return undefined;
      numeric.push(Number(part));
    }
    if (numeric.length < 3) wildcard = true;
    const minimum = [...numeric, 0, 0].slice(0, 3);
    const maximum = [...numeric, 0, 0].slice(0, 3);
    if (wildcard) {
      for (let index = numeric.length; index < 3; index += 1) {
        maximum[index] = Number.MAX_SAFE_INTEGER;
      }
    }
    return { minimum, maximum, wildcard, precision: numeric.length };
  }

  function compareParts(left, right) {
    for (let index = 0; index < 3; index += 1) {
      if (left[index] !== right[index]) return left[index] - right[index];
    }
    return 0;
  }

  function satisfiesClause(clause) {
    const match = clause.match(
      /^(>=|<=|>|<|=|\^|~)?(v?\d+(?:\.(?:\d+|x|X|\*)){0,2})$/,
    );
    if (!match) return false;
    const bounds = partialBounds(match[2]);
    if (!bounds) return false;
    const comparison = compareParts(actual, bounds.minimum);
    switch (match[1] ?? (bounds.wildcard ? "range" : "=")) {
      case ">=":
        return comparison >= 0;
      case "<=":
        return compareParts(actual, bounds.maximum) <= 0;
      case ">":
        return compareParts(actual, bounds.maximum) > 0;
      case "<":
        return comparison < 0;
      case "^":
        return comparison >= 0 && actual[0] === bounds.minimum[0];
      case "~":
        return (
          comparison >= 0 &&
          actual[0] === bounds.minimum[0] &&
          (bounds.precision === 1 || actual[1] === bounds.minimum[1])
        );
      case "range":
        return comparison >= 0 && compareParts(actual, bounds.maximum) <= 0;
      default:
        return bounds.wildcard
          ? comparison >= 0 && compareParts(actual, bounds.maximum) <= 0
          : comparison === 0;
    }
  }

  return range.split("||").some((alternative) => {
    const trimmed = alternative.trim();
    const hyphen = trimmed.match(
      /^(v?\d+(?:\.\d+){0,2})\s+-\s+(v?\d+(?:\.\d+){0,2})$/,
    );
    if (hyphen) {
      const lower = partialBounds(hyphen[1]);
      const upper = partialBounds(hyphen[2]);
      return (
        lower &&
        upper &&
        compareParts(actual, lower.minimum) >= 0 &&
        compareParts(actual, upper.maximum) <= 0
      );
    }
    return trimmed.split(/\s+/).filter(Boolean).every(satisfiesClause);
  });
}

function lockedVersion(lock, packageName) {
  return (
    lock.packages?.[`node_modules/${packageName}`]?.version ??
    lock.dependencies?.[packageName]?.version
  );
}

export function preflightToolchain(repoRoot, projectRoot = repoRoot) {
  const packageJsonPath = join(repoRoot, "package.json");
  const lockPath = join(repoRoot, "package-lock.json");
  if (!existsSync(packageJsonPath) || !existsSync(lockPath)) {
    throw new Error(
      "TypeSpec assessment compilation requires package.json and package-lock.json at the repository root.",
    );
  }
  const packageJson = JSON.parse(readFileSync(packageJsonPath, "utf8"));
  const lock = JSON.parse(readFileSync(lockPath, "utf8"));
  const engine = packageJson.engines?.node;
  const problems = [];
  if (engine && !satisfiesNodeEngine(process.version, engine)) {
    problems.push(
      `active Node ${process.version} does not satisfy package.json engines.node ${engine}`,
    );
  }
  for (const packageName of REQUIRED_PACKAGES) {
    const expected = lockedVersion(lock, packageName);
    const installedPath = findInstalledPackage(
      repoRoot,
      projectRoot,
      packageName,
    );
    const installed = installedPath
      ? JSON.parse(readFileSync(installedPath, "utf8")).version
      : undefined;
    if (!expected) {
      problems.push(`${packageName} is missing from package-lock.json`);
    } else if (!installed) {
      problems.push(`${packageName}@${expected} is not installed`);
    } else if (installed !== expected) {
      problems.push(
        `${packageName} installed version ${installed} does not match package-lock.json ${expected}`,
      );
    }
  }
  if (problems.length > 0) {
    throw new Error(
      `TypeSpec toolchain preflight failed:\n- ${problems.join("\n- ")}`,
    );
  }
}

export function findTspCommand(repoRoot, projectRoot) {
  const compilerPackage = findInstalledPackage(
    repoRoot,
    projectRoot,
    "@typespec/compiler",
  );
  if (!compilerPackage) {
    throw new Error("@typespec/compiler is not installed.");
  }
  const packageJson = JSON.parse(readFileSync(compilerPackage, "utf8"));
  const bin =
    typeof packageJson.bin === "string"
      ? packageJson.bin
      : packageJson.bin?.tsp;
  if (!bin) throw new Error("@typespec/compiler does not expose the tsp CLI.");
  return {
    command: process.execPath,
    prefix: [resolve(dirname(compilerPackage), bin)],
  };
}

export function summarizeCompilerFailure(logText, logPath) {
  const cleanLines = logText
    .replace(/\u001b\[[0-9;]*m/g, "")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  const diagnostics = cleanLines.filter(
    (line) => /\berror\b/i.test(line) && !/^\d+\s+error(s)?$/i.test(line),
  );
  const reportedCount = cleanLines
    .map((line) => line.match(/^(\d+)\s+error(s)?$/i))
    .find(Boolean);
  const foundCount = cleanLines
    .map((line) => line.match(/\bfound\s+(\d+)\s+error(s)?\b/i))
    .find(Boolean);
  const errorCount = reportedCount
    ? Number(reportedCount[1])
    : foundCount
      ? Number(foundCount[1])
      : Math.max(1, diagnostics.length);
  return `${diagnostics[0] ?? "TypeSpec compilation failed"} (${errorCount} error${errorCount === 1 ? "" : "s"}; log: ${logPath})`;
}

async function compileProject({
  repoRoot,
  checkoutRoot,
  projectRoot,
  side,
  outputRoot,
  tempRoot,
  artifactCacheRoot,
}) {
  const absoluteProject = resolve(checkoutRoot, projectRoot);
  if (!existsSync(absoluteProject)) return { side, status: "missing" };
  const configName = existsSync(join(absoluteProject, "tspconfig.yaml"))
    ? "tspconfig.yaml"
    : "tspconfig.yml";
  const originalConfig = join(absoluteProject, configName);
  if (!existsSync(originalConfig)) return { side, status: "missing-config" };

  linkDependencies(repoRoot, checkoutRoot);

  const projectId = safeProjectId(projectRoot);
  const command = findTspCommand(repoRoot, resolve(repoRoot, projectRoot));
  const cacheInputRoot = sparseRoots([projectRoot])[0];
  const treeish =
    cacheInputRoot === "." ? "HEAD^{tree}" : `HEAD:${cacheInputRoot}`;
  const projectTree = gitText(checkoutRoot, ["rev-parse", treeish]);
  const lockHash = createHash("sha256")
    .update(readFileSync(join(repoRoot, "package-lock.json")))
    .digest("hex");
  const results = await Promise.all(
    EMITTERS.map(async (emitter) => {
      const emitterId = emitter.id;
      const emitterOutput = join(
        outputRoot,
        "artifacts",
        projectId,
        side,
        emitterId,
      );
      const configPath = join(
        tempRoot,
        `${projectId}-${side}-${emitterId}.yaml`,
      );
      const logPath = join(
        outputRoot,
        "compile-logs",
        `${projectId}-${side}-${emitterId}.log`,
      );
      mkdirSync(emitterOutput, { recursive: true });
      mkdirSync(dirname(logPath), { recursive: true });
      writeEmitterConfig(
        configPath,
        originalConfig,
        emitter.name,
        emitterOutput,
      );
      const cacheKey = artifactCacheKey({
        projectTree,
        lockHash,
        node: process.version,
        emitter: emitter.name,
        config: readFileSync(originalConfig, "utf8"),
      });
      const cachePath = join(artifactCacheRoot, projectId, emitterId, cacheKey);
      if (restoreArtifactCache(cachePath, emitterOutput)) {
        const relativeLog = relative(outputRoot, logPath).replaceAll("\\", "/");
        writeFileSync(logPath, `Reused artifact cache ${cacheKey}.\n`);
        return {
          emitter: emitterId,
          status: "succeeded",
          exitCode: 0,
          durationMs: 0,
          cached: true,
          cacheKey,
          outputDirectory: relative(outputRoot, emitterOutput).replaceAll(
            "\\",
            "/",
          ),
          log: relativeLog,
        };
      }
      const startedAt = process.hrtime.bigint();
      const result = await timedProgress(
        `${side} ${projectRoot} ${emitterId} compilation`,
        () =>
          runAsync(
            command.command,
            [
              ...command.prefix,
              "compile",
              absoluteProject,
              "--config",
              configPath,
            ],
            { cwd: repoRoot, allowFailure: true },
          ),
      );
      const durationMs = elapsedMs(startedAt);
      const relativeLog = relative(outputRoot, logPath).replaceAll("\\", "/");
      const logText = `${result.stdout ?? ""}${result.stderr ?? ""}`;
      writeFileSync(logPath, logText);
      const emitterResult = {
        emitter: emitterId,
        status: result.status === 0 ? "succeeded" : "failed",
        exitCode: result.status,
        durationMs,
        cached: false,
        cacheKey,
        outputDirectory: relative(outputRoot, emitterOutput).replaceAll(
          "\\",
          "/",
        ),
        log: relativeLog,
      };
      if (result.status !== 0) {
        emitterResult.failureSummary = summarizeCompilerFailure(
          logText,
          relativeLog,
        );
      } else {
        storeArtifactCache(cachePath, emitterOutput);
      }
      return emitterResult;
    }),
  );
  return {
    side,
    status: results.every((item) => item.status === "succeeded")
      ? "succeeded"
      : "failed",
    emitters: results,
  };
}

function listFiles(root) {
  if (!existsSync(root)) return [];
  const files = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const path = join(root, entry.name);
    if (entry.isDirectory()) {
      files.push(...listFiles(path));
    } else {
      files.push(path);
    }
  }
  return files;
}

function createArtifactDiffs(outputRoot, projectRoot) {
  const projectId = safeProjectId(projectRoot);
  const results = [];
  for (const emitter of ["autorest", "tcgc"]) {
    const baseRoot = join(outputRoot, "artifacts", projectId, "base", emitter);
    const headRoot = join(outputRoot, "artifacts", projectId, "head", emitter);
    const relativeFiles = new Set([
      ...listFiles(baseRoot).map((path) => relative(baseRoot, path)),
      ...listFiles(headRoot).map((path) => relative(headRoot, path)),
    ]);
    for (const file of [...relativeFiles].sort()) {
      const baseFile = join(baseRoot, file);
      const headFile = join(headRoot, file);
      const diffPath = join(
        outputRoot,
        "diffs",
        projectId,
        emitter,
        `${file}.diff`,
      );
      mkdirSync(dirname(diffPath), { recursive: true });
      let diff = "";
      if (existsSync(baseFile) && existsSync(headFile)) {
        const result = run(
          "git",
          ["diff", "--no-index", "--no-color", "--", baseFile, headFile],
          { allowFailure: true },
        );
        diff = result.stdout;
      } else if (existsSync(headFile)) {
        diff = `Added artifact: ${file}\n${readFileSync(headFile, "utf8")}`;
      } else if (existsSync(baseFile)) {
        diff = `Removed artifact: ${file}\n${readFileSync(baseFile, "utf8")}`;
      }
      if (diff) {
        writeFileSync(diffPath, diff);
        results.push(relative(outputRoot, diffPath));
      }
    }
  }
  return results;
}

function sparseRoots(projectRoots) {
  return [
    ...new Set(
      projectRoots.map((projectRoot) => {
        const parent = dirname(projectRoot).replaceAll("\\", "/");
        return parent === "." ? projectRoot : parent;
      }),
    ),
  ].sort();
}

export function prepareSparseCheckout({
  repoRoot,
  checkoutRoot,
  commit,
  projectRoots,
  inputKey,
  previousInputKey,
}) {
  if (
    inputKey &&
    previousInputKey === inputKey &&
    existsSync(join(checkoutRoot, ".git")) &&
    git(checkoutRoot, ["status", "--porcelain"], {
      allowFailure: true,
    }).stdout.trim() === ""
  ) {
    return { reused: true };
  }
  if (!existsSync(join(checkoutRoot, ".git"))) {
    git(repoRoot, ["worktree", "prune"]);
    git(repoRoot, [
      "worktree",
      "add",
      "--detach",
      "--no-checkout",
      checkoutRoot,
      commit,
    ]);
    git(checkoutRoot, ["sparse-checkout", "init", "--cone"]);
  } else {
    git(checkoutRoot, ["reset", "--hard", "--quiet"]);
    git(checkoutRoot, ["clean", "-fd", "--quiet"]);
  }
  git(checkoutRoot, [
    "sparse-checkout",
    "set",
    "--cone",
    "--",
    ...sparseRoots(projectRoots),
  ]);
  git(checkoutRoot, ["checkout", "--detach", "--force", commit]);
  return { reused: false };
}

function overlayWorkingTree(repoRoot, checkoutRoot, tempRoot, projectRoots) {
  const roots = sparseRoots(projectRoots);
  const patch = git(repoRoot, ["diff", "--binary", "HEAD", "--", ...roots], {
    allowFailure: true,
  }).stdout;
  if (patch) {
    const patchPath = join(tempRoot, "head-working-tree.patch");
    writeFileSync(patchPath, patch);
    git(checkoutRoot, ["apply", "--binary", patchPath]);
  }

  const untracked = gitText(repoRoot, [
    "ls-files",
    "--others",
    "--exclude-standard",
    "--",
    ...roots,
  ])
    .split(/\r?\n/)
    .filter(Boolean);
  for (const path of untracked) {
    const source = join(repoRoot, path);
    const target = join(checkoutRoot, path);
    mkdirSync(dirname(target), { recursive: true });
    cpSync(source, target, { recursive: true });
  }

  if (
    git(checkoutRoot, ["status", "--porcelain"], {
      allowFailure: true,
    }).stdout.trim()
  ) {
    git(checkoutRoot, ["add", "--all"]);
    git(checkoutRoot, [
      "-c",
      "user.name=TypeSpec Assessment",
      "-c",
      "user.email=typespec-assessment@localhost",
      "commit",
      "--quiet",
      "-m",
      "Synthetic assessment head",
    ]);
  }
}

export async function prepareAssessment(options) {
  const assessmentStartedAt = process.hrtime.bigint();
  const phaseDurations = {};
  const checkoutReuse = { base: false, head: false };
  async function measuredPhase(key, label, operation) {
    const startedAt = process.hrtime.bigint();
    try {
      return await timedProgress(label, operation);
    } finally {
      phaseDurations[key] = elapsedMs(startedAt);
    }
  }
  function measuredSync(key, operation) {
    const startedAt = process.hrtime.bigint();
    try {
      return operation();
    } finally {
      phaseDurations[key] = elapsedMs(startedAt);
    }
  }
  const repoRoot = resolve(
    gitText(resolve(options.repo), ["rev-parse", "--show-toplevel"]),
  );
  const outputRoot = isAbsolute(options.output)
    ? options.output
    : resolve(repoRoot, options.output);
  const configuredArtifactCache =
    options.artifactCache ??
    join(canonicalTempDirectory(), "typespec-assessment-artifact-cache");
  const artifactCacheRoot = isAbsolute(configuredArtifactCache)
    ? configuredArtifactCache
    : resolve(repoRoot, configuredArtifactCache);
  const repositoryCacheId = createHash("sha256")
    .update(repoRoot.toLowerCase())
    .digest("hex")
    .slice(0, 16);
  const configuredCheckoutCache =
    options.checkoutCache ??
    join(
      canonicalTempDirectory(),
      "typespec-assessment-checkouts",
      repositoryCacheId,
    );
  const checkoutCacheRoot = isAbsolute(configuredCheckoutCache)
    ? configuredCheckoutCache
    : resolve(repoRoot, configuredCheckoutCache);
  const checkoutStatePath = join(checkoutCacheRoot, "state.json");
  const checkoutState = existsSync(checkoutStatePath)
    ? JSON.parse(readFileSync(checkoutStatePath, "utf8"))
    : {};
  const { baseRef, baseCommit } = await measuredPhase(
    "baselineResolutionMs",
    "baseline resolution",
    async () => resolveBaseline(repoRoot, options.base),
  );
  const sourceEvidence = measuredSync("sourceDiscoveryMs", () => {
    const headCommit = gitText(repoRoot, ["rev-parse", "HEAD"]);
    const hasLocalTypeSpecChanges =
      git(
        repoRoot,
        ["status", "--porcelain", "--untracked-files=all", "--", "*.tsp"],
        { allowFailure: true },
      ).stdout.trim().length > 0;
    const remoteUrl = git(
      repoRoot,
      ["config", "--get", `remote.${baseRef.split("/")[0]}.url`],
      { allowFailure: true },
    ).stdout.trim();
    const excludedRelatives = [
      outputRoot,
      artifactCacheRoot,
      checkoutCacheRoot,
      ...(options.excludePaths ?? []),
    ]
      .map((path) => relative(repoRoot, path).replaceAll("\\", "/"))
      .filter((path) => !path.startsWith(".."));
    const changedFiles = listChangedFiles(repoRoot, baseCommit).filter(
      (path) =>
        !excludedRelatives.some(
          (excluded) => path === excluded || path.startsWith(`${excluded}/`),
        ),
    );
    const projectRoots = discoverProjectRoots(
      repoRoot,
      changedFiles,
      baseCommit,
    );
    const sourceDiff = git(
      repoRoot,
      ["diff", "--unified=0", "--no-color", baseCommit, "--", "*.tsp"],
      { allowFailure: true },
    ).stdout;
    const untrackedFiles = untrackedTypeSpecFiles(repoRoot, changedFiles);
    const sourceReferences = [
      ...parseSourceHunks(
        sourceDiff,
        "head",
        baseCommit,
        remoteUrl,
        headCommit,
        hasLocalTypeSpecChanges,
      ),
      ...untrackedReferences(
        repoRoot,
        changedFiles,
        headCommit,
        remoteUrl,
        untrackedFiles,
      ),
    ];
    const displaySourceDiff = git(
      repoRoot,
      ["diff", "--unified=3", "--no-color", baseCommit, "--", "*.tsp"],
      { allowFailure: true },
    ).stdout;
    const typeSpecDiffs = [
      ...parseTypeSpecDiffHunks(displaySourceDiff),
      ...untrackedFiles.map((path) =>
        untrackedTypeSpecDiffHunk(
          path,
          readFileSync(join(repoRoot, path), "utf8"),
        ),
      ),
    ];
    const inputRoots = sparseRoots(projectRoots);
    const localInputHash = createHash("sha256").update(
      git(repoRoot, ["diff", "--binary", "HEAD", "--", ...inputRoots], {
        allowFailure: true,
      }).stdout,
    );
    for (const path of untrackedFiles.sort()) {
      localInputHash.update(path);
      localInputHash.update(readFileSync(join(repoRoot, path)));
    }
    return {
      changedFiles,
      headCommit,
      projectRoots,
      sourceReferences,
      typeSpecDiffs,
      localInputHash: localInputHash.digest("hex"),
    };
  });
  const {
    changedFiles,
    headCommit,
    projectRoots,
    sourceReferences,
    typeSpecDiffs,
    localInputHash,
  } = sourceEvidence;
  progress(
    `discovered ${projectRoots.length} affected TypeSpec project(s) from ${changedFiles.length} changed file(s)`,
  );

  rmSync(outputRoot, { recursive: true, force: true });
  mkdirSync(outputRoot, { recursive: true });

  const tempRoot = mkdtempSync(
    join(canonicalTempDirectory(), "typespec-assessment-"),
  );
  const baseCheckout = join(checkoutCacheRoot, "base");
  const headCheckout = join(checkoutCacheRoot, "head");
  const projects = [];
  try {
    if (!options.skipCompile && projectRoots.length > 0) {
      measuredSync("toolchainPreflightMs", () => preflightToolchain(repoRoot));
      await measuredPhase(
        "sparseCheckoutsMs",
        "sparse assessment checkouts",
        async () => {
          const rootsKey = sparseRoots(projectRoots).join("\n");
          const baseInputKey = createHash("sha256")
            .update(`${baseCommit}\n${rootsKey}`)
            .digest("hex");
          const headInputKey = createHash("sha256")
            .update(`${headCommit}\n${localInputHash}\n${rootsKey}`)
            .digest("hex");
          const basePreparation = prepareSparseCheckout({
            repoRoot,
            checkoutRoot: baseCheckout,
            commit: baseCommit,
            projectRoots,
            inputKey: baseInputKey,
            previousInputKey: checkoutState.baseInputKey,
          });
          const headPreparation = prepareSparseCheckout({
            repoRoot,
            checkoutRoot: headCheckout,
            commit: headCommit,
            projectRoots,
            inputKey: headInputKey,
            previousInputKey: checkoutState.headInputKey,
          });
          if (!headPreparation.reused) {
            overlayWorkingTree(repoRoot, headCheckout, tempRoot, projectRoots);
          }
          mkdirSync(checkoutCacheRoot, { recursive: true });
          writeFileSync(
            checkoutStatePath,
            `${JSON.stringify(
              {
                baseInputKey,
                headInputKey,
              },
              null,
              2,
            )}\n`,
          );
          checkoutReuse.base = basePreparation.reused;
          checkoutReuse.head = headPreparation.reused;
        },
      );
    }
    const projectProcessingStartedAt = process.hrtime.bigint();
    for (const projectRoot of projectRoots) {
      const projectStartedAt = process.hrtime.bigint();
      const project = {
        path: projectRoot,
        compilations: [],
        artifactDiffs: [],
      };
      if (!options.skipCompile) {
        project.compilations.push(
          ...(await Promise.all([
            compileProject({
              repoRoot,
              checkoutRoot: baseCheckout,
              projectRoot,
              side: "base",
              outputRoot,
              tempRoot,
              artifactCacheRoot,
            }),
            compileProject({
              repoRoot,
              checkoutRoot: headCheckout,
              projectRoot,
              side: "head",
              outputRoot,
              tempRoot,
              artifactCacheRoot,
            }),
          ])),
        );
        if (options.rawArtifactDiffs) {
          project.artifactDiffs = await timedProgress(
            `${projectRoot} artifact diffs`,
            async () => createArtifactDiffs(outputRoot, projectRoot),
          );
        }
      }
      project.durationMs = elapsedMs(projectStartedAt);
      projects.push(project);
    }
    phaseDurations.projectProcessingMs = elapsedMs(projectProcessingStartedAt);
  } finally {
    const cleanupStartedAt = process.hrtime.bigint();
    rmSync(tempRoot, { recursive: true, force: true });
    phaseDurations.cleanupMs = elapsedMs(cleanupStartedAt);
  }

  const worktreeStatus = git(repoRoot, ["status", "--porcelain"], {
    allowFailure: true,
  })
    .stdout.split(/\r?\n/)
    .filter(Boolean);
  const evidence = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    durationMs: 0,
    phaseDurations,
    repositoryRoot: repoRoot,
    baseline: { ref: baseRef, commit: baseCommit },
    head: {
      commit: headCommit,
      hasWorkingTreeChanges: worktreeStatus.length > 0,
      changeScope: {
        staged: worktreeStatus.some(
          (line) => line[0] !== " " && line[0] !== "?",
        ),
        unstaged: worktreeStatus.some(
          (line) => line[1] !== " " && line[1] !== "?",
        ),
        untracked: worktreeStatus.some((line) => line.startsWith("??")),
      },
    },
    changedFiles,
    sourceReferences,
    typeSpecDiffs,
    projects,
    artifactCache: {
      root: artifactCacheRoot,
      hits: projects
        .flatMap((project) => project.compilations)
        .flatMap((compilation) => compilation.emitters ?? [])
        .filter((emitter) => emitter.cached).length,
      misses: projects
        .flatMap((project) => project.compilations)
        .flatMap((compilation) => compilation.emitters ?? [])
        .filter((emitter) => emitter.cached === false).length,
    },
    checkoutCache: {
      root: checkoutCacheRoot,
      persistent: true,
      reused: checkoutReuse,
    },
    complianceAssessment: {
      method: "authoritative-document-pattern-assessment",
      agenticSearchProcedure:
        ".github/skills/azure-typespec-assessment/references/agentic-search.md",
      referenceCatalog:
        ".github/skills/azure-typespec-assessment/references/reference-document-links.md",
      instructions:
        "Run the assessment skill's local agentic search procedure, retain matching fetched-content excerpts, and compare each documented pattern directly with the exact changed TypeSpec declaration.",
    },
    compileSkipped: options.skipCompile,
    errors: projects.flatMap((project) =>
      project.compilations
        .filter((compilation) => compilation.status === "failed")
        .map((compilation) => ({
          project: project.path,
          side: compilation.side,
          message: compilation.emitters
            .filter((emitter) => emitter.status === "failed")
            .map((emitter) => `${emitter.emitter}: ${emitter.failureSummary}`)
            .join("; "),
        })),
    ),
  };
  const analysis = analyzeArtifacts(evidence, outputRoot);
  phaseDurations.deterministicAnalysisMs = analysis.durationMs;
  evidence.durationMs = elapsedMs(assessmentStartedAt);
  writeFileSync(
    join(outputRoot, "evidence.json"),
    `${JSON.stringify(evidence, null, 2)}\n`,
  );
  writeFileSync(
    join(outputRoot, "analysis.json"),
    `${JSON.stringify(analysis)}\n`,
  );
  progress(
    `assessment completed in ${(evidence.durationMs / 1000).toFixed(1)}s with ${evidence.errors.length} blocking error(s)`,
  );
  return evidence;
}

async function main() {
  try {
    const options = parseArgs(process.argv.slice(2));
    const evidence = await prepareAssessment(options);
    process.stdout.write(
      `Prepared assessment evidence for ${evidence.projects.length} TypeSpec project(s).\n`,
    );
    if (evidence.errors.length > 0) process.exitCode = 2;
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    if (error.stderr) process.stderr.write(error.stderr);
    process.exitCode = 1;
  }
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) main();
