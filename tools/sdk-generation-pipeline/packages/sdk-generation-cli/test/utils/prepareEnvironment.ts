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
        execSync(`git clone https://github.com/Azure/azure-sdk-for-go.git sdk-repo`, {
            cwd: 'tmp',
            stdio: 'inherit'
        });
    }
    execSync(`git checkout 0fd3f1b104984ab6e35dfbd2079fde4531bf6e60`, {
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