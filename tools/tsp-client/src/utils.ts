import { joinPaths, normalizeSlashes } from "@typespec/compiler";
import { randomUUID } from "node:crypto";
import { mkdir } from "node:fs/promises";

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

export async function makeSparseSpecPath(repoRoot:string): Promise<string> {
    const spareSpecPath = joinPaths(repoRoot, "..", `sparse-spec${randomUUID()}`);
    await mkdir(spareSpecPath, { recursive: true });
    return spareSpecPath;
}
