import { CodegenToSdkConfig, getCodegenToSdkConfig, requireJsonc, StringMap } from '@azure-tools/sdk-generation-lib';
import { execSync } from 'child_process';
import * as fs from 'fs';
import { writeFileSync } from 'fs';
import * as path from 'path';
import { Logger } from 'winston';

import { extractAutorestConfigs } from '../../../utils/autorestConfigExtractorUtils';
import { GitOperationWrapper } from '../../../utils/GitOperationWrapper';
import { dockerTaskEngineInput } from '../schema/dockerTaskEngineInput';
import { DockerContext } from './DockerContext';
import { DockerRunningModel } from './DockerRunningModel';
import { GenerateAndBuildTask } from './tasks/GenerateAndBuildTask';
import { InitTask } from './tasks/InitTask';
import { MockTestTask } from './tasks/MockTestTask';
import { SDKGenerationTaskBase } from './tasks/SDKGenerationTaskBase';

export class DockerTaskEngineContext {
    logger: Logger;
    configFilePath: string;
    initOutputJsonFile: string;
    generateAndBuildInputJsonFile: string;
    generateAndBuildOutputJsonFile: string;
    mockTestInputJsonFile: string;
    mockTestOutputJsonFile: string;
    initTaskLog: string;
    generateAndBuildTaskLog: string;
    mockTestTaskLog: string;
    readmeMdPath: string;
    typespecProjectFolderPath: string;
    specRepo: {
        repoPath: string;
        headSha: string;
        headRef: string;
        repoHttpsUrl: string;
    };
    serviceType?: string;
    tag?: string;
    sdkRepo: string;
    resultOutputFolder?: string;
    envs?: StringMap<string | boolean | number>;
    packageFolders?: string[];
    mockServerHost?: string;
    taskResults?: {};
    taskResultJsonPath: string;
    changeOwner: boolean;
    mode: DockerRunningModel;
    autorestConfig: string | undefined;
    skipGeneration: boolean;

    public async initialize(dockerContext: DockerContext) {
        // before execute task engine, safe spec repos and sdk repos because they may be owned by others
        await new GitOperationWrapper(dockerContext.specRepo).safeDirectory();
        await new GitOperationWrapper(dockerContext.sdkRepo).safeDirectory();
        const dockerTaskEngineConfigProperties = dockerTaskEngineInput.getProperties();
        this.logger = dockerContext.logger;
        this.configFilePath = dockerTaskEngineConfigProperties.configFilePath;
        this.initOutputJsonFile = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.initOutputJsonFile);
        this.generateAndBuildInputJsonFile = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildInputJsonFile);
        this.generateAndBuildOutputJsonFile = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildOutputJsonFile);
        this.mockTestInputJsonFile = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestInputJsonFile);
        this.mockTestOutputJsonFile = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestOutputJsonFile);
        this.initTaskLog = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.initTaskLog);
        this.generateAndBuildTaskLog = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildTaskLog);
        this.mockTestTaskLog = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestTaskLog);
        this.readmeMdPath = dockerContext.readmeMdPath;
        this.typespecProjectFolderPath = dockerContext.typespecProjectFolderPath;
        this.specRepo = {
            repoPath: dockerContext.specRepo,
            headSha: dockerTaskEngineConfigProperties.headSha ?? dockerContext.isPublicRepo?
                await new GitOperationWrapper(dockerContext.specRepo).getHeadSha() : '',
            headRef: dockerTaskEngineConfigProperties.headRef ?? await new GitOperationWrapper(dockerContext.specRepo).getHeadRef(),
            repoHttpsUrl: dockerTaskEngineConfigProperties.repoHttpsUrl?? (await new GitOperationWrapper(dockerContext.specRepo).getRemote())??
                `https://github.com/Azure/azure-rest-api-specs`
        };
        this.serviceType = (dockerContext.readmeMdPath.includes('data-plane') && dockerTaskEngineConfigProperties.serviceType) ||
        !!dockerContext.typespecProjectFolderPath ? 'data-plane': 'resource-manager';
        this.tag = dockerContext.tag;
        this.sdkRepo = dockerContext.sdkRepo;
        this.resultOutputFolder = dockerContext.resultOutputFolder ?? '/tmp/output';
        this.mockServerHost = dockerTaskEngineConfigProperties.mockServerHost;
        this.taskResultJsonPath = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.taskResultJson);
        this.changeOwner = dockerTaskEngineConfigProperties.changeOwner;
        this.mode = dockerContext.mode;
        this.autorestConfig = extractAutorestConfigs(dockerContext.autorestConfigFilePath, dockerContext.sdkRepo, dockerContext.logger);
        this.skipGeneration = dockerContext.skipGeneration;
    }

    public async beforeRunTaskEngine() {
        if (!!this.resultOutputFolder && !fs.existsSync(this.resultOutputFolder)) {
            fs.mkdirSync(this.resultOutputFolder, { recursive: true });
        }
        if (!!this.sdkRepo && fs.existsSync(this.sdkRepo)) {
            await new GitOperationWrapper(this.sdkRepo).disableFileMode();
        }
        this.logger.info(`Start to run task engine in ${path.basename(this.sdkRepo)}`);
    }

    public async afterRunTaskEngine() {
        if (this.changeOwner && !!this.specRepo?.repoPath && !!fs.existsSync(this.specRepo.repoPath)) {
            const userGroupId = (execSync(`stat -c "%u:%g" ${this.specRepo.repoPath}`, { encoding: 'utf8' })).trim();
            if (!!this.resultOutputFolder && fs.existsSync(this.resultOutputFolder)) {
                execSync(`chown -R ${userGroupId} ${this.specRepo.repoPath}`);
            }
            if (!!this.sdkRepo && fs.existsSync(this.sdkRepo)) {
                execSync(`chown -R ${userGroupId} ${this.sdkRepo}`, { encoding: 'utf8' });
                await new GitOperationWrapper(this.sdkRepo).disableFileMode();
            }
        }
        if (!!this.taskResults) {
            writeFileSync(this.taskResultJsonPath, JSON.stringify(this.taskResults, undefined, 2), 'utf-8');
        }
        this.logger.info(`Finish running task engine in ${path.basename(this.sdkRepo)}`);
    }

    public async getTaskToRun(): Promise<SDKGenerationTaskBase[]> {
        const codegenToSdkConfig: CodegenToSdkConfig = getCodegenToSdkConfig(requireJsonc(path.join(this.sdkRepo, this.configFilePath)));
        this.logger.info(`Get codegen_to_sdk_config.json`);
        this.logger.info(JSON.stringify(codegenToSdkConfig, undefined, 2));
        const tasksToRun: SDKGenerationTaskBase[] = [];
        for (const taskName of Object.keys(codegenToSdkConfig)) {
            let task: SDKGenerationTaskBase;
            switch (taskName) {
            case 'init':
                task = new InitTask(this);
                break;
            case 'generateAndBuild':
                task = new GenerateAndBuildTask(this);
                break;
            case 'mockTest':
                task = new MockTestTask(this);
                break;
            }

            if (!!task) {
                tasksToRun.push(task);
                if (!this.taskResults) {
                    this.taskResults = {};
                }
                this.taskResults[taskName] = 'skipped';
            }
        }
        tasksToRun.sort((a, b) => a.order - b.order);
        this.logger.info(`Get tasks to run: ${tasksToRun.map((task) => task.taskType).join(',')}`);
        return tasksToRun;
    }

    public async runTaskEngine() {
        await this.beforeRunTaskEngine();
        try {
            const tasksToRun: SDKGenerationTaskBase[] = await this.getTaskToRun();
            for (const task of tasksToRun) {
                await task.execute();
            }
        } finally {
            await this.afterRunTaskEngine();
        }
    }
}

