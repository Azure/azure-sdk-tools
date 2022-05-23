import convict from 'convict';

import { assertNullOrEmpty } from '../utils/validator';

export class GenerateResultCliInput {
    pipelineBuildId: string;
    logfile: string;
    logFilterStr?: string;
    taskName: string;
    exeResult?: string;
    taskOutputPath?: string;
    resultOutputPath: string;
    dockerResultFile?: string;
}

export const generateResultCliInput = convict<GenerateResultCliInput>({
    pipelineBuildId: {
        default: null,
        format: assertNullOrEmpty,
        env: 'PIPELINE_BUILDID',
        arg: 'buildId',
    },
    logfile: {
        default: null,
        env: 'LOG_FILE',
        format: assertNullOrEmpty,
        arg: 'logfile',
    },
    logFilterStr: {
        default: null,
        env: 'LOG_FILTERSTR',
        nullable: true,
        format: String,
    },
    taskName: {
        default: null,
        env: 'TASK_NAME',
        format: ['init', 'generateAndBuild', 'mockTest', 'liveTest'],
        arg: 'taskName',
    },
    exeResult: {
        default: null,
        env: 'EXE_RESULT',
        nullable: true,
        format: ['success', 'failure'],
    },
    taskOutputPath: {
        default: null,
        env: 'TASK_OUTPUT_PATH',
        nullable: true,
        format: String,
        arg: 'taskOutputPath',
    },
    resultOutputPath: {
        default: null,
        env: 'RESULT_OUTPUT_PATH',
        format: assertNullOrEmpty,
        arg: 'resultOutputPath',
    },
    dockerResultFile: {
        default: null,
        env: 'DOCKER_RESULT_FILE',
        nullable: true,
        format: String,
        arg: 'dockerResultFile',
    },
});
