import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { parseArgs, isMain, runMain, writeJson } from "./cli.mjs";
import {
  collectChanges,
  createSparseWorktree,
  deriveServiceRoot,
  discoverProjects,
  resolveComparison,
} from "./git-evidence.mjs";
import { addCompilerEvidence, buildSourceIndex } from "./source-index.mjs";
import { runProjectCompilers } from "./compiler-runner.mjs";
import { resolveProjectApiVersions } from "./api-version-selection.mjs";

const REQUIRED_TOOLCHAIN_PACKAGES = [
  "@typespec/compiler",
  "@typespec/openapi3",
  "@azure-tools/typespec-autorest",
  "@azure-tools/typespec-azure-resource-manager",
  "@azure-tools/typespec-client-generator-core",
];

export function preflightToolchain(root) {
  const lockPath = path.join(root, "package-lock.json");
  if (!fs.existsSync(lockPath)) throw new Error(`package-lock.json is missing from ${root}`);
  const lock = JSON.parse(fs.readFileSync(lockPath, "utf8"));
  const failures = [];
  for (const packageName of REQUIRED_TOOLCHAIN_PACKAGES) {
    const expected = lock.packages?.[`node_modules/${packageName}`]?.version;
    const packagePath = path.join(root, "node_modules", ...packageName.split("/"), "package.json");
    if (!expected) {
      failures.push(`${packageName} is absent from package-lock.json`);
      continue;
    }
    if (!fs.existsSync(packagePath)) {
      failures.push(`${packageName} is not installed`);
      continue;
    }
    const installed = JSON.parse(fs.readFileSync(packagePath, "utf8")).version;
    if (installed !== expected) {
      failures.push(`${packageName} installed version ${installed} does not match lock version ${expected}`);
    }
  }
  if (failures.length) throw new Error(failures.join("\n"));
  return { packages: REQUIRED_TOOLCHAIN_PACKAGES };
}

function stableProjectId(project) {
  return `project-${crypto.createHash("sha256").update(project).digest("hex").slice(0, 12)}`;
}

function copyOverlay(repo, currentWorktree, changedFiles) {
  for (const file of changedFiles) {
    const source = path.join(repo, file.path);
    const target = path.join(currentWorktree, file.path);
    if (fs.existsSync(source)) {
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.copyFileSync(source, target);
    } else if (fs.existsSync(target)) {
      fs.rmSync(target);
    }
  }
}

function ensureDependencies(worktree, work, reuseRoot) {
  const lockFile = path.join(worktree, "package-lock.json");
  const packageFile = path.join(worktree, "package.json");
  if (!fs.existsSync(lockFile) || !fs.existsSync(packageFile)) {
    throw new Error(`Root package.json/package-lock.json are unavailable in ${worktree}.`);
  }
  const lockHash = crypto.createHash("sha256").update(fs.readFileSync(lockFile)).digest("hex");
  const cache = path.join(work, "cache", "toolchains", lockHash);
  const source = path.join(cache, "node_modules");
  const target = path.join(worktree, "node_modules");
  if (fs.existsSync(target)) return;
  if (!fs.existsSync(source)) {
    fs.mkdirSync(cache, { recursive: true });
    fs.copyFileSync(packageFile, path.join(cache, "package.json"));
    fs.copyFileSync(lockFile, path.join(cache, "package-lock.json"));
    let reused = false;
    if (reuseRoot && fs.existsSync(path.join(reuseRoot, "node_modules"))) {
      try {
        const reuseLock = fs.readFileSync(path.join(reuseRoot, "package-lock.json"));
        const reuseLockHash = crypto.createHash("sha256").update(reuseLock).digest("hex");
        if (reuseLockHash !== lockHash) throw new Error("lockfile hash mismatch");
        preflightToolchain(reuseRoot);
        fs.symlinkSync(
          path.join(reuseRoot, "node_modules"),
          source,
          process.platform === "win32" ? "junction" : "dir",
        );
        reused = true;
      } catch {
        reused = false;
      }
    }
    if (!reused) {
      const npm = process.platform === "win32" ? "npm.cmd" : "npm";
      const install = spawnSync(npm, ["ci", "--ignore-scripts", "--no-audit", "--no-fund"], {
        cwd: cache,
        encoding: "utf8",
        maxBuffer: 64 * 1024 * 1024,
        shell: process.platform === "win32",
      });
      fs.writeFileSync(
        path.join(cache, "npm-ci.log"),
        [install.stdout, install.stderr].filter(Boolean).join("\n"),
      );
      if (install.status !== 0) {
        throw new Error(`npm ci failed for toolchain ${lockHash.slice(0, 12)}.`);
      }
    }
  }
  fs.symlinkSync(source, target, process.platform === "win32" ? "junction" : "dir");
  preflightToolchain(worktree);
}

function findExternalLocalImports(repo, projects, serviceRoot) {
  const serviceBoundary = path.resolve(repo, serviceRoot);
  const failures = [];
  const visit = (directory) => {
    if (!fs.existsSync(directory)) return;
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) visit(file);
      else if (entry.name.endsWith(".tsp")) {
        const content = fs.readFileSync(file, "utf8");
        for (const match of content.matchAll(/\bimport\s+["']([^"']+)["']/g)) {
          if (!match[1].startsWith(".")) continue;
          const resolved = path.resolve(path.dirname(file), match[1]);
          if (resolved !== serviceBoundary && !resolved.startsWith(`${serviceBoundary}${path.sep}`)) {
            failures.push({
              file: path.relative(repo, file).replaceAll("\\", "/"),
              import: match[1],
            });
          }
        }
      }
    }
  };
  for (const project of projects) visit(path.join(repo, project));
  return failures;
}

export async function prepareAssessment({ repo, base, specification, output }) {
  const started = performance.now();
  const repository = path.resolve(repo ?? process.cwd());
  const work = path.resolve(output);
  fs.mkdirSync(work, { recursive: true });
  const comparison = resolveComparison(repository, base ?? "origin/main");
  const serviceRoot = deriveServiceRoot(specification);
  const changedFiles = collectChanges(repository, comparison.mergeBaseCommit, specification);
  const blockers = [];
  const manifest = {
    schemaVersion: 1,
    repository: { root: repository, remoteUrl: comparison.remoteUrl },
    comparison: {
      baseRef: comparison.baseRef,
      mergeBaseCommit: comparison.mergeBaseCommit,
      headCommit: comparison.headCommit,
      workingTree: {
        staged: changedFiles.some((file) => file.origins.includes("staged")),
        unstaged: changedFiles.some((file) => file.origins.includes("unstaged")),
        untracked: changedFiles.some((file) => file.origins.includes("untracked")),
      },
    },
    sparseCheckout: { mode: "cone", roots: [serviceRoot], verified: false },
    changedFiles,
    projects: [],
    blockers,
    timings: {},
  };
  if (!changedFiles.length) {
    manifest.status = "no-changes";
    manifest.timings.totalMs = Math.round(performance.now() - started);
    writeJson(path.join(work, "preparation-manifest.json"), manifest);
    return manifest;
  }

  const sourceIndex = buildSourceIndex({
    repo: repository,
    mergeBase: comparison.mergeBaseCommit,
    headCommit: comparison.headCommit,
    changedFiles,
    remoteUrl: comparison.remoteUrl,
  });
  writeJson(path.join(work, "source", "changed-files.json"), changedFiles);
  writeJson(path.join(work, "source", "source-index.json"), sourceIndex);
  writeJson(
    path.join(work, "source", "typespec-diff.json"),
    sourceIndex.sourceChanges.map(({ id, path: file, hunks }) => ({ id, path: file, hunks })),
  );

  const baseWorktree = path.join(work, "worktrees", "base");
  const currentWorktree = path.join(work, "worktrees", "current");
  try {
    createSparseWorktree(repository, comparison.mergeBaseCommit, serviceRoot, baseWorktree);
    createSparseWorktree(repository, comparison.headCommit, serviceRoot, currentWorktree);
    manifest.sparseCheckout.verified = true;
    copyOverlay(repository, currentWorktree, changedFiles);
  } catch (error) {
    blockers.push({ code: "workspace-preparation-failed", message: error.message });
  }

  const discoveredProjects = discoverProjects(repository, changedFiles, serviceRoot);
  const projects = discoveredProjects.filter((project) =>
    !discoveredProjects.some((candidate) =>
      candidate !== project && candidate.startsWith(`${project}/`)));
  if (!projects.length) {
    blockers.push({
      code: "project-not-found",
      message: `No affected tspconfig.yaml was found under ${specification}.`,
    });
  }
  const externalImports = findExternalLocalImports(repository, projects, serviceRoot);
  if (externalImports.length) {
    blockers.push({
      code: "unsupported-import-outside-service",
      message: `Local imports leave ${serviceRoot}: ${externalImports
        .map((item) => `${item.file} -> ${item.import}`)
        .join(", ")}`,
    });
  }
  try {
    if (!blockers.length) {
      ensureDependencies(baseWorktree, work, repository);
      ensureDependencies(currentWorktree, work, repository);
    }
  } catch (error) {
    blockers.push({ code: "dependency-setup-failed", message: error.message });
  }
  if (!blockers.length && projects.length) {
    await addCompilerEvidence({
      sourceIndex,
      baseWorktree,
      currentWorktree,
      projects,
    });
    writeJson(path.join(work, "source", "source-index.json"), sourceIndex);
  }
  for (const project of projects) {
    const projectId = stableProjectId(project);
    const projectSourceIds = sourceIndex.sourceChanges
      .filter(
        (change) =>
          change.path.startsWith(`${project}/`) ||
          change.path === project ||
          !projects.some(
            (candidate) =>
              change.path.startsWith(`${candidate}/`) || change.path === candidate,
          ),
      )
      .map((change) => change.id);
    const record = { id: projectId, path: project, sourceChangeIds: projectSourceIds, artifacts: {} };
    if (!blockers.length) {
      try {
        record.artifactComparison = resolveProjectApiVersions({
          baseWorktree,
          currentWorktree,
          project,
          baseCommit: comparison.mergeBaseCommit,
          headCommit: comparison.headCommit,
        });
        record.apiVersions = {
          base: record.artifactComparison.baseline.apiVersion,
          current: record.artifactComparison.target.apiVersion,
          baseReason: record.artifactComparison.baseline.reason,
          currentReason: record.artifactComparison.target.reason,
          addedCurrentVersions: record.artifactComparison.addedCurrentVersions,
          available: record.artifactComparison.available,
        };
      } catch (error) {
        blockers.push({
          code: "api-version-resolution-failed",
          projectId,
          message: error.message,
        });
      }
    }
    if (!blockers.length) {
      for (const comparisonRole of ["baseline", "target"]) {
        const selection = record.artifactComparison[comparisonRole];
        const worktree = selection.sourceRevision === "base" ? baseWorktree : currentWorktree;
        try {
          record.artifacts[comparisonRole] = runProjectCompilers({
            worktree,
            project,
            projectId,
            comparisonRole,
            sourceRevision: selection.sourceRevision,
            sourceCommit: selection.commit,
            workRoot: work,
            apiVersion: selection.apiVersion,
          });
          for (const emitter of ["autorest", "tcgc"]) {
            if (record.artifacts[comparisonRole][emitter].status === "failed") {
              blockers.push({
                code: `${emitter}-compile-failed`,
                projectId,
                comparisonRole,
                sourceRevision: selection.sourceRevision,
                message: `${emitter} compilation failed for ${project} (${comparisonRole}: ${selection.sourceRevision}@${selection.apiVersion ?? "unversioned"}).`,
              });
            }
          }
        } catch (error) {
          blockers.push({
            code: "compiler-runner-failed",
            projectId,
            comparisonRole,
            sourceRevision: selection.sourceRevision,
            message: error.message,
          });
        }
      }
    }
    manifest.projects.push(record);
  }
  manifest.status = blockers.length ? "blocked" : "ready";
  manifest.timings.totalMs = Math.round(performance.now() - started);
  writeJson(path.join(work, "preparation-manifest.json"), manifest);
  return manifest;
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const args = parseArgs(process.argv.slice(2), {
      required: ["specification", "output"],
      defaults: { repo: process.cwd(), base: "origin/main" },
    });
    const result = await prepareAssessment(args);
    console.log(path.join(path.resolve(args.output), "preparation-manifest.json"));
    if (result.status === "blocked") process.exitCode = 1;
  });
}
