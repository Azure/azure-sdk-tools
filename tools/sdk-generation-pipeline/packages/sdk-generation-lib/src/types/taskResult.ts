import { GenerateAndBuildOutput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildOutput';
import { InitOutput } from '../types/taskInputAndOuputSchemaTypes/InitOutput';
import { TestOutput } from '../types/taskInputAndOuputSchemaTypes/TestOutput';
import { getTaskBasicConfig, TaskBasicConfig } from './taskBasicConfig';
import * as fs from 'fs';

export type PipelineResult = 'success' | 'failure' | 'timed_out';

export type Extra = {
    [key: string]: any;
};

export type MessageLevel = 'Info' | 'Warning' | 'Error';

export type JsonPath = {
    tag: string; // meta info about the path, e.g. "swagger" or "example"
    path: string;
};

export type MesssageContext = {
    toolVersion: string;
};
export type BaseMessageRecord = {
    level: MessageLevel;
    message: string;
    time: Date;
    extra?: Extra;
};

export type ResultMessageRecord = BaseMessageRecord & {
    type: 'Result';
    id?: string;
    code?: string;
    docUrl?: string;
    paths: JsonPath[];
};

export type RawMessageRecord = BaseMessageRecord & {
    type: 'Raw';
};

export type MarkdownMessageRecord = BaseMessageRecord & {
    type: 'Markdown';
    mode: 'replace' | 'append';
};
export type MessageRecord = ResultMessageRecord | RawMessageRecord | MarkdownMessageRecord;

export type TaskResultCommon = {
    name: string;
    pipelineBuildId: string;
    result?: PipelineResult;
    errorCount?: number;
    warningCount?: number;
    logUrl?: string;
    messages?: MessageRecord[];
    codeUrl?: string;
    pullRequest?: string;
    artifacts?: Artifact[];
};

export type Artifact = {
    name: string;
    path: string;
    remoteUrl?: string;
};

export interface TestCase {
    name: string;
    passed: boolean;
    message: string;
}
export type TestTaskResult = TaskResultCommon & {
    total?: number;
    success?: number;
    fail?: number;
    apiCoverage?: number;
    codeCoverage?: number;
    testCases?: TestCase[];
};

export type TaskResult = TaskResultCommon | TestTaskResult;
export type TaskOutput = InitOutput | GenerateAndBuildOutput | TestOutput | undefined;

export function setTaskResult(config: TaskBasicConfig, taskName: string) {
    taskResult = {
        name: taskName,
        pipelineBuildId: "",
        result: 'success',
        errorCount: 0,
        warningCount: 0,
    };
}

export let taskResult: TaskResult;
export let testTaskResult: TestTaskResult;

export function saveTaskResult() {
    const config: TaskBasicConfig = getTaskBasicConfig.getProperties();
    fs.writeFileSync(config.pipeLog, JSON.stringify(taskResult, null, 2), { encoding: 'utf-8' });
}
