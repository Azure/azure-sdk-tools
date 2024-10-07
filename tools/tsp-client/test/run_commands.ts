// The script will run the tsp-client commands on the test/examples directory.
// The script will copy the emitter-package.json found in test/utils/ to the eng directory at the root of the repo then clean it up.
// The init command will download the specification from the tspconfig.yaml url and generate the client library in the test/examples directory.
// The update command will sync and generate the client library under test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest.
// The sync command will sync the specification from the tsp-location.yaml under test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest.
// The generate command will generate the client library under test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest.
// The script will log the success of each command.
// The script will throw an error if any of the commands fail.

import { spawn } from "child_process";
import { cp, unlink, readFile } from "fs/promises";
import { join, resolve } from "path";
import { getRepoRoot } from "../src/git";

export async function runCommand(workingDir: string, args: string[]): Promise<void> {
  const baseArgs = ["tsx", "./src/index.ts", "--no-prompt"];
  const commandArgs = baseArgs.concat(args);
  return new Promise((resolve, reject) => {
    const npm = spawn("npx", commandArgs, {
      cwd: workingDir,
      stdio: "inherit",
      shell: true,
    });
    npm.once("exit", (code) => {
      if (code === 0) {
        console.log(`${args[0]} ---------------> RAN SUCCESSFULLY`);
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

async function main() {
  await verifySwaggerSorting();

  // Variables for the target directories
  const baseDir = resolve(".");
  const examplesDir = resolve("./test/examples/");
  const constosoJsSdkDir = resolve(
    "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest/",
  );
  const baseSpecDir = "./test/examples/specification/convert/";
  const mgmtSpecLink =
    "https://github.com/Azure/azure-rest-api-specs/blob/589cb3afdc76fe9a989ef36952eec816906d481f/specification/sphere/resource-manager/readme.md";
  const contosoSpecDir =
    "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager/";
  const tspConfig =
    "https://github.com/Azure/azure-rest-api-specs/blob/db63bea839f5648462c94e685d5cc96f8e8b38ba/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml";

  const emitterPackageJson = resolve("./test/utils/emitter-package.json");
  const emitterPackageLockJson = resolve("./test/utils/emitter-package-lock.json");

  // Copy the emitter-package.json to the eng directory at the root of the repo
  const repoRoot = await getRepoRoot(".");

  await cp(emitterPackageJson, join(repoRoot, "eng/emitter-package.json"));
  console.log("emitter-package.json ---------------> copied successfully");
  await cp(emitterPackageLockJson, join(repoRoot, "eng/emitter-package-lock.json"));
  console.log("emitter-package-lock.json ---------------> copied successfully");

  // Run the init command
  await runCommand(baseDir, ["init", "-c", tspConfig, "-o", examplesDir]);

  // Run the update command
  await runCommand(baseDir, ["update", "-o", constosoJsSdkDir]);

  // Run the sync commands
  await runCommand(baseDir, ["sync", "-o", constosoJsSdkDir]);

  // Run the generate command
  await runCommand(baseDir, ["generate", "-o", constosoJsSdkDir]);

  // Run the convert command on a mgmt spec
  await runCommand(baseDir, [
    "convert",
    "-o",
    baseSpecDir,
    "--arm",
    "--swagger-readme",
    mgmtSpecLink,
  ]);

  // Run the compare command to ensure it is working
  await runCommand(baseDir, [
    "compare",
    "--lhs",
    "./test/examples/specification/compare/lhs",
    "--rhs",
    "./test/examples/specification/compare/rhs",
    "--compile-tsp",
  ]);

  await unlink(join(repoRoot, "eng/emitter-package.json"));
  console.log("emitter-package.json ---------------> deleted successfully");
  await unlink(join(repoRoot, "eng/emitter-package-lock.json"));
  console.log("emitter-package-lock.json ---------------> deleted successfully");
}

async function compareFiles(file1, file2) {
  try {
    const data1 = await readFile(file1, "utf8");
    const data2 = await readFile(file2, "utf8");
    return data1 === data2;
  } catch (err) {
    console.error("Error reading files:", err);
    throw err;
  }
}

async function verifySwaggerSorting() {
  const baseDir = resolve(".");
  const specDir = resolve("./test/examples/specification/sort-swagger");
  const unsortedJson = join(specDir, "unsorted.json");
  const sortedJson = join(specDir, "sorted.json");

  await cp(unsortedJson, sortedJson);

  await runCommand(baseDir, ["sort-swagger", sortedJson]);

  if (await compareFiles(sortedJson, join(specDir, "expected-sorted.json")))
    console.log("sort-swagger verified successfully");
  else console.error("\x1b[31m", "sort-swagger ---------------> verification FAILED!", "\x1b[0m");
}

main().catch((e) => {
  throw e;
});
