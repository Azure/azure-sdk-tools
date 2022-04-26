#!/usr/bin/env node
import { DockerCliConfig, dockerCliConfig } from "./schema/dockerCliConfig";
import * as fs from "fs";
import * as path from "path";
import { generateCodesInLocal, sdkToRepoMap } from "./core/generateCodesInLocal";
import { generateCodesInPipeline } from "./core/generateCodesInPipeline";
import { Logger } from 'winston';
import { initializeLogger } from "@azure-tools/sdk-generation-lib";
import { runMockHost } from "./core/runMockHost";
import { ChildProcessWithoutNullStreams } from "child_process";
import { growUp } from "./core/growUp";

export class DockerContext {
    mode: 'generateCodesInLocal' | 'growUp' | 'generateCodesInPipeline';
    readmeMdPath?: string;
    tag?: string;
    sdkToGenerate: string[];
    specRepo?: string;
    workDir?: string;
    sdkRepo?: string;
    resultOutputFolder?: string;
    logger: Logger;
    mockHostProcess?: ChildProcessWithoutNullStreams

    /*
    * there are different modes to use the docker image:
    * 1. local: generate codes
    * 2. local: grow up
    * 3. pipeline: generate codes
    * */
    public initialize(inputParams: DockerCliConfig) {
        this.readmeMdPath = inputParams.readmeMdPath;
        this.tag = inputParams.tag;
        this.sdkToGenerate = inputParams.sdkToGenerate?.split(',').map(e => e.trim()).filter(e => e.length > 0);
        this.specRepo = inputParams.specRepo;
        this.workDir = inputParams.workDir;
        this.sdkRepo = inputParams.sdkRepo;
        this.resultOutputFolder = inputParams.resultOutputFolder;

        this.logger = initializeLogger(path.join(inputParams.resultOutputFolder, inputParams.dockerLogger), 'docker');

        if (this.sdkToGenerate?.length === 0 && fs.existsSync(this.workDir)) {
            this.logger.info('Preparing environment to do grow up development');
            this.mode = 'growUp';
            this.validateSpecRepo();
            this.validateWorkDir();
        } else if (fs.existsSync(this.workDir)) {
            this.logger.info('Preparing environment to generate codes and do grow up development in local');
            this.mode = "generateCodesInLocal";
            this.validateSpecRepo();
            this.validateReadmeMdPath();
            this.validateSdkToGenerate();
        } else {
            this.logger.info('Preparing environment to generate codes in pipeline');
            this.mode = 'generateCodesInPipeline';
            this.validateSdkRepo();
            this.validateSpecRepo();
            this.validateReadmeMdPath();
            this.validateOutputFolder();
        }
    }

    private validateSpecRepo() {
        if (!fs.existsSync(this.specRepo)) {
            throw new Error(`Cannot find ${this.sdkRepo}, please mount it to docker container`);
        }
    }

    private validateReadmeMdPath() {
        if (!this.readmeMdPath) {
            throw new Error(`Get empty readme.md path, please input it with --readme`);
        }
        if (!fs.existsSync(path.join(this.specRepo, this.readmeMdPath))) {
            throw new Error(`Cannot find file ${this.readmeMdPath}, please input a valid one`);
        }
    }

    private validateSdkToGenerate() {
        const supportedSdk = Object.keys(sdkToRepoMap);
        const unSupportedSdk: string[] = [];
        for (const sdk of this.sdkToGenerate) {
            if (!supportedSdk.includes(sdk)) {
                unSupportedSdk.push(sdk);
            }
        }
        if (unSupportedSdk.length > 0) {
            throw new Error(`Docker container doesn't support the following sdks: ${unSupportedSdk.join(', ')}`);
        }
    }

    private validateWorkDir() {
        if (!fs.existsSync(this.workDir)) {
            throw new Error(`Cannot find ${this.workDir}, please mount it to docker container`);
        }
    }

    private validateSdkRepo() {
        if (!fs.existsSync(this.sdkRepo)) {
            throw new Error(`Cannot find ${this.sdkRepo}, please mount it to docker container`);
        }
    }

    private validateOutputFolder() {
        if (!fs.existsSync(this.resultOutputFolder)) {
            throw new Error(`Cannot find ${this.resultOutputFolder}, please mount it to docker container`);
        }
    }
}

async function main() {
    const inputParams: DockerCliConfig = dockerCliConfig.getProperties();
    const context: DockerContext = new DockerContext();
    context.initialize(inputParams);

    // run mock test before everything
    runMockHost(context);

    switch (context.mode) {
        case "generateCodesInLocal":
            await generateCodesInLocal(context);
            break;
        case "growUp":
            await growUp(context);
            break;
        case "generateCodesInPipeline":
            await generateCodesInPipeline(context);
            break;
    }
}

main().catch(e => {
    console.error("\x1b[31m", e.toString());
    console.error("\x1b[31m", e.message);
    console.error("\x1b[31m", e.stack);
    process.exit(1);
})