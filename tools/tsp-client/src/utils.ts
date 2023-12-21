export function formatAdditionalDirectories(additionalDirectories?: string[]): string {
    let additionalDirOutput = "";
    for (const dir of additionalDirectories ?? []) {
        additionalDirOutput += `\n- ${dir}`;
    }
    return additionalDirOutput;
}
