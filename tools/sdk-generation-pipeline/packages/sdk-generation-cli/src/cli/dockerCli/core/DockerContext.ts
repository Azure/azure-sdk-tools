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
    typespecProjectFolderPath?: string;
    tag?: string;
    sdkList: string[];
    specRepo?: string;
    workDir?: string;
    sdkRepo?: string;
    resultOutputFolder?: string;
    autorestConfigFilePath?: string;
    specLink?: string;
    sdkWorkBranchLink?: string;
    skipGeneration: boolean;
    isPublicRepo: boolean;
    logger: Logger;

    /*
    * there are different modes to use the docker image:
    * 1. local: generate codes
    * 2. local: grow up
    * 3. pipeline: generate codes
    * */
    public initialize(inputParams: DockerCliInput) {
        this.readmeMdPath = inputParams.readmeMdPath;
        this.typespecProjectFolderPath = inputParams.typespecProjectFolderPath;
        this.tag = inputParams.tag;
        this.sdkList = inputParams.sdkList?.split(',').map((e) => e.trim()).filter((e) => e.length > 0);
        this.specRepo = inputParams.specRepo;
        this.workDir = inputParams.workDir;
        this.sdkRepo = inputParams.sdkRepo;
        this.resultOutputFolder = inputParams.resultOutputFolder;
        this.autorestConfigFilePath = inputParams.autorestConfigFilePath;
        this.specLink = inputParams.specLink;
        this.sdkWorkBranchLink = inputParams.sdkWorkBranchLink;
        this.skipGeneration = inputParams.skipGeneration;
        this.logger = initializeLogger(path.join(inputParams.resultOutputFolder, inputParams.dockerLogger), 'docker');

        if (this.sdkList?.length === 0 && fs.existsSync(this.workDir)) {
            this.logger.info('Preparing environment to do grow up development');
            this.mode = DockerRunningModel.GrowUp;
            this.isPublicRepo = false;
            if (!this.specLink) {
                try {
                    this.validateSpecRepo();
                } catch (e) {
                    throw new Error(`Cannot get spec repo link by parameter --spec-link, or get mounted swagger repo.`);
                }
            } else {
                this.validateSpecLink();
            }

            this.validateWorkDir();
            if (!!this.sdkWorkBranchLink) {
                this.validateWorkBranchLink();
            }
        } else if (fs.existsSync(this.workDir)) {
            this.logger.info('Preparing environment to generate codes and do grow up development in local');
            this.mode = DockerRunningModel.CodeGenAndGrowUp;
            this.isPublicRepo = false;
            this.validateSpecRepo();
            this.validateReadmeMdPathOrTypeSpecProjectFolderPath();
            this.validateSdk();
        } else {
            this.logger.info('Preparing environment to generate codes in pipeline');
            this.mode = DockerRunningModel.Pipeline;
            this.isPublicRepo = inputParams.isPublicRepo;
            this.validateSdkRepo();
            this.validateSpecRepo();
            this.validateReadmeMdPathOrTypeSpecProjectFolderPath();
            this.validateOutputFolder();
        }
    }

    private validateSpecRepo() {
        if (!fs.existsSync(this.specRepo)) {
            throw new Error(`Cannot find ${this.sdkRepo}, please mount it to docker container`);
        }
    }

    private validateReadmeMdPathOrTypeSpecProjectFolderPath() {
        if (!this.readmeMdPath && !this.typespecProjectFolderPath) {
            throw new Error(`Get empty readme.md path and typespec project folder path, please input it with --readme or --typespec-project`);
        }
        if (!!this.readmeMdPath && !fs.existsSync(path.join(this.specRepo, this.readmeMdPath))) {
            throw new Error(`Cannot find file ${this.readmeMdPath}, please input a valid one`);
        }
        if (!!this.typespecProjectFolderPath && !fs.existsSync(path.join(this.specRepo, this.typespecProjectFolderPath))) {
            throw new Error(`Cannot find file ${this.typespecProjectFolderPath}, please input a valid one`);
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

    private validateWorkBranchLink() {
        const match = this.sdkWorkBranchLink.match(/(https.*\/([^\/]*))\/tree\/(.*)/);
        if (match?.length !== 4) {
            throw new Error(`Get invalid sdk work branch link: ${this.sdkWorkBranchLink}`);
        }
    }

    private validateSpecLink() {
        const match = this.specLink.match(/http.*/);
        if (!match) {
            throw new Error(`Get invalid sdk work branch link: ${this.specLink}`);
        }
    }
}
