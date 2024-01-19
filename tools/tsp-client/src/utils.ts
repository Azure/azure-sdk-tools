import { normalizeSlashes } from "@typespec/compiler";

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
