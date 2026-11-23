import { access, readFile, realpath, stat, statfs, writeFile } from "node:fs/promises";
import { constants } from "node:fs";
import { spawn, execFile } from "node:child_process";
import { createHash } from "node:crypto";
import { tmpdir } from "node:os";
import { delimiter, dirname, isAbsolute, join } from "node:path";
import { promisify } from "node:util";
import { joinPaths } from "@typespec/compiler";
import { Logger } from "./log.js";

/**
 * Queries the npm registry for a published package's devDependencies.
 * Returns the parsed JSON object, or undefined if the query fails.
 */
export async function npmViewPackageDevDependencies(
  packageName: string,
  version: string,
): Promise<Record<string, any> | undefined> {
  const spec = `${packageName}@${version}`;
  Logger.debug(`Running npm view ${spec} --json`);

  return new Promise((resolve) => {
    execFile(
      "npm",
      ["view", spec, "devDependencies", "--json"],
      { shell: true, timeout: 30_000 },
      (error, stdout) => {
        if (error) {
          Logger.warn(`npm view ${spec} failed: ${error.message}`);
          resolve(undefined);
          return;
        }
        try {
          const result = JSON.parse(stdout);
          Logger.debug(
            `\`npm view ${spec} devDependencies --json\` result: \n${JSON.stringify(result, null, 2)}`,
          );
          resolve(result);
        } catch (parseError: any) {
          Logger.warn(`Failed to parse npm view output for ${spec}: ${parseError.message}`);
          resolve(undefined);
        }
      },
    );
  });
}

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

/** Identify the tested stock toolchain and its effective project/install settings. */
export async function npmReuseFingerprint(
  workingDir: string,
  packageRoot: string,
): Promise<string | undefined> {
  if (
    process.platform !== "linux" ||
    process.env["NODE_OPTIONS"] ||
    process.env["NODE_PATH"] ||
    Object.keys(process.env).some((key) =>
      /^(https?_proxy|all_proxy|no_proxy|node_extra_ca_certs|ssl_cert_file|ssl_cert_dir)$/i.test(
        key,
      ),
    )
  )
    return undefined;
  const search = (process.env["PATH"] ?? "").split(delimiter);
  if (search.some((entry) => !isAbsolute(entry))) return undefined;
  async function executable(name: string) {
    for (const entry of search) {
      const candidate = join(entry, name);
      try {
        await access(candidate, constants.X_OK);
        return await realpath(candidate);
      } catch {}
    }
    return undefined;
  }
  const npm = await executable("npm");
  const node = await executable("node");
  if (
    !npm ||
    !node ||
    node !== (await realpath(process.execPath)) ||
    !npm.endsWith("/npm/bin/npm-cli.js")
  )
    return undefined;
  // ci --package-lock-only audit and lock validation are verified for this implementation.
  const npmPackage = JSON.parse(await readFile(join(dirname(npm), "../package.json"), "utf8"));
  if (npmPackage.name !== "npm" || npmPackage.version !== "12.2.3") return undefined;
  const exec = promisify(execFile);
  const query = async (...args: string[]) =>
    // npmCommand launches through /bin/sh on Linux. The shell updates PWD,
    // which npm can expand in configuration paths. Use the same launch context.
    (
      await exec("/bin/sh", ["-c", 'exec npm "$@"', "npm", ...args], {
        cwd: workingDir,
        timeout: 30_000,
        maxBuffer: 2 * 1024 * 1024,
      })
    ).stdout.trim();
  if ((await query("prefix")) !== packageRoot || (await query("--version")) !== npmPackage.version)
    return undefined;
  // The JSON view omits authentication settings entirely. Stock npm's non-default
  // text view marks protected values without printing them; never log that output.
  const textConfig = await query("config", "list", "--no-json");
  if (
    !/^; "cli" config from command line options$/m.test(textConfig) ||
    // JSON omits any value that the text view redacts, not only credentials.
    textConfig.includes("***") ||
    /^\s*@[^\s:]+:registry\s*=/im.test(textConfig) ||
    /= \(protected\)\s*$/m.test(textConfig) ||
    /^\s*(?:https-proxy|proxy|noproxy|ca|cafile|cert|certfile|key|keyfile|strict-ssl|cache-min|cache-max)\s*=/im.test(
      textConfig,
    )
  )
    return undefined;
  const config = JSON.parse(await query("config", "list", "--json"));
  if (
    config.registry !== "https://registry.npmjs.org/" ||
    Object.keys(config).some((key) => /^@.*:registry$/.test(key)) ||
    config.global ||
    config["allow-remote"] !== "all" ||
    config.force ||
    config.offline ||
    config["prefer-online"] ||
    config["prefer-offline"] ||
    config["dry-run"] ||
    config["package-lock-only"] ||
    config.workspaces ||
    config.workspace?.length ||
    config["install-strategy"] !== "hoisted" ||
    config["install-links"] ||
    config.cpu ||
    config.os ||
    config.libc ||
    config.omit?.length ||
    config.include?.length
  )
    return undefined;
  // A copy would keep the first install's modes and need not preserve ACLs. Bypass
  // nonstandard permission inheritance, including on the temporary snapshot filesystem.
  const getfacl = await executable("getfacl");
  if (!getfacl) return undefined;
  const dirs = [packageRoot, tmpdir()];
  for (const dir of dirs) {
    const [info, filesystem] = await Promise.all([stat(dir), statfs(dir, { bigint: true })]);
    // Local Linux ext4, tmpfs, XFS, Btrfs and overlayfs have known POSIX ACL semantics.
    if (
      !info.isDirectory() ||
      info.mode & 0o2000 ||
      ![0xef53n, 0x01021994n, 0x58465342n, 0x9123683en, 0x794c7630n].includes(
        BigInt.asUintN(32, filesystem.type),
      )
    )
      return undefined;
  }
  const { stdout: acl } = await exec(getfacl, ["-cp", "--", ...dirs], {
    timeout: 5_000,
    maxBuffer: 16 * 1024,
  });
  const plainAcl = /^(?:user::[rwx-]{3}\ngroup::[rwx-]{3}\nother::[rwx-]{3})$/;
  const entries = acl.trim().split(/\n\s*\n/);
  if (entries.length !== dirs.length || entries.some((entry) => !plainAcl.test(entry)))
    return undefined;
  // Compare config/environment within the invocation without retaining or logging secrets.
  return createHash("sha256")
    .update(
      JSON.stringify([
        npm,
        node,
        npmPackage.version,
        process.version,
        process.arch,
        process.versions.modules,
        process.umask(),
        process.getuid?.(),
        process.getgid?.(),
        Object.entries(config).sort(),
        Object.entries(process.env).sort(),
      ]),
    )
    .digest("hex");
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
  Logger.debug("npx " + args.join(" "));

  return new Promise((resolve, reject) => {
    const npx = spawn("npx", args, {
      cwd: workingDir,
      stdio: "inherit",
      shell: true,
    });
    npx.once("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`npx ${args[0]} failed exited with code ${code}`));
      }
    });
    npx.once("error", (err) => {
      reject(new Error(`npx ${args[0]} failed with error: ${err}`));
    });
  });
}
