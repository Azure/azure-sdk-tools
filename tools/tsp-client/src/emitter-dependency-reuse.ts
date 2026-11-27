import { createHash } from "node:crypto";
import {
  closeSync,
  constants,
  lstatSync,
  mkdtempSync,
  openSync,
  readSync,
  readdirSync,
  readlinkSync,
  realpathSync,
  rmSync,
} from "node:fs";
import { cp, lstat, readFile, realpath, rename, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, isAbsolute, join, relative, sep } from "node:path";
import { Logger } from "./log.js";
import { npmCommand, npmReuseFingerprint } from "./npm.js";

const packagePart = "(?:@[a-z0-9_.-]+/)?[a-z0-9_.-]+";
const packagePath = new RegExp(
  `^node_modules/${packagePart}(?:/node_modules/${packagePart})*$`,
  "i",
);
const dependencyLifecycle = ["preinstall", "install", "postinstall"];
const rootLifecycle = [
  ...dependencyLifecycle,
  "prepublish",
  "preprepare",
  "prepare",
  "postprepare",
  "predependencies",
  "dependencies",
  "postdependencies",
];

interface LockedPackage {
  version: string;
  resolved: string;
  integrity: string;
  name?: string;
  hasInstallScript?: boolean;
  scripts?: Record<string, unknown>;
  link?: boolean;
  inBundle?: boolean;
  os?: string[];
  cpu?: string[];
  libc?: string[];
}

type LockedPackages = Record<string, LockedPackage>;

interface InstallInputs {
  key: string;
  packages: LockedPackages;
}

async function exists(path: string): Promise<boolean> {
  try {
    await lstat(path);
    return true;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
    return false;
  }
}

function hasLifecycle(scripts: Record<string, unknown> | undefined, events: string[]): boolean {
  return events.some((name) => scripts && Object.hasOwn(scripts, name));
}

async function readInputs(root: string, srcDir: string): Promise<InstallInputs | undefined> {
  if (
    dirname(srcDir) !== root ||
    (await realpath(root)) !== root ||
    (await realpath(srcDir)) !== srcDir
  )
    return undefined;
  for (const name of [
    "node_modules",
    "npm-shrinkwrap.json",
    ".npm-extension",
    "binding.gyp",
    "patches",
  ]) {
    if (await exists(join(root, name))) return undefined;
  }
  const manifest = await readFile(join(root, "package.json"), "utf8");
  const lock = await readFile(join(root, "package-lock.json"), "utf8");
  const pkg = JSON.parse(manifest);
  if (
    pkg.workspaces ||
    pkg.overrides ||
    pkg.packageExtensions ||
    pkg.patches ||
    pkg.patchedDependencies ||
    hasLifecycle(pkg.scripts, rootLifecycle)
  )
    return undefined;
  const json = JSON.parse(lock);
  if (json.lockfileVersion !== 3 || !json.packages?.[""]) return undefined;
  const packages: LockedPackages = { ...json.packages };
  delete packages[""];
  if (Object.keys(packages).length === 0) return undefined;
  for (const [path, entry] of Object.entries(packages)) {
    if (
      !packagePath.test(path) ||
      path.split("/").some((segment) => segment === "." || segment === "..") ||
      entry.link ||
      entry.inBundle ||
      entry.hasInstallScript ||
      hasLifecycle(entry.scripts, dependencyLifecycle) ||
      entry.os ||
      entry.cpu ||
      entry.libc ||
      typeof entry.version !== "string" ||
      !/^https:\/\/registry\.npmjs\.org\/[^?#]+\.tgz$/.test(entry.resolved) ||
      !/^sha512-[A-Za-z0-9+/]{86}==$/.test(entry.integrity)
    )
      return undefined;
  }
  const npm = await npmReuseFingerprint(srcDir, root);
  if (!npm) return undefined;
  return {
    key: createHash("sha256")
      .update(JSON.stringify([manifest, lock, npm]))
      .digest("hex"),
    packages,
  };
}

/** Check that a clean install actually produced the complete admitted tree, including optional dependencies. */
async function validateInstall(modules: string, expected: LockedPackages): Promise<void> {
  const installed = JSON.parse(await readFile(join(modules, ".package-lock.json"), "utf8"));
  const actual = installed.packages as LockedPackages;
  if (!actual || Object.keys(actual).length !== Object.keys(expected).length)
    throw new Error("Incomplete dependency tree");
  for (const [path, entry] of Object.entries(expected)) {
    const meta = actual[path];
    if (
      meta?.version !== entry.version ||
      meta.integrity !== entry.integrity ||
      meta.resolved !== entry.resolved ||
      meta.hasInstallScript ||
      hasLifecycle(meta.scripts, dependencyLifecycle)
    ) {
      throw new Error("Installed dependency does not match lockfile");
    }
    const dir = join(dirname(modules), path);
    const pkg = JSON.parse(await readFile(join(dir, "package.json"), "utf8"));
    if (
      pkg.version !== entry.version ||
      // npm runs prepare for linked packages, not for these published tarballs.
      hasLifecycle(pkg.scripts, dependencyLifecycle) ||
      pkg.bundleDependencies ||
      pkg.bundledDependencies ||
      (await exists(join(dir, "binding.gyp")))
    ) {
      throw new Error("Unsupported installed dependency");
    }
  }
}

/** Digest bytes, modes and in-tree npm .bin links without dereferencing them during copy. */
function treeDigest(root: string): string {
  const hash = createHash("sha256");
  // The CLI processes batch children sequentially. A bounded synchronous buffer avoids
  // scheduling thousands of separate file streams and doesn't allocate whole large files.
  const buffer = Buffer.allocUnsafe(64 * 1024);
  function visit(path: string): void {
    const stat = lstatSync(path);
    const name = relative(root, path);
    const header = (kind: string, data?: string | number) =>
      hash.update(JSON.stringify([name, kind, stat.mode & 0o777, data]));
    if (stat.isDirectory()) {
      header("dir");
      for (const child of readdirSync(path).sort()) visit(join(path, child));
    } else if (stat.isFile()) {
      header("file", stat.size);
      const fd = openSync(path, constants.O_RDONLY | constants.O_NOFOLLOW);
      try {
        for (let bytes = readSync(fd, buffer); bytes > 0; bytes = readSync(fd, buffer)) {
          hash.update(buffer.subarray(0, bytes));
        }
      } finally {
        closeSync(fd);
      }
    } else if (stat.isSymbolicLink() && dirname(path).endsWith(`${sep}.bin`)) {
      const target = readlinkSync(path);
      const resolved = relative(root, realpathSync(path));
      if (
        !resolved ||
        isAbsolute(target) ||
        isAbsolute(resolved) ||
        resolved === ".." ||
        resolved.startsWith(`..${sep}`)
      ) {
        throw new Error("Unsupported npm bin link");
      }
      header("link", target);
    } else {
      throw new Error("Unsupported dependency file");
    }
  }
  visit(root);
  return hash.digest("hex");
}

async function copyTree(source: string, destination: string): Promise<void> {
  await cp(source, destination, {
    recursive: true,
    verbatimSymlinks: true,
    force: false,
    errorOnExist: true,
    mode: constants.COPYFILE_FICLONE,
  });
}

/** One isolated snapshot, owned by one sequential update batch. */
export class BatchEmitterDependencies {
  private cacheRoot?: string;
  private staging?: string;
  private snapshot?: { key: string; digest: string };
  private readonly cleanup = () => {
    for (const path of [this.staging, this.cacheRoot]) {
      if (path) {
        try {
          rmSync(path, { recursive: true, force: true });
        } catch {}
      }
    }
  };

  private onSignal(signal: "SIGINT" | "SIGTERM") {
    this.cleanup();
    // The once listener has already been removed. Preserve the default signal
    // exit status when nothing else owns the signal (do not intercept other handlers).
    if (process.listenerCount(signal) === 0) process.kill(process.pid, signal);
  }

  private readonly onInterrupt = () => this.onSignal("SIGINT");
  private readonly onTerminate = () => this.onSignal("SIGTERM");

  async dispose(): Promise<void> {
    const root = this.cacheRoot;
    if (root)
      await rm(root, { recursive: true, force: true }).catch(() =>
        Logger.debug("Could not remove the emitter dependency snapshot."),
      );
    // Keep the path and handlers live until recursive removal settles.
    this.cacheRoot = undefined;
    this.snapshot = undefined;
    process.removeListener("exit", this.cleanup);
    process.removeListener("SIGINT", this.onInterrupt);
    process.removeListener("SIGTERM", this.onTerminate);
  }

  async install(root: string, srcDir: string, args: string[]): Promise<void> {
    const inputs =
      args.length === 1 && args[0] === "ci" && process.env["TSP_CLIENT_EMITTER_REUSE"] !== "0"
        ? await readInputs(root, srcDir).catch(() => undefined)
        : undefined;
    if (inputs && this.snapshot?.key === inputs.key && (await this.restore(root, srcDir))) return;
    await npmCommand(srcDir, args);
    if (inputs) await this.capture(root, inputs);
  }

  private async restore(root: string, srcDir: string): Promise<boolean> {
    let staging: string | undefined;
    try {
      // Track both directories in the same turn they are created so a signal
      // cannot leave a directory that the cleanup handlers do not yet own.
      staging = mkdtempSync(join(root, ".emitter-reuse-"));
      this.staging = staging;
      const stagedModules = join(staging, "node_modules");
      await copyTree(join(this.cacheRoot!, "node_modules"), stagedModules);
      if (treeDigest(stagedModules) !== this.snapshot!.digest)
        throw new Error("Emitter dependency snapshot changed");
      // Keep npm's lockfile checks and audit in the requesting workspace.
      await npmCommand(srcDir, ["ci", "--package-lock-only"]);
      if (await exists(join(root, "node_modules")))
        throw new Error("Install destination is no longer empty");
      await rename(stagedModules, join(root, "node_modules"));
      Logger.debug("Reused the batch's installed emitter dependencies.");
      return true;
    } catch {
      Logger.debug("Could not reuse emitter dependencies; running a clean install.");
      return false;
    } finally {
      if (staging) {
        await rm(staging, { recursive: true, force: true }).catch(() =>
          Logger.debug("Could not remove temporary dependency staging."),
        );
        this.staging = undefined;
      }
    }
  }

  private async capture(root: string, inputs: InstallInputs): Promise<void> {
    await this.dispose();
    try {
      const modules = join(root, "node_modules");
      await validateInstall(modules, inputs.packages);
      this.cacheRoot = mkdtempSync(join(tmpdir(), "tsp-emitter-deps-"));
      process.once("exit", this.cleanup);
      process.once("SIGINT", this.onInterrupt);
      process.once("SIGTERM", this.onTerminate);
      // Inspect the source before copying, then record the bytes in the private store.
      const digest = treeDigest(modules);
      const stage = join(this.cacheRoot, "staged");
      await copyTree(modules, stage);
      if (treeDigest(stage) !== digest) throw new Error("Dependency tree changed during capture");
      await rename(stage, join(this.cacheRoot, "node_modules"));
      this.snapshot = { key: inputs.key, digest };
    } catch {
      Logger.debug("Emitter dependency tree is not eligible for reuse.");
      await this.dispose();
    }
  }
}
