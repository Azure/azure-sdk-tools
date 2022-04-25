import { execSync, spawn } from "child_process";
import { DockerContext } from "../dockerCli";
import { dockerMockHostConfig } from "../schema/mockHostCliSchema";
import * as path from "path";
import { Logger } from "winston";
import { initializeLogger } from "@azure-tools/sdk-generation-lib";

export type DockerMockHostContext = {
    readmeMdPath?: string;
    specRepo?: string;
    mockHostPath: string;
    logger: Logger;
}

export function initializeDockerMockHostContext(dockerContext: DockerContext) {
    const dockerMockHostConfigProperties = dockerMockHostConfig.getProperties();
    const dockerMockHostContext: DockerMockHostContext = {
        readmeMdPath: dockerContext.readmeMdPath,
        specRepo: dockerContext.specRepo,
        mockHostPath: dockerMockHostConfigProperties.mockHostPath,
        logger: initializeLogger(path.join(dockerContext.resultOutputFolder, dockerMockHostConfigProperties.mockHostLogger), 'mock-host', false),
    }
    return dockerMockHostContext;
}

export async function runMockHost(dockerContext: DockerContext) {
    const context = initializeDockerMockHostContext(dockerContext);
    const swaggerJsonFilePattern = context.readmeMdPath
        ? context.readmeMdPath.replace(/readme[.a-z-]*.md/gi, '**/*.json')
        : undefined;
    const child = spawn(`node`,[`node_modules/@azure-tools/mock-service-host/dist/src/main.js`], {
        cwd: context.mockHostPath,
        env: {
            ...process.env,
            'specRetrievalMethod': 'filesystem',
            'specRetrievalLocalRelativePath': context.specRepo,
            'validationPathsPattern': swaggerJsonFilePattern
        },
    });
    child.stdout.on('data', data => context.logger.log('cmdout', data.toString()));
    child.stderr.on('data', data => context.logger.log('cmderr', data.toString()));
}