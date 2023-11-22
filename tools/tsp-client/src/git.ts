import { spawn } from "child_process";
import { simpleGit } from "simple-git";

export async function getRepoRoot(repoPath: string): Promise<string> {
  return simpleGit(repoPath).revparse(["--show-toplevel"]);
}

export async function cloneRepo(rootUrl: string, cloneDir: string, repo: string): Promise<void> {
  return new Promise((resolve, reject) => {
    simpleGit(rootUrl).clone(repo, cloneDir, ["--no-checkout", "--filter=tree:0"], (err) => {
      if (err) {
        reject(err);
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

export async function checkoutCommit(cloneDir: string, commit: string): Promise<void> {
  return new Promise((resolve, reject) => {
    simpleGit(cloneDir).checkout(commit, undefined, (err) => {
      if (err) {
        reject(err);
      }
      resolve();
    });
  });
}
