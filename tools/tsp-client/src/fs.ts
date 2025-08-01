import { mkdir, rm, stat, readFile, access } from "node:fs/promises";
import { Logger } from "./log.js";
import { parse as parseYaml } from "yaml";
import { joinPaths, normalizePath, resolvePath } from "@typespec/compiler";
import { TspLocation } from "./typespec.js";

export async function ensureDirectory(path: string) {
  await mkdir(path, { recursive: true });
}

export async function removeDirectory(path: string) {
  await rm(path, { recursive: true, force: true });
}

export async function createTempDirectory(outputDir: string): Promise<string> {
  const tempRoot = joinPaths(outputDir, "TempTypeSpecFiles");
  await mkdir(tempRoot, { recursive: true });
  Logger.debug(`Created temporary working directory ${tempRoot}`);
  return tempRoot;
}

/**
 * Reads the tsp-location.yaml file from the expected directory.
 *
 * By default, it searches in the current working directory. If not found,
 * it searches in the provided fallback directory which is likely the output directory.
 *
 * @param fallbackDir Path to the fallback directory to search for tsp-location.yaml.
 * @returns The parsed TspLocation object.
 */
export async function readTspLocation(fallbackDir: string): Promise<TspLocation> {
  try {
    const tspLocationYaml = await findTspLocation(fallbackDir);
    if (tspLocationYaml) {
      const fileContents = await readFile(tspLocationYaml, "utf8");
      const tspLocation: TspLocation = parseYaml(fileContents);
      if (!tspLocation.directory || !tspLocation.commit || !tspLocation.repo) {
        throw new Error("Invalid tsp-location.yaml");
      }
      if (!tspLocation.additionalDirectories) {
        tspLocation.additionalDirectories = [];
      }

      // Normalize the directory path and remove trailing slash
      tspLocation.directory = normalizeDirectory(tspLocation.directory);
      if (typeof tspLocation.additionalDirectories === "string") {
        tspLocation.additionalDirectories = [normalizeDirectory(tspLocation.additionalDirectories)];
      } else {
        // List of additional directories
        tspLocation.additionalDirectories =
          tspLocation.additionalDirectories.map(normalizeDirectory);
      }

      return tspLocation;
    }
    throw new Error("Could not find tsp-location.yaml");
  } catch (e) {
    Logger.error(`Error reading tsp-location.yaml: ${e}`);
    throw e;
  }
}

export async function getEmitterFromRepoConfig(emitterPath: string): Promise<string> {
  await access(emitterPath);
  const data = await readFile(emitterPath, "utf8");
  const obj = JSON.parse(data);
  if (!obj || !obj.dependencies) {
    throw new Error("Invalid emitter-package.json");
  }
  const languages: string[] = [
    "@azure-tools/typespec-",
    "@typespec/http-",
    "@typespec/openapi3",
    "@azure-typespec/",
  ];
  for (const lang of languages) {
    const emitter = Object.keys(obj.dependencies).find((dep: string) => dep.startsWith(lang));
    if (emitter) {
      Logger.info(`Found emitter package ${emitter}@${obj.dependencies[emitter]}`);
      return emitter;
    }
  }
  throw new Error("Could not find emitter package");
}

export function normalizeDirectory(directory: string): string {
  const normalizedDir = normalizePath(directory);
  return normalizedDir.endsWith("/") ? normalizedDir.slice(0, -1) : normalizedDir;
}

/**
 * Searches for tsp-location.yaml in the current working directory and the fallback directory.
 * If found, returns the path to the file; otherwise, returns undefined.
 *
 * @param fallbackDir Path to the fallback directory to search for tsp-location.yaml.
 */
async function findTspLocation(fallbackDir: string): Promise<string | undefined> {
  let yamlPath = resolvePath(process.cwd(), "tsp-location.yaml");
  let fstat = await stat(yamlPath);
  if (fstat.isFile()) {
    Logger.debug(`Found tsp-location.yaml at ${yamlPath}`);
    return yamlPath;
  }

  // If not found, check the fallback directory
  yamlPath = resolvePath(fallbackDir, "tsp-location.yaml");
  fstat = await stat(yamlPath);
  if (fstat.isFile()) {
    Logger.debug(`Found tsp-location.yaml at ${yamlPath}`);
    return yamlPath;
  }

  return undefined;
}
