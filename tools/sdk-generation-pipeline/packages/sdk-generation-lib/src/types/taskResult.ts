import * as fs from 'fs';
import { Column, Entity, ObjectIdColumn } from 'typeorm';

import { logger } from '../utils/logger';
import { requireJsonc } from '../utils/requireJsonc';
import { getTaskBasicConfig, TaskBasicConfig } from './taskBasicConfig';
import { GenerateAndBuildOutput } from './taskInputAndOuputSchemaTypes/GenerateAndBuildOutput';
import { InitOutput } from './taskInputAndOuputSchemaTypes/InitOutput';
import { TestOutput } from './taskInputAndOuputSchemaTypes/TestOutput';

@Entity('sdkGenerationResults')
export class TaskResultEntity {
    @ObjectIdColumn()
        id: string;
    @Column()
        key: string;
    @Column()
        pipelineBuildId: string;
    @Column()
        taskResult: TaskResult;
}

export enum TaskResultStatus {
    Success = 'succeeded',
    Failure = 'failed',
}

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
    result: TaskResultStatus;
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
        pipelineBuildId: '',
        result: TaskResultStatus.Success,
        errorCount: 0,
        warningCount: 0
    };
}

export let taskResult: TaskResult;
export let testTaskResult: TestTaskResult;

export function saveTaskResult() {
    const config: TaskBasicConfig = getTaskBasicConfig.getProperties();
    fs.writeFileSync(config.pipeLog, JSON.stringify(taskResult, null, 2), { encoding: 'utf-8' });
}

export function generateTotalResult(taskResults: TaskResult[], pipelineBuildId: string): TaskResult {
    const totalResult: TaskResult = {
        name: 'total',
        pipelineBuildId: pipelineBuildId,
        result: TaskResultStatus.Success,
        errorCount: 0,
        messages: []
    };

    if (taskResults.length === 0) {
        totalResult.result = TaskResultStatus.Failure;
        return totalResult;
    }

    for (const taskResult of taskResults) {
        if (taskResult.result !== TaskResultStatus.Success) {
            totalResult.result = taskResult.result;
        }
        totalResult.errorCount += taskResult.errorCount;
        if (taskResult.messages) {
            for (const msg of taskResult.messages) {
                totalResult.messages.push(msg);
            }
        }
    }

    return totalResult;
}

export function getTaskResults(taskResultsPath: string): TaskResult[] {
    const taskResultsPathArray = JSON.parse(taskResultsPath);
    const taskResults: TaskResult[] = [];
    for (const taskResultPath of taskResultsPathArray) {
        if (fs.existsSync(taskResultPath)) {
            taskResults.push(requireJsonc(taskResultPath));
        } else {
            logger.warn(`${taskResultPath} isn't exist, skip.`);
        }
    }
    return taskResults;
}
