#!/usr/bin/env node
// @ts-check

import { execFile, spawn } from "node:child_process";
import { promisify } from "node:util";
import { rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const execFileAsync = promisify(execFile);
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const npmCommand = "npm";
const npmShell = process.platform === "win32";
const registry =
  "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-js/npm/registry/";
const packagePaths = [
  path.join(scriptDir, "package.json"),
  path.join(scriptDir, "package-lock.json"),
];
const dependencyNames = [
  "@github/copilot",
  "@github/copilot-sdk",
  "adm-zip",
];

async function getPackageVersion(packageName) {
  try {
    const { stdout } = await execFileAsync(
      npmCommand,
      [
        "view",
        packageName,
        "version",
        "--json",
        `--registry=${registry}`,
      ],
      { cwd: scriptDir, encoding: "utf8", shell: npmShell },
    );
    const version = JSON.parse(stdout);
    if (typeof version !== "string" || !version) {
      throw new Error(`Unexpected version response: ${stdout.trim()}`);
    }
    return version;
  } catch (error) {
    const detail =
      error && typeof error === "object" && "stderr" in error
        ? String(error.stderr).trim()
        : String(error);
    throw new Error(`Failed to resolve ${packageName} from CFS: ${detail}`);
  }
}

async function run(command, args, cwd, shell = false) {
  await new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd, stdio: "inherit", shell });
    child.on("error", reject);
    child.on("close", (code, signal) => {
      if (code === 0) {
        resolve();
      } else {
        reject(
          new Error(
            signal
              ? `${command} terminated by ${signal}`
              : `${command} exited with code ${code}`,
          ),
        );
      }
    });
  });
}

async function main() {
  try {
    const versions = await Promise.all(
      dependencyNames.map((name) => getPackageVersion(name)),
    );
    const dependencies = Object.fromEntries(
      dependencyNames.map((name, index) => [name, versions[index]]),
    );
    const packageJson = {
      name: "azure-typespec-author-doc-refinement",
      private: true,
      description:
        "Temporary dependencies for the azure-typespec-author doc-refinement workflow.",
      engines: {
        node: ">=20.0.0",
      },
      dependencies,
    };

    await writeFile(
      packagePaths[0],
      `${JSON.stringify(packageJson, null, 2)}\n`,
      "utf8",
    );
    process.stderr.write("Installing current dependency versions from CFS...\n");
    await run(
      npmCommand,
      [
        "install",
        "--no-audit",
        "--no-fund",
        `--registry=${registry}`,
      ],
      scriptDir,
      npmShell,
    );
    await run(
      process.execPath,
      [path.join(scriptDir, "doc-refinement.mjs"), ...process.argv.slice(2)],
      process.cwd(),
    );
  } finally {
    await Promise.all(packagePaths.map((file) => rm(file, { force: true })));
  }
}

main().catch((error) => {
  process.stderr.write(`FAILED: ${error?.stack || error}\n`);
  process.exitCode = 1;
});
