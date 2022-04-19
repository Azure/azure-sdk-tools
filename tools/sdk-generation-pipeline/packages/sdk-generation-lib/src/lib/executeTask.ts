import { getTaskBasicConfig, TaskBasicConfig } from '../types/taskBasicConfig';
import { RunOptions } from '../types/taskInputAndOuputSchemaTypes/CodegenToSdkConfig';
import { GenerateAndBuildInput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildInput';
import { GenerateAndBuildOutput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildOutput';
import { InitOutput } from '../types/taskInputAndOuputSchemaTypes/InitOutput';
import { LiveTestInput } from '../types/taskInputAndOuputSchemaTypes/LiveTestInput';
import { MockTestInput } from '../types/taskInputAndOuputSchemaTypes/MockTestInput';
import { TestOutput } from '../types/taskInputAndOuputSchemaTypes/TestOutput';
import { setTaskResult, taskResult, TaskResult } from '../types/taskResult';
import { requireJsonc } from '../utils/requireJsonc';
import { runScript } from './runScript';
import * as fs from 'fs';
import { createTaskResult } from './generateResult';
import { CodeGenerationPipelineTaskName } from '../types/commonType';

export async function executeTask(
    taskName: CodeGenerationPipelineTaskName,
    runScriptOptions: RunOptions,
    cwd: string,
    inputJson?: GenerateAndBuildInput | MockTestInput | LiveTestInput,
): Promise<{ taskResult: TaskResult; output: InitOutput | GenerateAndBuildOutput | TestOutput | undefined }> {
    const inputJsonPath = '/tmp/input.json';
    const outputJsonPath = '/tmp/output.json';
    if (inputJson) {
        fs.writeFileSync(inputJsonPath, JSON.stringify(inputJson, null, 2), { encoding: 'utf-8' });
    }
    const config: TaskBasicConfig = getTaskBasicConfig.getProperties();
    const args = [];
    if (inputJson) {
        args.push(inputJsonPath);
    }
    args.push(outputJsonPath);
    const result = await runScript(runScriptOptions, {
        cwd: cwd,
        args: args,
    });
    if (result === 'failed') {
        taskResult.result = 'failure';
    }
    if (fs.existsSync(outputJsonPath)) {
        const outputJson = requireJsonc(outputJsonPath);
        return {
            taskResult: createTaskResult("", taskName, config.pipeFullLog, runScriptOptions.logFilter, outputJson),
            output: outputJson,
        };
    } else {
        return {
            taskResult: createTaskResult("", taskName, config.pipeFullLog, runScriptOptions.logFilter, undefined),
            output: undefined,
        };
    }
}
