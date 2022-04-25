import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';
import * as path from 'path';

export const codegenToSdkConfigSchema = requireJsonc(path.join(__dirname, 'CodegenToSdkConfigSchema.json'));

export type RunOptions = {
    path: string;
    script?: string;
    envs?: string[];
    logPrefix?: string;
    logFilter?: LogFilter;
    exitWithNonZeroCode?: {
        storeLog: boolean;
        result: 'error' | 'warning' | 'ignore';
    };
};

export type LogFilter = {
    error?: RegExp;
    warning?: RegExp;
};

export type InitOptions = {
    initScript: RunOptions;
};
export type GenerateAndBuildOptions = {
    generateAndBuildScript: RunOptions;
};
export type MockTestOptions = {
    mockTestScript: RunOptions;
};
export type LiveTestOptions = {
    liveTestScript: RunOptions;
};

export type CodegenToSdkConfig = {
    init: InitOptions;
    generateAndBuild: GenerateAndBuildOptions;
    mockTest: MockTestOptions;
    liveTest: LiveTestOptions;
};

export const getCodegenToSdkConfig = getTypeTransformer<CodegenToSdkConfig>(codegenToSdkConfigSchema, 'CodegenToSdkConfig');
