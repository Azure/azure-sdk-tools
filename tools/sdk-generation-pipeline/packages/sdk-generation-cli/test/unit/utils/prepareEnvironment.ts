import { execSync } from 'child_process';
import { existsSync, mkdirSync } from 'fs';
import * as path from 'path';

function mkdirTmpFolderIfNotExist(tmpFolder: string) {
    if (!existsSync(tmpFolder)) {
        mkdirSync(tmpFolder);
    }
}

function cloneSpecRepoIfNotExist(tmpFolder: string) {
    if (!existsSync(path.join(tmpFolder, 'spec-repo'))) {
        execSync(`git clone https://github.com/Azure/azure-rest-api-specs.git spec-repo`, {
            cwd: tmpFolder,
            stdio: 'inherit'
        });
    }
    execSync(`git checkout 0baca05c851c1749e92beb0d2134cd958827dd54`, {
        cwd: path.join(tmpFolder, 'spec-repo'),
        stdio: 'inherit'
    });
}

function cloneSdkRepoIfNotExist(tmpFolder: string) {
    if (!existsSync(path.join(tmpFolder, 'sdk-repo'))) {
        execSync(`git clone https://github.com/Azure/azure-sdk-for-js.git sdk-repo`, {
            cwd: tmpFolder,
            stdio: 'inherit'
        });
    }
    execSync(`git checkout . && git clean -fd`, {
        cwd: path.join(tmpFolder, 'sdk-repo'),
        stdio: 'inherit'
    });
    execSync(`git checkout 67946c5b0ce135f58ecfeab1443e5be52604908e`, {
        cwd: path.join(tmpFolder, 'sdk-repo'),
        stdio: 'inherit'
    });
}

function mkdirResultOutputFolderIfNotExist(tmpFolder: string) {
    if (!existsSync(path.join(tmpFolder, 'output'))) {
        mkdirSync(path.join(tmpFolder, 'output'));
    }
}

async function main() {
    const tmpFolder = path.join(path.resolve(__dirname), '..', 'tmp');
    mkdirTmpFolderIfNotExist(tmpFolder);
    cloneSpecRepoIfNotExist(tmpFolder);
    cloneSdkRepoIfNotExist(tmpFolder);
    mkdirResultOutputFolderIfNotExist(tmpFolder);
}

main().catch((e) => {
    console.log(e);
});
