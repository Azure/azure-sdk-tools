import * as child_process from "child_process";
import { execSync } from "child_process";
import simpleGit, { SimpleGit } from 'simple-git';

export function getFileListInPackageFolder(packageFolder: string) {
    child_process.execSync('git add .', {encoding: "utf8", cwd: packageFolder});
    const files = child_process.execSync('git ls-files', {encoding: "utf8", cwd: packageFolder}).trim().split('\n');

    return files;
}

export function getHeadSha(specRepo: string) {
    const headSha = execSync(`git rev-parse HEAD`, {encoding: "utf8", cwd: specRepo});
    return headSha.trim();
}

export function getHeadRef(specRepo: string) {
    const headRef = execSync(`git rev-parse --abbrev-ref HEAD`, {encoding: "utf8", cwd: specRepo});
    return headRef.trim();
}

export function safeDirectory(sdkRepo: string) {
    execSync(`git config --global --add safe.directory ${sdkRepo}`, {encoding: "utf8", cwd: sdkRepo})
}

export function disableFileMode(sdkRepo: string) {
    execSync(`git config core.fileMode false --replace-all`, {encoding: "utf8", cwd: sdkRepo});
}

export async function getChangedPackageDirectory(repo: string): Promise<Set<string>> {
    const git: SimpleGit = simpleGit({baseDir: repo});
    const changedPackageDirectories: Set<string> = new Set<string>();
    const gitStatus = await git.status();
    const files = gitStatus.files;
    for (const file of files) {
        const filePath = file.path;
        if (filePath.match(/sdk\/[^\/]*\/.*/)) {
            const packageDirectory = /sdk\/[^\/]*\/[^\/]*/.exec(filePath);
            if (packageDirectory) {
                changedPackageDirectories.add(packageDirectory[0]);
            }
        }
    }
    return changedPackageDirectories;
}