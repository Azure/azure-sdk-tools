import * as path from "node:path";

import { createPackageJson, installDependencies } from "./npm.js";
import { createTempDirectory, removeDirectory, tryReadTspLocation, writeFileTree } from "./fs.js";
import { Logger, printBanner, enableDebug, printVersion } from "./log.js";
import { doesFileExist, downloadTsp, isValidUrl, rewriteGitHubUrl } from "./network.js";
import { compileTsp } from "./typespec.js";
import { getEmitterPackage } from "./languageSettings.js";
import { getOptions } from "./options.js";

async function discoverMainFileUrl(outputDir: string): Promise<string> {
  const tspUrl = await tryReadTspLocation(outputDir);
  if (!tspUrl) {
    throw new Error("No tsp-location.yaml found, and no main file specified");
  }
  Logger.debug(`Using base url from  tsp-location.yaml: ${tspUrl}`);
  for (const mainFile of ["client.tsp", "main.tsp"]) {
    let url = new URL(mainFile, tspUrl).toString();
    Logger.debug(`Checking for file: ${url}`);
    if (await doesFileExist(url)) {
      Logger.debug(`Found main file: ${url}`);
      return url;
    }
  }
  throw new Error(`No main.tsp or client.tsp found for base url ${tspUrl}`);
}

async function downloadAndCompile({
  rootUrl,
  emitter,
  noCleanup,
  emitterOutputPath,
}: {
  rootUrl: string;
  emitter: string;
  noCleanup: boolean;
  emitterOutputPath: string;
}) {
  const { moduleImports, fileTree } = await downloadTsp(rootUrl);
  const { mainFilePath, files } = await fileTree.createTree();
  const tempRoot = await createTempDirectory();
  const srcDir = path.join(tempRoot, "src");
  await writeFileTree(srcDir, files);

  const emitterPackage = getEmitterPackage(emitter);
  moduleImports.add(emitterPackage);
  await createPackageJson(tempRoot, moduleImports);
  Logger.info("Installing dependencies from npm...");
  await installDependencies(tempRoot);
  const resolvedMainFilePath = path.join(srcDir, mainFilePath);
  // todo: allow extra emitter options for debugging
  Logger.info(`Compiling tsp using ${emitterPackage}...`);
  await compileTsp({
    language: emitter,
    emitterPackage,
    resolvedMainFilePath,
    tempRoot,
    emitterOutputPath,
  });

  if (noCleanup) {
    Logger.debug(`Skipping cleanup of temp directory: ${tempRoot}`);
  } else {
    Logger.debug("Cleaning up temp directory");
    await removeDirectory(tempRoot);
  }
}

async function main() {
  const options = await getOptions();
  if (options.debug) {
    enableDebug();
  }
  printBanner();
  await printVersion();
  let rootUrl: string;
  if (options.mainFile) {
    Logger.debug(`Using main file: ${options.mainFile}`);
    if (!isValidUrl(options.mainFile)) {
      Logger.error(`Invalid url passed to command: ${options.mainFile}`);
      return;
    }
    if (!options.mainFile.endsWith(".tsp")) {
      Logger.error(`Expected url to end in 'tsp': ${options.mainFile}`);
      return;
    }
    rootUrl = rewriteGitHubUrl(options.mainFile);
  } else {
    // check for tsp-location.yaml
    rootUrl = await discoverMainFileUrl(options.outputDir);
  }

  await downloadAndCompile({
    rootUrl,
    emitter: options.emitter,
    noCleanup: options.noCleanup,
    emitterOutputPath: options.outputDir,
  });
}

main().catch((err) => {
  Logger.error(err);
  process.exit(1);
});
