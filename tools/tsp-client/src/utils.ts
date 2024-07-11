import { joinPaths, normalizeSlashes } from "@typespec/compiler";
import { randomUUID } from "node:crypto";
import { access, constants, mkdir } from "node:fs/promises";
import { Logger } from "./log.js";
import { dirname, join } from "node:path";

export function formatAdditionalDirectories(additionalDirectories?: string[]): string {
    let additionalDirOutput = "";
    for (const dir of additionalDirectories ?? []) {
        additionalDirOutput += `\n- ${dir}`;
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
    const serviceDir = configYaml?.options?.[emitter]?.["service-dir"] ?? configYaml?.parameters?.["service-dir"]?.default;
    if (!serviceDir) {
      throw new Error(`Parameter service-dir is not defined correctly in tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`)
    }
    Logger.debug(`Service directory: ${serviceDir}`)
    return serviceDir;
}

export async function getPathToDependency(dependency: string): Promise<string | undefined> {
    const entrypoint = require.resolve(dependency);
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
            }
            else {
                // Reached fs root but no package.json found
                return;
            }
        }
    }
}
