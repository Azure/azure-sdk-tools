import * as fs from 'fs';

import { AzureSDKTaskName } from '../types/commonType';
import { LogFilter } from '../types/taskInputAndOuputSchemaTypes/CodegenToSdkConfig';
import { TestOutput } from '../types/taskInputAndOuputSchemaTypes/TestOutput';
import {
    MessageRecord,
    RawMessageRecord,
    TaskOutput,
    TaskResult,
    TaskResultCommon,
    TaskResultStatus,
    TestTaskResult
} from '../types/taskResult';
import { logger } from '../utils/logger';
import { isLineMatch } from './runScript';

const logSeparatorLength = 26; // length of '20xx-xx-xx xx:xx:xx cmdout'
const timestampLength = 19; // length of '20xx-xx-xx xx:xx:xx'

export function spliteLog(fullLog: string): string[] {
    const lines: string[] = [];
    const splitFilter = /(19|20)\d{2}-(0|1)\d-[0-3]\d (20|21|22|23|[0-1]\d):[0-5]\d:[0-5]\d (cmdout|cmderr)/g;
    let startPoint = fullLog.search(splitFilter);
    if (startPoint === -1) {
        return [];
    }
    fullLog = fullLog.substring(startPoint);
    startPoint = 0;
    let findPoint = fullLog.substring(logSeparatorLength).search(splitFilter);
    while (findPoint !== -1) {
        lines.push(fullLog.substring(startPoint, findPoint - 1 + logSeparatorLength)); // -1 for the last \n
        fullLog = fullLog.substring(findPoint + logSeparatorLength);
        findPoint = fullLog.substring(logSeparatorLength).search(splitFilter);
    }
    lines.push(fullLog);

    return lines;
}

export function parseGenerateLog(
    pipelineBuildId: string,
    taskName: string,
    logfile: string,
    logFilter: LogFilter,
    taskExeResult: TaskResultStatus
): TaskResultCommon {
    let errorNum = 0;
    let warnNum = 0;
    const defaultErrorFilter = /(error|Error|ERROR|failed|Failed|FAILED|exception|Exception|EXCEPTION)/g;
    const defaultWarningFilter = /warn/g;
    const logErrorFilter: RegExp =
        logFilter === undefined || logFilter.error === undefined ? defaultErrorFilter : logFilter.error;
    const logWarningFilter: RegExp =
        logFilter === undefined || logFilter.warning === undefined ? defaultWarningFilter : logFilter.warning;
    const messages: MessageRecord[] = [];
    if (fs.existsSync(logfile)) {
        const fullLog = fs.readFileSync(logfile, 'utf-8');
        const lines = spliteLog(fullLog);
        lines.forEach((line) => {
            if (isLineMatch(line.toLowerCase(), logErrorFilter)) {
                errorNum++;
                const message: RawMessageRecord = {
                    level: 'Error',
                    message: line,
                    time: new Date(line.substring(0, timestampLength)),
                    type: 'Raw'
                };
                messages.push(message);
            } else if (isLineMatch(line.toLowerCase(), logWarningFilter)) {
                warnNum++;
                const message: RawMessageRecord = {
                    level: 'Warning',
                    message: line,
                    time: new Date(line.substring(0, timestampLength)),
                    type: 'Raw'
                };
                messages.push(message);
            }
        });
    } else {
        logger.error('logfile ' + logfile + ' does not exist.');
    }

    const result: TaskResultCommon = {
        name: taskName,
        pipelineBuildId: pipelineBuildId,
        errorCount: errorNum,
        warningCount: warnNum,
        messages: messages,
        result: taskExeResult
    };

    return result;
}

export function createTaskResult(
    pipelineBuildId: string,
    taskname: AzureSDKTaskName,
    taskExeResult: TaskResultStatus,
    logfile: string,
    logFilter: LogFilter,
    taskOutput: TaskOutput
): TaskResult {
    let commonResult: TaskResultCommon = undefined;
    if (taskExeResult === TaskResultStatus.Success) {
        commonResult = {
            name: taskname,
            pipelineBuildId: pipelineBuildId,
            result: taskExeResult,
            errorCount: 0,
            warningCount: 0
        };
    } else {
        commonResult = parseGenerateLog(pipelineBuildId, taskname, logfile, logFilter, taskExeResult);
    }
    if (taskname === AzureSDKTaskName.MockTest || taskname === AzureSDKTaskName.LiveTest) {
        if (taskOutput === undefined) {
            logger.warn('taskOutput is undefined');
            return {
                total: 0,
                success: 0,
                fail: 0,
                apiCoverage: 0,
                codeCoverage: 0,
                result: taskExeResult,
                ...commonResult
            };
        }
        const testOutput: TestOutput = taskOutput as TestOutput;
        const testTaskResult: TestTaskResult = {
            total: testOutput.total,
            success: testOutput.success,
            fail: testOutput.fail,
            apiCoverage: testOutput.apiCoverage,
            codeCoverage: testOutput.codeCoverage,
            result: taskExeResult,
            ...commonResult
        };
        return testTaskResult;
    }

    return commonResult;
}
