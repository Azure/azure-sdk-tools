import * as childProcess from 'child_process';
import { execSync } from 'child_process';
import * as os from 'os';

export function getFileListInPackageFolder(packageFolder: string) {
    childProcess.execSync('git add .', { encoding: 'utf8', cwd: packageFolder });
    const files = childProcess.execSync('git ls-files', { encoding: 'utf8', cwd: packageFolder }).trim().split('\n');

    return files;
}

export function getHeadSha(specRepo: string) {
    const headSha = execSync(`git rev-parse HEAD`, { encoding: 'utf8', cwd: specRepo });
    return headSha.trim();
}

export function getHeadRef(specRepo: string) {
    const headRef = execSync(`git rev-parse --abbrev-ref HEAD`, { encoding: 'utf8', cwd: specRepo });
    return headRef.trim();
}

export function safeDirectory(sdkRepo: string) {
    execSync(`git config --global --add safe.directory ${sdkRepo}`, { encoding: 'utf8', cwd: sdkRepo });
}

export function disableFileMode(sdkRepo: string) {
    execSync(`git config core.fileMode false --replace-all`, { encoding: 'utf8', cwd: sdkRepo });
}

export async function getChangedPackageDirectory(repo: string): Promise<Set<string>> {
    const changedPackageDirectories: Set<string> = new Set<string>();
    const gitLsFiles = execSync(`git ls-files -mdo --exclude-standard`, { encoding: 'utf8', cwd: repo });
    const files = gitLsFiles.split(os.EOL);
    for (const filePath of files) {
        if (filePath.match(/sdk\/[^\/0-9]*\/.*/)) {
            const packageDirectory = /sdk\/[^\/0-9]*\/[^\/]*/.exec(filePath);
            if (packageDirectory) {
                changedPackageDirectories.add(packageDirectory[0]);
            }
        }
    }
    return changedPackageDirectories;
}
