#!/usr/bin/env node

import { dockerCliConfig, DockerCliConfig } from "./schema/dockerCliConfig";
import { DockerContext } from "./dockerCli";
import { cloneRepoIfNotExist, sdkToRepoMap } from "./core/generateCodesInLocal";
import { initializeDockerTaskEngineContext, runTaskEngine } from "./core/DockerTaskEngine";

async function main() {
    const inputParams: DockerCliConfig = dockerCliConfig.getProperties();
    const dockerContext: DockerContext = new DockerContext();
    dockerContext.initialize(inputParams);
    const sdkRepos: string[] = dockerContext.sdkToGenerate.map(ele => sdkToRepoMap[ele]);
    await cloneRepoIfNotExist(dockerContext, sdkRepos);
    for (const sdk of dockerContext.sdkToGenerate) {
        const dockerTaskEngineContext = initializeDockerTaskEngineContext(dockerContext);
        await runTaskEngine(dockerTaskEngineContext);
    }
    dockerContext.logger.info(`Finish re-run task engine for sdk ${dockerContext.sdkToGenerate.join(', ')}.`);
}

main().catch(e => {
    console.error("\x1b[31m", e.toString());
    console.error("\x1b[31m", e.message);
    console.error("\x1b[31m", e.stack);
    process.exit(1);
})