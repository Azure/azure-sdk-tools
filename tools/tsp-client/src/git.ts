import { spawn } from "child_process";
import { readdir } from "node:fs/promises";
import { isAbsolute, join } from "node:path";
import { fileURLToPath } from "node:url";
import { simpleGit } from "simple-git";

export async function getRepoRoot(repoPath: string): Promise<string> {
  return simpleGit(repoPath)
    .revparse(["--show-toplevel"])
    .catch((err) => {
      throw new Error(`Failed to get repo root: ${err}`);
    });
}

export async function cloneRepo(rootUrl: string, cloneDir: string, repo: string): Promise<void> {
  return new Promise((resolve, reject) => {
    simpleGit(rootUrl).clone(repo, cloneDir, ["--no-checkout", "--filter=tree:0"], (err) => {
      if (err) {
        reject(new Error(`git clone failed with error: ${err}`));
      }
      resolve();
    });
  });
}

export async function sparseCheckout(cloneDir: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const git = spawn("git", ["sparse-checkout", "init"], {
      cwd: cloneDir,
      stdio: "inherit",
    });
    git.once("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`git sparse-checkout failed exited with code ${code}`));
      }
    });
    git.once("error", (err) => {
      reject(new Error(`git sparse-checkout failed with error: ${err}`));
    });
  });
}

export async function addSpecFiles(cloneDir: string, ...subDirs: string[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const git = spawn("git", ["sparse-checkout", "add", ...subDirs], {
      cwd: cloneDir,
      stdio: "inherit",
    });
    git.once("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`git sparse-checkout add failed exited with code ${code}`));
      }
    });
    git.once("error", (err) => {
      reject(new Error(`git sparse-checkout add failed with error: ${err}`));
    });
  });
}

/** Expanding a detached checkout is only equivalent to a fresh checkout for ordinary Git settings. */
export async function canExpandSpecCheckout(cloneDir: string): Promise<boolean> {
  // GIT_CONFIG can redirect only the config probe, not checkout itself.
  if (
    [
      "GIT_CONFIG",
      "GIT_ATTR_SOURCE",
      "GIT_DIR",
      "GIT_COMMON_DIR",
      "GIT_WORK_TREE",
      "GIT_INDEX_FILE",
    ].some((name) => process.env[name] !== undefined)
  ) {
    return false;
  }
  // Clone, the safety probe and the independent fallback run from different directories.
  for (const name of [
    "GIT_CONFIG_GLOBAL",
    "GIT_CONFIG_SYSTEM",
    "GIT_TEMPLATE_DIR",
    "HOME",
    "XDG_CONFIG_HOME",
  ]) {
    const path = process.env[name];
    if (path !== undefined && !isAbsolute(path)) return false;
  }
  const git = simpleGit(cloneDir);
  const configKeys = (await git.raw(["config", "--null", "--list"]))
    .split("\0")
    .map((entry) => entry.split("\n", 1)[0]?.toLowerCase() ?? "");
  if (
    configKeys.some(
      (key) =>
        key.startsWith("includeif.") ||
        key === "include.path" ||
        key.startsWith("filter.") ||
        key === "core.hookspath" ||
        key === "core.attributesfile" ||
        key === "init.templatedir" ||
        key === "clone.defaultremotename",
    )
  ) {
    return false;
  }

  // An insteadOf rewrite can turn the source into a path relative to each clone's cwd.
  const effectiveRemote = (await git.raw(["remote", "get-url", "origin"])).trim();
  if (effectiveRemote.startsWith("file:///")) {
    fileURLToPath(effectiveRemote);
  } else if (!/^https?:\/\/[^/]/i.test(effectiveRemote)) {
    return false;
  }

  // A template can supply hooks for clone, checkout, ref or index updates.
  try {
    return (await readdir(join(cloneDir, ".git", "hooks"))).every((name) =>
      name.endsWith(".sample"),
    );
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
    return true;
  }
}

export async function isPinnedSpecCheckout(cloneDir: string, commit: string): Promise<boolean> {
  return (
    /^[0-9a-f]{40}$/.test(commit) &&
    (await simpleGit(cloneDir).revparse(["HEAD"])).trim() === commit
  );
}

export async function checkoutCommit(cloneDir: string, commit: string): Promise<void> {
  return new Promise((resolve, reject) => {
    simpleGit(cloneDir).checkout(commit, undefined, (err) => {
      if (err) {
        reject(new Error(`git checkout failed with error: ${err}`));
      }
      resolve();
    });
  });
}
