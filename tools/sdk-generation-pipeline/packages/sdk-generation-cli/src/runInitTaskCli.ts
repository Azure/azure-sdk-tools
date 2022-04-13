#!/usr/bin/env node

import {logger} from "@azure-tools/sdk-generation-lib";
import {runInitTaskCliConfig, RunInitTaskCliConfig} from "./cliSchema/runInitTaskCliConfig";
import {getTask} from "@azure-tools/sdk-generation-lib";
import * as path from "path";
import {InitOptions} from "@azure-tools/sdk-generation-lib";
import {runScript} from "@azure-tools/sdk-generation-lib";
import {requireJsonc} from "@azure-tools/sdk-generation-lib";
import * as fs from "fs";
import {initOutput} from "@azure-tools/sdk-generation-lib";
import {saveTaskResult, setTaskResult} from "@azure-tools/sdk-generation-lib";

const config: RunInitTaskCliConfig = runInitTaskCliConfig.getProperties();
export let initTaskRunSuccessfully = true;

async function main() {
    setTaskResult(config, 'Init');

    const initTask = getTask(path.join(config.sdkRepo, config.configPath), 'init');
    if (!initTask) {
        throw `Init task is ${initTask}`;
    }
    const initOptions = initTask as InitOptions;
    const runOptions = initOptions.initScript;
    const executeResult = await runScript(runOptions, {
        cwd: path.resolve(config.sdkRepo),
        args: [config.initOutput]
    });
    if (executeResult === 'failed') {
        throw `Execute init script failed.`
    }
    if (fs.existsSync(config.initOutput)) {
        const initOutputJson = initOutput(requireJsonc(config.initOutput));
        if (initOutputJson?.envs) {
            for (const v of Object.keys(initOutputJson.envs)) {
                console.log(`##vso[task.setVariable variable=${v};isOutput=true]${initOutputJson.envs[v]}`);
            }
        }
    }
}

main().catch(e => {
    logger.error(`${e.message}
    ${e.stack}`);

    initTaskRunSuccessfully = false;
}).finally(() => {
    saveTaskResult();
    if (!initTaskRunSuccessfully) {
        console.log('##vso[task.setVariable variable=StepResult]failure');
        process.exit(1);
    } else {
        console.log('##vso[task.setVariable variable=StepResult]success');
    }
})
