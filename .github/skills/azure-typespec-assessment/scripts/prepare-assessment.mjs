#!/usr/bin/env node

import { spawn, spawnSync } from "node:child_process";
import {
  cpSync,
  copyFileSync,
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

function elapsedMs(startedAt) {
  return Math.round(Number(process.hrtime.bigint() - startedAt) / 1_000_000);
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
    skipCompile: false,
    skipValidation: false,
  };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === "--skip-compile") {
      args.skipCompile = true;
    } else if (value === "--skip-validation") {
      args.skipValidation = true;
    } else if (["--repo", "--output", "--base"].includes(value)) {
      const next = argv[index + 1];
      if (!next) throw new Error(`${value} requires a value`);
      args[value.slice(2)] = next;
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

function baselineFileExists(repoRoot, baseCommit, path, cache) {
  if (!baseCommit) return false;
  const normalized = relative(repoRoot, path).replaceAll("\\", "/");
  const key = `${baseCommit}:${normalized}`;
  if (!cache.has(key)) {
    cache.set(
      key,
      git(repoRoot, ["cat-file", "-e", key], { allowFailure: true }).status ===
        0,
    );
  }
  return cache.get(key);
}

export function discoverProjectRoots(repoRoot, changedFiles, baseCommit) {
  const roots = new Set();
  const baselineFiles = new Map();
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
        (baselineFileExists(
          repoRoot,
          baseCommit,
          join(current, "tspconfig.yaml"),
          baselineFiles,
        ) ||
          baselineFileExists(
            repoRoot,
            baseCommit,
            join(current, "tspconfig.yml"),
            baselineFiles,
          )) &&
        baselineFileExists(
          repoRoot,
          baseCommit,
          join(current, "main.tsp"),
          baselineFiles,
        );
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
) {
  const fragment = `#L${startLine}-L${endLine}`;
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
) {
  return changedFiles
    .filter((path) => path.endsWith(".tsp"))
    .filter(
      (path) =>
        git(repoRoot, ["ls-files", "--error-unmatch", path], {
          allowFailure: true,
        }).status !== 0,
    )
    .map((path) => {
      const lines = readFileSync(join(repoRoot, path), "utf8").split(
        /\r?\n/,
      ).length;
      return {
        path,
        revision: "head",
        startLine: 1,
        endLine: lines,
        link: sourceLink(path, "head", headCommit, remoteUrl, 1, lines),
      };
    });
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

export function findTypeSpecValidationCommand(
  repoRoot,
  nodeCommand = process.execPath,
) {
  const script = join(
    repoRoot,
    "eng",
    "tools",
    "typespec-validation",
    "cmd",
    "tsv.js",
  );
  if (!existsSync(script)) return undefined;
  return { command: nodeCommand, prefix: [script] };
}

export function ensureNodeWithNpm(
  tempRoot,
  npmExecPath = process.env.npm_execpath,
) {
  const bundledNpm = join(
    dirname(process.execPath),
    "node_modules",
    "npm",
    "bin",
    "npm-cli.js",
  );
  if (existsSync(bundledNpm)) return process.execPath;
  if (!npmExecPath || !existsSync(npmExecPath)) {
    throw new Error(
      `Node runtime ${process.execPath} does not bundle npm and npm_execpath is unavailable.`,
    );
  }

  const runtimeRoot = join(tempRoot, "node-runtime");
  const runtimeNode = join(runtimeRoot, basename(process.execPath));
  const runtimeNpm = join(runtimeRoot, "node_modules", "npm");
  mkdirSync(dirname(runtimeNpm), { recursive: true });
  copyFileSync(process.execPath, runtimeNode);
  symlinkSync(
    resolve(dirname(npmExecPath), ".."),
    runtimeNpm,
    process.platform === "win32" ? "junction" : "dir",
  );
  return runtimeNode;
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

export function summarizeValidationFailure(logText, logPath) {
  const cleanLines = logText
    .replace(/\u001b\[[0-9;]*m/g, "")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  const diagnostic =
    cleanLines.find(
      (line) =>
        /\b(error|warning|failed|invalid|missing|must)\b/i.test(line) &&
        !/^Rule .+ failed$/i.test(line),
    ) ?? "TypeSpec Validation failed";
  return `${diagnostic} (log: ${logPath})`;
}

async function runTypeSpecValidation({
  repoRoot,
  checkoutRoot,
  projectRoot,
  outputRoot,
  nodeCommand,
}) {
  const projectId = safeProjectId(projectRoot);
  const logPath = join(outputRoot, "validation-logs", `${projectId}-head.log`);
  mkdirSync(dirname(logPath), { recursive: true });
  const relativeLog = relative(outputRoot, logPath).replaceAll("\\", "/");
  const validationCommand = findTypeSpecValidationCommand(
    repoRoot,
    nodeCommand,
  );
  if (!validationCommand) {
    const failureSummary = `Repository TypeSpec Validation CLI was not found at eng/tools/typespec-validation/cmd/tsv.js (log: ${relativeLog})`;
    writeFileSync(logPath, `${failureSummary}\n`);
    return {
      tool: "TypeSpecValidation",
      status: "unavailable",
      exitCode: null,
      durationMs: 0,
      log: relativeLog,
      failureSummary,
    };
  }

  linkDependencies(repoRoot, checkoutRoot);
  const startedAt = process.hrtime.bigint();
  const result = await timedProgress(
    `${projectRoot} TypeSpec Validation`,
    async () =>
      runAsync(
        validationCommand.command,
        [...validationCommand.prefix, resolve(checkoutRoot, projectRoot)],
        { cwd: checkoutRoot, allowFailure: true },
      ),
  );
  const durationMs = elapsedMs(startedAt);
  const logText = `${result.stdout ?? ""}${result.stderr ?? ""}`;
  writeFileSync(logPath, logText);
  const validation = {
    tool: "TypeSpecValidation",
    status: result.status === 0 ? "succeeded" : "failed",
    exitCode: result.status,
    durationMs,
    log: relativeLog,
  };
  if (result.status !== 0) {
    validation.failureSummary = summarizeValidationFailure(
      logText,
      relativeLog,
    );
  }
  return validation;
}

async function compileProject({
  repoRoot,
  checkoutRoot,
  projectRoot,
  side,
  outputRoot,
  tempRoot,
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
  const results = [];
  for (const emitter of EMITTERS) {
    const emitterId = emitter.id;
    const emitterOutput = join(
      outputRoot,
      "artifacts",
      projectId,
      side,
      emitterId,
    );
    const configPath = join(tempRoot, `${projectId}-${side}-${emitterId}.yaml`);
    const logPath = join(
      outputRoot,
      "compile-logs",
      `${projectId}-${side}-${emitterId}.log`,
    );
    mkdirSync(emitterOutput, { recursive: true });
    mkdirSync(dirname(logPath), { recursive: true });
    writeEmitterConfig(configPath, originalConfig, emitter.name, emitterOutput);
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
    }
    results.push(emitterResult);
  }
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
  const repoRoot = resolve(
    gitText(resolve(options.repo), ["rev-parse", "--show-toplevel"]),
  );
  const outputRoot = isAbsolute(options.output)
    ? options.output
    : resolve(repoRoot, options.output);
  const { baseRef, baseCommit } = await timedProgress(
    "baseline resolution",
    async () => resolveBaseline(repoRoot, options.base),
  );
  const headCommit = gitText(repoRoot, ["rev-parse", "HEAD"]);
  const remoteUrl = git(
    repoRoot,
    ["config", "--get", `remote.${baseRef.split("/")[0]}.url`],
    { allowFailure: true },
  ).stdout.trim();
  const outputRelative = relative(repoRoot, outputRoot).replaceAll("\\", "/");
  const changedFiles = listChangedFiles(repoRoot, baseCommit).filter(
    (path) =>
      outputRelative.startsWith("..") ||
      (path !== outputRelative && !path.startsWith(`${outputRelative}/`)),
  );
  const projectRoots = discoverProjectRoots(repoRoot, changedFiles, baseCommit);
  progress(
    `discovered ${projectRoots.length} affected TypeSpec project(s) from ${changedFiles.length} changed file(s)`,
  );
  const sourceDiff = git(
    repoRoot,
    ["diff", "--unified=0", "--no-color", baseCommit, "--", "*.tsp"],
    { allowFailure: true },
  ).stdout;
  const sourceReferences = [
    ...parseSourceHunks(sourceDiff, "head", baseCommit, remoteUrl, headCommit),
    ...untrackedReferences(repoRoot, changedFiles, headCommit, remoteUrl),
  ];

  rmSync(outputRoot, { recursive: true, force: true });
  mkdirSync(outputRoot, { recursive: true });

  const tempRoot = mkdtempSync(
    join(canonicalTempDirectory(), "typespec-assessment-"),
  );
  const baseCheckout = join(tempRoot, "base");
  const headCheckout = join(tempRoot, "head");
  const validationNodeCommand =
    options.skipCompile || options.skipValidation
      ? process.execPath
      : ensureNodeWithNpm(tempRoot);
  const projects = [];
  try {
    if (!options.skipCompile && projectRoots.length > 0) {
      preflightToolchain(repoRoot);
      await timedProgress("sparse assessment checkouts", async () => {
        git(repoRoot, [
          "worktree",
          "add",
          "--detach",
          "--no-checkout",
          baseCheckout,
          baseCommit,
        ]);
        git(baseCheckout, ["sparse-checkout", "init", "--cone"]);
        git(baseCheckout, [
          "sparse-checkout",
          "set",
          "--cone",
          "--",
          ...sparseRoots(projectRoots),
        ]);
        git(baseCheckout, ["checkout", "--detach", baseCommit]);
        git(repoRoot, [
          "worktree",
          "add",
          "--detach",
          "--no-checkout",
          headCheckout,
          headCommit,
        ]);
        git(headCheckout, ["sparse-checkout", "init", "--cone"]);
        git(headCheckout, [
          "sparse-checkout",
          "set",
          "--cone",
          "--",
          ...sparseRoots(projectRoots),
        ]);
        git(headCheckout, ["checkout", "--detach", headCommit]);
        overlayWorkingTree(repoRoot, headCheckout, tempRoot, projectRoots);
      });
    }
    for (const projectRoot of projectRoots) {
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
            }),
            compileProject({
              repoRoot,
              checkoutRoot: headCheckout,
              projectRoot,
              side: "head",
              outputRoot,
              tempRoot,
            }),
          ])),
        );
        project.artifactDiffs = await timedProgress(
          `${projectRoot} artifact diffs`,
          async () => createArtifactDiffs(outputRoot, projectRoot),
        );
        project.validation = options.skipValidation
          ? {
              tool: "TypeSpecValidation",
              status: "skipped",
              durationMs: 0,
              reason: "Skipped by explicit --skip-validation request.",
            }
          : await runTypeSpecValidation({
              repoRoot,
              checkoutRoot: headCheckout,
              projectRoot,
              outputRoot,
              nodeCommand: validationNodeCommand,
            });
      }
      projects.push(project);
    }
  } finally {
    if (existsSync(baseCheckout)) {
      git(repoRoot, ["worktree", "remove", "--force", baseCheckout], {
        allowFailure: true,
      });
    }
    if (existsSync(headCheckout)) {
      git(repoRoot, ["worktree", "remove", "--force", headCheckout], {
        allowFailure: true,
      });
    }
    rmSync(tempRoot, { recursive: true, force: true });
  }

  const evidence = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    durationMs: elapsedMs(assessmentStartedAt),
    repositoryRoot: repoRoot,
    baseline: { ref: baseRef, commit: baseCommit },
    head: {
      commit: headCommit,
      hasWorkingTreeChanges:
        git(repoRoot, ["status", "--porcelain"], {
          allowFailure: true,
        }).stdout.trim().length > 0,
    },
    changedFiles,
    sourceReferences,
    projects,
    complianceAssessment: {
      method:
        "repository-validation-and-authoritative-document-pattern-assessment",
      repositoryValidation:
        "Each affected head project is validated with the repository-native TypeSpecValidation CLI before documentation assessment.",
      agenticSearchProcedure:
        ".github/skills/azure-typespec-author/references/agentic-search.md",
      referenceCatalog:
        ".github/skills/azure-typespec-author/references/reference-document-links.md",
      instructions:
        "Run the shared agentic search procedure, retain matching fetched-content excerpts, and compare each documented pattern with the exact changed declaration.",
    },
    compileSkipped: options.skipCompile,
    validationSkipped: options.skipValidation,
    errors: projects.flatMap((project) => [
      ...(project.validation &&
      ["failed", "unavailable"].includes(project.validation.status)
        ? [
            {
              project: project.path,
              side: "head",
              message: project.validation.failureSummary,
            },
          ]
        : []),
      ...project.compilations
        .filter((compilation) => compilation.status === "failed")
        .map((compilation) => ({
          project: project.path,
          side: compilation.side,
          message: compilation.emitters
            .filter((emitter) => emitter.status === "failed")
            .map((emitter) => `${emitter.emitter}: ${emitter.failureSummary}`)
            .join("; "),
        })),
    ]),
  };
  writeFileSync(
    join(outputRoot, "evidence.json"),
    `${JSON.stringify(evidence, null, 2)}\n`,
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
