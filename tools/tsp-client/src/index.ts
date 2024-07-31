import { npmCommand, nodeCommand } from "./npm.js";
import { createTempDirectory, removeDirectory, readTspLocation, getEmitterFromRepoConfig } from "./fs.js";
import { Logger, printBanner, enableDebug, printVersion } from "./log.js";
import { TspLocation, compileTsp, discoverMainFile, resolveTspConfigUrl } from "./typespec.js";
import { getOptions } from "./options.js";
import { mkdir, cp, readFile, stat, rename, unlink, writeFile } from "node:fs/promises";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./git.js";
import { doesFileExist } from "./network.js";
import { parse as parseYaml } from "yaml";
import { joinPaths, normalizePath, resolvePath } from "@typespec/compiler";
import { getAdditionalDirectoryName, getPathToDependency, getServiceDir, makeSparseSpecDir, writeTspLocationYaml } from "./utils.js";
import { resolve } from "node:path";
import { config as dotenvConfig } from "dotenv";
import { sortOpenAPIDocument } from "@azure-tools/typespec-autorest";

async function sdkInit(
  {
    config,
    outputDir,
    emitter,
    commit,
    repo,
    isUrl,
  }: {
    config: string;
    outputDir: string;
    emitter: string;
    commit: string | undefined;
    repo: string | undefined;
    isUrl: boolean;
  }): Promise<string> {
  if (isUrl) {
    // URL scenario
    const repoRoot = await getRepoRoot(outputDir);
    const resolvedConfigUrl = resolveTspConfigUrl(config);
    const cloneDir = await makeSparseSpecDir(repoRoot);
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(outputDir, cloneDir, `https://github.com/${resolvedConfigUrl.repo}.git`);
    await sparseCheckout(cloneDir);
    const tspConfigPath = joinPaths(resolvedConfigUrl.path, "tspconfig.yaml");
    await addSpecFiles(cloneDir, tspConfigPath)
    await checkoutCommit(cloneDir, resolvedConfigUrl.commit);
    let data;
    try {
      data = await readFile(joinPaths(cloneDir, tspConfigPath), "utf8");
    } catch (err) {
      throw new Error(`Could not read tspconfig.yaml at ${tspConfigPath}. Error: ${err}`);
    }
    if (!data) {
      throw new Error(`tspconfig.yaml is empty at ${tspConfigPath}`);
    }
    const configYaml = parseYaml(data);
    const serviceDir = getServiceDir(configYaml, emitter);
    const packageDir: string | undefined = configYaml?.options?.[emitter]?.["package-dir"];
    if (!packageDir) {
      throw new Error(`Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`);
    }
    const newPackageDir = joinPaths(outputDir, serviceDir, packageDir!)
    await mkdir(newPackageDir, { recursive: true });
    const tspLocationData: TspLocation = {
        directory: resolvedConfigUrl.path,
        commit: resolvedConfigUrl.commit,
        repo: resolvedConfigUrl.repo,
        additionalDirectories: configYaml?.parameters?.dependencies?.additionalDirectories,
        };
    await writeTspLocationYaml(tspLocationData, newPackageDir);
    Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
    return newPackageDir;
  } else {
    // Local directory scenario
    if (!config.endsWith("tspconfig.yaml")) {
      config = joinPaths(config, "tspconfig.yaml");
    }
    let data;
    try {
      data = await readFile(config, "utf8");
    } catch (err) {
      throw new Error(`Could not read tspconfig.yaml at ${config}`);
    }
    if (!data) {
      throw new Error(`tspconfig.yaml is empty at ${config}`);
    }
    const configYaml = parseYaml(data);
    const serviceDir = getServiceDir(configYaml, emitter);
    const packageDir = configYaml?.options?.[emitter]?.["package-dir"];
    if (!packageDir) {
      throw new Error(`Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`);
    }
    const newPackageDir = joinPaths(outputDir, serviceDir, packageDir)
    await mkdir(newPackageDir, { recursive: true });
    config = config.replaceAll("\\", "/");
    const matchRes = config.match('.*/(?<path>specification/.*)/tspconfig.yaml$')
    var directory = "";
    if (matchRes) {
      if (matchRes.groups) {
        directory = matchRes.groups!["path"]!;
      }
    }
    const tspLocationData: TspLocation = {
        directory: directory,
        commit: commit ?? "",
        repo: repo ?? "",
        additionalDirectories: configYaml?.parameters?.dependencies?.additionalDirectories,
        };
    await writeTspLocationYaml(tspLocationData, newPackageDir);
    return newPackageDir;
  }
}

async function syncTspFiles(outputDir: string, localSpecRepo?: string) {
  const tempRoot = await createTempDirectory(outputDir);

  const repoRoot = await getRepoRoot(outputDir);
  Logger.debug(`Repo root is ${repoRoot}`);
  if (!repoRoot) {
    throw new Error("Could not find repo root");
  }
  const tspLocation: TspLocation = await readTspLocation(outputDir);
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  Logger.debug(`Using project name: ${projectName}`)
  if (!projectName) {
    projectName = "src";
  }
  const srcDir = joinPaths(tempRoot, projectName);
  await mkdir(srcDir, { recursive: true });

  if (localSpecRepo) {
    Logger.info("NOTE: A path to a local spec was provided, will generate based off of local files...");
    Logger.debug(`Using local spec directory: ${localSpecRepo}`);
    function filter(src: string): boolean {
      if (src.includes("node_modules")) {
        return false;
      }
      if (src.includes("tsp-output")) {
        return false;
      }
      return true;
    }
    await cp(localSpecRepo, srcDir, { recursive: true, filter: filter });
    const localSpecRepoRoot = await getRepoRoot(localSpecRepo);
    Logger.info(`Local spec repo root is ${localSpecRepoRoot}`)
    if (!localSpecRepoRoot) {
      throw new Error("Could not find local spec repo root, please make sure the path is correct");
    }
    for (const dir of tspLocation.additionalDirectories!) {
      Logger.info(`Syncing additional directory: ${dir}`);;
      await cp(joinPaths(localSpecRepoRoot, dir), joinPaths(tempRoot, getAdditionalDirectoryName(dir)), { recursive: true, filter: filter });
    }
  } else {
    const cloneDir = await makeSparseSpecDir(repoRoot);
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(tempRoot, cloneDir, `https://github.com/${tspLocation.repo}.git`);
    await sparseCheckout(cloneDir);
    await addSpecFiles(cloneDir, tspLocation.directory)
    for (const dir of tspLocation.additionalDirectories ?? []) {
      Logger.info(`Processing additional directory: ${dir}`);
      await addSpecFiles(cloneDir, dir);
    }
    await checkoutCommit(cloneDir, tspLocation.commit);
    await cp(joinPaths(cloneDir, tspLocation.directory), srcDir, { recursive: true });
    for (const dir of tspLocation.additionalDirectories!) {
      Logger.info(`Syncing additional directory: ${dir}`);
      await cp(joinPaths(cloneDir, dir), joinPaths(tempRoot, getAdditionalDirectoryName(dir)), { recursive: true });
    }
    Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
  }

  try {
    const emitterLockPath = joinPaths(repoRoot, "eng", "emitter-package-lock.json");
    await cp(emitterLockPath, joinPaths(srcDir, "package-lock.json"), { recursive: true });
  } catch (err) {
    Logger.debug(`Ran into the following error when looking for emitter-package-lock.json: ${err}`);
    Logger.debug("Will attempt look for emitter-package.json...");
  }
  try {
    const emitterPath = joinPaths(repoRoot, "eng", "emitter-package.json");
    await cp(emitterPath, joinPaths(srcDir, "package.json"), { recursive: true });
  } catch (err) {
    throw new Error(`Ran into the following error: ${err}\nTo continue using tsp-client, please provide a valid emitter-package.json file in the eng/ directory of the repository.`);
  }
}


async function generate({
  rootUrl,
  noCleanup,
  additionalEmitterOptions,
}: {
  rootUrl: string;
  noCleanup: boolean;
  additionalEmitterOptions?: string;
}) {
  const tempRoot = joinPaths(rootUrl, "TempTypeSpecFiles");
  const tspLocation = await readTspLocation(rootUrl);
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  if (!projectName) {
    throw new Error("cannot find project name");
  }
  const srcDir = joinPaths(tempRoot, projectName);
  const emitter = await getEmitterFromRepoConfig(joinPaths(await getRepoRoot(rootUrl), "eng", "emitter-package.json"));
  if (!emitter) {
    throw new Error("emitter is undefined");
  }
  const mainFilePath = await discoverMainFile(srcDir);
  const resolvedMainFilePath = joinPaths(srcDir, mainFilePath);
  Logger.info("Installing dependencies from npm...");
  const args: string[] = [];
  try {
    // Check if package-lock.json exists, if it does, we'll install dependencies through `npm ci`
    await stat(joinPaths(srcDir, "package-lock.json"));
    args.push("ci");
  } catch (err) {
    // If package-lock.json doesn't exist, we'll attempt to install dependencies through `npm install`
    args.push("install");
  }
  // NOTE: This environment variable should be used for developer testing only. A force
  // install may ignore any conflicting dependencies and result in unexpected behavior.
  dotenvConfig({path: resolve(await getRepoRoot(rootUrl), ".env")});
  if (process.env['TSPCLIENT_FORCE_INSTALL']?.toLowerCase() === "true") {
    args.push("--force");
  }
  await npmCommand(srcDir, args);
  await compileTsp({ emitterPackage: emitter, outputPath: rootUrl, resolvedMainFilePath, saveInputs: noCleanup, additionalEmitterOptions });

  if (noCleanup) {
    Logger.debug(`Skipping cleanup of temp directory: ${tempRoot}`);
  } else {
    Logger.debug("Cleaning up temp directory");
    await removeDirectory(tempRoot);
  }
}


async function convert(readme: string, outputDir: string, arm?: boolean): Promise<void> {
  const autorestPath = await getPathToDependency("autorest");
  const autorestPackageJson = JSON.parse(await readFile(joinPaths(autorestPath, "package.json"), "utf8"));
  const autorestBinPath = joinPaths(autorestPath, autorestPackageJson["bin"]["autorest"]);

  const autorestOpenApiToTypeSpecPath = await getPathToDependency("@autorest/openapi-to-typespec");
  const args = [autorestBinPath, "--openapi-to-typespec", "--csharp=false", `--output-folder="${outputDir}"`, `--use="${autorestOpenApiToTypeSpecPath}"`, `"${readme}"`];
  if (arm) {
    const autorestCsharpPath = await getPathToDependency("@autorest/csharp");
    const generateMetadataCmd = [autorestBinPath, "--csharp", "--max-memory-size=8192", `--use="${autorestCsharpPath}"`, `--output-folder="${outputDir}"`, "--mgmt-debug.only-generate-metadata", "--azure-arm", "--skip-csproj", `"${readme}"`];
    try {
      await nodeCommand(outputDir, generateMetadataCmd);
    } catch (err) {
      Logger.error(`Error occurred while attempting to generate ARM metadata: ${err}`);
      process.exit(1);
    }
    try {
      await rename(joinPaths(outputDir, "metadata.json"), joinPaths(outputDir, "resources.json"));
    } catch (err) {
      Logger.error(`Error occurred while attempting to rename metadata.json to resources.json: ${err}`);
      process.exit(1);
    }
    args.push("--isArm");
  }
  return await nodeCommand(outputDir, args);
}

async function sortSwagger(swaggerFileName: string): Promise<void> {
        const content = await readFile(swaggerFileName);
        const document = JSON.parse(content.toString());
        const sorted = sortOpenAPIDocument(document);
        await writeFile(swaggerFileName, JSON.stringify(sorted, null, 2));
}

async function generateLockFile(rootUrl: string, repoRoot: string) {
  Logger.info("Generating lock file...");
  const args: string[] = ["install"];
  if (process.env['TSPCLIENT_FORCE_INSTALL']?.toLowerCase() === "true") {
    args.push("--force");
  }
  const tempRoot = await createTempDirectory(rootUrl);
  await cp(joinPaths(repoRoot, "eng", "emitter-package.json"), joinPaths(tempRoot, "package.json"));
  await npmCommand(tempRoot, args);
  const lockFile = await stat(joinPaths(tempRoot, "package-lock.json"));
  if (lockFile.isFile()) {
    await cp(joinPaths(tempRoot, "package-lock.json"), joinPaths(repoRoot, "eng", "emitter-package-lock.json"));
  }
  await removeDirectory(tempRoot);
  Logger.info(`Lock file generated in ${joinPaths(repoRoot, "eng", "emitter-package-lock.json")}`);
}

async function main() {
  const options = await getOptions();
  if (options.debug) {
    enableDebug();
  }
  printBanner();
  await printVersion();

  let rootUrl = resolvePath(".");
  if (options.outputDir) {
    rootUrl = resolvePath(options.outputDir);
  }

  const repoRoot = await getRepoRoot(rootUrl);

  if (options.generateLockFile) {
    await generateLockFile(rootUrl, repoRoot);
    return;
  }

  switch (options.command) {
      case "init":
        const emitter = await getEmitterFromRepoConfig(joinPaths(repoRoot, "eng", "emitter-package.json"));
        if (!emitter) {
          throw new Error("Couldn't find emitter-package.json in the repo");
        }
        const outputDir = await sdkInit({config: options.tspConfig!, outputDir: rootUrl, emitter, commit: options.commit, repo: options.repo, isUrl: options.isUrl});
        Logger.info(`SDK initialized in ${outputDir}`);
        if (!options.skipSyncAndGenerate) {
          await syncTspFiles(outputDir, options.localSpecRepo);
          await generate({ rootUrl: outputDir, noCleanup: options.noCleanup, additionalEmitterOptions: options.emitterOptions});
        }
        break;
      case "sync":
        await syncTspFiles(rootUrl, options.localSpecRepo);
        break;
      case "generate":
        await generate({ rootUrl, noCleanup: options.noCleanup, additionalEmitterOptions: options.emitterOptions});
        break;
      case "update":
        if (options.repo && !options.commit) {
            throw new Error("Commit SHA is required when specifying `--repo`, please specify a commit using `--commit`");
        }
        if (options.commit) {
          const tspLocation: TspLocation = await readTspLocation(rootUrl);
          tspLocation.commit = options.commit ?? tspLocation.commit;
          tspLocation.repo = options.repo ?? tspLocation.repo;
          await writeTspLocationYaml(tspLocation, rootUrl);
        } else if (options.tspConfig) {
          const tspLocation: TspLocation = await readTspLocation(rootUrl);
          const tspConfig = resolveTspConfigUrl(options.tspConfig);
          tspLocation.commit = tspConfig.commit ?? tspLocation.commit;
          tspLocation.repo = tspConfig.repo ?? tspLocation.repo;
          await writeTspLocationYaml(tspLocation, rootUrl);
        }
        await syncTspFiles(rootUrl, options.localSpecRepo);
        await generate({ rootUrl, noCleanup: options.noCleanup, additionalEmitterOptions: options.emitterOptions});
        break;
      case "convert":
        Logger.info("Converting swagger to typespec...");
        let readme = options.swaggerReadme!;
        if (await doesFileExist(readme)) {
          readme = normalizePath(resolve(readme));
        }
        await convert(readme, rootUrl, options.arm);
        if (options.arm) {
          try {
            await unlink(joinPaths(rootUrl, "resources.json"));
          } catch (err) {
            Logger.error(`Error occurred while attempting to delete resources.json: ${err}`);
            process.exit(1);
          }
        }
        break;
    case "sortSwagger":
        Logger.info("Sorting a swagger content...");
        let swaggerFile =  options.swaggerFile;
        if (swaggerFile === undefined || await !doesFileExist(swaggerFile)) {
            throw new Error(`Swagger file not found: ${swaggerFile??"[Not Specified]"}`);
        }
        swaggerFile = normalizePath(resolve(swaggerFile));
        await sortSwagger(swaggerFile);
        Logger.info(`${swaggerFile} has been sorted.`);
        break;        
      default:
        throw new Error(`Unknown command: ${options.command}`);
  }
}

main().catch((err) => {
  Logger.error(err);
  process.exit(1);
});
