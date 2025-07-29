import { Logger } from "./log.js";
import { joinPaths, normalizePath, resolvePath } from "@typespec/compiler";
import { addSpecFiles, checkoutCommit, cloneRepo, getRepoRoot, sparseCheckout } from "./git.js";
import {
  createTempDirectory,
  getEmitterFromRepoConfig,
  readTspLocation,
  removeDirectory,
} from "./fs.js";
import { cp, mkdir, readFile, stat, unlink, writeFile } from "fs/promises";
import { npmCommand, nodeCommand } from "./npm.js";
import {
  compileTsp,
  discoverEntrypointFile,
  resolveAdditionalDirectory,
  resolveTspConfigUrl,
  TspLocation,
} from "./typespec.js";
import {
  writeTspLocationYaml,
  getAdditionalDirectoryName,
  getServiceDir,
  makeSparseSpecDir,
  getPathToDependency,
  loadTspConfig,
  createRemoteConfigLoader,
  createLocalConfigLoader,
  extractSpecificationPath,
} from "./utils.js";
import { stringify } from "yaml";
import { config as dotenvConfig } from "dotenv";
import { basename, dirname, extname, relative, resolve } from "node:path";
import { doesFileExist } from "./network.js";
import { sortOpenAPIDocument } from "@azure-tools/typespec-autorest";

const defaultRelativeEmitterPackageJsonPath = joinPaths("eng", "emitter-package.json");

export async function initCommand(argv: any) {
  let outputDir = argv["output-dir"];
  let tspConfig = argv["tsp-config"];
  const skipSyncAndGenerate = argv["skip-sync-and-generate"];
  const commit = argv["commit"] ?? "<replace with your value>";
  const repo = argv["repo"] ?? "<replace with your value>";
  const repoRoot = await getRepoRoot(outputDir);

  const emitterPackageOverride = resolveEmitterPathFromArgs(argv);

  const emitter = await getEmitterFromRepoConfig(
    emitterPackageOverride ?? joinPaths(repoRoot, defaultRelativeEmitterPackageJsonPath),
  );
  if (!emitter) {
    throw new Error("Couldn't find emitter-package.json in the repo");
  }

  let isUrl = true;
  if (argv["local-spec-repo"]) {
    const localSpecRepo = argv["local-spec-repo"];
    if (!(await doesFileExist(localSpecRepo))) {
      throw new Error(`Local spec repo not found: ${localSpecRepo}`);
    }
    isUrl = false;
    tspConfig = localSpecRepo;
  } else if (await doesFileExist(tspConfig)) {
    isUrl = false;
  }
  const { config: configYaml, rootMetadata: rootConfigPath } = isUrl
                                                                  ? await loadTspConfig<{ url: string; directory: string; commit: string; repo: string }>(tspConfig, createRemoteConfigLoader())
                                                                  : await loadTspConfig<string>(tspConfig, createLocalConfigLoader());                                                        
  if (!configYaml) {
      throw new Error(`tspconfig.yaml is empty at ${tspConfig}`);
  }
  const serviceDir = getServiceDir(configYaml, emitter);
  const packageDir = configYaml?.options?.[emitter]?.["package-dir"];
  if (!packageDir) {
    throw new Error(
      `Missing package-dir in ${emitter} options of tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`,
    );
  }
  const newPackageDir = joinPaths(outputDir, serviceDir, packageDir);
  await mkdir(newPackageDir, { recursive: true });
  let tspLocationData: TspLocation;
  if (isUrl) {
    const resolvedConfigUrl = resolveTspConfigUrl(tspConfig);
    const cloneDir = await makeSparseSpecDir(repoRoot);
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(outputDir, cloneDir, `https://github.com/${resolvedConfigUrl.repo}.git`);
    await sparseCheckout(cloneDir);
    const tspConfigPath = joinPaths(resolvedConfigUrl.path, "tspconfig.yaml");
    await addSpecFiles(cloneDir, tspConfigPath);
    await checkoutCommit(cloneDir, resolvedConfigUrl.commit);
    
    tspLocationData = {
      directory: resolvedConfigUrl.path,
      commit: resolvedConfigUrl.commit,
      repo: resolvedConfigUrl.repo,
      additionalDirectories:
        configYaml?.options?.["@azure-tools/typespec-client-generator-cli"]?.[
          "additionalDirectories"
        ] ?? [],
    };
    if (argv["emitter-package-json-path"]) {
      tspLocationData.emitterPackageJsonPath = argv["emitter-package-json-path"];
    }
    Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
  } else {
    // Local directory scenario
    if (!tspConfig.endsWith("tspconfig.yaml")) {
      tspConfig = joinPaths(tspConfig, "tspconfig.yaml");
    }
    tspConfig = tspConfig.replaceAll("\\", "/");
    const matchRes = tspConfig.match(".*/(?<path>specification/.*)/tspconfig.yaml$");
    var directory = "";
    if (matchRes) {
      if (matchRes.groups) {
        directory = matchRes.groups!["path"]!;
      }
    }
    tspLocationData = {
      directory: directory,
      commit: commit ?? "",
      repo: repo ?? "",
      additionalDirectories:
        configYaml?.options?.["@azure-tools/typespec-client-generator-cli"]?.[
          "additionalDirectories"
        ] ?? [],
    };
    const emitterPackageOverride = resolveEmitterPathFromArgs(argv);
    if (emitterPackageOverride) {
      // store relative path to repo root
      tspLocationData.emitterPackageJsonPath = relative(repoRoot, emitterPackageOverride);
    }
  }
  await writeTspLocationYaml(tspLocationData, newPackageDir);
  outputDir = newPackageDir;

  if (!skipSyncAndGenerate) {
    // update argv in case anything changed and call into sync and generate
    argv["output-dir"] = outputDir;
    if (!isUrl) {
      // If the local spec repo is provided, we need to update the local-spec-repo argument for syncing as well
      argv["local-spec-repo"] = tspConfig;
    }
    await syncCommand(argv);
    await generateCommand(argv);
  }
  return outputDir;
}

export async function syncCommand(argv: any) {
  let outputDir = argv["output-dir"];
  let localSpecRepo = argv["local-spec-repo"];

  const tempRoot = await createTempDirectory(outputDir);
  const repoRoot = await getRepoRoot(outputDir);
  Logger.debug(`Repo root is ${repoRoot}`);
  if (!repoRoot) {
    throw new Error("Could not find repo root");
  }
  const tspLocation: TspLocation = await readTspLocation(outputDir);
  const emitterPackageJsonPath = getEmitterPackageJsonPath(repoRoot, tspLocation);

  const { config: resolvedConfig, rootMetadata: rootConfigPath } = localSpecRepo
                                                                  ? await loadTspConfig<string>(localSpecRepo, createLocalConfigLoader())
                                                                  : await loadTspConfig<{ url: string; directory: string; commit: string; repo: string }>(`https://raw.githubusercontent.com/${tspLocation.repo}/${tspLocation.commit}/${tspLocation.directory}/tspconfig.yaml`, createRemoteConfigLoader());
  const rootConfigPathStr = typeof rootConfigPath === "string" ? rootConfigPath : rootConfigPath.url;
  // assume directory of root tspconfig.yaml is ${projectName}.xxx
  const dirSplit = rootConfigPathStr.split("/");
  let projectName = dirSplit[dirSplit.length - 2];
  Logger.debug(`Using project name: ${projectName}`);
  if (!projectName) {
    projectName = "src";
  }
  const srcDir = joinPaths(tempRoot, projectName);
  await mkdir(srcDir, { recursive: true });
  // Copy resolved config to temp directory
  await writeFile(
    joinPaths(srcDir, "tspconfig.yaml"), 
    stringify(resolvedConfig)
  );

  let isLocalRootConfig = await doesFileExist(rootConfigPathStr) ? true : false;
  function filter(src: string): boolean {
    if (src.includes("node_modules")) {
      return false;
    }
    if (src.includes("tsp-output")) {
      return false;
    }
    return true;
  }
  if (isLocalRootConfig) {
    // root config path is guaranteed to be tspconfig.yaml
    const specPath = rootConfigPathStr.split("tspconfig.yaml")[0]!;
    Logger.info(
      "NOTE: A path to a local spec was provided, will generate based off of local files...",
    );
    Logger.debug(`Using local spec directory: ${specPath}`);
    await cp(specPath, srcDir, { recursive: true, filter: filter });
    const localSpecRepoRoot = await getRepoRoot(specPath);
    Logger.info(`Local spec repo root is ${localSpecRepoRoot}`);
    if (!localSpecRepoRoot) {
      throw new Error("Could not find local spec repo root, please make sure the path is correct");
    }
  } else {
    const rootConfigMetadata = rootConfigPath as { url: string; directory: string; commit: string; repo: string };
    const cloneDir = await makeSparseSpecDir(repoRoot);
    Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
    Logger.debug(`Cloning repo to ${cloneDir}`);
    await cloneRepo(tempRoot, cloneDir, `https://github.com/${rootConfigMetadata.repo}.git`);
    await sparseCheckout(cloneDir);
    await addSpecFiles(cloneDir, rootConfigMetadata.directory);
    await checkoutCommit(cloneDir, rootConfigMetadata.commit);
    await cp(joinPaths(cloneDir, rootConfigMetadata.directory), srcDir, { recursive: true });
    Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
    await removeDirectory(cloneDir);
  }

  // handle additional directories
  // can safely assume that additional directories are absolute paths
  for (const dir of tspLocation.additionalDirectories!) {
    Logger.info(`Syncing additional directory: ${dir}`);
    if (await doesFileExist(dir)) {
      // local additional directory
      await cp(
        dir,
        joinPaths(tempRoot, getAdditionalDirectoryName(dir)),
        { recursive: true, filter: filter },
      );
    } else {
      // remote additional directory
      const resolvedDir = resolveAdditionalDirectory(dir);
      const cloneDir = await makeSparseSpecDir(repoRoot);
      Logger.debug(`Created temporary sparse-checkout directory ${cloneDir}`);
      Logger.debug(`Cloning repo to ${cloneDir}`);
      await cloneRepo(tempRoot, cloneDir, `https://github.com/${resolvedDir.repo}.git`);
      await sparseCheckout(cloneDir);
      await addSpecFiles(cloneDir, resolvedDir.path);
      await checkoutCommit(cloneDir, resolvedDir.commit);
      await cp(joinPaths(cloneDir, resolvedDir.path), joinPaths(tempRoot, getAdditionalDirectoryName(dir)), { recursive: true });
      Logger.debug(`Removing sparse-checkout directory ${cloneDir}`);
      await removeDirectory(cloneDir);
    }
  }
  Logger.info(`Finished Syncing additional directories`);

  try {
    let emitterLockPath = getEmitterLockPath(getEmitterPackageJsonPath(repoRoot, tspLocation));

    // Copy the emitter lock file to the temp directory and rename it to package-lock.json so that npm can use it.
    await cp(emitterLockPath, joinPaths(tempRoot, "package-lock.json"), { recursive: true });
  } catch (err) {
    Logger.debug(`Ran into the following error when looking for emitter-package-lock.json: ${err}`);
    Logger.debug("Will attempt look for emitter-package.json...");
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
  const localSpecRepo = argv["local-spec-repo"];

  const tempRoot = joinPaths(outputDir, "TempTypeSpecFiles");
  const tspLocation = await readTspLocation(outputDir);
  const { config: resolvedConfig, rootMetadata: rootConfigPath } = localSpecRepo
                                                                  ? await loadTspConfig<string>(localSpecRepo, createLocalConfigLoader())
                                                                  : await loadTspConfig<{ url: string; directory: string; commit: string; repo: string }>(`https://raw.githubusercontent.com/${tspLocation.repo}/${tspLocation.commit}/${tspLocation.directory}/tspconfig.yaml`, createRemoteConfigLoader());
  const rootConfigPathStr = typeof rootConfigPath === "string" ? rootConfigPath : rootConfigPath.url;
  // assume directory of root tspconfig.yaml is ${projectName}.xxx
  const dirSplit = rootConfigPathStr.split("/");
  let projectName = dirSplit[dirSplit.length - 2];
  Logger.debug(`Using project name: ${projectName}`);
  if (!projectName) {
    throw new Error("cannot find project name");
  }
  const srcDir = joinPaths(tempRoot, projectName);

  // Before compilation, ensure the temp tspconfig.yaml is fully resolved
  const tspConfigPath = joinPaths(srcDir, "tspconfig.yaml");
  if (await doesFileExist(tspConfigPath)) {
    const { config: resolvedConfig } = await loadTspConfig<string>(tspConfigPath, createLocalConfigLoader());
    await writeFile(tspConfigPath, stringify(resolvedConfig));
  }
  
  const emitter = await getEmitterFromRepoConfig(
    getEmitterPackageJsonPath(await getRepoRoot(outputDir), tspLocation),
  );
  if (!emitter) {
    throw new Error("emitter is undefined");
  }
  const mainFilePath = await discoverEntrypointFile(srcDir, tspLocation.entrypointFile);
  const resolvedMainFilePath = joinPaths(srcDir, mainFilePath);
  if (skipInstall) {
    Logger.info("Skipping installation of dependencies");
  } else {
    Logger.info("Installing dependencies from npm...");
    const args: string[] = [];
    try {
      // Check if package-lock.json exists, if it does, we'll install dependencies through `npm ci`
      await stat(joinPaths(tempRoot, "package-lock.json"));
      args.push("ci");
    } catch (err) {
      // If package-lock.json doesn't exist, we'll attempt to install dependencies through `npm install`
      args.push("install");
    }
    // NOTE: This environment variable should be used for developer testing only. A force
    // install may ignore any conflicting dependencies and result in unexpected behavior.
    dotenvConfig({ path: resolve(await getRepoRoot(outputDir), ".env") });
    if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
      args.push("--force");
    }
    await npmCommand(srcDir, args);

    // Log all package versions for diagnostics, ignoring errors
    try {
      await npmCommand(srcDir, ["ls", "-a", "|", "grep", "-E", "'typespec|azure-tools'"]);
    } catch (err) {}

  }
  const [success, exampleCmd] = await compileTsp({
    emitterPackage: emitter,
    outputPath: outputDir,
    resolvedMainFilePath,
    saveInputs: saveInputs,
    additionalEmitterOptions: emitterOptions,
    trace: argv["trace"],
  });

  if (argv["debug"]) {
    Logger.warn(`Example of how to compile using the tsp commandline. NOTE: tsp-client does NOT directly run this command, results may vary:
        ${exampleCmd}
        `);
  }

  if (saveInputs) {
    Logger.debug(`Skipping cleanup of temp directory: ${tempRoot}`);
  } else {
    Logger.debug("Cleaning up temp directory");
    await removeDirectory(tempRoot);
  }

  if (!success) {
    Logger.error("Failed to generate client");
    process.exit(1);
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
  const tspLocation: TspLocation = await readTspLocation(outputDir);
  if (commit) {
    tspLocation.commit = commit ?? tspLocation.commit;
    tspLocation.repo = repo ?? tspLocation.repo;
    await writeTspLocationYaml(tspLocation, outputDir);
  } else if (tspConfig) {
    // Determine if tspConfig is a URL or local path
    if (!await doesFileExist(tspConfig)) {
      tspConfig = resolveTspConfigUrl(tspConfig);
      tspLocation.commit = tspConfig.commit ?? tspLocation.commit;
      tspLocation.repo = tspConfig.repo ?? tspLocation.repo;
      tspLocation.directory = tspConfig.path ?? tspLocation.directory;
    } else {
      // Note: For local files, commit and repo are not updated since they come from the existing tsp-location.yaml
      tspLocation.directory = !tspConfig.endsWith("tspconfig.yaml") ? tspConfig : dirname(tspConfig);

    }
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

  // Resolve autorest dependency
  const autorestPath = await getPathToDependency("autorest");
  const autorestPackageJson = JSON.parse(
    await readFile(joinPaths(autorestPath, "package.json"), "utf8"),
  );
  const autorestBinPath = joinPaths(autorestPath, autorestPackageJson["bin"]["autorest"]);

  // Resolve core dependency
  const autorestCorePath = await getPathToDependency("@autorest/core");

  // Resolve extension dependencies
  const autorestOpenApiToTypeSpecPath = await getPathToDependency("@autorest/openapi-to-typespec");

  // Build the command to convert swagger to typespec
  const args = [
    autorestBinPath,
    `--version="${autorestCorePath}"`,
    "--openapi-to-typespec",
    "--csharp=false",
    `--output-folder="${outputDir}"`,
    `--use="${autorestOpenApiToTypeSpecPath}"`,
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
  const emitterPackageJson: Record<string, any> = {
    main: "dist/src/index.js",
    dependencies: {},
  };

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

  for (const pinnedPackage of possiblyPinnedPackages) {
    const pinnedVersion = packageJson["devDependencies"][pinnedPackage];
    if (pinnedVersion && !overrideJson[pinnedPackage]) {
      Logger.info(`Pinning ${pinnedPackage} to ${pinnedVersion}`);
      devDependencies[pinnedPackage] = pinnedVersion;
    }
  }

  if (Object.keys(devDependencies).length > 0) {
    emitterPackageJson["devDependencies"] = devDependencies;
  }
  if (Object.keys(overrideJson).length > 0) {
    emitterPackageJson["overrides"] = overrideJson;
  }

  const emitterPath =
    resolveEmitterPathFromArgs(argv) ??
    joinPaths(await getRepoRoot(outputDir), defaultRelativeEmitterPackageJsonPath);

  let existingEmitterPackageJson: Record<string, any> | undefined;
  try {
    existingEmitterPackageJson = JSON.parse(await readFile(emitterPath, "utf8"));
  } catch (err) {
    Logger.debug(
      `Couldn't read ${basename(emitterPath)}. If the file exists it will be over-written. Error: ${err}`,
    );
  }
  // If there's an existing emitter-package.json, we need to check for any manually added dependencies and devDependencies
  if (existingEmitterPackageJson) {
    // Register all manually added regular dependencies and their current values
    const manualDependencies = {};
    for (const [key, value] of Object.entries(
      existingEmitterPackageJson["dependencies"] ?? {},
    )) {
      if (!Object.keys(emitterPackageJson["dependencies"] ?? {}).includes(key)) {
        Object.assign(manualDependencies, { [key]: value });
      }
    }

    // Preserve manually added regular dependencies
    emitterPackageJson["dependencies"] = {
      ...manualDependencies,
      ...emitterPackageJson["dependencies"],
    };

    // Register all manually pinned dev dependencies and their current values
    const manualDevDependencies = {};
    for (const [key, value] of Object.entries(
      existingEmitterPackageJson["devDependencies"] ?? {},
    )) {
      if (!Object.keys(emitterPackageJson["devDependencies"] ?? {}).includes(key)) {
        Object.assign(manualDevDependencies, { [key]: value });
      }
    }

    if (
      Object.keys(manualDevDependencies).length > 0 &&
      emitterPackageJson["devDependencies"] === undefined
    ) {
      // Add a devDependencies entry in the new emitter-package.json content to create
      emitterPackageJson["devDependencies"] = {};
    }
    emitterPackageJson["devDependencies"] = {
      ...manualDevDependencies,
      ...emitterPackageJson["devDependencies"],
    };
  }
  await writeFile(emitterPath, JSON.stringify(emitterPackageJson, null, 2));
  Logger.info(`${basename(emitterPath)} file generated in '${dirname(emitterPath)}' directory`);

  await generateLockFileCommandCore(outputDir, emitterPath);
}

export async function generateLockFileCommand(argv: any) {
  await generateLockFileCommandCore(
    argv["output-dir"],
    resolveEmitterPathFromArgs(argv) ??
      joinPaths(await getRepoRoot(argv["output-dir"]), defaultRelativeEmitterPackageJsonPath),
  );
}

export async function generateLockFileCommandCore(
  outputDir: string,
  emitterPackageJsonPath: string,
) {
  Logger.info("Generating lock file...");
  const args: string[] = ["install"];
  if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
    args.push("--force");
  }
  const tempRoot = await createTempDirectory(outputDir);
  await cp(emitterPackageJsonPath, joinPaths(tempRoot, "package.json"));
  await npmCommand(tempRoot, args);
  const lockFile = await stat(joinPaths(tempRoot, "package-lock.json"));
  const emitterLockPath = getEmitterLockPath(emitterPackageJsonPath);
  if (lockFile.isFile()) {
    await cp(joinPaths(tempRoot, "package-lock.json"), emitterLockPath);
  }
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
  await cp(
    joinPaths(repoRoot, "eng", "emitter-package.json"),
    joinPaths(installPath, "package.json"),
  );
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
