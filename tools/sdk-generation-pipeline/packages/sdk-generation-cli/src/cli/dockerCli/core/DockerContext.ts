import { initializeLogger } from '@azure-tools/sdk-generation-lib';
import fs from 'fs';
import path from 'path';
import { Logger } from 'winston';

import { DockerCliInput } from '../schema/dockerCliInput';
import { sdkToRepoMap } from './constants';
import { DockerRunningModel } from './DockerRunningModel';

export class DockerContext {
    mode: DockerRunningModel;
    readmeMdPath?: string;
    tag?: string;
    sdkList: string[];
    specRepo?: string;
    workDir?: string;
    sdkRepo?: string;
    resultOutputFolder?: string;
    logger: Logger;

    /*
    * there are different modes to use the docker image:
    * 1. local: generate codes
    * 2. local: grow up
    * 3. pipeline: generate codes
    * */
    public initialize(inputParams: DockerCliInput) {
        this.readmeMdPath = inputParams.readmeMdPath;
        this.tag = inputParams.tag;
        this.sdkList = inputParams.sdkList?.split(',').map((e) => e.trim()).filter((e) => e.length > 0);
        this.specRepo = inputParams.specRepo;
        this.workDir = inputParams.workDir;
        this.sdkRepo = inputParams.sdkRepo;
        this.resultOutputFolder = inputParams.resultOutputFolder;

        this.logger = initializeLogger(path.join(inputParams.resultOutputFolder, inputParams.dockerLogger), 'docker');

        if (this.sdkList?.length === 0 && fs.existsSync(this.workDir)) {
            this.logger.info('Preparing environment to do grow up development');
            this.mode = DockerRunningModel.GrowUp;
            this.validateSpecRepo();
            this.validateWorkDir();
        } else if (fs.existsSync(this.workDir)) {
            this.logger.info('Preparing environment to generate codes and do grow up development in local');
            this.mode = DockerRunningModel.CodeGenAndGrowUp;
            this.validateSpecRepo();
            this.validateReadmeMdPath();
            this.validateSdk();
        } else {
            this.logger.info('Preparing environment to generate codes in pipeline');
            this.mode = DockerRunningModel.Pipeline;
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

    private validateSdk() {
        const supportedSdk = Object.keys(sdkToRepoMap);
        const unSupportedSdk: string[] = [];
        for (const sdk of this.sdkList) {
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
