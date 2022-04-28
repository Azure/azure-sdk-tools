import { existsSync } from "fs";
import * as path from "path";
import { spawn } from "child_process";
import { initializeDockerTaskEngineContext, runTaskEngine } from "./dockerTaskEngine";
import { doNotExitDockerContainer } from "./doNotExitDockerContainer";
import { DockerContext } from "./DockerContext";

export const sdkToRepoMap = {
    js: 'azure-sdk-for-js',
    python: 'azure-sdk-for-java',
    go: 'azure-sdk-for-go',
    java: 'azure-sdk-for-java',
    '.net': 'azure-sdk-for-net'
}

export async function cloneRepoIfNotExist(context: DockerContext, sdkRepos: string[]) {
    for (const sdkRepo of sdkRepos) {
        if (!existsSync(path.join(context.workDir, sdkRepo))) {
            const child = spawn(`git`, [`clone`, `https://github.com/Azure/${sdkRepo}.git`], {
                cwd: context.workDir,
                stdio: ['ignore', 'pipe', 'pipe'],
            });
            child.stdout.on('data', data => context.logger.log('cmdout', data.toString()));
            child.stderr.on('data', data => context.logger.log('cmderr', data.toString()));
            await new Promise((resolve) => {
                child.on('exit', (code, signal) => {
                    resolve({ code, signal });
                });
            });
        }
        context.sdkRepo = path.join(context.workDir, sdkRepo);
    }
}

export async function generateCodesInLocal(dockerContext: DockerContext) {
    const sdkRepos: string[] = dockerContext.sdkToGenerate.map(ele => sdkToRepoMap[ele]);
    await cloneRepoIfNotExist(dockerContext, sdkRepos);
    for (const sdk of dockerContext.sdkToGenerate) {
        dockerContext.sdkRepo = path.join(dockerContext.workDir, sdkToRepoMap[sdk]);
        const dockerTaskEngineContext = initializeDockerTaskEngineContext(dockerContext);
        await runTaskEngine(dockerTaskEngineContext);
    }
    dockerContext.logger.info(`Finish generating sdk for ${dockerContext.sdkToGenerate.join(', ')}. Please use vscode to connect this container.`);
    doNotExitDockerContainer();
}