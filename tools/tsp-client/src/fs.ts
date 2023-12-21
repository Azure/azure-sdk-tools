import { mkdir, rm, stat, readFile, access } from "node:fs/promises";
import { Logger } from "./log.js";
import { parse as parseYaml } from "yaml";
import { TspLocation } from "./typespec.js";
import { joinPaths, resolvePath } from "@typespec/compiler";

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
  const data = await readFile(emitterPath, 'utf8');
  const obj = JSON.parse(data);
  if (!obj || !obj.dependencies) {
    throw new Error("Invalid emitter-package.json");
  }
  const languages: string[] = ["@azure-tools/typespec-", "@typespec/openapi3"];
  for (const lang of languages) {
    const emitter = Object.keys(obj.dependencies).find((dep: string) => dep.startsWith(lang));
    if (emitter) {
      Logger.info(`Found emitter package ${emitter}`);
      return emitter;
    }
  }
  throw new Error("Could not find emitter package");
}
