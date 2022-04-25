import {DockerContext} from "../dockerCli";
import { existsSync } from "fs";
import * as path from "path";
import { execSync } from "child_process";

export const sdkToRepoMap = {
    js: 'azure-sdk-for-js',
    python: 'azure-sdk-for-java',
    go: 'azure-sdk-for-go',
    java: 'azure-sdk-for-java',
    '.net': 'azure-sdk-for-net'
}

function cloneRepoIfNotExist(context: DockerContext, sdkRepos: string[]) {
    for (const sdkRepo of sdkRepos) {
        if (!existsSync(path.join(context.workDir, sdkRepo))) {
            execSync(`git clone https://github.com/Azure/${sdkRepos}.git`);
        }
    }
}

export async function generateCodesInLocal(context: DockerContext) {
    throw new Error(`Not Implement`);
    const sdkRepos: string[] = context.sdkToGenerate.map(ele => sdkToRepoMap[ele]);
    cloneRepoIfNotExist(context, sdkRepos);
    for (const sdk of context.sdkToGenerate) {

    }
}