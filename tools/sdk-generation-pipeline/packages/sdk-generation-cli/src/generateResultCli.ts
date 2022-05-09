#!/usr/bin/env node
import * as fs from 'fs';
import {
    AzureSDKTaskName,
    createTaskResult,
    LogFilter,
    logger,
    PipelineResult,
    requireJsonc,
    TaskResult,
    TaskOutput,
} from '@azure-tools/sdk-generation-lib';

import { generateResultCliConfig, GenerateResultCliConfig } from './cliSchema/generateResultCliConfig';

generateResultCliConfig.validate();
const config: GenerateResultCliConfig = generateResultCliConfig.getProperties();

async function main() {
    let taskOutputObj: TaskOutput = undefined;
    let logFilter: LogFilter = undefined;
    let taskResult: TaskResult = undefined;

    if (!Object.values(AzureSDKTaskName).includes(config.taskName as AzureSDKTaskName)) {
        throw new Error(`invalid taskName` + config.taskName);
    }

    if (!config.exeResult && !config.dockerResultFile) {
        throw new Error(`Task execute result and dockerResultFile is empty`);
    }

    if (config.taskOutputPath && fs.existsSync(config.taskOutputPath)) {
        taskOutputObj = requireJsonc(config.taskOutputPath);
    }
    if (config.logFilterStr) {
        logFilter = JSON.parse(config.logFilterStr);
    }

    if (config.exeResult) {
        taskResult = createTaskResult(
            config.pipelineBuildId,
            config.taskName as AzureSDKTaskName,
            config.exeResult as PipelineResult,
            config.logfile,
            logFilter,
            taskOutputObj
        );
    } else if (config.dockerResultFile && fs.existsSync(config.dockerResultFile)) {
        const dockerTaskResult = JSON.parse(fs.readFileSync(config.dockerResultFile, 'utf-8'));
        if (dockerTaskResult[config.taskName].includes('skipped')) {
            console.log(config.taskName + `skipped`);
            return;
        } else {
            taskResult = createTaskResult(
                config.pipelineBuildId,
                config.taskName as AzureSDKTaskName,
                dockerTaskResult[config.taskName],
                config.logfile,
                logFilter,
                taskOutputObj
            );
        }
    } else {
        taskResult = createTaskResult(
            config.pipelineBuildId,
            config.taskName as AzureSDKTaskName,
            undefined,
            config.logfile,
            logFilter,
            taskOutputObj
        );
    }

    fs.writeFileSync(config.resultOutputPath, JSON.stringify(taskResult, null, 2), {
        encoding: 'utf-8',
    });
    console.log('Generate Success !!!');

    return;
}

main().catch((e) => {
    logger.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
