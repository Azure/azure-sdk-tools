import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

function git(repo, args, options = {}) {
  const result = spawnSync("git", ["-C", repo, ...args], {
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
    ...options,
  });
  if (result.status !== 0 && !options.allowFailure) {
    throw new Error(`git ${args.join(" ")} failed: ${result.stderr.trim()}`);
  }
  return result;
}

export function resolveComparison(repo, baseRef) {
  const mergeBase = git(repo, ["merge-base", "HEAD", baseRef]).stdout.trim();
  const headCommit = git(repo, ["rev-parse", "HEAD"]).stdout.trim();
  const remoteUrl = git(repo, ["remote", "get-url", "origin"], {
    allowFailure: true,
  }).stdout.trim();
  return { baseRef, mergeBaseCommit: mergeBase, headCommit, remoteUrl };
}

function nameStatus(repo, args, origin) {
  const output = git(repo, args).stdout.trim();
  if (!output) return [];
  return output.split(/\r?\n/).map((line) => {
    const fields = line.split("\t");
    const code = fields[0][0];
    return {
      path: fields.at(-1).replaceAll("\\", "/"),
      previousPath: fields.length > 2 ? fields[1].replaceAll("\\", "/") : undefined,
      status: code === "A" ? "added" : code === "D" ? "removed" : "modified",
      origin,
    };
  });
}

export function collectChanges(repo, mergeBase, scope) {
  const scoped = scope ? ["--", scope] : [];
  const entries = [
    ...nameStatus(repo, ["diff", "--name-status", mergeBase, "HEAD", ...scoped], "committed"),
    ...nameStatus(repo, ["diff", "--cached", "--name-status", ...scoped], "staged"),
    ...nameStatus(repo, ["diff", "--name-status", ...scoped], "unstaged"),
  ];
  const untracked = git(repo, [
    "ls-files",
    "--others",
    "--exclude-standard",
    ...(scope ? ["--", scope] : []),
  ]).stdout
    .trim()
    .split(/\r?\n/)
    .filter(Boolean)
    .map((file) => ({
      path: file.replaceAll("\\", "/"),
      status: "added",
      origin: "untracked",
    }));
  const merged = new Map();
  for (const entry of [...entries, ...untracked]) {
    if (!entry.path.endsWith(".tsp") && path.basename(entry.path) !== "tspconfig.yaml") continue;
    const current = merged.get(entry.path) ?? { ...entry, origins: [] };
    current.status = entry.status;
    if (!current.origins.includes(entry.origin)) current.origins.push(entry.origin);
    merged.set(entry.path, current);
  }
  return [...merged.values()].sort((left, right) => left.path.localeCompare(right.path));
}

export function readRevisionFile(repo, revision, file) {
  if (revision === "working") {
    const fullPath = path.join(repo, file);
    return fs.existsSync(fullPath) ? fs.readFileSync(fullPath, "utf8") : null;
  }
  const result = git(repo, ["show", `${revision}:${file}`], { allowFailure: true });
  return result.status === 0 ? result.stdout : null;
}

export function unifiedDiff(repo, mergeBase, file) {
  return git(repo, ["diff", "--no-ext-diff", "--unified=3", mergeBase, "--", file]).stdout;
}

export function deriveServiceRoot(specification) {
  const normalized = specification.replaceAll("\\", "/").replace(/^\.?\//, "");
  if (normalized === "specification") return normalized;
  const match = /^(specification\/[^/]+)/.exec(normalized);
  if (!match) throw new Error(`Specification must be under specification/<service>: ${specification}`);
  return match[1];
}

export function discoverProjects(repo, files, serviceRoot) {
  const projects = new Set();
  let hasSharedChange = false;
  for (const file of files) {
    let directory = path.dirname(path.join(repo, file.path));
    const boundary = path.join(repo, serviceRoot);
    let matched = false;
    while (directory.startsWith(boundary)) {
      if (fs.existsSync(path.join(directory, "tspconfig.yaml"))) {
        projects.add(path.relative(repo, directory).replaceAll("\\", "/"));
        matched = true;
        break;
      }
      if (directory === boundary) break;
      directory = path.dirname(directory);
    }
    if (!matched) hasSharedChange = true;
  }
  if (hasSharedChange) {
    const visit = (directory) => {
      if (!fs.existsSync(directory)) return;
      for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;
        const child = path.join(directory, entry.name);
        if (fs.existsSync(path.join(child, "tspconfig.yaml"))) {
          projects.add(path.relative(repo, child).replaceAll("\\", "/"));
        } else {
          visit(child);
        }
      }
    };
    visit(path.join(repo, serviceRoot));
  }
  return [...projects].sort();
}

export function createSparseWorktree(repo, commit, serviceRoot, destination) {
  if (fs.existsSync(destination)) {
    throw new Error(`Worktree destination already exists: ${destination}`);
  }
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  git(repo, ["worktree", "add", "--detach", "--no-checkout", destination, commit]);
  git(destination, ["sparse-checkout", "init", "--cone"]);
  git(destination, ["sparse-checkout", "set", serviceRoot]);
  git(destination, ["checkout", "--detach", commit]);
  const roots = git(destination, ["sparse-checkout", "list"]).stdout
    .trim()
    .split(/\r?\n/)
    .filter(Boolean)
    .map((item) => item.replaceAll("\\", "/"));
  if (roots.length !== 1 || roots[0] !== serviceRoot) {
    throw new Error(`Sparse checkout verification failed for ${destination}.`);
  }
  return roots;
}

export function removeWorktree(repo, destination) {
  if (fs.existsSync(destination)) {
    git(repo, ["worktree", "remove", "--force", destination], { allowFailure: true });
  }
}
