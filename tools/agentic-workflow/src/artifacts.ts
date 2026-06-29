/**
 * Artifact storage: resolve the run directory, write atomically, and keep the scratch tree out
 * of git WITHOUT mutating any tracked file.
 *
 * Deliberately minimal (thinnerplan T1.3): no content hashing, no run lock yet — those are
 * deferred until a real format/concurrency need appears.
 */
import { spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import * as fs from "node:fs";
import * as path from "node:path";

export const ARTIFACT_ROOT = ".agentic-workflow";

/** Resolve the git repo root, or fall back to cwd when not in a git repo. */
export function repoRoot(cwd: string = process.cwd()): string {
    const r = spawnSync("git", ["rev-parse", "--show-toplevel"], { cwd, encoding: "utf8" });
    if (r.status === 0 && r.stdout.trim()) {
        return r.stdout.trim();
    }
    return cwd;
}

export function isGitRepo(cwd: string = process.cwd()): boolean {
    const r = spawnSync("git", ["rev-parse", "--is-inside-work-tree"], { cwd, encoding: "utf8" });
    return r.status === 0 && r.stdout.trim() === "true";
}

/** Slugify a task into a filesystem-safe fragment. */
export function slugify(task: string, max = 32): string {
    const slug = task
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "")
        .slice(0, max)
        .replace(/-+$/g, "");
    return slug || "task";
}

/** Human-readable run id: `YYYYMMDD-HHMM-<task-slug>`. */
export function makeRunId(task: string, now: Date = new Date()): string {
    const pad = (n: number) => String(n).padStart(2, "0");
    const ts =
        `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}` +
        `-${pad(now.getHours())}${pad(now.getMinutes())}`;
    return `${ts}-${slugify(task)}`;
}

export interface RunDir {
    /** Absolute path to the run directory. */
    dir: string;
    /** Absolute path to the artifact root (the locally-ignored scratch tree). */
    root: string;
}

/**
 * Resolve and create the run directory, applying local git-ignore without touching tracked files.
 * When `outRoot` is provided and lives outside the default scratch root, ignoring is skipped (the
 * user is explicitly choosing where artifacts land).
 */
export function ensureRunDir(runId: string, opts: { cwd?: string; outRoot?: string } = {}): RunDir {
    const cwd = opts.cwd ?? process.cwd();
    const base = repoRoot(cwd);
    const usingDefaultRoot = !opts.outRoot;
    const root = path.resolve(base, opts.outRoot ?? ARTIFACT_ROOT);
    const dir = path.join(root, runId);
    fs.mkdirSync(dir, { recursive: true });

    if (usingDefaultRoot) {
        applyLocalIgnore(base, root);
    }
    return { dir, root };
}

/**
 * Ignore the scratch tree two ways, neither of which edits the tracked root `.gitignore`:
 *  1. `.git/info/exclude` — a local, untracked ignore file.
 *  2. an inner `.agentic-workflow/.gitignore` (`*` + `!.gitignore`) that travels with the dir.
 * Both no-op if already present or if this is not a git repo.
 */
function applyLocalIgnore(base: string, root: string): void {
    const innerGitignore = path.join(root, ".gitignore");
    if (!fs.existsSync(innerGitignore)) {
        fs.mkdirSync(root, { recursive: true });
        fs.writeFileSync(innerGitignore, "*\n!.gitignore\n", "utf8");
    }

    if (!isGitRepo(base)) {
        return;
    }
    const excludePath = path.join(base, ".git", "info", "exclude");
    const rel = `${path.relative(base, root)}/`;
    try {
        let contents = fs.existsSync(excludePath) ? fs.readFileSync(excludePath, "utf8") : "";
        const lines = contents.split(/\r?\n/).map((l) => l.trim());
        if (!lines.includes(rel)) {
            if (contents.length && !contents.endsWith("\n")) {
                contents += "\n";
            }
            contents += `${rel}\n`;
            fs.mkdirSync(path.dirname(excludePath), { recursive: true });
            fs.writeFileSync(excludePath, contents, "utf8");
        }
    } catch {
        // Local ignore is best-effort; the inner .gitignore already covers the tree.
    }
}

/** Write a file atomically (temp file + rename) within the run dir, creating parents. */
export function atomicWrite(filePath: string, content: string): void {
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    const tmp = `${filePath}.${randomBytes(6).toString("hex")}.tmp`;
    fs.writeFileSync(tmp, content, "utf8");
    fs.renameSync(tmp, filePath);
}

/**
 * Resolve a relative artifact path inside the run dir, rejecting traversal/absolute escapes.
 * This is the guard behind the `write_artifact` custom tool.
 */
export function resolveInRunDir(runDir: string, relPath: string): string {
    const normalized = path.normalize(relPath);
    if (path.isAbsolute(normalized) || normalized.startsWith("..")) {
        throw new Error(`write_artifact: path escapes run dir: ${relPath}`);
    }
    const abs = path.resolve(runDir, normalized);
    const rel = path.relative(runDir, abs);
    if (rel.startsWith("..") || path.isAbsolute(rel)) {
        throw new Error(`write_artifact: path escapes run dir: ${relPath}`);
    }
    return abs;
}

/** Append text to an artifact (used for execution-log.md / handoff.md). */
export function appendArtifact(filePath: string, content: string): void {
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.appendFileSync(filePath, content, "utf8");
}

export function readIfExists(filePath: string): string | undefined {
    return fs.existsSync(filePath) ? fs.readFileSync(filePath, "utf8") : undefined;
}
