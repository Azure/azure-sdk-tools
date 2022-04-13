#!/usr/bin/env node

import {logger} from "@azure-tools/sdk-generation-lib";
import {getTask} from "@azure-tools/sdk-generation-lib";
import * as path from "path";
import {GenerateAndBuildOptions} from "@azure-tools/sdk-generation-lib";
import {runScript} from "@azure-tools/sdk-generation-lib";
import {runGenerateAndBuildTaskCliConfig, RunGenerateAndBuildTaskCliConfig} from "./cliSchema/runGenerateAndBuildTaskCliConfig";
import {GenerateAndBuildInput} from "@azure-tools/sdk-generation-lib";
import * as fs from "fs";
import {processGenerateAndBuildOutput} from "./lib/processGenerateAndBuildOutput";
import {saveTaskResult, setTaskResult} from "@azure-tools/sdk-generation-lib";

const config: RunGenerateAndBuildTaskCliConfig = runGenerateAndBuildTaskCliConfig.getProperties();
export let generateAndBuildTaskRunSuccessfully = true;

async function main() {
    setTaskResult(config, 'GenerateAndBuild');
    const generateAndBuildTask = getTask(path.join(config.sdkRepo, config.configPath), 'generateAndBuild');
    if (!generateAndBuildTask) {
        throw `Generate and build task is ${generateAndBuildTask}`;
    }
    const generateAndBuildOptions = generateAndBuildTask as GenerateAndBuildOptions;
    const runOptions = generateAndBuildOptions.generateAndBuildScript;
    const relatedReadmeMdFileAbsolutePath = path.join(config.specFolder, config.relatedReadmeMdFile);
    const specFolder = config.specFolder.includes('specification')? config.specFolder : path.join(config.specFolder, 'specification');
    const relatedReadmeMdFileRelativePath = path.relative(specFolder, relatedReadmeMdFileAbsolutePath);
    const inputContent: GenerateAndBuildInput = {
        specFolder: specFolder,
        headSha: config.headSha,
        headRef: config.headRef,
        repoHttpsUrl: config.repoHttpsUrl,
        relatedReadmeMdFile: relatedReadmeMdFileRelativePath,
        serviceType: config.serviceType
    };
    const inputJson = JSON.stringify(inputContent, undefined, 2)
    logger.info(inputJson);
    fs.writeFileSync(config.generateAndBuildInputJson, inputJson, {encoding: 'utf-8'});
    const executeResult = await runScript(runOptions, {
        cwd: path.resolve(config.sdkRepo),
        args: [config.generateAndBuildInputJson, config.generateAndBuildOutputJson]
    });
    if (executeResult === 'failed') {
        throw `Execute generateAndBuild script failed.`
    }
    const result = await processGenerateAndBuildOutput(config);
    if (result.hasFailedResult) {
        generateAndBuildTaskRunSuccessfully = false;
    }
}

main().catch(e => {
    logger.error(`${e.message}
    ${e.stack}`);
    generateAndBuildTaskRunSuccessfully = false;
}).finally(() => {
    saveTaskResult();
    if (!generateAndBuildTaskRunSuccessfully) {
        console.log('##vso[task.setVariable variable=StepResult]failure');
        process.exit(1);
    } else {
        console.log('##vso[task.setVariable variable=StepResult]success');
    }
})
