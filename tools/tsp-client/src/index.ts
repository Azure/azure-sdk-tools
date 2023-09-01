import * as path from "node:path";

import { installDependencies } from "./npm.js";
import { createTempDirectory, removeDirectory,readTspLocation, findEmitterPackage } from "./fs.js";
import { Logger, printBanner, enableDebug, printVersion } from "./log.js";
import { runTspCompile } from "./typespec.js";
import { getOptions } from "./options.js";
import { mkdir, readdir, writeFile } from "node:fs/promises";
import { cp, existsSync } from "node:fs";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./gh.js";
import { doesFileExist, fetch } from "./network.js";
import { parse as parseYaml } from "yaml";

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

async function sdkInit(
  {
    config,
    outputDir,
    // commit, //TODO
    // repoUrl, // TODO
  }: {
    config: string;
    outputDir: string;
    // commit: string;
    // repoUrl: string;
  }): Promise<string> {
  if (await doesFileExist(config)) {
    // URL scenario
    const tspConfigUrl = new URL(config).toString();
    const matchRes = tspConfigUrl.match('^https://(?<urlRoot>github).com/(?<repo>[^/]*/azure-rest-api-specs(-pr)?)/blob/(?<commit>[0-9a-f]{40})/(?<path>.*)/tspconfig.yaml$')
    if (matchRes) {
      if (matchRes.groups) {
        if (matchRes.groups["urlRoot"]! === "github") {
          var resolvedConfigUrl = tspConfigUrl.replace("github.com", "raw.githubusercontent.com");
          resolvedConfigUrl = resolvedConfigUrl.replace("/blob/", "/");
          Logger.debug(`Resolved config url: ${resolvedConfigUrl}`)
          const tspConfig = await fetch(resolvedConfigUrl);
          const configYaml = parseYaml(tspConfig);
          if (configYaml["parameters"] && configYaml["parameters"]["service-dir"]){
            const serviceDir = configYaml["parameters"]["service-dir"]["default"];
            Logger.debug(`Service directory: ${serviceDir}`)
            mkdir(path.join(outputDir, serviceDir), { recursive: true }).then(() => {
                writeFile(path.join(path.join(outputDir, serviceDir), "tsp-location.yaml"), `directory: ${matchRes.groups!["path"]!}\ncommit: ${matchRes.groups!["commit"]!}\nrepo: ${matchRes.groups!["repo"]!}`);
            });
            if (configYaml["parameters"]["dependencies"] && configYaml["parameters"]["dependencies"]["additionalDirectories"]) {
              const additionalDirs = configYaml["parameters"]["dependencies"]["additionalDirectories"];
              // FIXME add this
              Logger.info(`Additional directories: ${additionalDirs}`)
            }
            return path.join(outputDir, serviceDir);
          } else {
            Logger.error("Missing service-dir in parameters section of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.")
          }
        }
      }
    }
  } else {
    // File scenario
    throw new Error("File scenario not implemented yet")
  }
  throw new Error("Invalid tspconfig.yaml");  
}

async function syncTspFiles(outputDir: string) {
  const tempRoot = await createTempDirectory(outputDir);

  const repoRoot = getRepoRoot();
  Logger.debug(`Repo root is ${repoRoot}`);
  if (repoRoot === undefined) {
    throw new Error("Could not find repo root");
  }

  const cloneDir = path.join(repoRoot, "..", "sparse-spec");
  Logger.debug(`Cloning repo to ${cloneDir}`);
  const [ directory, commit, repo, additionalDirectories ] = await readTspLocation(outputDir);
  const dirSplit = directory.split("/");
  var projectName = dirSplit[dirSplit.length - 1];
  Logger.debug(`Using project name: ${projectName}`)
  if (projectName === undefined) {
    projectName = "src";
  }
  const srcDir = path.join(tempRoot, projectName);
  mkdir(srcDir, { recursive: true });
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
    const dirSplit = dir.split("/");
    var projectName = dirSplit[dirSplit.length - 1];
    if (projectName === undefined) {
      projectName = "src";
    }
    const dirName = path.join(tempRoot, projectName);
    cp(path.join(cloneDir, dir), dirName, { recursive: true }, (err) => {
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
}


async function generate({
  rootUrl,
  noCleanup,
}: {
  rootUrl: string;
  noCleanup: boolean;
}) {
  const tempRoot = path.join(rootUrl, "TempTypeSpecFiles");
  const tspLocation = await readTspLocation(rootUrl);
  const dirSplit = tspLocation[0].split("/");
  var projectName = dirSplit[dirSplit.length - 1];
  if (projectName === undefined) {
    projectName = "src";
  }
  const srcDir = path.join(tempRoot, projectName);
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
  syncTspFiles(outputDir).then(() => {
    generate({ rootUrl: outputDir, noCleanup});
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
      case "init":
        if (options.tspConfig === undefined) {
          throw new Error("tspConfig is undefined");
        }
        sdkInit({config: options.tspConfig, outputDir: rootUrl}).then((result) => {
        Logger.info(`SDK initialized in ${result}`);
        if (!options.skipSyncAndGenerate) {
          syncAndCompile({outputDir: result, noCleanup: options.noCleanup})
        }
        });
        break;
      case "sync":
        syncTspFiles(rootUrl);
        break;
      case "generate":
        generate({ rootUrl, noCleanup: options.noCleanup});
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
