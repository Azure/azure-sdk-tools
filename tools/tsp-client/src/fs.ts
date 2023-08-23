import { mkdir, rm, writeFile, mkdtemp, stat, readFile } from "node:fs/promises";
import { tmpdir } from "node:os";
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

export async function createTempDirectory(): Promise<string> {
  const tempRoot = await mkdtemp(path.join(tmpdir(), "tsp-client-"));
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
