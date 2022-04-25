#!/usr/bin/env node

import {logger} from "@azure-tools/sdk-generation-lib";
import {getTask} from "@azure-tools/sdk-generation-lib";
import * as path from "path";
import {MockTestOptions} from "@azure-tools/sdk-generation-lib";
import {runScript} from "@azure-tools/sdk-generation-lib";
import * as fs from "fs";
import {runMockTestTaskCliConfig, RunMockTestTaskCliConfig} from "./cliSchema/runMockTestTaskCliConfig";
import {MockTestInput} from "@azure-tools/sdk-generation-lib";
import {processMockTestOutput} from "./lib/processMockTestOutput";
import {saveTaskResult, setTaskResult} from "@azure-tools/sdk-generation-lib";

const config: RunMockTestTaskCliConfig = runMockTestTaskCliConfig.getProperties();
export let mockTestTaskRunSuccessfully = true;

async function main() {
    setTaskResult(config, 'MockTest');
    const mockTestTask = getTask(path.join(config.sdkRepo, config.configPath), 'mockTest');
    if (!mockTestTask) {
        throw `Init task is ${mockTestTask}`;
    }
    const mockTestOptions = mockTestTask as MockTestOptions;
    const runOptions = mockTestOptions.mockTestScript;
    for (const packageFolder of config.packageFolders.split(';')) {
        logger.info(`Run MockTest for ${packageFolder}`);

        const inputContent: MockTestInput = {
            packageFolder: packageFolder,
            mockServerHost: config.mockServerHost
        };
        const inputJson = JSON.stringify(inputContent, undefined, 2)
        logger.info(inputJson);
        fs.writeFileSync(config.mockTestInputJson, inputJson, {encoding: 'utf-8'});
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(config.sdkRepo),
            args: [config.mockTestInputJson, config.mockTestOutputJson]
        });
        if (executeResult === 'failed') {
            logger.error(`Execute mockTest script for ${packageFolder} failed.`);
            mockTestTaskRunSuccessfully = false;
        } else {
            const result = await processMockTestOutput(config);
            if (!result) {
                mockTestTaskRunSuccessfully = false;
            }
        }
    }
}

main().catch(e => {
    logger.error(`${e.message}
    ${e.stack}`);
    mockTestTaskRunSuccessfully = false;
}).finally(() => {
    saveTaskResult();
    if (!mockTestTaskRunSuccessfully) {
        console.log('##vso[task.setVariable variable=StepResult]failure');
        process.exit(1);
    } else {
        console.log('##vso[task.setVariable variable=StepResult]success');
    }
})
