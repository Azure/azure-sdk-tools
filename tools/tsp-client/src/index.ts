import * as path from "node:path";

import { installDependencies } from "./npm.js";
import { createTempDirectory, removeDirectory,readTspLocation, findEmitterPackage } from "./fs.js";
import { Logger, printBanner, enableDebug, printVersion } from "./log.js";
import { runTspCompile } from "./typespec.js";
import { getOptions } from "./options.js";
import { mkdir, readdir } from "node:fs/promises";
import { cp, existsSync } from "node:fs";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./gh.js";

// async function discoverMainFileUrl(outputDir: string): Promise<string> {
//   const tspUrl = await tryReadTspLocation(outputDir);
//   if (!tspUrl) {
//     throw new Error("No tsp-location.yaml found, and no main file specified");
//   }
//   Logger.debug(`Using base url from  tsp-location.yaml: ${tspUrl}`);
//   for (const mainFile of ["client.tsp", "main.tsp"]) {
//     let url = new URL(mainFile, tspUrl).toString();
//     Logger.debug(`Checking for file: ${url}`);
//     if (await doesFileExist(url)) {
//       Logger.debug(`Found main file: ${url}`);
//       return url;
//     }
//   }
//   throw new Error(`No main.tsp or client.tsp found for base url ${tspUrl}`);
// }

async function getEmitterOptions(rootUrl: string, emitter: string): Promise<string> {
  // TODO: Add a way to specify emitter options like Language-Settings.ps1, could be a languageSettings.ts file
  // Method signature should just include the rootUrl. Everything else should be included in the language-settings.yaml file
  return `--options ${emitter}.emitter-output-dir=${rootUrl}`;
}

async function discoverMainFile(srcDir: string): Promise<string> {
  Logger.debug(`Discovering entry file in ${srcDir}`)
  var entryTsp = "";
  const files = await readdir(srcDir, {recursive: true });
  for (const file of files) {
    if (file.includes("client.tsp") || file.includes("main.tsp")) {
      entryTsp = file;
      Logger.debug(`Found entry file: ${entryTsp}`);
      return entryTsp;
    }
  };
  throw new Error(`No main.tsp or client.tsp found`);
}

// async function downloadTspFiles(rootUrl: string, emitter: string, outputDir: string) {
//   const { moduleImports, fileTree } = await downloadTsp(rootUrl);
//   const { mainFilePath, files } = await fileTree.createTree();
//   const tempRoot = await createTempDirectory(outputDir);
//   const srcDir = path.join(tempRoot, "src");
//   await writeFileTree(srcDir, files);

//   const emitterPackage = getEmitterPackage(emitter);
//   moduleImports.add(emitterPackage);
//   await createPackageJson(tempRoot, moduleImports);
//   return {
//     tempRoot: tempRoot,
//     srcDir: srcDir,
//     mainFilePath: mainFilePath,
//     emitterPackage: emitterPackage
//   };
// }

async function syncTspFiles(outputDir: string) {
  const tempRoot = await createTempDirectory(outputDir);
  const srcDir = path.join(tempRoot, "src");
  mkdir(srcDir, { recursive: true });

  const repoRoot = getRepoRoot();
  Logger.debug(`Repo root is ${repoRoot}`);
  if (repoRoot === undefined) {
    throw new Error("Could not find repo root");
  }

  const cloneDir = path.join(repoRoot, "..", "sparse-spec");
  Logger.debug(`Cloning repo to ${cloneDir}`);
  const [ directory, commit, repo, additionalDirectories ] = await readTspLocation(outputDir);

  if (existsSync(cloneDir)) {
    Logger.debug(`Removing existing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
  }
  await cloneRepo(tempRoot, cloneDir, `https://github.com/${repo}.git`);
  await sparseCheckout(cloneDir);
  await addSpecFiles(cloneDir, directory)
  Logger.info(`Processing additional directories: ${additionalDirectories}`)
  for (const dir of additionalDirectories) {
    await addSpecFiles(cloneDir, dir);
  }
  await checkoutCommit(cloneDir, commit);
  
  cp(path.join(cloneDir, directory), srcDir, { recursive: true }, (err) => {
    if (err) {
      throw new Error(`Error copying files to the src directory: ${err}`)
    }
  });
  const emitterPath = path.join(repoRoot, "eng", "emitter-package.json");
  cp(emitterPath, path.join(srcDir, "package.json"), { recursive: true }, (err) => {
    if (err) {
      throw new Error(`Error copying files to the src directory: ${err}`)
    }
  });
  for (const dir of additionalDirectories) {
    cp(path.join(cloneDir, dir), srcDir, { recursive: true }, (err) => {
      if (err) {
        throw new Error(`Error copying files to the src directory: ${err}`)
      }
    });
  }
  const emitterPackage = await findEmitterPackage(emitterPath);
  if (!emitterPackage) {
    throw new Error("emitterPackage is undefined");
  }

  // if (existsSync(cloneDir)) {
  //   Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
  //   await removeDirectory(cloneDir);
  // }

  return {
    srcDir: srcDir,
  };
}


async function generate({
  rootUrl,
  srcDir,
  noCleanup,
}: {
  rootUrl: string;
  srcDir: string;
  noCleanup: boolean;
}) {
  const tempRoot = path.join(rootUrl, "TempTypeSpecFiles");
  const emitter = await findEmitterPackage(path.join(getRepoRoot(), "eng", "emitter-package.json"));
  if (!emitter) {
    throw new Error("emitter is undefined");
  }
  Logger.info("Installing dependencies from npm...");
  await installDependencies(srcDir);

  const mainFilePath = await discoverMainFile(srcDir);
  const resolvedMainFilePath = path.join(srcDir, mainFilePath);
  // todo: allow extra emitter options for debugging
  Logger.info(`Compiling tsp using ${emitter}...`);
  const emitterOptions = await getEmitterOptions(rootUrl, emitter);
  await runTspCompile({tempDir: rootUrl, mainFilePath: resolvedMainFilePath, emitter, emitterOptions});

  if (noCleanup) {
    Logger.debug(`Skipping cleanup of temp directory: ${tempRoot}`);
  } else {
    Logger.debug("Cleaning up temp directory");
    await removeDirectory(tempRoot);
  }
}

async function syncAndCompile({
  outputDir,
  noCleanup,
}: {
  outputDir: string;
  noCleanup: boolean;
}) {
  syncTspFiles(outputDir).then((result) => {
    generate({ rootUrl: outputDir, srcDir: result.srcDir, noCleanup});
  });
}

async function main() {
  const options = await getOptions();
  if (options.debug) {
    enableDebug();
  }
  printBanner();
  await printVersion();

  var rootUrl = path.resolve(".");
  if (options.outputDir) {
    rootUrl = path.resolve(options.outputDir);
  }

  switch (options.command) {
      case "sync":
        syncTspFiles(rootUrl);
        break;
      case "generate":
        generate({ rootUrl, srcDir: path.join(rootUrl, "TempTypeSpecFiles", "src"), noCleanup: options.noCleanup});
        break;
      case "update":
        syncAndCompile({outputDir: rootUrl, noCleanup: options.noCleanup});
        break;
      default:
        Logger.error(`Unknown command: ${options.command}`);
  }
}

main().catch((err) => {
  Logger.error(err);
  process.exit(1);
});
