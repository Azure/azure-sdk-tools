#!/usr/bin/env node

import {logger} from '@azure-tools/sdk-generation-lib';
import {GetStepsToRunCliConfig, getStepsToRunCliConfig} from "./cliSchema/getStepsToRunCliConfig";
import * as fs from "fs";
import * as path from "path";
import {CodegenToSdkConfig, getCodegenToSdkConfig} from "@azure-tools/sdk-generation-lib";
import {requireJsonc} from "@azure-tools/sdk-generation-lib";

async function main() {
    const config: GetStepsToRunCliConfig = getStepsToRunCliConfig.getProperties();
    if (!fs.existsSync(config.sdkRepo)) {
        throw `Cannot find sdk repo in ${config.sdkRepo}`;
    }
    const codegenToSdkConfig: CodegenToSdkConfig = getCodegenToSdkConfig(requireJsonc(path.join(config.sdkRepo, config.configPath)));
    const jobsToRun: string[] = [];
    for (const task of Object.keys(codegenToSdkConfig)) {
        if (config.skippedSteps.includes(task)) {
            continue;
        }
        jobsToRun.push(task);
    }
    console.log(`##vso[task.setVariable variable=TasksToRun]${jobsToRun.join(';')}`);
    console.log('##vso[task.setVariable variable=StepResult]success');
}

main().catch(e => {
    logger.error(`${e.message}
    ${e.stack}`);
    console.log('##vso[task.setVariable variable=StepResult]failure');
    process.exit(1);
})
