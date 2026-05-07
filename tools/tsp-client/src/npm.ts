import { writeFile } from "node:fs/promises";
import { spawn, execFile } from "node:child_process";
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
