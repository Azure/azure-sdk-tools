import * as convict from 'convict';

const stringMustHaveLength = (value: string) => {
    if (value.length === 0) {
        throw new Error('must not be empty!');
    }
};

export class GenerateResultCliConfig {
    pipelineBuildId: string;
    logfile: string;
    logFilterStr?: string;
    taskName: string;
    exeResult?: string;
    taskOutputPath?: string;
    resultOutputPath: string;
    dockerResultFile?: string;
}

export const generateResultCliConfig = convict<GenerateResultCliConfig>({
    pipelineBuildId: {
        default: '',
        format: stringMustHaveLength,
        env: 'PIPELINE_BUILDID',
    },
    logfile: {
        default: '',
        env: 'LOG_FILE',
        format: stringMustHaveLength,
    },
    logFilterStr: {
        default: '',
        env: 'LOG_FILTERSTR',
        nullable: true,
        format: String,
    },
    taskName: {
        default: '',
        env: 'TASK_NAME',
        format: ['init', 'generateAndBuild', 'mockTest', 'liveTest'],
    },
    exeResult: {
        default: null,
        env: 'EXE_RESULT',
        nullable: true,
        format: ['success', 'failure', 'timed_out'],
    },
    taskOutputPath: {
        default: '',
        env: 'TASK_OUTPUT_PATH',
        nullable: true,
        format: String,
    },
    resultOutputPath: {
        default: '',
        env: 'RESULT_OUTPUT_PATH',
        format: stringMustHaveLength,
    },
    dockerResultFile: {
        default: '',
        env: 'DOCKER_RESULT_FILE',
        nullable: true,
        format: String,
    },
});
