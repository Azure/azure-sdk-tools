import * as path from "node:path";

import { installDependencies } from "./npm.js";
import { createTempDirectory, removeDirectory,readTspLocation, findEmitterPackage } from "./fs.js";
import { Logger, printBanner, enableDebug, printVersion } from "./log.js";
import { runTspCompile } from "./typespec.js";
import { getOptions } from "./options.js";
import { mkdir, readdir, writeFile, cp } from "node:fs/promises";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./git.js";
import { doesFileExist, fetch } from "./network.js";
import { parse as parseYaml } from "yaml";

async function getEmitterOptions(rootUrl: string, emitter: string): Promise<string> {
  // TODO: Add a way to specify emitter options like Language-Settings.ps1, could be a languageSettings.ts file
  // Method signature should just include the rootUrl. Everything else should be included in the languageSettings.ts file
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
    const matchRes = config.match('^https://(?<urlRoot>github|raw.githubusercontent).com/(?<repo>[^/]*/azure-rest-api-specs(-pr)?)/(tree/|blob/)?(?<commit>[0-9a-f]{40})/(?<path>.*)/tspconfig.yaml$')
    if (matchRes) {
      if (matchRes.groups) {
        var resolvedConfigUrl = config;
        if (matchRes.groups["urlRoot"]! === "github") {
          resolvedConfigUrl = config.replace("github.com", "raw.githubusercontent.com");
          resolvedConfigUrl = resolvedConfigUrl.replace("/blob/", "/");
        }
        Logger.debug(`Resolved config url: ${resolvedConfigUrl}`)
        const tspConfig = await fetch(resolvedConfigUrl);
        const configYaml = parseYaml(tspConfig);
        if (configYaml["parameters"] && configYaml["parameters"]["service-dir"]){
          const serviceDir = configYaml["parameters"]["service-dir"]["default"];
          Logger.debug(`Service directory: ${serviceDir}`)
          var additionalDirs: string[] = [];
          if (configYaml["parameters"]["dependencies"] && configYaml["parameters"]["dependencies"]["additionalDirectories"]) {
            additionalDirs = configYaml["parameters"]["dependencies"]["additionalDirectories"];
          }
          mkdir(path.join(outputDir, serviceDir), { recursive: true }).then(() => {
              writeFile(
                path.join(path.join(outputDir, serviceDir), "tsp-location.yaml"),
              `directory: ${matchRes.groups!["path"]!}\ncommit: ${matchRes.groups!["commit"]!}\nrepo: ${matchRes.groups!["repo"]!}\nadditionalDirectories: ${additionalDirs}`);
          });
          return path.join(outputDir, serviceDir);
        } else {
          Logger.error("Missing service-dir in parameters section of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.")
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
  await cloneRepo(tempRoot, cloneDir, `https://github.com/${repo}.git`);
  await sparseCheckout(cloneDir);
  await addSpecFiles(cloneDir, directory)
  Logger.info(`Processing additional directories: ${additionalDirectories}`)
  for (const dir of additionalDirectories) {
    await addSpecFiles(cloneDir, dir);
  }
  await checkoutCommit(cloneDir, commit);
  
  await cp(path.join(cloneDir, directory), srcDir, { recursive: true });
  const emitterPath = path.join(repoRoot, "eng", "emitter-package.json");
  await cp(emitterPath, path.join(srcDir, "package.json"), { recursive: true });
  for (const dir of additionalDirectories) {
    const dirSplit = dir.split("/");
    var projectName = dirSplit[dirSplit.length - 1];
    if (projectName === undefined) {
      projectName = "src";
    }
    const dirName = path.join(tempRoot, projectName);
    await cp(path.join(cloneDir, dir), dirName, { recursive: true });
  }
  const emitterPackage = await findEmitterPackage(emitterPath);
  if (!emitterPackage) {
    throw new Error("emitterPackage is undefined");
  }
  Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
  await removeDirectory(cloneDir);
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

async function syncAndGenerate({
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
        await sdkInit({config: options.tspConfig, outputDir: rootUrl}).then((result) => {
          Logger.info(`SDK initialized in ${result}`);
          if (!options.skipSyncAndGenerate) {
            syncAndGenerate({outputDir: result, noCleanup: options.noCleanup})
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
        syncAndGenerate({outputDir: rootUrl, noCleanup: options.noCleanup});
        break;
      default:
        Logger.error(`Unknown command: ${options.command}`);
  }
}

main().catch((err) => {
  Logger.error(err);
  process.exit(1);
});
