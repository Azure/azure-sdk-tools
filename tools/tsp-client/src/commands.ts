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
  resolveTspConfigUrl,
  TspLocation,
} from "./typespec.js";
import {
  writeTspLocationYaml,
  getAdditionalDirectoryName,
  getServiceDir,
  makeSparseSpecDir,
  getPathToDependency,
} from "./utils.js";
import { parse as parseYaml } from "yaml";
import { config as dotenvConfig } from "dotenv";
import { dirname, resolve } from "node:path";
import { doesFileExist } from "./network.js";
import { sortOpenAPIDocument } from "@azure-tools/typespec-autorest";

export async function initCommand(argv: any) {
  let outputDir = argv["output-dir"];
  let tspConfig = argv["tsp-config"];
  const skipSyncAndGenerate = argv["skip-sync-and-generate"];
  const commit = argv["commit"] ?? "<replace with your value>";
  const repo = argv["repo"] ?? "<replace with your value>";

  const repoRoot = await getRepoRoot(outputDir);

  const emitterPackageJsonPath =
    argv["emitter-package-json-path"] ?? joinPaths(repoRoot, "eng", "emitter-package.json");

  const emitter = await getEmitterFromRepoConfig(emitterPackageJsonPath);
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
  if (isUrl) {
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
    if (argv["emitter-package-json-path"]) {
      tspLocationData.emitterPackageJsonPath = argv["emitter-package-json-path"];
    }
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
    if (argv["emitter-package-json-path"]) {
      tspLocationData.emitterPackageJsonPath = argv["emitter-package-json-path"];
    }
    await writeTspLocationYaml(tspLocationData, newPackageDir);
    outputDir = newPackageDir;
  }

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
  const emitterPackageJsonPath =
    tspLocation.emitterPackageJsonPath ?? joinPaths(repoRoot, "eng", "emitter-package.json");
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  Logger.debug(`Using project name: ${projectName}`);
  if (!projectName) {
    projectName = "src";
  }
  const srcDir = joinPaths(tempRoot, projectName);
  await mkdir(srcDir, { recursive: true });

  if (localSpecRepo) {
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
    let emitterLockPath = joinPaths(repoRoot, "eng", "emitter-package-lock.json");
    if (tspLocation.emitterPackageJsonPath) {
      // If the emitter package json path is provided, we need to check for the lock file in the same directory
      const emitterPackageJsonDir = dirname(tspLocation.emitterPackageJsonPath);
      const lockFileExists = await stat(
        joinPaths(emitterPackageJsonDir, "emitter-package-lock.json"),
      );
      if (lockFileExists.isFile()) {
        emitterLockPath = joinPaths(emitterPackageJsonDir, "emitter-package-lock.json");
      }
    }
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

  const tempRoot = joinPaths(outputDir, "TempTypeSpecFiles");
  const tspLocation = await readTspLocation(outputDir);
  const dirSplit = tspLocation.directory.split("/");
  let projectName = dirSplit[dirSplit.length - 1];
  if (!projectName) {
    throw new Error("cannot find project name");
  }
  const srcDir = joinPaths(tempRoot, projectName);
  const emitter = await getEmitterFromRepoConfig(
    tspLocation.emitterPackageJsonPath ??
      joinPaths(await getRepoRoot(outputDir), "eng", "emitter-package.json"),
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
  }
  const [success, exampleCmd] = await compileTsp({
    emitterPackage: emitter,
    outputPath: outputDir,
    resolvedMainFilePath,
    saveInputs: saveInputs,
    additionalEmitterOptions: emitterOptions,
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

export async function compareCommand(argv: any, args: string[]) {
  let outputDir = argv["output-dir"];
  const openApiDiffPath = await getPathToDependency("@azure-tools/rest-api-diff");
  const command = [openApiDiffPath, ...args];
  try {
    await nodeCommand(outputDir, command);
  } catch (err) {
    Logger.error(`Error occurred while attempting to compare: ${err}`);
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
  const repoRoot = await getRepoRoot(outputDir);
  const packageJsonPath = normalizePath(resolve(argv["package-json"]));
  const overridePath = argv["overrides"] ?? undefined;

  if (packageJsonPath === undefined || !(await doesFileExist(packageJsonPath))) {
    throw new Error(`package.json not found in: ${packageJsonPath ?? "[Not Specified]"}`);
  }
  Logger.info("Generating emitter-package.json file...");
  const content = await readFile(packageJsonPath);
  const packageJson: Record<string, any> = JSON.parse(content.toString());
  const emitterPackageJson: Record<string, any> = {
    name: "dist/src/index.js",
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
  await writeFile(
    joinPaths(repoRoot, "eng", "emitter-package.json"),
    JSON.stringify(emitterPackageJson, null, 2),
  );
  Logger.info(`emitter-package.json file generated in '${joinPaths(repoRoot, "eng")}' directory`);

  await generateLockFileCommand(argv);
}

export async function generateLockFileCommand(argv: any) {
  const outputDir = argv["output-dir"];
  const repoRoot = await getRepoRoot(outputDir);

  Logger.info("Generating lock file...");
  const args: string[] = ["install"];
  if (process.env["TSPCLIENT_FORCE_INSTALL"]?.toLowerCase() === "true") {
    args.push("--force");
  }
  const tempRoot = await createTempDirectory(outputDir);
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
