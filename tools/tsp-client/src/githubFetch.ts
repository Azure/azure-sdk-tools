import { spawn } from "child_process";
import { mkdir, readdir, rm, writeFile } from "fs/promises";
import { dirname, join as joinFsPath } from "path";
import { Logger } from "./log.js";

/**
 * Options that control how a GitHub-based fetch is performed.
 *
 * Resolved once per top-level call via {@link resolveGitHubFetchOptions} and
 * propagated to all nested calls so the strategy stays consistent for an
 * entire spec download.
 */
export interface GitHubFetchOptions {
  useGhCli: boolean;
  token?: string;
}

/**
 * Returns the GitHub token from the environment, preferring `GITHUB_TOKEN`
 * over `GH_TOKEN` (the variables `gh` itself recognises).
 */
export function getGitHubToken(): string | undefined {
  return process.env["GITHUB_TOKEN"] || process.env["GH_TOKEN"] || undefined;
}

let ghCliCache: Promise<boolean> | undefined;
let ghCliOverrideForTests: boolean | undefined;

/**
 * Test-only hook: forces {@link isGhCliAvailable} to return a fixed value.
 * Call with `undefined` to reset back to live detection.
 */
export function _setGhCliAvailableForTests(value: boolean | undefined): void {
  ghCliOverrideForTests = value;
  ghCliCache = undefined;
}

/**
 * Returns `true` when `gh --version` succeeds on PATH. The result is cached
 * for the lifetime of the process.
 */
export async function isGhCliAvailable(): Promise<boolean> {
  if (ghCliOverrideForTests !== undefined) {
    return ghCliOverrideForTests;
  }
  if (!ghCliCache) {
    ghCliCache = new Promise<boolean>((resolve) => {
      try {
        const proc = spawn("gh", ["--version"], {
          stdio: "ignore",
          shell: process.platform === "win32",
        });
        proc.once("error", () => resolve(false));
        proc.once("exit", (code) => resolve(code === 0));
      } catch {
        resolve(false);
      }
    });
  }
  return ghCliCache;
}

interface SpawnResult {
  code: number | null;
  stdout: Buffer;
  stderr: string;
}

/**
 * Test-only hook for stubbing out the `gh` subprocess. Pass `undefined` to
 * restore the real spawn-based runner.
 */
export type GhRunner = (args: string[]) => Promise<SpawnResult>;
let ghRunnerOverride: GhRunner | undefined;
export function _setGhRunnerForTests(runner: GhRunner | undefined): void {
  ghRunnerOverride = runner;
}

async function runGh(args: string[]): Promise<SpawnResult> {
  if (ghRunnerOverride) {
    return ghRunnerOverride(args);
  }
  return new Promise((resolve, reject) => {
    const proc = spawn("gh", args, { shell: process.platform === "win32" });
    const stdoutChunks: Buffer[] = [];
    let stderr = "";
    proc.stdout.on("data", (d: Buffer) => stdoutChunks.push(d));
    proc.stderr.on("data", (d: Buffer) => {
      stderr += d.toString();
    });
    proc.once("error", reject);
    proc.once("exit", (code) => {
      resolve({ code, stdout: Buffer.concat(stdoutChunks), stderr });
    });
  });
}

async function ghApiJson(path: string): Promise<any> {
  const result = await runGh(["api", path]);
  if (result.code !== 0) {
    throw new Error(
      `gh api ${path} failed (exit ${result.code}): ${result.stderr.trim() || "no stderr"}`,
    );
  }
  try {
    return JSON.parse(result.stdout.toString("utf-8"));
  } catch (err) {
    throw new Error(`gh api ${path} returned non-JSON output: ${(err as Error).message}`);
  }
}

async function readGhAuthToken(): Promise<string | undefined> {
  try {
    const result = await runGh(["auth", "token"]);
    if (result.code === 0) {
      const token = result.stdout.toString("utf-8").trim();
      return token || undefined;
    }
  } catch {
    // ignore
  }
  return undefined;
}

async function fetchWithRetry(url: string, token?: string, accept?: string): Promise<Response> {
  const headers: Record<string, string> = {
    "User-Agent": "azure-tools-tsp-client",
  };
  if (accept) headers["Accept"] = accept;
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const maxAttempts = 3;
  let lastErr: unknown;
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const res = await fetch(url, { headers });
      if (
        (res.status === 429 || (res.status >= 500 && res.status < 600)) &&
        attempt < maxAttempts
      ) {
        const wait = 500 * Math.pow(2, attempt - 1);
        Logger.debug(`GET ${url} -> ${res.status}; retrying in ${wait}ms`);
        await new Promise((r) => setTimeout(r, wait));
        continue;
      }
      return res;
    } catch (err) {
      lastErr = err;
      if (attempt < maxAttempts) {
        const wait = 500 * attempt;
        Logger.debug(`GET ${url} threw ${(err as Error).message}; retrying in ${wait}ms`);
        await new Promise((r) => setTimeout(r, wait));
        continue;
      }
      throw err;
    }
  }
  throw lastErr;
}

async function fetchJson(url: string, token?: string): Promise<any> {
  const res = await fetchWithRetry(url, token, "application/vnd.github+json");
  if (!res.ok) {
    throw new Error(`GET ${url} failed: ${res.status} ${res.statusText}`);
  }
  return res.json();
}

async function fetchBuffer(url: string, token?: string): Promise<Buffer> {
  const res = await fetchWithRetry(url, token);
  if (!res.ok) {
    throw new Error(`GET ${url} failed: ${res.status} ${res.statusText}`);
  }
  const arr = await res.arrayBuffer();
  return Buffer.from(arr);
}

/**
 * Builds the `raw.githubusercontent.com` URL for a file pinned to a specific
 * commit SHA in `repo` (e.g. `Azure/azure-rest-api-specs`).
 */
export function buildRawUrl(repo: string, commit: string, path: string): string {
  return `https://raw.githubusercontent.com/${repo}/${commit}/${encodePath(path)}`;
}

/**
 * Builds the recursive Trees API URL for a tree SHA in `repo`.
 */
export function buildTreeApiUrl(repo: string, treeSha: string, recursive: boolean): string {
  const suffix = recursive ? "?recursive=1" : "";
  return `https://api.github.com/repos/${repo}/git/trees/${treeSha}${suffix}`;
}

function encodePath(path: string): string {
  return path
    .split("/")
    .map((segment) => encodeURIComponent(segment))
    .join("/");
}

export interface TreeBlob {
  /** Path relative to repo root. */
  path: string;
  sha: string;
  size?: number;
}

interface TreeEntry {
  path: string;
  type: "blob" | "tree" | "commit";
  sha: string;
  size?: number;
}

interface TreeResponse {
  sha: string;
  tree: TreeEntry[];
  truncated: boolean;
}

async function getTreeRecursive(
  repo: string,
  treeSha: string,
  opts: GitHubFetchOptions,
): Promise<TreeResponse> {
  if (opts.useGhCli) {
    return ghApiJson(`repos/${repo}/git/trees/${treeSha}?recursive=1`);
  }
  return fetchJson(buildTreeApiUrl(repo, treeSha, true), opts.token);
}

async function getTreeShallow(
  repo: string,
  treeSha: string,
  opts: GitHubFetchOptions,
): Promise<TreeResponse> {
  if (opts.useGhCli) {
    return ghApiJson(`repos/${repo}/git/trees/${treeSha}`);
  }
  return fetchJson(buildTreeApiUrl(repo, treeSha, false), opts.token);
}

async function getCommitRootTreeSha(
  repo: string,
  commit: string,
  opts: GitHubFetchOptions,
): Promise<string> {
  const info = opts.useGhCli
    ? await ghApiJson(`repos/${repo}/commits/${commit}`)
    : await fetchJson(`https://api.github.com/repos/${repo}/commits/${commit}`, opts.token);
  const sha = info?.commit?.tree?.sha;
  if (typeof sha !== "string") {
    throw new Error(`Could not resolve root tree SHA for ${repo}@${commit}`);
  }
  return sha;
}

/**
 * Walks `subDir` segment-by-segment from the commit's root tree to its tree SHA.
 * Throws when any segment is missing or is not a directory.
 */
async function resolveSubtreeSha(
  repo: string,
  commit: string,
  subDir: string,
  opts: GitHubFetchOptions,
): Promise<string> {
  let currentSha = await getCommitRootTreeSha(repo, commit, opts);
  const segments = subDir.split("/").filter((s) => s.length > 0);
  for (const seg of segments) {
    const tree = await getTreeShallow(repo, currentSha, opts);
    const entry = tree.tree.find((e) => e.path === seg && e.type === "tree");
    if (!entry) {
      throw new Error(
        `Could not find directory segment "${seg}" while resolving "${subDir}" in ${repo}@${commit}`,
      );
    }
    currentSha = entry.sha;
  }
  return currentSha;
}

/**
 * Recursively collects every blob under `treeSha`, transparently handling
 * GitHub's `truncated: true` response by descending into immediate subtrees.
 */
async function collectBlobs(
  repo: string,
  treeSha: string,
  pathPrefix: string,
  opts: GitHubFetchOptions,
): Promise<TreeBlob[]> {
  const tree = await getTreeRecursive(repo, treeSha, opts);
  if (!tree.truncated) {
    const blobs: TreeBlob[] = [];
    for (const entry of tree.tree) {
      if (entry.type === "blob") {
        blobs.push({
          path: pathPrefix ? `${pathPrefix}/${entry.path}` : entry.path,
          sha: entry.sha,
          size: entry.size,
        });
      }
    }
    return blobs;
  }

  Logger.debug(`Tree ${treeSha} is truncated; recursing into immediate subtrees`);
  const shallow = await getTreeShallow(repo, treeSha, opts);
  const results: TreeBlob[] = [];
  for (const entry of shallow.tree) {
    const childPath = pathPrefix ? `${pathPrefix}/${entry.path}` : entry.path;
    if (entry.type === "blob") {
      results.push({ path: childPath, sha: entry.sha, size: entry.size });
    } else if (entry.type === "tree") {
      const nested = await collectBlobs(repo, entry.sha, childPath, opts);
      results.push(...nested);
    }
    // submodules ("commit") are skipped intentionally
  }
  return results;
}

/**
 * Returns every blob under `subDir` at `commit`, with paths relative to repo root.
 */
export async function listTree(
  repo: string,
  commit: string,
  subDir: string,
  opts: GitHubFetchOptions,
): Promise<TreeBlob[]> {
  const subtreeSha = await resolveSubtreeSha(repo, commit, subDir, opts);
  return collectBlobs(repo, subtreeSha, subDir, opts);
}

async function downloadBlob(
  repo: string,
  commit: string,
  relativePath: string,
  destFile: string,
  opts: GitHubFetchOptions,
): Promise<void> {
  await mkdir(dirname(destFile), { recursive: true });
  const buf = await fetchBuffer(buildRawUrl(repo, commit, relativePath), opts.token);
  await writeFile(destFile, buf);
}

const DEFAULT_CONCURRENCY = 8;

async function runWithConcurrency<T>(
  tasks: (() => Promise<T>)[],
  concurrency: number,
): Promise<T[]> {
  const results: T[] = new Array(tasks.length);
  let nextIndex = 0;
  async function worker(): Promise<void> {
    while (true) {
      const idx = nextIndex++;
      if (idx >= tasks.length) return;
      results[idx] = await tasks[idx]!();
    }
  }
  const workerCount = Math.max(1, Math.min(concurrency, tasks.length));
  const workers: Promise<void>[] = [];
  for (let i = 0; i < workerCount; i++) {
    workers.push(worker());
  }
  await Promise.all(workers);
  return results;
}

/**
 * Resolves how a GitHub fetch should be performed. Prefers the local `gh`
 * CLI when available (and uses `gh auth token` to authenticate raw downloads
 * when no env token is set). Falls back to env-only auth otherwise.
 */
export async function resolveGitHubFetchOptions(): Promise<GitHubFetchOptions> {
  const useGhCli = await isGhCliAvailable();
  let token = getGitHubToken();
  if (!token && useGhCli) {
    token = await readGhAuthToken();
  }
  return { useGhCli, token };
}

/**
 * Downloads a single file (e.g. `tspconfig.yaml`) from GitHub into `destFile`.
 */
export async function downloadFileFromGitHub(args: {
  repo: string;
  commit: string;
  path: string;
  destFile: string;
  opts?: GitHubFetchOptions;
}): Promise<void> {
  const opts = args.opts ?? (await resolveGitHubFetchOptions());
  await downloadBlob(args.repo, args.commit, args.path, args.destFile, opts);
}

/**
 * Downloads every file under `directory` (recursively) into
 * `${destRoot}/${directory}/...`, preserving repo-relative subpaths.
 */
export async function downloadDirectoryFromGitHub(args: {
  repo: string;
  commit: string;
  directory: string;
  destRoot: string;
  opts?: GitHubFetchOptions;
}): Promise<void> {
  const opts = args.opts ?? (await resolveGitHubFetchOptions());
  Logger.debug(
    `Listing ${args.repo}@${args.commit}:${args.directory} via ${
      opts.useGhCli ? "gh CLI" : "GitHub REST API"
    }`,
  );
  const blobs = await listTree(args.repo, args.commit, args.directory, opts);
  Logger.debug(`Found ${blobs.length} blob(s) under ${args.directory}`);
  const tasks = blobs.map((blob) => async () => {
    const destFile = joinPosix(args.destRoot, blob.path);
    await downloadBlob(args.repo, args.commit, blob.path, destFile, opts);
  });
  await runWithConcurrency(tasks, DEFAULT_CONCURRENCY);
}

function joinPosix(...parts: string[]): string {
  const normalized = parts
    .map((p) => p.replace(/\\/g, "/"))
    .map((p, i) => (i === 0 ? p.replace(/\/+$/, "") : p.replace(/^\/+|\/+$/g, "")))
    .filter((p) => p.length > 0);
  return normalized.join("/");
}

/**
 * Best-effort fetch of a spec directory (and any additional directories) from
 * GitHub. Returns `true` on success and `false` on any error (logged at debug
 * level) so callers can transparently fall back to a git sparse clone.
 *
 * Strategy order: gh CLI (if available) → REST API → return false. On any
 * failure, partial writes under `destRoot` are removed so a subsequent
 * `git clone` into the same directory works cleanly.
 */
export async function tryFetchSpecFromGitHub(args: {
  repo: string;
  commit: string;
  directory: string;
  additionalDirectories?: string[];
  destRoot: string;
}): Promise<boolean> {
  const doFetch = async (opts: GitHubFetchOptions) => {
    await downloadDirectoryFromGitHub({
      repo: args.repo,
      commit: args.commit,
      directory: args.directory,
      destRoot: args.destRoot,
      opts,
    });
    for (const dir of args.additionalDirectories ?? []) {
      Logger.debug(`Fetching additional directory from GitHub: ${dir}`);
      await downloadDirectoryFromGitHub({
        repo: args.repo,
        commit: args.commit,
        directory: dir,
        destRoot: args.destRoot,
        opts,
      });
    }
  };
  return runWithFallback({
    label: `spec fetch ${args.repo}@${args.commit}:${args.directory}`,
    cleanupRoot: args.destRoot,
    work: doFetch,
  });
}

/**
 * Best-effort fetch of a single file from GitHub. Returns `true` on success
 * and `false` on any error (logged at debug level).
 *
 * Strategy order matches {@link tryFetchSpecFromGitHub}. When `destRoot` is
 * provided, partial writes under it are removed on failure so a subsequent
 * `git clone` into the same directory works cleanly.
 */
export async function tryFetchFileFromGitHub(args: {
  repo: string;
  commit: string;
  path: string;
  destFile: string;
  destRoot?: string;
}): Promise<boolean> {
  const doFetch = async (opts: GitHubFetchOptions) => {
    await downloadFileFromGitHub({
      repo: args.repo,
      commit: args.commit,
      path: args.path,
      destFile: args.destFile,
      opts,
    });
  };
  return runWithFallback({
    label: `file fetch ${args.repo}@${args.commit}:${args.path}`,
    cleanupRoot: args.destRoot,
    work: doFetch,
  });
}

/**
 * Runs `work` with the resolved primary strategy. If the primary strategy is
 * the `gh` CLI and it fails, retries once via the REST API path before giving
 * up. Empties `cleanupRoot` between attempts and on final failure to keep the
 * destination directory in a clean state for the git fallback.
 */
async function runWithFallback(args: {
  label: string;
  cleanupRoot?: string;
  work: (opts: GitHubFetchOptions) => Promise<void>;
}): Promise<boolean> {
  const primary = await resolveGitHubFetchOptions();
  Logger.debug(`Attempting GitHub-based ${args.label} via ${describeStrategy(primary)}`);
  try {
    await args.work(primary);
    return true;
  } catch (primaryErr) {
    Logger.debug(
      `GitHub-based ${args.label} via ${describeStrategy(primary)} failed: ${
        (primaryErr as Error).message
      }`,
    );
    if (primary.useGhCli) {
      // gh CLI may be installed but unauthenticated, rate-limited, or otherwise
      // failing for transient reasons. Try the REST API path before giving up.
      if (args.cleanupRoot) {
        await emptyDirectoryContents(args.cleanupRoot);
      }
      const fallback: GitHubFetchOptions = {
        useGhCli: false,
        token: primary.token ?? getGitHubToken(),
      };
      Logger.debug(`Retrying ${args.label} via ${describeStrategy(fallback)}`);
      try {
        await args.work(fallback);
        return true;
      } catch (fallbackErr) {
        Logger.debug(
          `GitHub-based ${args.label} via ${describeStrategy(fallback)} failed: ${
            (fallbackErr as Error).message
          }`,
        );
      }
    }
    if (args.cleanupRoot) {
      await emptyDirectoryContents(args.cleanupRoot);
    }
    Logger.debug(`Falling back to git sparse clone for ${args.label}`);
    return false;
  }
}

/**
 * Removes every entry inside `dir` while leaving `dir` itself in place. Used
 * to roll back partial GitHub-fetch writes so a subsequent `git clone` into
 * the same directory does not complain about a non-empty target.
 */
async function emptyDirectoryContents(dir: string): Promise<void> {
  let entries: string[];
  try {
    entries = await readdir(dir);
  } catch {
    return;
  }
  await Promise.all(
    entries.map((entry) => rm(joinFsPath(dir, entry), { recursive: true, force: true })),
  );
}

function describeStrategy(opts: GitHubFetchOptions): string {
  if (opts.useGhCli) {
    return opts.token ? "gh CLI (+ token for raw)" : "gh CLI";
  }
  return opts.token ? "REST API (authenticated)" : "REST API (unauthenticated)";
}
