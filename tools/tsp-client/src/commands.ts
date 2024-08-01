import { Logger } from "./log.js";
import { joinPaths, normalizePath, resolvePath } from "@typespec/compiler";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./git.js";
import {
  createTempDirectory,
  getEmitterFromRepoConfig,
  readTspLocation,
  removeDirectory,
} from "./fs.js";
import { cp, mkdir, readFile, rename, stat, unlink } from "fs/promises";
import { npmCommand, nodeCommand } from "./npm.js";
import { compileTsp, discoverMainFile, resolveTspConfigUrl, TspLocation } from "./typespec.js";
import {
  writeTspLocationYaml,
  getAdditionalDirectoryName,
  getServiceDir,
  makeSparseSpecDir,
} from "./utils.js";
import { parse as parseYaml } from "yaml";
import { config as dotenvConfig } from "dotenv";
import { resolve } from "node:path";
import { doesFileExist } from "./network.js";

export async function initCommand(argv: any) {
  let outputDir = argv["output-dir"];
  let tspConfig = argv["tsp-config"];
  const skipSyncAndGenerate = argv["skip-sync-and-generate"];
  const commit = argv["commit"];
  const repo = argv["repo"];

  let rootUrl = resolvePath(outputDir);
  const repoRoot = await getRepoRoot(rootUrl);

  const emitter = await getEmitterFromRepoConfig(
    joinPaths(repoRoot, "eng", "emitter-package.json"),
  );
  if (!emitter) {
    throw new Error("Couldn't find emitter-package.json in the repo");
  }

  let isFolder = true;
  if (await doesFileExist(tspConfig)) {
    isFolder = false;
  }
  if (!isFolder) {
    if (!commit || !repo) {
      Logger.error("--commit and --repo are required when --tsp-config is a local directory");
      process.exit(1);
    }
  }

  if (isFolder) {
    // URL scenario
    const resolvedConfigUrl = resolveTspConfigUrl(tspConfig);
    const cloneDir = await makeSparseSpecDir(repoRoot);
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(outputDir, cloneDir, `https://github.com/${resolvedConfigUrl.repo}.git`);
    await sparseCheckout(cloneDir);
    const tspConfigPath = joinPaths(resolvedConfigUrl.path, "tspconfig.yaml");
    await addSpecFiles(cloneDir, tspConfigPath);
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
      throw new Error(
        `Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`,
      );
    }
    const newPackageDir = joinPaths(outputDir, serviceDir, packageDir!);
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
    outputDir = newPackageDir;
  } else {
    // Local directory scenario
    if (!tspConfig.endsWith("tspconfig.yaml")) {
      tspConfig = joinPaths(tspConfig, "tspconfig.yaml");
    }
    let data;
    try {
      data = await readFile(tspConfig, "utf8");
    } catch (err) {
      throw new Error(`Could not read tspconfig.yaml at ${tspConfig}`);
    }
    if (!data) {
      throw new Error(`tspconfig.yaml is empty at ${tspConfig}`);
    }
    const configYaml = parseYaml(data);
    const serviceDir = getServiceDir(configYaml, emitter);
    const packageDir = configYaml?.options?.[emitter]?.["package-dir"];
    if (!packageDir) {
      throw new Error(
        `Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`,
      );
    }
    const newPackageDir = joinPaths(outputDir, serviceDir, packageDir);
    await mkdir(newPackageDir, { recursive: true });
    tspConfig = tspConfig.replaceAll("\\", "/");
    const matchRes = tspConfig.match(".*/(?<path>specification/.*)/tspconfig.yaml$");
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
  }

  if (!skipSyncAndGenerate) {
    // update argv in case anything changed and call into sync and generate
    argv["output-dir"] = outputDir;
    await syncCommand(argv);
    await generateCommand(argv);
  }
  return outputDir;
}

export async function syncCommand(argv: any) {
  let outputDir = argv["output-dir"];
  const localSpecRepo = argv["local-spec-repo"];

  const tempRoot = await createTempDirectory(outputDir);
  const repoRoot = await getRepoRoot(outputDir);
  Logger.debug(`Repo root is ${repoRoot}`);
  if (!repoRoot) {
    throw new Error("Could not find repo root");
  }
  const tspLocation: TspLocation = await readTspLocation(outputDir);
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  Logger.debug(`Using project name: ${projectName}`);
  if (!projectName) {
    projectName = "src";
  }
  const srcDir = joinPaths(tempRoot, projectName);
  await mkdir(srcDir, { recursive: true });

  if (localSpecRepo) {
    Logger.info(
      "NOTE: A path to a local spec was provided, will generate based off of local files...",
    );
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
    Logger.info(`Local spec repo root is ${localSpecRepoRoot}`);
    if (!localSpecRepoRoot) {
      throw new Error("Could not find local spec repo root, please make sure the path is correct");
    }
    for (const dir of tspLocation.additionalDirectories!) {
      Logger.info(`Syncing additional directory: ${dir}`);
      await cp(
        joinPaths(localSpecRepoRoot, dir),
        joinPaths(tempRoot, getAdditionalDirectoryName(dir)),
        { recursive: true, filter: filter },
      );
    }
  } else {
    const cloneDir = await makeSparseSpecDir(repoRoot);
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(tempRoot, cloneDir, `https://github.com/${tspLocation.repo}.git`);
    await sparseCheckout(cloneDir);
    await addSpecFiles(cloneDir, tspLocation.directory);
    for (const dir of tspLocation.additionalDirectories ?? []) {
      Logger.info(`Processing additional directory: ${dir}`);
      await addSpecFiles(cloneDir, dir);
    }
    await checkoutCommit(cloneDir, tspLocation.commit);
    await cp(joinPaths(cloneDir, tspLocation.directory), srcDir, { recursive: true });
    for (const dir of tspLocation.additionalDirectories!) {
      Logger.info(`Syncing additional directory: ${dir}`);
      await cp(joinPaths(cloneDir, dir), joinPaths(tempRoot, getAdditionalDirectoryName(dir)), {
        recursive: true,
      });
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
    throw new Error(
      `Ran into the following error: ${err}\nTo continue using tsp-client, please provide a valid emitter-package.json file in the eng/ directory of the repository.`,
    );
  }
}

export async function generateCommand(argv: any) {
  let outputDir = argv["output-dir"];
  const emitterOptions = argv["emitter-options"];
  const saveInputs = argv["save-inputs"];

  let rootUrl = resolvePath(outputDir);

  const tempRoot = joinPaths(rootUrl, "TempTypeSpecFiles");
  const tspLocation = await readTspLocation(rootUrl);
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  if (!projectName) {
    throw new Error("cannot find project name");
  }
  const srcDir = joinPaths(tempRoot, projectName);
  const emitter = await getEmitterFromRepoConfig(
    joinPaths(await getRepoRoot(rootUrl), "eng", "emitter-package.json"),
  );
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
  dotenvConfig({ path: resolve(await getRepoRoot(rootUrl), ".env") });
  if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
    args.push("--force");
  }
  await npmCommand(srcDir, args);
  await compileTsp({
    emitterPackage: emitter,
    outputPath: rootUrl,
    resolvedMainFilePath,
    saveInputs: saveInputs,
    additionalEmitterOptions: emitterOptions,
  });

  if (saveInputs) {
    Logger.debug(`Skipping cleanup of temp directory: ${tempRoot}`);
  } else {
    Logger.debug("Cleaning up temp directory");
    await removeDirectory(tempRoot);
  }
}

export async function updateCommand(argv: any) {
  const outputDir = argv["output-dir"];
  const repo = argv["repo"];
  const commit = argv["commit"];
  let tspConfig = argv["tsp-config"];

  if (repo && !commit) {
    throw new Error(
      "Commit SHA is required when specifying `--repo`; please specify a commit using `--commit`",
    );
  }
  if (commit) {
    const tspLocation: TspLocation = await readTspLocation(outputDir);
    tspLocation.commit = commit ?? tspLocation.commit;
    tspLocation.repo = repo ?? tspLocation.repo;
    await writeTspLocationYaml(tspLocation, outputDir);
  } else if (tspConfig) {
    const tspLocation: TspLocation = await readTspLocation(outputDir);
    tspConfig = resolveTspConfigUrl(tspConfig);
    tspLocation.commit = tspConfig.commit ?? tspLocation.commit;
    tspLocation.repo = tspConfig.repo ?? tspLocation.repo;
    await writeTspLocationYaml(tspLocation, outputDir);
  }
  // update argv in case anything changed and call into sync and generate
  await syncCommand(argv);
  await generateCommand(argv);
}

export async function convertCommand(argv: any): Promise<void> {
  const outputDir = argv["output-dir"];
  const swaggerReadme = argv["swagger-readme"];
  const arm = argv["arm"];
  let rootUrl = resolvePath(outputDir);

  Logger.info("Converting swagger to typespec...");
  let readme = swaggerReadme!;
  if (await doesFileExist(readme)) {
    readme = normalizePath(resolve(readme));
  }

  const args = [
    "autorest",
    "--openapi-to-typespec",
    "--csharp=false",
    `--output-folder="${outputDir}"`,
    "--use=@autorest/openapi-to-typespec",
    `"${readme}"`,
  ];
  if (arm) {
    const generateMetadataCmd = [
      "autorest",
      "--csharp",
      "--max-memory-size=8192",
      '--use="https://aka.ms/azsdk/openapi-to-typespec-csharp"',
      `--output-folder="${outputDir}"`,
      "--mgmt-debug.only-generate-metadata",
      "--azure-arm",
      "--skip-csproj",
      `"${readme}"`,
    ];
    try {
      await nodeCommand(outputDir, generateMetadataCmd);
    } catch (err) {
      Logger.error(`Error occurred while attempting to generate ARM metadata: ${err}`);
      process.exit(1);
    }
    try {
      await rename(joinPaths(outputDir, "metadata.json"), joinPaths(outputDir, "resources.json"));
    } catch (err) {
      Logger.error(
        `Error occurred while attempting to rename metadata.json to resources.json: ${err}`,
      );
      process.exit(1);
    }
    args.push("--isArm");
  }
  await nodeCommand(outputDir, args);

  if (arm) {
    try {
      await unlink(joinPaths(rootUrl, "resources.json"));
    } catch (err) {
      Logger.error(`Error occurred while attempting to delete resources.json: ${err}`);
      process.exit(1);
    }
  }
}

export async function generateLockFileCommand(argv: any) {
  const outputDir = argv["output-dir"];
  let rootUrl = resolvePath(outputDir);
  const repoRoot = await getRepoRoot(rootUrl);

  Logger.info("Generating lock file...");
  const args: string[] = ["install"];
  if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
    args.push("--force");
  }
  const tempRoot = await createTempDirectory(rootUrl);
  await cp(joinPaths(repoRoot, "eng", "emitter-package.json"), joinPaths(tempRoot, "package.json"));
  await npmCommand(tempRoot, args);
  const lockFile = await stat(joinPaths(tempRoot, "package-lock.json"));
  if (lockFile.isFile()) {
    await cp(
      joinPaths(tempRoot, "package-lock.json"),
      joinPaths(repoRoot, "eng", "emitter-package-lock.json"),
    );
  }
  await removeDirectory(tempRoot);
  Logger.info(`Lock file generated in ${joinPaths(repoRoot, "eng", "emitter-package-lock.json")}`);
}
