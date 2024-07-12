import * as path from "node:path";
import { readFile, writeFile } from "node:fs/promises";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { joinPaths } from "@typespec/compiler";
import { Logger } from "./log.js";

export async function createPackageJson(rootPath: string, deps: Set<string>): Promise<void> {
  const dependencies: Record<string, string> = {};

  for (const dep of deps) {
    dependencies[dep] = "latest";
  }

  const packageJson = JSON.stringify({
    dependencies,
  });

  const filePath = joinPaths(rootPath, "package.json");
  await writeFile(filePath, packageJson);
}

export async function npmCommand(workingDir: string, args: string[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const npm = spawn("npm", args, {
      cwd: workingDir,
      stdio: "inherit",
      shell: true,
    });
    npm.once("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`npm ${args[0]} failed exited with code ${code}`));
      }
    });
    npm.once("error", (err) => {
      reject(new Error(`npm ${args[0]} failed with error: ${err}`));
    });
  });
}

export async function npxCommand(workingDir: string, args: string[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const npm = spawn("npx", args, {
      cwd: workingDir,
      stdio: "inherit",
      shell: true,
    });
    npm.once("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`npx ${args[0]} failed exited with code ${code}`));
      }
    });
    npm.once("error", (err) => {
      reject(new Error(`npx ${args[0]} failed with error: ${err}`));
    });
  });
}

export async function nodeCommand(workingDir: string, args: string[]): Promise<void> {
  Logger.debug("node " + args.join(' '));

  return new Promise((resolve, reject) => {
    const npm = spawn("node", args, {
      cwd: workingDir,
      stdio: "inherit",
      shell: true,
    });
    npm.once("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`node ${args[0]} failed exited with code ${code}`));
      }
    });
    npm.once("error", (err) => {
      reject(new Error(`node ${args[0]} failed with error: ${err}`));
    });
  });
}

let packageVersion: string;
export async function getPackageVersion(): Promise<string> {
  if (!packageVersion) {
    const __dirname = path.dirname(fileURLToPath(import.meta.url));
    const packageJson = JSON.parse(
      await readFile(joinPaths(__dirname, "..", "package.json"), "utf-8"),
    );
    packageVersion = packageJson.version ?? "unknown";
  }
  return packageVersion;
}
