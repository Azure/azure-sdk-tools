#!/usr/bin/env node

import { initializeLogger } from '@azure-tools/sdk-generation-lib';
import { spawn } from 'child_process';
import fs from 'fs';
import * as path from 'path';
import { Logger } from 'winston';

import { DockerCliInput, dockerCliInput } from './schema/dockerCliInput';
import { DockerMockHostInput, dockerMockHostInput } from './schema/mockHostCliInput';

export type DockerMockHostContext = {
    readmeMdPath?: string;
    specRepo?: string;
    mockHostPath: string;
    logger: Logger;
}

export function initializeDockerMockHostContext(inputParams: DockerMockHostInput & DockerCliInput) {
    const dockerMockHostConfigProperties = dockerMockHostInput.getProperties();
    const dockerMockHostContext: DockerMockHostContext = {
        readmeMdPath: inputParams.readmeMdPath,
        specRepo: inputParams.specRepo,
        mockHostPath: dockerMockHostConfigProperties.mockHostPath,
        logger: initializeLogger(path.join(inputParams.resultOutputFolder, dockerMockHostConfigProperties.mockHostLogger), 'mock-host', false)
    };
    return dockerMockHostContext;
}

export async function runMockHost() {
    const inputParams: DockerMockHostInput & DockerCliInput = {
        ...dockerCliInput.getProperties(),
        ...dockerMockHostInput.getProperties()
    };
    const context = initializeDockerMockHostContext(inputParams);
    if (!context.specRepo) {
        context.logger.log('cmdout', `Cannot start mock server because ${context.specRepo} doesn't exist.`);
        return;
    }

    function sleep(ms) {
        return new Promise((resolve) => {
            setTimeout(resolve, ms);
        });
    }

    while (!fs.existsSync(context.specRepo)) {
        await sleep(10000);
    }

    if (!context.readmeMdPath) {
        context.logger.log('cmdout', `Cannot get valid readme, so do not start mock server.`);
        return;
    }
    const swaggerJsonFilePattern = context.readmeMdPath.replace(/readme[.a-z-]*.md/gi, '**/*.json');
    const child = spawn(`node`, [`node_modules/@azure-tools/mock-service-host/dist/src/main.js`], {
        cwd: context.mockHostPath,
        env: {
            ...process.env,
            'specRetrievalMethod': 'filesystem',
            'specRetrievalLocalRelativePath': context.specRepo,
            'validationPathsPattern': swaggerJsonFilePattern
        }
    });
    child.stdout.on('data', (data) => context.logger.log('cmdout', data.toString()));
    child.stderr.on('data', (data) => context.logger.log('cmderr', data.toString()));
}

runMockHost();
