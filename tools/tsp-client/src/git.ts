import { execSync, spawn } from "child_process";

export function getRepoRoot(): string {
    return execSync('git rev-parse --show-toplevel').toString().trim();
}
  
export async function cloneRepo(rootUrl: string, cloneDir: string, repo: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const git = spawn("git", ["clone", "--no-checkout", "--filter=tree:0", repo, cloneDir], {
        cwd: rootUrl,
        stdio: "inherit",
      });
      git.once("exit", (code) => {
        if (code === 0) {
          resolve();
        } else {
          reject(new Error(`git clone failed exited with code ${code}`));
        }
      });
      git.once("error", (err) => {
        reject(new Error(`git clone failed with error: ${err}`));
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
      const git = spawn("git", ["checkout", commit], {
        cwd: cloneDir,
        stdio: "inherit",
      });
      git.once("exit", (code) => {
        if (code === 0) {
          resolve();
        } else {
          reject(new Error(`git checkout failed exited with code ${code}`));
        }
      });
      git.once("error", (err) => {
        reject(new Error(`git checkout failed with error: ${err}`));
      });
    });
  }
