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
        arg: 'buildId'
    },
    logfile: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'logfile'
    },
    logFilterStr: {
        default: null,
        nullable: true,
        format: String,
        arg: 'logFilterStr'
    },
    taskName: {
        default: null,
        format: ['init', 'generateAndBuild', 'mockTest', 'liveTest'],
        arg: 'taskName'
    },
    exeResult: {
        default: null,
        nullable: true,
        format: ['succeeded', 'failed'],
        arg: 'exeResult'
    },
    taskOutputPath: {
        default: null,
        nullable: true,
        format: String,
        arg: 'taskOutputPath'
    },
    resultOutputPath: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'resultOutputPath'
    },
    dockerResultFile: {
        default: null,
        nullable: true,
        format: String,
        arg: 'dockerResultFile'
    }
});
