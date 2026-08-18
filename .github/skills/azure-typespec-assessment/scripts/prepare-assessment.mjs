#!/usr/bin/env node

import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
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
  };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === "--skip-compile") {
      args.skipCompile = true;
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

export function discoverProjectRoots(repoRoot, changedFiles) {
  const roots = new Set();
  for (const changedFile of changedFiles.filter(isTypeSpecRelated)) {
    let current = resolve(repoRoot, dirname(changedFile));
    if (PROJECT_FILES.has(basename(changedFile)))
      current = resolve(repoRoot, dirname(changedFile));
    while (current.startsWith(repoRoot)) {
      if (
        existsSync(join(current, "tspconfig.yaml")) ||
        existsSync(join(current, "tspconfig.yml"))
      ) {
        roots.add((relative(repoRoot, current) || ".").replaceAll("\\", "/"));
        break;
      }
      if (current === repoRoot) break;
      current = dirname(current);
    }
  }
  return [...roots].sort();
}

export function parseSourceHunks(diffText, revision, commit, remoteUrl) {
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
    references.push({
      path,
      revision: sourceRevision,
      startLine,
      endLine: Math.max(startLine, startLine + count - 1),
      link: sourceLink(
        path,
        sourceRevision,
        commit,
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

function sourceLink(path, revision, commit, remoteUrl, startLine, endLine) {
  const fragment = `#L${startLine}-L${endLine}`;
  if (revision === "head") return `${path}${fragment}`;
  const github = normalizeRemoteUrl(remoteUrl);
  return github
    ? `${github}/blob/${commit}/${path}${fragment}`
    : `${commit}:${path}${fragment}`;
}

function untrackedReferences(repoRoot, changedFiles) {
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
        link: `${path}#L1-L${lines}`,
      };
    });
}

function safeProjectId(projectRoot) {
  return projectRoot === "."
    ? "repository-root"
    : projectRoot.replace(/[\\/]/g, "__");
}

function writeEmitterConfig(
  configPath,
  originalConfig,
  emitter,
  emitterOutputDir,
) {
  const normalizedOriginal = originalConfig.replaceAll("\\", "/");
  const normalizedOutput = emitterOutputDir.replaceAll("\\", "/");
  const lines = [
    `extends: ${JSON.stringify(normalizedOriginal)}`,
    "emit:",
    `  - ${JSON.stringify(emitter)}`,
    "options:",
    `  ${JSON.stringify(emitter)}:`,
    `    emitter-output-dir: ${JSON.stringify(normalizedOutput)}`,
  ];
  if (emitter === "@azure-tools/typespec-autorest") {
    lines.push(
      '    output-file: "{service-name}/{version-status}/{version}/openapi.yaml"',
    );
    lines.push('    service-yaml: "never"');
  } else {
    lines.push('    emitter-name: "generic"');
  }
  writeFileSync(configPath, `${lines.join("\n")}\n`);
}

function linkDependencies(sourceRoot, targetRoot) {
  const source = join(sourceRoot, "node_modules");
  const target = join(targetRoot, "node_modules");
  if (!existsSync(source) || existsSync(target)) return;
  symlinkSync(
    source,
    target,
    process.platform === "win32" ? "junction" : "dir",
  );
}

function findTspCommand(repoRoot, projectRoot) {
  for (const root of [repoRoot, projectRoot]) {
    const local = join(
      root,
      "node_modules",
      ".bin",
      process.platform === "win32" ? "tsp.cmd" : "tsp",
    );
    if (existsSync(local)) return { command: local, prefix: [] };
  }
  return {
    command: process.platform === "win32" ? "npx.cmd" : "npx",
    prefix: ["--no-install", "tsp"],
  };
}

function compileProject({
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
  if (checkoutRoot !== repoRoot) {
    const currentProject = resolve(repoRoot, projectRoot);
    if (existsSync(currentProject))
      linkDependencies(currentProject, absoluteProject);
  }

  const projectId = safeProjectId(projectRoot);
  const command = findTspCommand(repoRoot, resolve(repoRoot, projectRoot));
  const results = [];
  for (const emitter of [
    "@azure-tools/typespec-autorest",
    "@azure-tools/typespec-client-generator-core",
  ]) {
    const emitterId = emitter.endsWith("typespec-autorest")
      ? "autorest"
      : "tcgc";
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
    writeEmitterConfig(configPath, originalConfig, emitter, emitterOutput);
    const result = run(
      command.command,
      [...command.prefix, "compile", absoluteProject, "--config", configPath],
      { cwd: repoRoot, allowFailure: true },
    );
    writeFileSync(
      logPath,
      `${result.stdout ?? ""}${result.stderr ?? ""}${result.error?.message ?? ""}`,
    );
    results.push({
      emitter: emitterId,
      status: result.status === 0 ? "succeeded" : "failed",
      exitCode: result.status,
      outputDirectory: relative(outputRoot, emitterOutput),
      log: relative(outputRoot, logPath),
    });
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

export function prepareAssessment(options) {
  const repoRoot = resolve(
    gitText(resolve(options.repo), ["rev-parse", "--show-toplevel"]),
  );
  const outputRoot = isAbsolute(options.output)
    ? options.output
    : resolve(repoRoot, options.output);
  const { baseRef, baseCommit } = resolveBaseline(repoRoot, options.base);
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
  const projectRoots = discoverProjectRoots(repoRoot, changedFiles);
  const sourceDiff = git(
    repoRoot,
    ["diff", "--unified=0", "--no-color", baseCommit, "--", "*.tsp"],
    { allowFailure: true },
  ).stdout;
  const sourceReferences = [
    ...parseSourceHunks(sourceDiff, "head", baseCommit, remoteUrl),
    ...untrackedReferences(repoRoot, changedFiles),
  ];

  rmSync(outputRoot, { recursive: true, force: true });
  mkdirSync(outputRoot, { recursive: true });

  const tempRoot = mkdtempSync(join(tmpdir(), "typespec-assessment-"));
  const baseCheckout = join(tempRoot, "base");
  const projects = [];
  try {
    if (!options.skipCompile && projectRoots.length > 0) {
      git(repoRoot, ["worktree", "add", "--detach", baseCheckout, baseCommit]);
    }
    for (const projectRoot of projectRoots) {
      const project = {
        path: projectRoot,
        compilations: [],
        artifactDiffs: [],
      };
      if (!options.skipCompile) {
        project.compilations.push(
          compileProject({
            repoRoot,
            checkoutRoot: baseCheckout,
            projectRoot,
            side: "base",
            outputRoot,
            tempRoot,
          }),
        );
        project.compilations.push(
          compileProject({
            repoRoot,
            checkoutRoot: repoRoot,
            projectRoot,
            side: "head",
            outputRoot,
            tempRoot,
          }),
        );
        project.artifactDiffs = createArtifactDiffs(outputRoot, projectRoot);
      }
      projects.push(project);
    }
  } finally {
    if (existsSync(baseCheckout)) {
      git(repoRoot, ["worktree", "remove", "--force", baseCheckout], {
        allowFailure: true,
      });
    }
    rmSync(tempRoot, { recursive: true, force: true });
  }

  const evidence = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
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
    compileSkipped: options.skipCompile,
    errors: projects.flatMap((project) =>
      project.compilations
        .filter((compilation) => compilation.status === "failed")
        .map((compilation) => ({
          project: project.path,
          side: compilation.side,
          message:
            "One or more TypeSpec emitter compilations failed. See compile logs.",
        })),
    ),
  };
  writeFileSync(
    join(outputRoot, "evidence.json"),
    `${JSON.stringify(evidence, null, 2)}\n`,
  );
  return evidence;
}

function main() {
  try {
    const options = parseArgs(process.argv.slice(2));
    const evidence = prepareAssessment(options);
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
