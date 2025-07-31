import { joinPaths, normalizeSlashes } from "@typespec/compiler";
import { randomUUID } from "node:crypto";
import { access, constants, mkdir, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Logger } from "./log.js";
import { TspLocation } from "./typespec.js";
import { normalizeDirectory, readTspLocation } from "./fs.js";

export function formatAdditionalDirectories(additionalDirectories?: string[]): string {
  let additionalDirOutput = "\n";
  for (const dir of additionalDirectories ?? []) {
    additionalDirOutput += `- ${normalizeDirectory(dir)}\n`;
  }
  return additionalDirOutput;
}

export function getAdditionalDirectoryName(dir: string): string {
  let normalizedDir = normalizeSlashes(dir);
  if (normalizedDir.slice(-1) === "/") {
    normalizedDir = normalizedDir.slice(0, -1);
  }
  const finalDirName = normalizedDir.split("/").pop();
  if (!finalDirName) {
    throw new Error(`Could not find a final directory for the following value: ${normalizedDir}`);
  }
  return finalDirName;
}

export async function makeSparseSpecDir(repoRoot: string): Promise<string> {
  const spareSpecPath = joinPaths(repoRoot, "..", `sparse-spec${randomUUID()}`);
  await mkdir(spareSpecPath, { recursive: true });
  return spareSpecPath;
}

export function getServiceDir(configYaml: any, emitter: string): string {
  // Check if service-dir is defined in the emitter specific configurations in tspconfig.yaml.
  // Default to the top level service-dir parameter in tspconfig.yaml.
  const serviceDir =
    configYaml?.options?.[emitter]?.["service-dir"] ??
    configYaml?.parameters?.["service-dir"]?.default;
  if (!serviceDir) {
    throw new Error(
      `Parameter service-dir is not defined correctly in tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`,
    );
  }
  Logger.debug(`Service directory: ${serviceDir}`);
  return serviceDir;
}

/**
 * Returns path to a dependency package under node_modules
 *
 * @param dependency Name of dependency.
 *
 * @example
 * ```
 * // Prints '/home/user/foo/node_modules/@autorest/bar':
 * console.log(getPathToDependency("@autorest/bar"));
 * ```
 */
export async function getPathToDependency(dependency: string): Promise<string> {
  // Example: /home/user/foo/node_modules/@autorest/bar/dist/index.js
  const entrypoint = fileURLToPath(import.meta.resolve(dependency));

  // Walk up directory tree to first folder containing "package.json"
  let currentDir = dirname(entrypoint);
  while (true) {
    const packageJsonFile = join(currentDir, "package.json");
    try {
      // Throws if file cannot be read
      await access(packageJsonFile, constants.R_OK);
      return currentDir;
    } catch {
      const parentDir = dirname(currentDir);
      if (parentDir !== currentDir) {
        currentDir = parentDir;
      } else {
        // Reached fs root but no package.json found
        throw new Error(`Unable to find package.json in folder tree above '${entrypoint}'`);
      }
    }
  }
}

/**
 * Writes tsp-location.yaml file at the given projectPath. Ensures additional directories are formatted correctly.
 *
 * @param tspLocation TspLocation object containing tsp location information.
 * @param projectPath Path to the project.
 */
export async function writeTspLocationYaml(
  tspLocation: TspLocation,
  projectPath: string,
): Promise<void> {
  let tspLocationContent = `directory: ${tspLocation.directory}\ncommit: ${tspLocation.commit}\nrepo: ${tspLocation.repo}\nadditionalDirectories: ${formatAdditionalDirectories(tspLocation.additionalDirectories)}`;
  if (tspLocation.entrypointFile) {
    tspLocationContent += `\nentrypointFile: ${tspLocation.entrypointFile}`;
  }
  if (tspLocation.emitterPackageJsonPath) {
    tspLocationContent += `\nemitterPackageJsonPath: ${tspLocation.emitterPackageJsonPath}`;
  }
  await writeFile(joinPaths(projectPath, "tsp-location.yaml"), tspLocationContent);
}

export async function updateExistingTspLocation(
  tspLocationData: TspLocation,
  projectPath: string,
): Promise<TspLocation> {
  try {
    const existingTspLocation = await readTspLocation(projectPath);

    // Used to update tsp-location.yaml data by iterating over properties
    const updatedTspLocation = { ...existingTspLocation };

    // Define the properties that can be updated
    const updatableProperties: (keyof TspLocation)[] = [
      "repo",
      "commit",
      "directory",
      "entrypointFile",
      "additionalDirectories",
      "emitterPackageJsonPath",
    ];

    // Update each property if it has a valid value
    for (const property of updatableProperties) {
      const value = tspLocationData[property];
      if (value !== undefined && value !== "<replace with your value>") {
        (updatedTspLocation as any)[property] = value;
      }
    }

    return updatedTspLocation;
  } catch (error) {
    Logger.debug(`Will create a new tsp-location.yaml. Error reading tsp-location.yaml: ${error}`);
    return tspLocationData;
  }
}
