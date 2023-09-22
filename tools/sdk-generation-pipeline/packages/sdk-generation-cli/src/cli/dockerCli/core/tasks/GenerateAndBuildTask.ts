import {
    addFileLog,
    GenerateAndBuildInput,
    GenerateAndBuildOptions,
    getGenerateAndBuildOutput,
    getTask,
    removeFileLog,
    requireJsonc,
    runScript,
    TaskResultStatus
} from '@azure-tools/sdk-generation-lib';
import fs from 'fs';
import path from 'path';

import { DockerTaskEngineContext } from '../DockerTaskEngineContext';
import { SDKGenerationTaskBase, TaskType } from './SDKGenerationTaskBase';

export class GenerateAndBuildTask implements SDKGenerationTaskBase {
    taskType: TaskType;
    order: number;
    context: DockerTaskEngineContext;

    constructor(context: DockerTaskEngineContext) {
        this.taskType = 'GenerateAndBuildTask';
        this.order = 1;
        this.context = context;
    }

    public async execute() {
        const generateAndBuildTask = getTask(path.join(this.context.sdkRepo, this.context.configFilePath), 'generateAndBuild');
        if (!generateAndBuildTask) {
            throw new Error(`Generate and build task is ${generateAndBuildTask}`);
        }
        const generateAndBuildOptions = generateAndBuildTask as GenerateAndBuildOptions;
        const runOptions = generateAndBuildOptions.generateAndBuildScript;
        const readmeMdAbsolutePath = !!this.context.readmeMdPath? path.join(this.context.specRepo.repoPath, this.context.readmeMdPath) : '';
        const typespecProjectFolderAbsolutPath = !!this.context.typespecProjectFolderPath?
            path.join(this.context.specRepo.repoPath, this.context.typespecProjectFolderPath) : '';
        const specRepoPath = this.context.specRepo.repoPath.includes('specification')?
            this.context.specRepo.repoPath : path.join(this.context.specRepo.repoPath, 'specification');
        const relatedReadmeMdFileRelativePath = !!readmeMdAbsolutePath? path.relative(specRepoPath, readmeMdAbsolutePath) : '';
        const relatedTypeSpecProjectFolderRelativePath = !!typespecProjectFolderAbsolutPath? path.relative(specRepoPath, typespecProjectFolderAbsolutPath) : '';
        const inputContent: GenerateAndBuildInput = {
            specFolder: specRepoPath,
            headSha: this.context.specRepo.headSha,
            headRef: this.context.specRepo.headRef,
            repoHttpsUrl: this.context.specRepo.repoHttpsUrl,
            serviceType: this.context.serviceType,
            autorestConfig: this.context.autorestConfig,
            skipGeneration: this.context.skipGeneration
        };
        if (!!relatedTypeSpecProjectFolderRelativePath) {
            inputContent.relatedTypeSpecProjectFolder = relatedTypeSpecProjectFolderRelativePath;
        } else if (!!relatedReadmeMdFileRelativePath) {
            inputContent.relatedReadmeMdFile = relatedReadmeMdFileRelativePath;
        }
        const inputJson = JSON.stringify(inputContent, undefined, 2);
        this.context.logger.info(`Get ${path.basename(this.context.generateAndBuildInputJsonFile)}:`);
        this.context.logger.info(inputJson);
        fs.writeFileSync(this.context.generateAndBuildInputJsonFile, inputJson, { encoding: 'utf-8' });
        addFileLog(this.context.logger, this.context.generateAndBuildTaskLog, 'generateAndBuild');
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(this.context.sdkRepo),
            args: [this.context.generateAndBuildInputJsonFile, this.context.generateAndBuildOutputJsonFile],
            envs: this.context.envs,
            customizedLogger: this.context.logger
        });
        removeFileLog(this.context.logger, 'generateAndBuild');
        this.context.taskResults['generateAndBuild'] = executeResult;
        if (executeResult === TaskResultStatus.Failure) {
            throw new Error(`Execute generateAndBuild script failed.`);
        }
        if (fs.existsSync(this.context.generateAndBuildOutputJsonFile)) {
            const generateAndBuildOutputJson = getGenerateAndBuildOutput(requireJsonc(this.context.generateAndBuildOutputJsonFile));
            this.context.logger.info(`Get ${path.basename(this.context.generateAndBuildOutputJsonFile)}:`);
            this.context.logger.info(JSON.stringify(generateAndBuildOutputJson, undefined, 2));
            const packageFolders: string[] = [];
            for (const p of generateAndBuildOutputJson.packages) {
                packageFolders.push(p.packageFolder);
                if (p.result === TaskResultStatus.Failure) {
                    this.context.taskResults['generateAndBuild'] = TaskResultStatus.Failure;
                }
            }
            this.context.packageFolders = packageFolders;
        }
    }
}
