import path from "node:path";
import * as fs from "node:fs/promises";
import crypto from "node:crypto";

async function readAllFiles(fileOrFolderPath: string): Promise<string[]> {
    const stats = await fs.stat(fileOrFolderPath);
    if (stats.isFile()) {
        return [fileOrFolderPath];
    }
    if (!stats.isDirectory()) {
        throw new Error(`Invalid path: ${fileOrFolderPath}`);
    }
    const entries = await fs.readdir(fileOrFolderPath, { withFileTypes: true });
    const files = await Promise.all(
        entries.map(async (entry) =>
            readAllFiles(path.join(fileOrFolderPath, entry.name)),
        ),
    );
    return files.flat();
}

export async function getWorkspaceFiles(
    fileOrFolderPath: string,
): Promise<WorkspaceFile[]> {
    return Promise.all(
        (await readAllFiles(fileOrFolderPath)).map(workspace.readText),
    );
}

export function getUniqueDirName() {
    const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
    const random = crypto.randomBytes(6).toString("hex");
    return `${timestamp}-${random}`;
}
