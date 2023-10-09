import { mkdir, rm, writeFile, stat, readFile, access } from "node:fs/promises";
import { FileTreeResult } from "./fileTree.js";
import * as path from "node:path";
import { Logger } from "./log.js";
import { parse as parseYaml } from "yaml";

export async function ensureDirectory(path: string) {
  await mkdir(path, { recursive: true });
}

export async function removeDirectory(path: string) {
  await rm(path, { recursive: true, force: true });
}

export async function createTempDirectory(outputDir: string): Promise<string> {
  const tempRoot = path.join(outputDir, "TempTypeSpecFiles");
  await mkdir(tempRoot, { recursive: true });
  Logger.debug(`Created temporary working directory ${tempRoot}`);
  return tempRoot;
}

export async function writeFileTree(rootDir: string, files: FileTreeResult["files"]) {
  for (const [relativeFilePath, contents] of files) {
    const filePath = path.join(rootDir, relativeFilePath);
    await ensureDirectory(path.dirname(filePath));
    Logger.debug(`writing ${filePath}`);
    await writeFile(filePath, contents);
  }
}

export async function tryReadTspLocation(rootDir: string): Promise<string | undefined> {
  try {
    const yamlPath = path.resolve(rootDir, "tsp-location.yaml");
    const fileStat = await stat(yamlPath);
    if (fileStat.isFile()) {
      const fileContents = await readFile(yamlPath, "utf8");
      const locationYaml = parseYaml(fileContents);
      const { directory, commit, repo } = locationYaml;
      if (!directory || !commit || !repo) {
        throw new Error("Invalid tsp-location.yaml");
      }
      // make GitHub URL
      return `https://raw.githubusercontent.com/${repo}/${commit}/${directory}/`;
    }
  } catch (e) {
    Logger.error(`Error reading tsp-location.yaml: ${e}`);
  }
  return undefined;
}

export async function readTspLocation(rootDir: string): Promise<[string, string, string, string[]]> {
  try {
    const yamlPath = path.resolve(rootDir, "tsp-location.yaml");
    const fileStat = await stat(yamlPath);
    if (fileStat.isFile()) {
      const fileContents = await readFile(yamlPath, "utf8");
      const locationYaml = parseYaml(fileContents);
      let { directory, commit, repo, additionalDirectories } = locationYaml;
      if (!directory || !commit || !repo) {
        throw new Error("Invalid tsp-location.yaml");
      }
      Logger.info(`Additional directories: ${additionalDirectories}`)
      if (!additionalDirectories) {
        additionalDirectories = [];
      }
      return [ directory, commit, repo, additionalDirectories ];
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
