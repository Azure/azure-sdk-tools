#!/usr/bin/env node

import {logger} from "@azure-tools/sdk-generation-lib";
import {getTask} from "@azure-tools/sdk-generation-lib";
import * as path from "path";
import {LiveTestOptions} from "@azure-tools/sdk-generation-lib";
import {runScript} from "@azure-tools/sdk-generation-lib";
import * as fs from "fs";
import {runLiveTestTaskCliConfig, RunLiveTestTaskCliConfig} from "./cliSchema/runLiveTestTaskCliConfig";
import {LiveTestInput} from "@azure-tools/sdk-generation-lib";
import {processLiveTestOutput} from "./lib/processLiveTestOutput";
import {saveTaskResult, setTaskResult} from "@azure-tools/sdk-generation-lib";

const config: RunLiveTestTaskCliConfig = runLiveTestTaskCliConfig.getProperties();
export let liveTestTaskRunSuccessfully = true;

async function main() {
    // TODO: currently, keep it similar to mockTest
    setTaskResult(config, 'LiveTest');
    const liveTestTask = getTask(path.join(config.sdkRepo, config.configPath), 'liveTest');
    if (!liveTestTask) {
        throw `Init task is ${liveTestTask}`;
    }
    const liveTestOptions = liveTestTask as LiveTestOptions;
    const runOptions = liveTestOptions.liveTestScript;
    for (const packageFolder of config.packageFolders.split(';')) {
        logger.info(`Run MockTest for ${packageFolder}`);

        const inputContent: LiveTestInput = {
            packageFolder: packageFolder
        };
        const inputJson = JSON.stringify(inputContent, undefined, 2)
        logger.info(inputJson);
        fs.writeFileSync(config.liveTestInputJson, inputJson, {encoding: 'utf-8'});
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(config.sdkRepo),
            args: [config.liveTestInputJson, config.liveTestOutputJson]
        });
        if (executeResult === 'failed') {
            logger.error(`Execute liveTest script for ${packageFolder} failed.`);
            liveTestTaskRunSuccessfully = false;
        } else {
            const result = await processLiveTestOutput(config);
            if (!result) {
                liveTestTaskRunSuccessfully = false;
            }
        }
    }
}

main().catch(e => {
    logger.error(`${e.message}
    ${e.stack}`);
    liveTestTaskRunSuccessfully = false;
}).finally(() => {
    saveTaskResult();
    if (!liveTestTaskRunSuccessfully) {
        console.log('##vso[task.setVariable variable=StepResult]failure');
        process.exit(1);
    } else {
        console.log('##vso[task.setVariable variable=StepResult]success');
    }
})
