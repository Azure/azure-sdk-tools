import { Logger } from "./log.js";
import {
  joinPaths,
  NodeHost,
  normalizePath,
  resolveCompilerOptions,
  resolvePath,
} from "@typespec/compiler";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./git.js";
import {
  createTempDirectory,
  getEmitterFromRepoConfig,
  readTspLocation,
  removeDirectory,
} from "./fs.js";
import { cp, lstat, mkdir, mkdtemp, readFile, rm, stat, unlink, writeFile } from "fs/promises";
import { tmpdir } from "node:os";
import { isDeepStrictEqual } from "node:util";
import { npmCommand, npxCommand, npmViewPackageDevDependencies } from "./npm.js";
import {
  auditPnpmDependencies,
  pnpmCommand,
  pnpmEmitterLockPath,
  pnpmLockExists,
  pnpmWorkspaceContent,
  publishEmitterFile,
} from "./pnpm.js";
import {
  compileTsp,
  discoverEntrypointFile,
  resolveTspConfigUrl,
  TspLocation,
} from "./typespec.js";
import {
  writeTspLocationYaml,
  getAdditionalDirectoryName,
  getServiceDir,
  makeSparseSpecDir,
  updateExistingTspLocation,
  parseTspClientRepoConfig,
  TspClientConfig,
} from "./utils.js";
import { parse as parseYaml } from "yaml";
import { config as dotenvConfig } from "dotenv";
import { basename, dirname, extname, relative, resolve } from "node:path";
import { doesFileExist } from "./network.js";
import { sortOpenAPIDocument } from "@azure-tools/typespec-autorest";
import { createTspClientMetadata } from "./metadata.js";
import { parse as parseShell } from "shell-quote";

const defaultRelativeEmitterPackageJsonPath = joinPaths("eng", "emitter-package.json");

/**
 * Initializes client library directory and writes the tsp-location.yaml file by processing tspconfig.yaml data.
 *
 * This function reads and processes the tspconfig.yaml file, resolves emitter configuration,
 * determines the output directory based on emitter settings, and creates the tsp-location.yaml
 * file in the appropriate location. It handles both new installations and updates to existing
 * configurations when the update-if-exists flag is set.
 *
 * @param outputDir - The base output directory where the generated files should be placed
 * @param repoRoot - The root directory of the repository containing the project
 * @param tspConfigPath - The path to the tspconfig.yaml file to process
 * @param tspLocationData - tsp-location.yaml data containing repository, commit, and directory information
 * @param argv - Command line arguments object containing various options including:
 *   - update-if-exists: If true, updates existing tsp-location.yaml while preserving existing data
 *   - emitter-package-json-path: Optional path to override the default emitter package.json location
 * @returns Promise that resolves to the final package directory path where tsp-location.yaml was written
 * @throws Error if tspconfig.yaml cannot be read, is empty, or if required emitter configuration is missing
 */
async function initProcessDataAndWriteTspLocation(
  outputDir: string,
  repoRoot: string,
  tspConfigPath: string,
  tspLocationData: TspLocation,
  argv: any,
): Promise<string> {
  // Read the global tsp-client-config.yaml if it exists, otherwise tspclientGlobalConfigData will be undefined.
  const tspclientGlobalConfigData = await parseTspClientRepoConfig(repoRoot);

  // Read tspconfig.yaml contents
  let tspConfigData;
  try {
    tspConfigData = parseYaml(await readFile(tspConfigPath, "utf8"));
  } catch (err) {
    throw new Error(`Could not read tspconfig.yaml at ${tspConfigPath}. Error: ${err}`);
  }
  if (!tspConfigData) {
    throw new Error(`tspconfig.yaml is empty at ${tspConfigPath}`);
  }

  // Finish processing tsp-location.yaml data using the tspconfig.yaml contents
  tspLocationData.additionalDirectories =
    tspConfigData?.options?.["@azure-tools/typespec-client-generator-cli"]?.[
      "additionalDirectories"
    ] ?? [];

  const emitterPackageOverride = resolveEmitterPathFromArgs(argv);
  const emitterData = await getEmitter(
    repoRoot,
    tspConfigData,
    tspclientGlobalConfigData,
    emitterPackageOverride,
  );
  if (emitterData.path) {
    // store relative path to repo root
    tspLocationData.emitterPackageJsonPath = emitterData.path;
  }

  // Check for relevant package path variables and resolve
  let emitterOutputDir = tspConfigData?.options?.[emitterData.emitter]?.["emitter-output-dir"];
  const packageDir = tspConfigData?.options?.[emitterData.emitter]?.["package-dir"];
  let newPackageDir;
  if (packageDir) {
    // Warn that this behavior is deprecated
    Logger.warn(
      `Please update your tspconfig.yaml to include the "emitter-output-dir" option under the "${emitterData.emitter}" emitter options. "package-dir" support is deprecated and will be removed in future versions.`,
    );
    // If no emitter-output-dir is specified, fall back to the legacy package-dir path resolution for the new package directory
    newPackageDir = resolve(
      joinPaths(outputDir, getServiceDir(tspConfigData, emitterData.emitter), packageDir!),
    );
  } else if (emitterOutputDir) {
    const [options, _] = await resolveCompilerOptions(NodeHost, {
      cwd: process.cwd(),
      entrypoint: "main.tsp",
      configPath: tspConfigPath,
      overrides: {
        outputDir: repoRoot,
      },
    });
    emitterOutputDir = options.options?.[emitterData.emitter]?.["emitter-output-dir"];
    newPackageDir = resolve(emitterOutputDir);
  } else {
    throw new Error(
      `Missing emitter-output-dir in ${emitterData.emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`,
    );
  }

  Logger.info(`The resolved package directory path is ${newPackageDir}`);

  if (argv["update-if-exists"]) {
    // If the update-if-exists flag is set, check if there's an existing tsp-location.yaml
    // and update it with the new values while maintaining previously existing data.
    Logger.debug(`Trying to read existing tsp-location.yaml at ${newPackageDir}`);
    tspLocationData = await updateExistingTspLocation(
      tspLocationData,
      newPackageDir,
      emitterPackageOverride,
    );
  }
  await mkdir(newPackageDir, { recursive: true });
  await writeTspLocationYaml(tspLocationData, newPackageDir);
  return newPackageDir;
}

/**
 * Resolves the correct emitter to use in a language repository based on the provided arguments and configuration files.
 *
 * The function determines the emitter name and the path to the emitter's package.json file relative to the repository root.
 * It supports the following resolution order:
 * 1. If an emitter-package.json override is provided, it is used directly and stored relative to the repository root.
 * 2. If a global config file exists with supported emitters, it checks for a match in tspconfig.yaml options.
 * 3. Default to the emitter-package.json in the repository's eng/ directory. If the default emitter-package.json is used,
 * the path will be undefined.
 *
 * @param repoRoot - The root directory of the repository.
 * @param tspConfigData - The parsed tspconfig.yaml data.
 * @param globalConfigFile - Optional global tsp-client-config.yaml configuration.
 * @param emitterPackageJsonOverride - Optional explicit override path to an emitter-package.json file.
 * @returns An object containing the emitter name and an optional relative path to the emitter package.json file.
 * @throws If no valid emitter can be resolved or if the default emitter-package.json is missing or invalid.
 */
async function getEmitter(
  repoRoot: string,
  tspConfigData: any, // tspconfig.yaml data
  globalConfigFile?: TspClientConfig,
  emitterPackageJsonOverride?: string,
): Promise<{ emitter: string; path?: string }> {
  // If an emitter-package.json override value is explicitly provided, use it to get the emitter
  if (emitterPackageJsonOverride) {
    return {
      emitter: await getEmitterFromRepoConfig(emitterPackageJsonOverride),
      path: relative(repoRoot, emitterPackageJsonOverride),
    };
  }

  // If a global config file exists with supportedEmitters configured, use it to
  // find the right emitter. The list of supported emitters will be checked in order, stopping
  // at the first match in tspconfig.yaml.
  if (globalConfigFile && globalConfigFile.supportedEmitters) {
    // Create a Set of config emitter names for lookup
    const configEmitterNames = new Set(Object.keys(tspConfigData.options) ?? []);

    for (const supportedEmitter of globalConfigFile.supportedEmitters) {
      if (configEmitterNames.has(supportedEmitter.name)) {
        Logger.debug(
          `Using emitter: ${supportedEmitter.name} from tspconfig.yaml. There will be no further processing for other supported emitters.`,
        );
        return { emitter: supportedEmitter.name, path: supportedEmitter.path };
      }
    }
  }

  try {
    // If no emitter is found in the global config, fall back to the default emitter-package.json
    return {
      emitter: await getEmitterFromRepoConfig(
        joinPaths(repoRoot, defaultRelativeEmitterPackageJsonPath),
      ),
    };
  } catch (err) {
    throw new Error(
      `Failed to get emitter from default emitter-package.json. Please add a valid emitter-package.json file in the eng/ directory of the repository. Error: ${err}`,
    );
  }
}

export async function initCommand(argv: any) {
  let outputDir = argv["output-dir"];
  let tspConfigPath = argv["tsp-config"];
  const skipSyncAndGenerate = argv["skip-sync-and-generate"];

  const repoRoot = await getRepoRoot(outputDir);

  let isUrl = true;
  if (argv["local-spec-repo"]) {
    const localSpecRepo = argv["local-spec-repo"];
    if (!(await doesFileExist(localSpecRepo))) {
      throw new Error(`Local spec repo not found: ${localSpecRepo}`);
    }
    isUrl = false;
    tspConfigPath = localSpecRepo;
  } else if (await doesFileExist(tspConfigPath)) {
    isUrl = false;
  }
  let tspLocationData: TspLocation = {
    directory: "",
    commit: "",
    repo: "",
    additionalDirectories: [],
  };
  if (isUrl) {
    // URL scenario
    const resolvedConfigUrl = resolveTspConfigUrl(tspConfigPath);
    const cloneDir = await makeSparseSpecDir(repoRoot);
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(outputDir, cloneDir, `https://github.com/${resolvedConfigUrl.repo}.git`);
    await sparseCheckout(cloneDir);
    tspConfigPath = joinPaths(resolvedConfigUrl.path, "tspconfig.yaml");
    await addSpecFiles(cloneDir, tspConfigPath);
    await checkoutCommit(cloneDir, resolvedConfigUrl.commit);

    tspLocationData.directory = resolvedConfigUrl.path;
    tspLocationData.commit = resolvedConfigUrl.commit;
    tspLocationData.repo = resolvedConfigUrl.repo;

    outputDir = await initProcessDataAndWriteTspLocation(
      outputDir,
      repoRoot,
      joinPaths(cloneDir, tspConfigPath),
      tspLocationData,
      argv,
    );
    Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
  } else {
    // Local directory scenario
    if (!tspConfigPath.endsWith("tspconfig.yaml")) {
      tspConfigPath = joinPaths(tspConfigPath, "tspconfig.yaml");
    }

    tspConfigPath = tspConfigPath.replaceAll("\\", "/");
    const matchRes = tspConfigPath.match(".*/(?<path>specification/.*)/tspconfig.yaml$");
    if (matchRes) {
      if (matchRes.groups) {
        tspLocationData.directory = matchRes.groups!["path"]!;
      }
    }
    tspLocationData.commit = argv["commit"] ?? "<replace with your value>";
    tspLocationData.repo = argv["repo"] ?? "<replace with your value>";

    outputDir = await initProcessDataAndWriteTspLocation(
      outputDir,
      repoRoot,
      tspConfigPath,
      tspLocationData,
      argv,
    );
  }

  if (!skipSyncAndGenerate) {
    // update argv in case anything changed and call into sync and generate
    argv["output-dir"] = outputDir;
    if (!isUrl) {
      // If the local spec repo is provided, we need to update the local-spec-repo argument for syncing as well
      argv["local-spec-repo"] = tspConfigPath;
    }
    await syncCommand(argv);
    await generateCommand(argv);
  } else {
    // If skip-sync-and-generate is set, just check if we should create the tsp-client-metadata.yaml file.
    const tspLocation: TspLocation = await readTspLocation(outputDir);
    await createTspClientMetadata(
      outputDir,
      repoRoot,
      getEmitterPackageJsonPath(repoRoot, tspLocation),
    );
  }
  return outputDir;
}

export async function syncCommand(argv: any) {
  let outputDir = argv["output-dir"];
  let localSpecRepo = argv["local-spec-repo"];
  const batch = argv["batch"] ?? false;

  const tempRoot = await createTempDirectory(outputDir);
  const repoRoot = await getRepoRoot(outputDir);
  Logger.debug(`Repo root is ${repoRoot}`);
  if (!repoRoot) {
    throw new Error("Could not find repo root");
  }
  const tspLocation: TspLocation = await readTspLocation(outputDir);
  if (!tspLocation.directory || !tspLocation.commit || !tspLocation.repo) {
    throw new Error(
      "tsp-location.yaml is missing required field(s) for sync operation: directory, commit, repo",
    );
  }
  const emitterPackageJsonPath = getEmitterPackageJsonPath(repoRoot, tspLocation);
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  Logger.debug(`Using project name: ${projectName}`);
  if (!projectName) {
    projectName = "src";
  }
  const srcDir = joinPaths(tempRoot, projectName);
  await mkdir(srcDir, { recursive: true });

  if (localSpecRepo) {
    if (batch) {
      localSpecRepo = resolve(localSpecRepo, tspLocation.directory);
      Logger.info(
        `Resolved local spec repo path using tsp-location.yaml directory: ${localSpecRepo}`,
      );
    }
    if (localSpecRepo.endsWith("tspconfig.yaml")) {
      // If the path is to tspconfig.yaml, we need to remove it to get the spec directory
      localSpecRepo = localSpecRepo.split("tspconfig.yaml")[0];
    }
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
    const clonedTspProject = joinPaths(cloneDir, tspLocation.directory);
    try {
      // Check if the cloned TypeSpec project exists
      await stat(clonedTspProject);
    } catch (err) {
      Logger.error(
        `Unexpected error reading the TypeSpec project directory after cloning the repository: '${clonedTspProject}'. Please verify that the TypeSpec directory path is correct and that it exists at the specified commit: '${tspLocation.commit}'.\n${err}`,
      );
      await removeDirectory(cloneDir);
      process.exit(1);
    }
    await cp(clonedTspProject, srcDir, { recursive: true });
    for (const dir of tspLocation.additionalDirectories!) {
      Logger.info(`Syncing additional directory: ${dir}`);
      await cp(joinPaths(cloneDir, dir), joinPaths(tempRoot, getAdditionalDirectoryName(dir)), {
        recursive: true,
      });
    }
    Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
  }

  const pnpmLock = pnpmEmitterLockPath(emitterPackageJsonPath);
  if (await pnpmLockExists(pnpmLock)) {
    const workspace = await pnpmWorkspaceContent(emitterPackageJsonPath);
    for (const name of ["package.json", "pnpm-lock.yaml", "pnpm-workspace.yaml"]) {
      await pnpmLockExists(joinPaths(tempRoot, name));
    }
    await removeStaleWorkspaceFile(joinPaths(tempRoot, "package-lock.json"));
    await cp(pnpmLock, joinPaths(tempRoot, "pnpm-lock.yaml"));
    await writeFile(joinPaths(tempRoot, "pnpm-workspace.yaml"), workspace);
  } else {
    // A previous --save-inputs run may have left a different manager's lock behind.
    for (const name of ["pnpm-lock.yaml", "pnpm-workspace.yaml"]) {
      await removeStaleWorkspaceFile(joinPaths(tempRoot, name));
    }
    const emitterLockPath = getEmitterLockPath(emitterPackageJsonPath);
    const stagedLockPath = joinPaths(tempRoot, "package-lock.json");
    try {
      await cp(emitterLockPath, stagedLockPath, { dereference: true });
    } catch (error: any) {
      if (error.code !== "ENOENT") throw error;
      await removeStaleWorkspaceFile(stagedLockPath);
    }
  }
  try {
    await cp(emitterPackageJsonPath, joinPaths(tempRoot, "package.json"), { recursive: true });
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
  const skipInstall = argv["skip-install"];

  const repoRoot = await getRepoRoot(outputDir);

  const tempRoot = joinPaths(outputDir, "TempTypeSpecFiles");
  const tspLocation = await readTspLocation(outputDir);
  if (!tspLocation.directory) {
    throw new Error(
      "tsp-location.yaml is missing required field(s) for generate operation: directory",
    );
  }
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  if (!projectName) {
    throw new Error("cannot find project name");
  }
  const srcDir = joinPaths(tempRoot, projectName);
  const emitterPackageJsonPath = getEmitterPackageJsonPath(repoRoot, tspLocation);
  const pnpmLock = pnpmEmitterLockPath(emitterPackageJsonPath);
  const usePnpm = await pnpmLockExists(pnpmLock);
  if (usePnpm) await pnpmLockExists(emitterPackageJsonPath);
  const emitter = await getEmitterFromRepoConfig(emitterPackageJsonPath);
  if (!emitter) {
    throw new Error("emitter is undefined");
  }

  // Check if we should create tsp-client-metadata.yaml file
  await createTspClientMetadata(outputDir, repoRoot, emitterPackageJsonPath);

  const mainFilePath = await discoverEntrypointFile(srcDir, tspLocation.entrypointFile);
  const resolvedMainFilePath = joinPaths(srcDir, mainFilePath);
  // Read tspconfig.yaml contents
  const tspConfigPath = joinPaths(srcDir, "tspconfig.yaml");
  let tspConfigData;
  try {
    tspConfigData = parseYaml(await readFile(tspConfigPath, "utf8"));
  } catch (err) {
    Logger.warn(
      `Could not read tspconfig.yaml at ${tspConfigPath}. Will use the repo root as the target output directory during compilation, this may cause unexpected behavior. Error: ${err}`,
    );
  }

  // Give preference to package-dir if both are specified
  // This is to avoid breaking existing behavior
  const legacyPathResolution = tspConfigData?.options?.[emitter]?.["package-dir"];

  if (skipInstall) {
    Logger.info("Skipping installation of dependencies");
  } else if (usePnpm) {
    const manifest = await readFile(emitterPackageJsonPath, "utf8");
    const workspace = await pnpmWorkspaceContent(emitterPackageJsonPath, manifest);
    const readStaged = async (name: string) => {
      const path = joinPaths(tempRoot, name);
      return (await pnpmLockExists(path)) ? readFile(path, "utf8") : undefined;
    };
    if (
      (await readStaged("pnpm-lock.yaml")) !== (await readFile(pnpmLock, "utf8")) ||
      (await readStaged("package.json")) !== manifest ||
      (await readStaged("pnpm-workspace.yaml")) !== workspace
    ) {
      throw new Error(
        "Staged emitter inputs are missing or out of date; run tsp-client sync before generate",
      );
    }
    Logger.info("Installing emitter dependencies with pnpm...");
    dotenvConfig({ path: resolve(repoRoot, ".env") });
    const args = ["install", "--frozen-lockfile"];
    if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") args.push("--force");
    await pnpmCommand(tempRoot, args);
    await auditPnpmDependencies(tempRoot);
  } else {
    if (
      await lstat(joinPaths(tempRoot, "pnpm-lock.yaml")).then(
        (entry) => entry.isFile() || entry.isSymbolicLink(),
        (error) => {
          if (error.code !== "ENOENT") throw error;
          return false;
        },
      )
    ) {
      throw new Error("Emitter package manager changed; run tsp-client sync before generate");
    }
    Logger.info("Installing dependencies from npm...");
    const args: string[] = [];
    try {
      // Check if package-lock.json exists, if it does, we'll install dependencies through `npm ci`
      const stagedLock = await stat(joinPaths(tempRoot, "package-lock.json"));
      args.push(stagedLock.isFile() ? "ci" : "install");
    } catch (err) {
      // If package-lock.json doesn't exist, we'll attempt to install dependencies through `npm install`
      args.push("install");
    }
    // NOTE: This environment variable should be used for developer testing only. A force
    // install may ignore any conflicting dependencies and result in unexpected behavior.
    dotenvConfig({ path: resolve(repoRoot, ".env") });
    if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
      args.push("--force");
    }
    await npmCommand(srcDir, args);

    // Log all package versions for diagnostics, ignoring errors
    try {
      await npmCommand(srcDir, ["ls", "-a", "|", "grep", "-E", "'typespec|azure-tools'"]);
    } catch (err) {}
  }
  const result = await compileTsp({
    emitterPackage: emitter,
    outputPath: legacyPathResolution ? outputDir : repoRoot, // always use repo root when using emitter-output-dir
    resolvedMainFilePath,
    saveInputs: saveInputs,
    additionalEmitterOptions: emitterOptions,
    trace: argv["trace"],
    legacyPathResolution: legacyPathResolution,
  });

  if (argv["debug"]) {
    Logger.warn(`Example of how to compile using the tsp commandline. NOTE: tsp-client does NOT directly run this command, results may vary:
        ${result.exampleCmd}
        `);
  }

  if (saveInputs) {
    Logger.debug(`Skipping cleanup of temp directory: ${tempRoot}`);
  } else {
    Logger.debug("Cleaning up temp directory");
    await removeDirectory(tempRoot);
  }

  if (!result.success) {
    Logger.error("Failed to generate client");
    process.exit(1);
  }
}

/**
 * Processes batch updates for multiple directories specified in the tsp-location.yaml file.
 *
 * Iterates over each directory listed in the `batch` property of the provided TspLocation object,
 * updating each by invoking the updateCommand with the appropriate output directory.
 * If any batch directory fails to process, the function logs the error and immediately throws,
 * halting further batch processing.
 *
 * @param tspLocation - The TspLocation object containing batch directory information.
 * @param outputDir - The base output directory where batch directories are located.
 * @param argv - Command line arguments object, which will be updated for each batch directory.
 * @returns Promise that resolves when all batch directories have been processed successfully.
 * @throws Error if processing any batch directory fails.
 */
async function processBatchUpdate(tspLocation: TspLocation, outputDir: string, argv: any) {
  // Process each directory in the batch
  for (const batchDir of tspLocation.batch ?? []) {
    const fullBatchPath = resolve(outputDir, batchDir);
    Logger.info(`Processing batch directory: ${batchDir}`);

    try {
      argv["output-dir"] = fullBatchPath;
      await updateCommand(argv);
      Logger.info(`Successfully processed batch directory: ${batchDir}`);
    } catch (error) {
      Logger.error(`Failed to process batch directory ${batchDir}: ${error}`);
      throw error; // Stop processing and propagate the error immediately
    }
  }

  Logger.info("All batch directories processed successfully");
}

export async function updateCommand(argv: any) {
  const outputDir = argv["output-dir"];
  const repo = argv["repo"];
  const commit = argv["commit"];
  let tspConfig = argv["tsp-config"];

  const tspLocation: TspLocation = await readTspLocation(outputDir);

  // Check if this is a batch configuration
  if (tspLocation.batch) {
    Logger.info(`Found batch configuration with ${tspLocation.batch.length} directories`);
    if (argv["local-spec-repo"]) {
      const specRepoRoot = await getRepoRoot(argv["local-spec-repo"]);
      Logger.info(
        `During batch processing will use local spec repo root with child library tsp-location.yaml data to resolve path to typespec project directory: ${specRepoRoot}`,
      );
      argv["local-spec-repo"] = specRepoRoot;
      argv["batch"] = true;
    }
    await processBatchUpdate(tspLocation, outputDir, argv);
    return;
  }

  // Original non-batch logic
  if (repo && !commit) {
    throw new Error(
      "Commit SHA is required when specifying `--repo`; please specify a commit using `--commit`",
    );
  }
  if (commit) {
    tspLocation.commit = commit ?? tspLocation.commit;
    tspLocation.repo = repo ?? tspLocation.repo;
    await writeTspLocationYaml(tspLocation, outputDir);
  } else if (tspConfig) {
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
  const fullyCompatible = argv["fully-compatible"];
  const debug = argv["debug"];
  let rootUrl = resolvePath(outputDir);

  Logger.info("Converting swagger to typespec...");
  let readme = swaggerReadme!;
  if (await doesFileExist(readme)) {
    readme = normalizePath(resolve(readme));
  }
  try {
    // Build the command to convert to typespec
    const args = [
      "autorest",
      "--openapi-to-typespec",
      "--csharp=false",
      "--use=@autorest/openapi-to-typespec",
      `--output-folder="${outputDir}"`,
      `"${readme}"`,
    ];

    if (arm) {
      args.push("--isArm");
    }

    if (fullyCompatible) {
      args.push("--isFullCompatible");
    }

    if (debug) {
      args.push("--debug");
    }
    await npxCommand(outputDir, args);

    if (arm) {
      try {
        await unlink(joinPaths(rootUrl, "resources.json"));
      } catch (err) {
        Logger.error(`Error occurred while attempting to delete resources.json: ${err}`);
        process.exit(1);
      }
    }
  } catch (err) {
    Logger.error(
      `An error occurred during convert command. Verify that autorest is accessible via npx (or installed globally) and try again. Error: ${err}`,
    );
    process.exit(1);
  }
}

export async function generateConfigFilesCommand(argv: any) {
  const outputDir = argv["output-dir"];
  const packageJsonPath = normalizePath(resolve(argv["package-json"]));
  const overridePath = argv["overrides"] ?? undefined;

  if (packageJsonPath === undefined || !(await doesFileExist(packageJsonPath))) {
    throw new Error(`package.json not found in: ${packageJsonPath ?? "[Not Specified]"}`);
  }
  Logger.info("Generating emitter-package.json file...");
  const content = await readFile(packageJsonPath);
  const packageJson: Record<string, any> = JSON.parse(content.toString());

  const emitterPath =
    resolveEmitterPathFromArgs(argv) ??
    joinPaths(await getRepoRoot(outputDir), defaultRelativeEmitterPackageJsonPath);
  const manager = await resolveEmitterManager(emitterPath, argv);
  if (manager === "pnpm" && overridePath) {
    throw new Error(
      "--overrides uses npm override semantics; configure pnpm overrides in the emitter pnpm workspace file instead",
    );
  }
  if (manager === "pnpm" && (await doesFileExist(emitterPath))) {
    await pnpmWorkspaceContent(emitterPath);
  }

  // Start with the existing emitter-package.json if it exists, otherwise create a new one
  let emitterPackageJson: Record<string, any>;
  try {
    emitterPackageJson = JSON.parse(await readFile(emitterPath, "utf8"));
    Logger.debug(`Updating existing ${basename(emitterPath)}`);
  } catch (err) {
    Logger.debug(`Couldn't read ${basename(emitterPath)}. Creating a new file. Error: ${err}`);
    emitterPackageJson = {};
  }

  // Always set the main field
  emitterPackageJson["main"] = "dist/src/index.js";

  // Initialize dependencies if not present
  if (!emitterPackageJson["dependencies"]) {
    emitterPackageJson["dependencies"] = {};
  }

  let overrideJson: Record<string, any> = {};
  if (overridePath) {
    overrideJson = JSON.parse((await readFile(overridePath)).toString()) ?? {};
  }

  // Add emitter as dependency
  emitterPackageJson["dependencies"][packageJson["name"]] =
    overrideJson[packageJson["name"]] ?? packageJson["version"];

  delete overrideJson[packageJson["name"]];
  const devDependencies: Record<string, any> = {};
  const peerDependencies = packageJson["peerDependencies"] ?? {};
  const possiblyPinnedPackages: Array<string> =
    packageJson["azure-sdk/emitter-package-json-pinning"] ?? Object.keys(peerDependencies);

  // Get the devDependencies from the emitter's package.json
  let localDevDeps: Record<string, string> = packageJson["devDependencies"] ?? {};

  // Only query npm if the flag to use npm pinning is set
  if (argv["use-npm-pinning"]) {
    Logger.info("Using npm to resolve devDependencies for pinning versions...");
    localDevDeps =
      (await npmViewPackageDevDependencies(packageJson["name"], packageJson["version"])) ?? {};
  }

  for (const pinnedPackage of possiblyPinnedPackages) {
    let pinnedVersion = localDevDeps[pinnedPackage];
    if (pinnedVersion && !overrideJson[pinnedPackage]) {
      Logger.info(`Pinning ${pinnedPackage} to ${pinnedVersion}`);
      devDependencies[pinnedPackage] = pinnedVersion;
    }
  }

  // Update devDependencies with new pinned packages
  if (Object.keys(devDependencies).length > 0) {
    if (!emitterPackageJson["devDependencies"]) {
      emitterPackageJson["devDependencies"] = {};
    }
    // Merge new devDependencies with existing ones
    emitterPackageJson["devDependencies"] = {
      ...emitterPackageJson["devDependencies"],
      ...devDependencies,
    };
  }

  if (Object.keys(overrideJson).length > 0) {
    emitterPackageJson["overrides"] = overrideJson;
  }

  const contents = JSON.stringify(emitterPackageJson, null, 2);
  if (manager === "pnpm") {
    await generateLockFileCommandCore(outputDir, emitterPath, [], manager, contents);
  } else {
    await writeFile(emitterPath, contents);
    await generateEmitterLock(outputDir, emitterPath, argv);
  }
  Logger.info(`${basename(emitterPath)} file generated in '${dirname(emitterPath)}' directory`);
}

async function resolveEmitterManager(emitterPath: string, argv: any): Promise<"npm" | "pnpm"> {
  const hasPnpmLock = await pnpmLockExists(pnpmEmitterLockPath(emitterPath));
  const manager = argv["package-manager"] ?? (hasPnpmLock ? "pnpm" : "npm");
  if (manager !== "npm" && manager !== "pnpm")
    throw new Error(`Unknown package manager: ${manager}`);
  if (manager === "npm" && hasPnpmLock)
    throw new Error("Remove the emitter pnpm lock before switching to npm");
  if (manager === "pnpm" && argv["npm-args"])
    throw new Error(
      "--npm-args cannot be used with pnpm; configure the registry in your user .npmrc",
    );
  return manager;
}

async function generateEmitterLock(outputDir: string, emitterPath: string, argv: any) {
  const manager = await resolveEmitterManager(emitterPath, argv);
  await generateLockFileCommandCore(
    outputDir,
    emitterPath,
    parseNpmArgs(argv["npm-args"]),
    manager,
  );
}

export async function generateLockFileCommand(argv: any) {
  await generateEmitterLock(
    argv["output-dir"],
    resolveEmitterPathFromArgs(argv) ??
      joinPaths(await getRepoRoot(argv["output-dir"]), defaultRelativeEmitterPackageJsonPath),
    argv,
  );
}

export async function generateLockFileCommandCore(
  outputDir: string,
  emitterPackageJsonPath: string,
  npmArgs: string[] = [],
  manager: "npm" | "pnpm" = "npm",
  pendingManifest?: string,
) {
  Logger.info("Generating lock file...");
  const args: string[] = ["install"];
  if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
    args.push("--force");
  }
  args.push(...npmArgs);
  if (npmArgs.length > 0) {
    Logger.info(`Passing additional ${manager} flags to the install command.`);
  }
  if (manager === "pnpm") {
    await pnpmLockExists(emitterPackageJsonPath);
    const contents = pendingManifest ?? (await readFile(emitterPackageJsonPath, "utf8"));
    const workspace = await pnpmWorkspaceContent(emitterPackageJsonPath, contents);
    const emitterLockPath = pnpmEmitterLockPath(emitterPackageJsonPath);
    const repoRoot = await getRepoRoot(outputDir ?? process.cwd()).catch(() => undefined);
    if (repoRoot) dotenvConfig({ path: resolve(repoRoot, ".env") });
    const tempRoot = await mkdtemp(joinPaths(tmpdir(), "tsp-emitter-lock-"));
    try {
      await writeFile(joinPaths(tempRoot, "package.json"), contents);
      await writeFile(joinPaths(tempRoot, "pnpm-workspace.yaml"), workspace);
      const previousLock = (await pnpmLockExists(emitterLockPath))
        ? await readFile(emitterLockPath, "utf8")
        : undefined;
      if (previousLock !== undefined) {
        await writeFile(joinPaths(tempRoot, "pnpm-lock.yaml"), previousLock);
      }
      await pnpmCommand(tempRoot, ["install", "--no-frozen-lockfile", ...args.slice(1)]);
      const resultingPolicy = await readFile(joinPaths(tempRoot, "pnpm-workspace.yaml"), "utf8");
      if (!isDeepStrictEqual(parseYaml(resultingPolicy), parseYaml(workspace))) {
        throw new Error(
          "pnpm changed the workspace policy while resolving dependencies; the emitter lockfile was not published",
        );
      }
      await auditPnpmDependencies(tempRoot);
      await publishEmitterFile(
        emitterLockPath,
        await readFile(joinPaths(tempRoot, "pnpm-lock.yaml"), "utf8"),
      );
      if (pendingManifest !== undefined) {
        try {
          await publishEmitterFile(emitterPackageJsonPath, pendingManifest);
        } catch (error) {
          try {
            if (previousLock === undefined) await rm(emitterLockPath, { force: true });
            else await publishEmitterFile(emitterLockPath, previousLock);
          } catch (rollbackError) {
            throw new AggregateError(
              [error, rollbackError],
              `Could not publish emitter manifest or restore ${emitterLockPath}`,
            );
          }
          throw error;
        }
      }
    } finally {
      await removeDirectory(tempRoot).catch((error) => {
        Logger.warn(`Could not clean up pnpm lock generation workspace: ${error}`);
      });
    }
    Logger.info(`Lock file generated in ${emitterLockPath}`);
    return;
  }
  const tempRoot = await createTempDirectory(outputDir);
  await cp(emitterPackageJsonPath, joinPaths(tempRoot, "package.json"));
  await npmCommand(tempRoot, args);
  const lockFile = await stat(joinPaths(tempRoot, "package-lock.json"));
  const emitterLockPath = getEmitterLockPath(emitterPackageJsonPath);
  if (lockFile.isFile()) await cp(joinPaths(tempRoot, "package-lock.json"), emitterLockPath);
  await removeDirectory(tempRoot);
  Logger.info(`Lock file generated in ${emitterLockPath}`);
}

export async function installDependencies(argv: any) {
  const outputPath = argv["path"];
  const repoRoot = await getRepoRoot(process.cwd());
  let installPath = repoRoot;
  if (outputPath !== undefined) {
    Logger.warn(
      "The install path of the node_modules/ directory must be in the path of the target project, otherwise other commands using npm will fail.",
    );
    installPath = resolvePath(outputPath);
  }

  const args: string[] = [];
  const emitterManifest = joinPaths(repoRoot, "eng", "emitter-package.json");
  const pnpmLock = pnpmEmitterLockPath(emitterManifest);
  if (await pnpmLockExists(pnpmLock)) {
    const workspace = await pnpmWorkspaceContent(emitterManifest);
    const lockContents = await readFile(pnpmLock, "utf8");
    await pnpmLockExists(joinPaths(installPath, "package.json"));
    // Never overwrite an unrelated repository workspace or lockfile.
    for (const [file, expected] of [
      [joinPaths(installPath, "pnpm-workspace.yaml"), workspace],
      [joinPaths(installPath, "pnpm-lock.yaml"), lockContents],
    ] as const) {
      if ((await pnpmLockExists(file)) && (await readFile(file, "utf8")) !== expected) {
        throw new Error(
          `Refusing to overwrite an existing ${file}; choose a separate install path`,
        );
      }
    }
    await cp(emitterManifest, joinPaths(installPath, "package.json"));
    await writeFile(joinPaths(installPath, "pnpm-workspace.yaml"), workspace);
    await writeFile(joinPaths(installPath, "pnpm-lock.yaml"), lockContents);
    dotenvConfig({ path: resolve(repoRoot, ".env") });
    const pnpmArgs = ["install", "--frozen-lockfile"];
    if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") pnpmArgs.push("--force");
    await pnpmCommand(installPath, pnpmArgs);
    await auditPnpmDependencies(installPath);
    return;
  }
  await cp(emitterManifest, joinPaths(installPath, "package.json"));
  try {
    // Check if emitter-package-lock.json exists, if it does, we'll install dependencies through `npm ci`
    const emitterLockPath = joinPaths(repoRoot, "eng", "emitter-package-lock.json");
    await stat(joinPaths(repoRoot, "eng", "emitter-package-lock.json"));
    await cp(emitterLockPath, joinPaths(installPath, "package-lock.json"));
    args.push("ci");
  } catch (err) {
    // If package-lock.json doesn't exist, we'll attempt to install dependencies through `npm install`
    args.push("install");
  }
  // NOTE: This environment variable should be used for developer testing only. A force
  // install may ignore any conflicting dependencies and result in unexpected behavior.
  dotenvConfig({ path: resolve(repoRoot, ".env") });
  if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
    args.push("--force");
  }
  Logger.info("Installing dependencies from npm...");
  await npmCommand(installPath, args);
}

export async function sortSwaggerCommand(argv: any): Promise<void> {
  Logger.info("Sorting a swagger content...");
  let swaggerFile = argv["swagger-file"];
  if (swaggerFile === undefined || !(await doesFileExist(swaggerFile))) {
    throw new Error(`Swagger file not found: ${swaggerFile ?? "[Not Specified]"}`);
  }
  swaggerFile = normalizePath(resolve(swaggerFile));

  const content = await readFile(swaggerFile);
  const document = JSON.parse(content.toString());
  const sorted = sortOpenAPIDocument(document);
  await writeFile(swaggerFile, JSON.stringify(sorted, null, 2));
  Logger.info(`${swaggerFile} has been sorted.`);
}

function getEmitterPackageJsonPath(repoRoot: string, tspLocation: TspLocation): string {
  const relativePath = tspLocation.emitterPackageJsonPath ?? defaultRelativeEmitterPackageJsonPath;
  return joinPaths(repoRoot, relativePath);
}

async function removeStaleWorkspaceFile(path: string): Promise<void> {
  try {
    const entry = await lstat(path);
    if (entry.isFile() || entry.isSymbolicLink()) await rm(path, { force: true });
  } catch (error: any) {
    if (error.code !== "ENOENT") throw error;
  }
}

function getEmitterLockPath(emitterPackageJsonPath: string): string {
  const emitterPackageJsonFileName = basename(
    emitterPackageJsonPath,
    extname(emitterPackageJsonPath),
  );
  return joinPaths(dirname(emitterPackageJsonPath), `${emitterPackageJsonFileName}-lock.json`);
}

function resolveEmitterPathFromArgs(argv: any): string | undefined {
  const emitterPath = argv["emitter-package-json-path"];
  if (emitterPath) {
    return resolve(emitterPath);
  }

  return undefined;
}

/**
 * Parses a shell-like string of npm arguments into an array using the shell-quote library.
 * Properly handles quoted strings, escaping, and all standard shell argument patterns.
 * Examples:
 *   - `--tag "foo bar"` becomes `["--tag", "foo bar"]`
 *   - `--name 'my package'` becomes `["--name", "my package"]`
 *   - `--message "He said \"hello\""` becomes `["--message", "He said \"hello\""]`
 * Returns an empty array if the input is undefined or empty.
 */
export function parseNpmArgs(npmArgsString: string | undefined): string[] {
  if (!npmArgsString) {
    return [];
  }

  const parsed = parseShell(npmArgsString);

  // Filter out any non-string entries (shell-quote can return objects for complex shell constructs)
  return parsed.filter((arg): arg is string => typeof arg === "string");
}
