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

export async function readTspLocation(rootDir: string): Promise<TspLocation> {
  try {
    const yamlPath = resolvePath(rootDir, "tsp-location.yaml");
    const fileStat = await stat(yamlPath);
    if (fileStat.isFile()) {
      const fileContents = await readFile(yamlPath, "utf8");
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
  const languages: string[] = ["@azure-tools/typespec-", "@typespec/openapi3"];
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
