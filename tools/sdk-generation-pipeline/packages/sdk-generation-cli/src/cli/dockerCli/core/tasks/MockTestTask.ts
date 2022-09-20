import {
    addFileLog,
    getTask,
    getTestOutput,
    MockTestInput,
    MockTestOptions, removeFileLog, requireJsonc,
    runScript, TaskResultStatus
} from '@azure-tools/sdk-generation-lib';
import fs from 'fs';
import path from 'path';

import { DockerTaskEngineContext } from '../DockerTaskEngineContext';
import { SDKGenerationTaskBase, TaskType } from './SDKGenerationTaskBase';

export class MockTestTask implements SDKGenerationTaskBase {
    context: DockerTaskEngineContext;
    order: number;
    taskType: TaskType;

    constructor(context: DockerTaskEngineContext) {
        this.taskType = 'MockTestTask';
        this.order = 2;
        this.context = context;
    }

    public async execute() {
        if (this.context.taskResults?.['generateAndBuild'] !== TaskResultStatus.Success) return;
        const mockTestTask = getTask(path.join(this.context.sdkRepo, this.context.configFilePath), 'mockTest');
        if (!mockTestTask) {
            throw new Error(`Init task is ${mockTestTask}`);
        }
        const mockTestOptions = mockTestTask as MockTestOptions;
        const runOptions = mockTestOptions.mockTestScript;
        for (const packageFolder of this.context.packageFolders) {
            this.context.logger.info(`Run MockTest for ${packageFolder}`);

            const inputContent: MockTestInput = {
                packageFolder: path.join(this.context.sdkRepo, packageFolder),
                mockServerHost: this.context.mockServerHost
            };
            const inputJson = JSON.stringify(inputContent, undefined, 2);
            const formattedPackageName = packageFolder.replace(/[^a-zA-z0-9]/g, '-');
            const mockTestInputJsonPath = this.context.packageFolders.length > 1 ?
                this.context.mockTestInputJsonFile.replace('.json', `${formattedPackageName}.json`) : this.context.mockTestInputJsonFile;
            const mockTestOutputJsonPath = this.context.packageFolders.length > 1 ?
                this.context.mockTestOutputJsonFile.replace('.json', `${formattedPackageName}.json`) : this.context.mockTestOutputJsonFile;
            const mockTestTaskLogPath = this.context.packageFolders.length > 1 ?
                this.context.mockTestTaskLog.replace('task.log', `${formattedPackageName}-task.log`) : this.context.mockTestTaskLog;
            fs.writeFileSync(mockTestInputJsonPath, inputJson, { encoding: 'utf-8' });
            this.context.logger.info(`Get ${path.basename(mockTestInputJsonPath)}:`);
            this.context.logger.info(inputJson);
            addFileLog(this.context.logger, mockTestTaskLogPath, `mockTest_${formattedPackageName}`);
            const executeResult = await runScript(runOptions, {
                cwd: path.resolve(this.context.sdkRepo),
                args: [mockTestInputJsonPath, mockTestOutputJsonPath],
                envs: this.context.envs,
                customizedLogger: this.context.logger
            });
            this.context.taskResults['mockTest'] = executeResult === TaskResultStatus.Success &&
            this.context.taskResults['mockTest'] !== TaskResultStatus.Failure ?
                TaskResultStatus.Success : TaskResultStatus.Failure;
            removeFileLog(this.context.logger, `mockTest_${formattedPackageName}`);
            if (fs.existsSync(mockTestOutputJsonPath)) {
                const mockTestOutputJson = getTestOutput(requireJsonc(mockTestOutputJsonPath));
                this.context.logger.info(`Get ${path.basename(mockTestOutputJsonPath)}:`);
                this.context.logger.info(JSON.stringify(mockTestOutputJson, undefined, 2));
            }
            if (this.context.taskResults['mockTest'] === TaskResultStatus.Failure) {
                throw new Error('Run Mock Test Failed');
            }
        }
    }
}
