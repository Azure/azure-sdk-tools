import {
    CodegenToSdkConfig,
    GenerateAndBuildOptions,
    getCodegenToSdkConfig,
    InitOptions,
    LiveTestOptions,
    MockTestOptions
} from '../types/taskInputAndOuputSchemaTypes/CodegenToSdkConfig';
import { requireJsonc } from '../utils/requireJsonc';

export function getTask(
    codegenToSdkConfigPath: string,
    taskName: string
): InitOptions | GenerateAndBuildOptions | MockTestOptions | LiveTestOptions | undefined {
    const codegenToSdkConfig: CodegenToSdkConfig = getCodegenToSdkConfig(requireJsonc(codegenToSdkConfigPath));
    for (const task of Object.keys(codegenToSdkConfig)) {
        if (task === taskName) {
            return codegenToSdkConfig[task];
        }
    }
    return undefined;
}
