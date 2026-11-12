#!/usr/bin/env node

import { spawnSync } from "node:child_process";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { parseArgs } from "node:util";

export function getPinnedPnpmVersion(specRoot: string) {
  const packageFile = path.join(specRoot, "package.json");
  const pkg: unknown = JSON.parse(fs.readFileSync(packageFile, "utf8"));
  const packageManager = typeof pkg === "object" && pkg !== null && "packageManager" in pkg
    ? pkg.packageManager : undefined;
  const match = typeof packageManager === "string"
    ? /^pnpm@(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)(?:\+.*)?$/.exec(packageManager) : null;
  if (!match) {
    throw new Error(`Expected pnpm packageManager in ${packageFile}, got: ${packageManager}`);
  }
  return match[1];
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const { values } = parseArgs({ options: { "spec-root": { type: "string" } } });
  const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../../../..");
  const specRoot = path.resolve(values["spec-root"] ?? path.join(repoRoot, "artifacts", "azure-rest-api-specs"));
  const version = getPinnedPnpmVersion(specRoot);
  const installed = spawnSync("pnpm", ["--version"], {
    encoding: "utf8", shell: process.platform === "win32", stdio: ["ignore", "pipe", "ignore"],
  });
  if (installed.error && (!("code" in installed.error) || installed.error.code !== "ENOENT")) {
    throw installed.error;
  }
  if (installed.status === 0 && installed.stdout.trim() === version) {
    console.log(`pnpm@${version} is already installed. Nothing to do.`);
  } else {
    const args = ["install", "--global", `pnpm@${version}`];
    console.log(`> npm ${args.join(" ")}`);
    const result = spawnSync("npm", args, { stdio: "inherit", shell: process.platform === "win32" });
    if (result.error) {
      throw result.error;
    }
    if (result.status !== 0) {
      throw new Error(`npm ${args.join(" ")} failed with exit code ${result.status}`);
    }
    console.log(`Installed pnpm@${version}.`);
  }
}
