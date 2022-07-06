import {
    addFileLog,
    getTask,
    InitOptions,
    initOutput,
    removeFileLog,
    requireJsonc,
    runScript
} from '@azure-tools/sdk-generation-lib';
import fs from 'fs';
import path from 'path';

import { DockerTaskEngineContext } from '../DockerTaskEngineContext';
import { SDKGenerationTaskBase, TaskType } from './SDKGenerationTaskBase';

export class InitTask implements SDKGenerationTaskBase {
    taskType: TaskType;
    order: number;
    context: DockerTaskEngineContext;

    constructor(context: DockerTaskEngineContext) {
        this.taskType = 'InitTask';
        this.order = 0;
        this.context = context;
    }

    public async execute() {
        const initTask = getTask(path.join(this.context.sdkRepo, this.context.configFilePath), 'init');
        if (!initTask) {
            throw new Error(`Init task is ${initTask}`);
        }
        const initOptions = initTask as InitOptions;
        const runOptions = initOptions.initScript;
        addFileLog(this.context.logger, this.context.initTaskLog, 'init');
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(this.context.sdkRepo),
            args: [this.context.initOutputJsonFile],
            customizedLogger: this.context.logger
        });
        removeFileLog(this.context.logger, 'init');
        this.context.taskResults['init'] = executeResult;
        if (executeResult === 'failed') {
            throw new Error(`Execute init script failed.`);
        }
        if (fs.existsSync(this.context.initOutputJsonFile)) {
            const initOutputJson = initOutput(requireJsonc(this.context.initOutputJsonFile));
            this.context.logger.info(`Get ${path.basename(this.context.initOutputJsonFile)}:`);
            this.context.logger.info(JSON.stringify(initOutputJson, undefined, 2));

            if (initOutputJson?.envs) {
                this.context.envs = initOutputJson.envs;
            }
        }
    }
}
