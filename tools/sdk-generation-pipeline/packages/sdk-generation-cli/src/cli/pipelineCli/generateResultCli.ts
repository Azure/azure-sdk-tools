#!/usr/bin/env node
import {
    AzureSDKTaskName,
    createTaskResult,
    LogFilter,
    logger,
    requireJsonc,
    TaskOutput,
    TaskResult,
    TaskResultStatus
} from '@azure-tools/sdk-generation-lib';
import * as fs from 'fs';

import { GenerateResultCliInput, generateResultCliInput } from '../../cliSchema/generateResultCliConfig';


generateResultCliInput.validate();
const config: GenerateResultCliInput = generateResultCliInput.getProperties();

async function main() {
    let taskOutputObj: TaskOutput = undefined;
    let logFilter: LogFilter = undefined;
    let taskResult: TaskResult = undefined;
    let exeResult: TaskResultStatus = undefined;

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
        exeResult = config.exeResult as TaskResultStatus;
    } else if (config.dockerResultFile && fs.existsSync(config.dockerResultFile)) {
        const dockerTaskResult = JSON.parse(fs.readFileSync(config.dockerResultFile, 'utf-8'));
        if (!dockerTaskResult[config.taskName] || dockerTaskResult[config.taskName].includes('skipped')) {
            return;
        } else {
            exeResult = dockerTaskResult[config.taskName];
        }
    } else {
        throw new Error(`exeResult is not provided.`);
    }

    taskResult = createTaskResult(
        config.pipelineBuildId,
        config.taskName as AzureSDKTaskName,
        exeResult,
        config.logfile,
        logFilter,
        taskOutputObj
    );

    fs.writeFileSync(config.resultOutputPath, JSON.stringify(taskResult, null, 2), {
        encoding: 'utf-8'
    });
    console.log('Generate Success !!!');

    return;
}

main().catch((e) => {
    logger.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
