import { spawn } from "node:child_process";
import { createRequire } from "node:module";
import { lstat, mkdtemp, readFile, rename, rm, writeFile } from "node:fs/promises";
import { basename, dirname, extname, join } from "node:path";
import { parse, stringify } from "yaml";
import { Logger } from "./log.js";

const require = createRequire(import.meta.url);

export function pnpmEmitterLockPath(manifest: string): string {
  return join(dirname(manifest), `${basename(manifest, extname(manifest))}-pnpm-lock.yaml`);
}

export function pnpmEmitterWorkspacePath(manifest: string): string {
  return join(dirname(manifest), `${basename(manifest, extname(manifest))}-pnpm-workspace.yaml`);
}

export async function pnpmLockExists(path: string): Promise<boolean> {
  try {
    if (!(await lstat(path)).isFile())
      throw new Error(`Expected a regular emitter lockfile: ${path}`);
    return true;
  } catch (error: any) {
    if (error.code === "ENOENT") return false;
    throw error;
  }
}

/** Stage the contents on the same filesystem so a failed replacement does not truncate the target. */
export async function publishEmitterFile(path: string, contents: string): Promise<void> {
  const dir = await mkdtemp(join(dirname(path), ".tsp-emitter-"));
  const staged = join(dir, "input");
  try {
    await writeFile(staged, contents);
    await rename(staged, path);
  } finally {
    await rm(dir, { force: true, recursive: true }).catch((error) => {
      Logger.warn(`Could not clean up emitter publication staging directory: ${error}`);
    });
  }
}

/** Construct an isolated, single-project workspace without discarding explicit pnpm policy. */
export async function pnpmWorkspaceContent(manifest: string, contents?: string): Promise<string> {
  await pnpmLockExists(manifest);
  const packageJson = JSON.parse(contents ?? (await readFile(manifest, "utf8")));
  if (
    packageJson.packageManager !== undefined ||
    packageJson.devEngines?.packageManager !== undefined
  ) {
    throw new Error(
      "Remove packageManager or devEngines.packageManager from the emitter manifest; tsp-client supplies its own pnpm version",
    );
  }
  if (packageJson.pnpm || packageJson.workspaces) {
    throw new Error("Configure pnpm in the emitter workspace sidecar, not in the emitter manifest");
  }
  function rejectLocalReferences(dependencies: Record<string, unknown> | undefined) {
    for (const spec of Object.values(dependencies ?? {})) {
      if (
        typeof spec === "string" &&
        /^(?:file:|link:|workspace:|portal:|patch:|\.{1,2}[\\/]|[\\/]|[a-z]:[\\/])/i.test(spec)
      ) {
        throw new Error(
          `Local dependency reference cannot be staged in the emitter workspace: ${spec}`,
        );
      }
    }
  }
  for (const key of [
    "dependencies",
    "devDependencies",
    "optionalDependencies",
    "peerDependencies",
  ]) {
    rejectLocalReferences(packageJson[key]);
  }
  if (packageJson.overrides && Object.keys(packageJson.overrides).length > 0) {
    throw new Error(
      "npm package.json overrides are not supported by pnpm; migrate them explicitly to the emitter pnpm workspace configuration",
    );
  }
  const sidecar = pnpmEmitterWorkspacePath(manifest);
  const content = (await pnpmLockExists(sidecar)) ? await readFile(sidecar, "utf8") : undefined;
  const config = content ? parse(content) : {};
  if (!config || typeof config !== "object" || Array.isArray(config)) {
    throw new Error("Emitter pnpm workspace configuration must be a YAML mapping");
  }
  if (
    config.packages !== undefined &&
    (!Array.isArray(config.packages) || config.packages.length)
  ) {
    throw new Error("Emitter pnpm workspace cannot include additional projects");
  }
  for (const key of Object.keys(config)) {
    if (!["packages", "overrides", "allowBuilds"].includes(key)) {
      throw new Error(`Unsupported emitter pnpm workspace setting: ${key}`);
    }
  }
  rejectLocalReferences(config.overrides);
  config.packages = [];
  // Fail rather than silently skip unapproved scripts; do not cache their side effects.
  config.strictDepBuilds = true;
  config.sideEffectsCache = false;
  return stringify(config);
}

export async function pnpmCommand(workingDir: string, args: string[]): Promise<void> {
  let cli: string;
  try {
    cli = require.resolve("pnpm/bin/pnpm.mjs");
  } catch (cause) {
    throw new Error(
      "Emitter pnpm support is unavailable; reinstall tsp-client with optional dependencies on a supported platform",
      { cause },
    );
  }
  await new Promise<void>((resolve, reject) => {
    const child = spawn(process.execPath, [cli, "--dir", workingDir, ...args], {
      cwd: workingDir,
      stdio: "inherit",
    });
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (code === 0) resolve();
      else
        reject(
          new Error(`pnpm ${args[0]} failed (${signal ? `signal ${signal}` : `exit ${code}`})`),
        );
    });
  });
}

/** npm install reports audit findings without failing the install; retain a visible audit. */
export async function auditPnpmDependencies(workingDir: string): Promise<void> {
  try {
    await pnpmCommand(workingDir, ["audit"]);
  } catch (error) {
    Logger.warn(`pnpm audit reported findings or could not complete: ${error}`);
  }
}
