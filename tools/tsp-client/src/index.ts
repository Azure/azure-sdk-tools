import * as path from "node:path";

import { installDependencies } from "./npm.js";
import { createTempDirectory, removeDirectory,readTspLocation, getEmitterFromRepoConfig } from "./fs.js";
import { Logger, printBanner, enableDebug, printVersion } from "./log.js";
import { compileTsp, discoverMainFile, getEmitterOptions, resolveTspConfigUrl } from "./typespec.js";
import { getOptions } from "./options.js";
import { mkdir, writeFile, cp, readFile } from "node:fs/promises";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./git.js";
import { fetch } from "./network.js";
import { parse as parseYaml } from "yaml";


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
    const resolvedConfigUrl = resolveTspConfigUrl(config);
    Logger.debug(`Resolved config url: ${resolvedConfigUrl.resolvedUrl}`)
    const tspConfig = await fetch(resolvedConfigUrl.resolvedUrl);
    const configYaml = parseYaml(tspConfig);
    if (configYaml["parameters"] && configYaml["parameters"]["service-dir"]){
      const serviceDir = configYaml["parameters"]["service-dir"]["default"];
      if (serviceDir === undefined) {
        Logger.error(`Parameter service-dir is not defined correctly in tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`)
      }
      Logger.debug(`Service directory: ${serviceDir}`)
      const additionalDirs: string[] = configYaml?.parameters?.dependencies?.additionalDirectories ?? [];
      const packageDir: string | undefined = configYaml?.options?.[emitter]?.["package-dir"];
      if (!packageDir) {
        Logger.error(`Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`);
      }
      const newPackageDir = path.join(outputDir, serviceDir, packageDir!)
      await mkdir(newPackageDir, { recursive: true });
      await writeFile(
        path.join(newPackageDir, "tsp-location.yaml"),
      `directory: ${resolvedConfigUrl.path}\ncommit: ${resolvedConfigUrl.commit}\nrepo: ${resolvedConfigUrl.repo}\nadditionalDirectories: ${additionalDirs}`);
      return newPackageDir;
    } else {
      Logger.error("Missing service-dir in parameters section of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.")
    }
  } else {
    // Local directory scenario
    let configFile = path.join(config, "tspconfig.yaml")
    const data = await readFile(configFile, "utf8");
    const configYaml = parseYaml(data);
    if (configYaml["parameters"] && configYaml["parameters"]["service-dir"]) {
      const serviceDir = configYaml["parameters"]["service-dir"]["default"];
      var additionalDirs: string[] = [];
      if (configYaml["parameters"]["dependencies"] && configYaml["parameters"]["dependencies"]["additionalDirectories"]) {
        additionalDirs = configYaml["parameters"]["dependencies"]["additionalDirectories"];
      }
      Logger.info(`Additional directories: ${additionalDirs}`)
      let packageDir: string | undefined = undefined;
      if (configYaml["options"][emitter] && configYaml["options"][emitter]["package-dir"]) {
        packageDir = configYaml["options"][emitter]["package-dir"];
      }
      if (packageDir === undefined) {
        throw new Error(`Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`);
      }
      const newPackageDir = path.join(outputDir, serviceDir, packageDir)
      await mkdir(newPackageDir, { recursive: true });
      configFile = configFile.replaceAll("\\", "/");
      const matchRes = configFile.match('.*/(?<path>specification/.*)/tspconfig.yaml$')
      var directory = "";
      if (matchRes) {
        if (matchRes.groups) {
          directory = matchRes.groups!["path"]!;
        }
      }
      writeFile(path.join(newPackageDir, "tsp-location.yaml"),
            `directory: ${directory}\ncommit: ${commit}\nrepo: ${repo}\nadditionalDirectories: ${additionalDirs}`);
      return newPackageDir;
    }
    throw new Error("Missing service-dir in parameters section of tspconfig.yaml. Please refer to  https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.")
  }
  throw new Error("Invalid tspconfig.yaml. Please refer to  https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.");  
}

async function syncTspFiles(outputDir: string, localSpecRepo?: string) {
  const tempRoot = await createTempDirectory(outputDir);

  const repoRoot = getRepoRoot();
  Logger.debug(`Repo root is ${repoRoot}`);
  if (repoRoot === undefined) {
    throw new Error("Could not find repo root");
  }
  const [ directory, commit, repo, additionalDirectories ] = await readTspLocation(outputDir);
  const dirSplit = directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  Logger.debug(`Using project name: ${projectName}`)
  if (projectName === undefined) {
    projectName = "src";
  }
  const srcDir = path.join(tempRoot, projectName);
  await mkdir(srcDir, { recursive: true });
  const cloneDir = path.join(repoRoot, "..", "sparse-spec");
  await mkdir(cloneDir, { recursive: true });
  Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
  
  if (localSpecRepo) {
    Logger.debug(`Using local spec directory: ${localSpecRepo}`);
    function filter(src: string, dest: string): boolean {
      if (src.includes("node_modules") || dest.includes("node_modules")) {
        return false;
      }
      return true;
    }
    const cpDir = path.join(cloneDir, directory);
    await cp(localSpecRepo, cpDir, { recursive: true, filter: filter });
    // TODO: additional directories not yet supported
    // const localSpecRepoRoot = await getRepoRoot(localSpecRepo);
    // if (localSpecRepoRoot === undefined) {
    //   throw new Error("Could not find local spec repo root, please make sure the path is correct");
    // }
    // for (const dir of additionalDirectories) {
    //   await cp(path.join(localSpecRepoRoot, dir), cpDir, { recursive: true, filter: filter });
    // }
  } else {
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(tempRoot, cloneDir, `https://github.com/${repo}.git`);
    await sparseCheckout(cloneDir);
    await addSpecFiles(cloneDir, directory)
    Logger.info(`Processing additional directories: ${additionalDirectories}`)
    for (const dir of additionalDirectories) {
      await addSpecFiles(cloneDir, dir);
    }
    await checkoutCommit(cloneDir, commit);  
  }

  await cp(path.join(cloneDir, directory), srcDir, { recursive: true });
  const emitterPath = path.join(repoRoot, "eng", "emitter-package.json");
  await cp(emitterPath, path.join(srcDir, "package.json"), { recursive: true });
  // FIXME: remove conditional once full support for local spec repo is added
  if (localSpecRepo) {
    Logger.info("local spec repo does not yet support additional directories");
  } else {
    for (const dir of additionalDirectories) {
      const dirSplit = dir.split("/");
      let projectName = dirSplit[dirSplit.length - 1];
      if (projectName === undefined) {
        projectName = "src";
      }
      const dirName = path.join(tempRoot, projectName);
      await cp(path.join(cloneDir, dir), dirName, { recursive: true });
    }
  }
  Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
  await removeDirectory(cloneDir);
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
  const tempRoot = path.join(rootUrl, "TempTypeSpecFiles");
  const tspLocation = await readTspLocation(rootUrl);
  const dirSplit = tspLocation[0].split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  if (projectName === undefined) {
    throw new Error("cannot find project name");
  }
  const srcDir = path.join(tempRoot, projectName);
  const emitter = await getEmitterFromRepoConfig(path.join(getRepoRoot(), "eng", "emitter-package.json"));
  if (!emitter) {
    throw new Error("emitter is undefined");
  }
  const mainFilePath = await discoverMainFile(srcDir);
  const resolvedMainFilePath = path.join(srcDir, mainFilePath);
  Logger.info(`Compiling tsp using ${emitter}...`);
  const emitterOpts = await getEmitterOptions(rootUrl, srcDir, emitter, noCleanup, additionalEmitterOptions);

  Logger.info("Installing dependencies from npm...");
  await installDependencies(srcDir);

  await compileTsp({ emitterPackage: emitter, outputPath: rootUrl, resolvedMainFilePath, options: emitterOpts });

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

  let rootUrl = path.resolve(".");
  if (options.outputDir) {
    rootUrl = path.resolve(options.outputDir);
  }

  switch (options.command) {
      case "init":
        const emitter = await getEmitterFromRepoConfig(path.join(getRepoRoot(), "eng", "emitter-package.json"));
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
          let [ directory, commit, repo, additionalDirectories ] = await readTspLocation(rootUrl);
          commit = options.commit ?? commit;
          repo = options.repo ?? repo;
          await writeFile(path.join(rootUrl, "tsp-location.yaml"), `directory: ${directory}\ncommit: ${commit}\nrepo: ${repo}\nadditionalDirectories: ${additionalDirectories}`);
        }
        if (options.tspConfig) {
          let [ directory, commit, repo, additionalDirectories ] = await readTspLocation(rootUrl);
          let tspConfig = resolveTspConfigUrl(options.tspConfig);
          commit = tspConfig.commit ?? commit;
          repo = tspConfig.repo ?? repo;
          await writeFile(path.join(rootUrl, "tsp-location.yaml"), `directory: ${directory}\ncommit: ${commit}\nrepo: ${repo}\nadditionalDirectories: ${additionalDirectories}`);
        }
        await syncTspFiles(rootUrl);
        await generate({ rootUrl, noCleanup: options.noCleanup, additionalEmitterOptions: options.emitterOptions});
        break;
      default:
        Logger.error(`Unknown command: ${options.command}`);
  }
}

main().catch((err) => {
  Logger.error(err);
  process.exit(1);
});
