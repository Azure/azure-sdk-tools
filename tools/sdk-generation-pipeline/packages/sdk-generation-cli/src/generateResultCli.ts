#!/usr/bin/env node
import * as fs from 'fs';
import { createTaskResult, TaskResult, CodeGenerationPipelineTaskName, TaskOutput, LogFilter, logger } from '@azure-tools/sdk-generation-lib';

function printHelp() {
    console.log('usage: generateResult --pipelineBuildId --logfile --taskname --resultOutputPath');
    console.log('                      [--taskOutput] [--logFilter]\n');
    console.log('taskname: must be one of [Init, GenerateAndBuild, MockTest, LiveTest]');
}

async function main() {
    const args = parseArgs(process.argv);
    const pipelineBuildId = args['pipelineBuildId'];
    const logfile = args['logfile'];
    const logFilterStr = args['logFilter'];
    const taskname = args['taskname'];
    const taskOutput = args['taskOutput'];
    const resultOutputPath = args['resultOutputPath'];
    let taskOutputObj: TaskOutput = undefined;
    let logFilter: LogFilter = undefined;

    if (pipelineBuildId === undefined) {
        printHelp();
        throw new Error(`pipelineBuildId is empty`);
    }
    if (logfile === undefined) {
        printHelp();
        throw new Error(`logfile is empty`);
    }
    if (taskname === undefined) {
        printHelp();
        throw new Error(`taskname is empty`);
    } else if (Object.values(CodeGenerationPipelineTaskName).includes(taskname)) {
        printHelp();
        throw new Error(`invalid taskname`);
    }
    if (resultOutputPath === undefined) {
        printHelp();
        throw new Error(`resultOutputPath is empty`);
    }
    if (taskOutput !== undefined) {
        taskOutputObj = JSON.parse(taskOutput);
    }
    if (logFilterStr !== undefined) {
        logFilter = JSON.parse(logFilterStr);
    }

    const taskResult: TaskResult = createTaskResult(pipelineBuildId, taskname, logfile, logFilter, taskOutputObj);

    fs.writeFileSync(resultOutputPath, JSON.stringify(taskResult, null, 2), { encoding: 'utf-8' });
    console.log('Generate Success !!!');

    return;
}

/**
 * Parse a list of command line arguments.
 * @param argv List of cli args(process.argv)
 */
const flagRegex = /^--([^=:]+)([=:](.+))?$/;
export function parseArgs(argv: string[]) {
    const result: any = {};
    for (const arg of argv) {
        const match = flagRegex.exec(arg);
        if (match) {
            const key = match[1];
            const rawValue = match[3];
            result[key] = rawValue;
        }
    }
    return result;
}

main().catch((e) => {
    logger.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
