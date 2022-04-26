#!/usr/bin/env node
import * as fs from "fs";
import {
    createTaskResult,
    AzureSDKTaskName,
    TaskResult,
    TaskOutput,
    LogFilter,
    logger,
} from "@azure-tools/sdk-generation-lib";

function printHelp() {
    console.log('usage: generateResult --pipelineBuildId --logfile --taskName --resultOutputPath');
    console.log('                      --taskResultFile [--taskOutput] [--logFilter]\n');
    console.log('taskName: must be one of [Init, GenerateAndBuild, MockTest, LiveTest]');
}

async function main() {
    const args = parseArgs(process.argv);
    const pipelineBuildId = args['pipelineBuildId'];
    const logfile = args['logfile'];
    const logFilterStr = args['logFilter'];
    const taskName = args['taskname'];
    const exeResult = args['taskExeResult']
    const taskOutput = args['taskOutput'];
    const resultOutputPath = args['resultOutputPath'];
    const taskResultFile = args['taskResultFile'];
    let taskOutputObj: TaskOutput = undefined;
    let logFilter: LogFilter = undefined;
    let taskResult: TaskResult = undefined;

    if (pipelineBuildId === undefined) {
        printHelp();
        throw new Error(`pipelineBuildId is empty`);
    }
    if (logfile === undefined) {
        printHelp();
        throw new Error(`logfile is empty`);
    }
    if (taskName === undefined) {
        printHelp();
        throw new Error(`taskName is empty`);
    } else if (!Object.values(AzureSDKTaskName).includes(taskName)) {
        printHelp();
        console.log("Current taskName is %s", taskName);
        throw new Error(`invalid taskName`);
    }

    if (exeResult === undefined && taskResultFile === undefined) {
        printHelp();
        throw new Error(`Task execute result and taskResultFile is empty`);
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

    if (exeResult) {
        taskResult = createTaskResult(pipelineBuildId, taskName, exeResult, logfile, logFilter, taskOutputObj);
    } else if (taskResultFile && fs.existsSync(taskResultFile)) {
        const totalTaskResult = JSON.parse(fs.readFileSync(taskResultFile, 'utf-8'));
        if (totalTaskResult[taskName].includes('skipped')) {
            console.log(taskName + `skipped`);
            return;
        } else {
            taskResult = createTaskResult(pipelineBuildId, taskName, totalTaskResult[taskName], logfile, logFilter, taskOutputObj);
        }
    } else {
        throw new Error(`taskResultFile:${taskResultFile} isn's exist`);
    }

    fs.writeFileSync(resultOutputPath, JSON.stringify(taskResult, null, 2), {
        encoding: "utf-8",
    });
    console.log("Generate Success !!!");

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
