import { execFile, spawn } from "child_process";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { promisify } from "node:util";
import { simpleGit } from "simple-git";

const execFileAsync = promisify(execFile);

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

export async function addSpecFiles(cloneDir: string, subDir: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const git = spawn("git", ["sparse-checkout", "add", subDir], {
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

export async function checkoutWithExclusions(
  cloneDir: string,
  commit: string,
  project: string,
  additionalDirectories: string[],
  exclusions: string[],
): Promise<boolean> {
  const git = simpleGit(cloneDir);
  // Let Git build its usual cone patterns, including the root and ancestor files.
  await git.raw(["sparse-checkout", "set", "--cone", "--", project, ...additionalDirectories]);
  const patternPath = (await git.revparse(["--git-path", "info/sparse-checkout"])).trimEnd();
  const patterns = (await readFile(resolve(cloneDir, patternPath), "utf8"))
    .split(/\r?\n/)
    .filter(Boolean);
  await git.raw([
    "sparse-checkout",
    "set",
    "--no-cone",
    "--",
    ...patterns,
    ...exclusions.map((name) => `!/${project}/${name}/`),
  ]);
  // Read before checkout detaches HEAD; onbranch includes can change afterward.
  // GIT_CONFIG redirects `git config` only, not the checkout it is measuring.
  const env = { ...process.env };
  delete env["GIT_CONFIG"];
  const { stdout } = await execFileAsync(
    "git",
    ["config", "--type=bool", "--default=false", "--get", "core.ignoreCase"],
    { cwd: cloneDir, env },
  );
  await git.checkout(commit);
  return stdout.trim() === "true";
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
