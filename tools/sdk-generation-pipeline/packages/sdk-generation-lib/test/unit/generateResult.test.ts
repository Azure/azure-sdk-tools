import * as fs from 'fs';

import { createTaskResult, parseGenerateLog, spliteLog } from '../../src/lib/generateResult';
import { AzureSDKTaskName } from '../../src/types/commonType';
import { TaskResult, TaskResultCommon, TestTaskResult } from '../../src/types/taskResult';

test('spliteLog', async () => {
    // Standard use case: single line
    const correctStr = '2022-03-18 06:11:22 cmderr Error test error';
    const correctLines = spliteLog(correctStr);
    expect(correctLines.length).toBeGreaterThan(0);

    // Standard use case2: multiple lines
    const correctStr2 = '2022-03-18 06:11:22 cmderr Error test error\n2022-03-18 06:11:22 cmdout nextline';
    const correctLines2 = spliteLog(correctStr2);
    expect(correctLines2.length).toBe(2);

    // Wrong use case: error data formate
    const unmatchStr = '03-18 06:11:22 cmderr Error test error';
    const unmatchLines = spliteLog(unmatchStr);
    expect(unmatchLines.length).toBe(0);
});

test('parseGenerateLog', async () => {
    // Standard use case
    const correctStr = '2022-03-18 06:11:22 cmderr Error test error\n2022-03-18 06:11:22 cmdout warning nextline';
    const parseGenerateLogTestFile = './parseGenerateLogTestFile.txt';

    if (fs.existsSync(parseGenerateLogTestFile)) {
        fs.unlinkSync(parseGenerateLogTestFile);
    }
    fs.writeFileSync(parseGenerateLogTestFile, correctStr, {
        encoding: 'utf-8'
    });
    const correctResult: TaskResultCommon = parseGenerateLog('testId', 'init', parseGenerateLogTestFile, undefined);
    expect(correctResult.name).toBe('init');
    expect(correctResult.pipelineBuildId).toBe('testId');
    expect(correctResult.errorCount).toBe(1);
    expect(correctResult.warningCount).toBe(1);
    expect(correctResult.messages.length).toBeGreaterThan(0);
    fs.unlinkSync(parseGenerateLogTestFile);

    // Wrong use case: logfile isn't exist, just throw error logs
    expect(parseGenerateLog('testId', 'init', './fileNotExist.txt', undefined)).toBeTruthy();
});

test('createTaskResult', async () => {
    // Standard use case
    const correctStr = '2022-03-18 06:11:22 cmderr Error test error\n2022-03-18 06:11:22 cmdout warning nextline';
    const createTaskResultTestFile = './createTaskResultTestFile.txt';

    if (fs.existsSync(createTaskResultTestFile)) {
        fs.unlinkSync(createTaskResultTestFile);
    }
    fs.writeFileSync(createTaskResultTestFile, correctStr, {
        encoding: 'utf-8'
    });

    const correctResult: TaskResult = createTaskResult(
        'testId',
        'init' as AzureSDKTaskName,
        'success',
        createTaskResultTestFile,
        undefined,
        undefined
    );
    expect(correctResult.name).toBe('init');
    expect(correctResult.pipelineBuildId).toBe('testId');
    expect(correctResult.result).toBe('success');
    expect(correctResult.errorCount).toBe(0);
    expect(correctResult.warningCount).toBe(0);
    expect(correctResult.messages).toBeUndefined();

    // Standard use case2: taskResult is failure
    const correctResult2: TestTaskResult = createTaskResult(
        'testId',
        'mockTest' as AzureSDKTaskName,
        'failure',
        createTaskResultTestFile,
        undefined,
        undefined
    );
    expect(correctResult2.name).toBe('mockTest');
    expect(correctResult2.pipelineBuildId).toBe('testId');
    expect(correctResult2.result).toBe('failure');
    expect(correctResult2.errorCount).toBe(1);
    expect(correctResult2.warningCount).toBe(1);
    expect(correctResult2.messages.length).toBeGreaterThan(0);
    expect(correctResult2.total).toBe(0);
    expect(correctResult2.success).toBe(0);
    expect(correctResult2.fail).toBe(0);
    expect(correctResult2.apiCoverage).toBe(0);
    expect(correctResult2.codeCoverage).toBe(0);

    // Standard use case3: taskResult is failure and taskOutput is valid
    const correctResult3: TestTaskResult = createTaskResult(
        'testId',
        'mockTest' as AzureSDKTaskName,
        'failure',
        createTaskResultTestFile,
        undefined,
        { total: 11, success: 10, fail: 1, apiCoverage: 99, codeCoverage: 80 }
    );
    expect(correctResult3.name).toBe('mockTest');
    expect(correctResult3.pipelineBuildId).toBe('testId');
    expect(correctResult3.result).toBe('failure');
    expect(correctResult3.errorCount).toBe(1);
    expect(correctResult3.warningCount).toBe(1);
    expect(correctResult3.messages.length).toBeGreaterThan(0);
    expect(correctResult3.total).toBe(11);
    expect(correctResult3.success).toBe(10);
    expect(correctResult3.fail).toBe(1);
    expect(correctResult3.apiCoverage).toBe(99);
    expect(correctResult3.codeCoverage).toBe(80);

    fs.unlinkSync(createTaskResultTestFile);
});
