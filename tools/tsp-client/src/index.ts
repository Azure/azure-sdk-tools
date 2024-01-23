import { installDependencies } from "./npm.js";
import { createTempDirectory, removeDirectory, readTspLocation, getEmitterFromRepoConfig } from "./fs.js";
import { Logger, printBanner, enableDebug, printVersion } from "./log.js";
import { TspLocation, compileTsp, discoverMainFile, resolveTspConfigUrl } from "./typespec.js";
import { getOptions } from "./options.js";
import { mkdir, writeFile, cp, readFile } from "node:fs/promises";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./git.js";
import { doesFileExist } from "./network.js";
import { parse as parseYaml } from "yaml";
import { joinPaths, normalizePath, resolvePath } from "@typespec/compiler";
import { formatAdditionalDirectories, getAdditionalDirectoryName } from "./utils.js";
import { resolve } from "node:path";
import { spawn } from "node:child_process";


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
    const cloneDir = joinPaths(repoRoot, "..", "sparse-spec");
    await mkdir(cloneDir, { recursive: true });
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(outputDir, cloneDir, `https://github.com/${resolvedConfigUrl.repo}.git`);
    await sparseCheckout(cloneDir);
    const tspConfigPath = joinPaths(resolvedConfigUrl.path, "tspconfig.yaml");
    await addSpecFiles(cloneDir, tspConfigPath)
    await checkoutCommit(cloneDir, resolvedConfigUrl.commit);
    const configYaml = parseYaml(joinPaths(cloneDir, tspConfigPath));
    const serviceDir = configYaml?.parameters?.["service-dir"]?.default;
    if (!serviceDir) {
      throw new Error(`Parameter service-dir is not defined correctly in tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`)
    }
    Logger.debug(`Service directory: ${serviceDir}`)
    const packageDir: string | undefined = configYaml?.options?.[emitter]?.["package-dir"];
    if (!packageDir) {
      throw new Error(`Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`);
    }
    const newPackageDir = joinPaths(outputDir, serviceDir, packageDir!)
    await mkdir(newPackageDir, { recursive: true });
    const additionalDirOutput = formatAdditionalDirectories(configYaml?.parameters?.dependencies?.additionalDirectories);
    await writeFile(
      joinPaths(newPackageDir, "tsp-location.yaml"),
    `directory: ${resolvedConfigUrl.path}\ncommit: ${resolvedConfigUrl.commit}\nrepo: ${resolvedConfigUrl.repo}\nadditionalDirectories:${additionalDirOutput}\n`);
    Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
    return newPackageDir;
  } else {
    // Local directory scenario
    let configFile = joinPaths(config, "tspconfig.yaml")
    const data = await readFile(configFile, "utf8");
    const configYaml = parseYaml(data);
    const serviceDir = configYaml?.parameters?.["service-dir"]?.default;
    if (!serviceDir) {
      throw new Error(`Parameter service-dir is not defined correctly in tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`)
    }
    Logger.debug(`Service directory: ${serviceDir}`)
    const additionalDirOutput = formatAdditionalDirectories(configYaml?.parameters?.dependencies?.additionalDirectories);
    const packageDir = configYaml?.options?.[emitter]?.["package-dir"];
    if (!packageDir) {
      throw new Error(`Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`);
    }
    const newPackageDir = joinPaths(outputDir, serviceDir, packageDir)
    await mkdir(newPackageDir, { recursive: true });
    configFile = configFile.replaceAll("\\", "/");
    const matchRes = configFile.match('.*/(?<path>specification/.*)/tspconfig.yaml$')
    var directory = "";
    if (matchRes) {
      if (matchRes.groups) {
        directory = matchRes.groups!["path"]!;
      }
    }
    writeFile(joinPaths(newPackageDir, "tsp-location.yaml"),
          `directory: ${directory}\ncommit: ${commit}\nrepo: ${repo}\nadditionalDirectories:${additionalDirOutput}\n`);
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
    const cloneDir = joinPaths(repoRoot, "..", "sparse-spec");
    await mkdir(cloneDir, { recursive: true });
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

  const emitterPath = joinPaths(repoRoot, "eng", "emitter-package.json");
  await cp(emitterPath, joinPaths(srcDir, "package.json"), { recursive: true });
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
  await installDependencies(srcDir);

  await compileTsp({ emitterPackage: emitter, outputPath: rootUrl, resolvedMainFilePath, saveInputs: noCleanup, additionalEmitterOptions });

  if (noCleanup) {
    Logger.debug(`Skipping cleanup of temp directory: ${tempRoot}`);
  } else {
    Logger.debug("Cleaning up temp directory");
    await removeDirectory(tempRoot);
  }
}


async function convert(readme: string, outputDir: string): Promise<void> {
  return new Promise((resolve, reject) => {
      const autorest = spawn("npx", ["autorest", readme, "--openapi-to-typespec", "--use=@autorest/openapi-to-typespec", `--output-folder=${outputDir}`], {
          cwd: outputDir,
          stdio: "inherit",
          shell: true,
      });
      autorest.once("exit", (code) => {
          if (code === 0) {
              resolve();
          } else {
              reject(new Error(`openapi to typespec conversion failed exited with code ${code}`));
          }
      });
      autorest.once("error", (err) => {
          reject(new Error(`openapi to typespec conversion failed with error: ${err}`));
      });
  });
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

  switch (options.command) {
      case "init":
        const emitter = await getEmitterFromRepoConfig(joinPaths(await getRepoRoot(rootUrl), "eng", "emitter-package.json"));
        if (!emitter) {
          throw new Error("Couldn't find emitter-package.json in the repo");
        }
        const outputDir = await sdkInit({config: options.tspConfig!, outputDir: rootUrl, emitter, commit: options.commit, repo: options.repo, isUrl: options.isUrl});
        Logger.info(`SDK initialized in ${outputDir}`);
        if (!options.skipSyncAndGenerate) {
          await syncTspFiles(outputDir);
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
          await writeFile(joinPaths(rootUrl, "tsp-location.yaml"), `directory: ${tspLocation.directory}\ncommit: ${tspLocation.commit}\nrepo: ${tspLocation.repo}\nadditionalDirectories: ${tspLocation.additionalDirectories}`);
        } else if (options.tspConfig) {
          const tspLocation: TspLocation = await readTspLocation(rootUrl);
          const tspConfig = resolveTspConfigUrl(options.tspConfig);
          tspLocation.commit = tspConfig.commit ?? tspLocation.commit;
          tspLocation.repo = tspConfig.repo ?? tspLocation.repo;
          await writeFile(joinPaths(rootUrl, "tsp-location.yaml"), `directory: ${tspLocation.directory}\ncommit: ${tspLocation.commit}\nrepo: ${tspLocation.repo}\nadditionalDirectories: ${tspLocation.additionalDirectories}`);
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
        await convert(readme, rootUrl);
        break;
      default:
        throw new Error(`Unknown command: ${options.command}`);
  }
}

main().catch((err) => {
  Logger.error(err);
  process.exit(1);
});
