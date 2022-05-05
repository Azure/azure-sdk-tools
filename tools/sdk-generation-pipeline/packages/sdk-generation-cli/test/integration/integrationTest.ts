import * as path from "path";
import { existsSync, mkdirSync } from "fs";
import { execSync } from "child_process";
import commandLineArgs from "command-line-args";

const repoCommitId = {
    'azure-rest-api-specs': '0baca05c851c1749e92beb0d2134cd958827dd54',
    'azure-sdk-for-js': '67946c5b0ce135f58ecfeab1443e5be52604908e'
}

const defaultImageName = 'sdkgeneration.azurecr.io/sdk-generation:v1.0';

async function prepareRepo(currentPath: string, repoName: string) {
    const tmpFolder = path.join(currentPath, 'tmp')
    if (!existsSync(tmpFolder)) {
        mkdirSync(tmpFolder);
    }

    if (!existsSync(path.join(tmpFolder, repoName))) {
        execSync(`git clone https://github.com/Azure/${repoName}.git`, {
            cwd: tmpFolder,
            stdio: 'inherit'
        });
    }

    execSync(`git checkout . && git clean -fd`, {
        cwd: path.join(tmpFolder, repoName),
        stdio: 'inherit'
    });

    execSync(`git checkout ${repoCommitId[repoName]}`, {
        cwd: path.join(tmpFolder, repoName),
        stdio: 'inherit'
    });
}

async function runDocker(currentPath: string, sdkRepoName: string, dockerImage: string) {
    const tmpFolder = path.join(currentPath, 'tmp');
    execSync(`docker run -v ${path.join(tmpFolder, 'azure-rest-api-specs')}:/spec-repo -v ${path.join(tmpFolder, sdkRepoName)}:/sdk-repo ${dockerImage} --readme=specification/agrifood/resource-manager/readme.md`, {
        stdio: 'inherit'
    })
}

async function buildDockImage(cwd: string) {
    execSync(`docker build -t ${defaultImageName} .`, {
        cwd: cwd,
        stdio: 'inherit'
    })
}

export async function main(options: any) {
    const currentPath = path.resolve(__dirname);
    if (!options['docker-image']) {
        await buildDockImage(path.join(currentPath, '..', '..', '..', '..'));
        options['docker-image'] = defaultImageName;
    }
    if (!options['sdk-repo']) {
        options['sdk-repo'] = Object.keys(repoCommitId).filter(ele => ele !== 'azure-rest-api-specs').join(',');
    }
    await prepareRepo(currentPath, 'azure-rest-api-specs');
    for (const sdkRepo of options['sdk-repo'].split(',')) {
        await prepareRepo(currentPath, sdkRepo);
        await runDocker(currentPath, sdkRepo, options['docker-image']);
    }
}

const optionDefinitions = [
    { name: 'docker-image',  type: String },
    { name: 'sdk-repo', type: String },
];
const options = commandLineArgs(optionDefinitions);

main(options).catch(err => console.log(err));