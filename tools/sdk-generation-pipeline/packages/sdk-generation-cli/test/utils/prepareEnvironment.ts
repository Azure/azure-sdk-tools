import { existsSync, mkdirSync } from "fs";
import * as path from "path";
import { execSync } from "child_process";

function mkdirTmpFolderIfNotExist() {
    if (!existsSync('tmp')) {
        mkdirSync('tmp');
    }
}

function cloneSpecRepoIfNotExist() {
    if (!existsSync(path.join('tmp', 'spec-repo'))) {
        execSync(`git clone https://github.com/Azure/azure-rest-api-specs.git spec-repo`, {
            cwd: 'tmp',
            stdio: 'inherit'
        });
    }
    execSync(`git checkout 0baca05c851c1749e92beb0d2134cd958827dd54`, {
        cwd: path.join('tmp', 'spec-repo'),
        stdio: 'inherit'
    });
}

function cloneSdkRepoIfNotExist() {
    if (!existsSync(path.join('tmp', 'sdk-repo'))) {
        execSync(`git clone https://github.com/Azure/azure-sdk-for-js.git sdk-repo`, {
            cwd: 'tmp',
            stdio: 'inherit'
        });
    }
    execSync(`git checkout . && git clean -fd`, {
        cwd: path.join('tmp', 'sdk-repo'),
        stdio: 'inherit'
    });
    execSync(`git checkout 67946c5b0ce135f58ecfeab1443e5be52604908e`, {
        cwd: path.join('tmp', 'sdk-repo'),
        stdio: 'inherit'
    });
}

function mkdirResultOutputFolderIfNotExist() {
    if (!existsSync(path.join('tmp', 'output'))) {
        mkdirSync(path.join('tmp', 'output'));
    }
}

async function main() {
    mkdirTmpFolderIfNotExist();
    cloneSpecRepoIfNotExist();
    cloneSdkRepoIfNotExist();
    mkdirResultOutputFolderIfNotExist();
}

main().catch(e => {
    console.log(e);
})