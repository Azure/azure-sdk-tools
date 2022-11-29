import simpleGit, {SimpleGit} from 'simple-git';
import {execSync} from "child_process";
import {logger} from "./logger";

const git: SimpleGit = simpleGit();
const path = require('path');

export async function getChangedPackageDirectory(throwErrorWhenFindingUnexpectedFile: boolean = true) {
    const changedPackageDirectories: Set<string> = new Set<string>();
    const gitStatus = await git.status();
    const files = gitStatus.files;
    for (const file of files) {
        const filePath = file.path;
        if (filePath.match(/sdk\/[^\/]*\/(arm-.*)|(.*-rest)/)) {
            const packageDirectory = /sdk\/[^\/]*\/(arm-[^\/]*)|(.*-rest)/.exec(filePath);
            if (packageDirectory) {
                changedPackageDirectories.add(packageDirectory[0]);
            }
        } else if (throwErrorWhenFindingUnexpectedFile && filePath.endsWith('.ts')) {
            throw new Error(`Find unexpected generated file: ${filePath}. Please confirm whether the output-folder is correct.`);
        }
    }
    return changedPackageDirectories;
}

export async function getChangedCiYmlFilesInSpecificFolder(folder: string) {
    const changedCiYmlFiles: Set<string> = new Set<string>();
    const gitStatus = await git.status([folder]);
    const files = gitStatus.files;
    for (const file of files) {
        const filePath = file.path;
        if (filePath.endsWith('ci.yml') || filePath.endsWith('ci.mgmt.yml')) {
            changedCiYmlFiles.add(filePath);
        }
    }
    return changedCiYmlFiles;
}


export async function getLastCommitId(repository: string) {
    let commitId = '';
    try {
        commitId = execSync(`git --git-dir=${path.join(repository, '.git')} log --format=%H -n 1`, {encoding: "utf8"});
    } catch (e) {
        logger.log(`cannot get commit id from ${repository}`);
    }
    return commitId.trim();
}
