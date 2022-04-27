import * as child_process from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { Connection, createConnection } from 'typeorm';

import { getGenerateAndBuildOutput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildOutput';
import { AzureBlobClient } from '../utils/blob/AzureBlobClient';
import { requireJsonc } from '../utils/requireJsonc';
import { logger } from '../utils/logger';
import { AzureSDKTaskName } from '../types/commonType';
import { TaskResultEntity, TaskResult } from '../types/taskResult';
import { TaskResultDaoImpl } from '../utils/db/taskResultDaoImpl';
import { TaskResultDao } from '../utils/db/taskResultDao';
import { CodeGeneration } from '../types/codeGeneration';
import { CodeGenerationDaoImpl } from '../utils/db/codeGenerationDaoImpl';
import { CodeGenerationDao } from '../utils/db/codeGenerationDao';
import { GenerateAndBuildOutput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildOutput'

export async function sendSdkTaskResultToDB(pipelineBuildId: string, dbConnection: Connection, taskResults: TaskResult[]) {
    if (pipelineBuildId === undefined) {
        throw new Error('pipelineBuildId is empty!');
    }

    const taskResultDao: TaskResultDao = new TaskResultDaoImpl(dbConnection);
    for (const taskResult of taskResults) {
        await taskResultDao.put(pipelineBuildId, taskResult);
    }
}

export async function sendSdkGenerationToDB(
    dbConnection: Connection,
    pipelineBuildId: string,
    sdkGenerationName: string,
    service: string,
    serviceType: string,
    language: string,
    swaggerRepo: string,
    sdkRepo: string,
    codegenRepo: string,
    triggerType: string,
    status: string,
    owner?: string,
    tag?: string,
    swaggerPR?: string,
    codePR?: string
) {
    if (
        pipelineBuildId === undefined ||
        sdkGenerationName === undefined ||
        service === undefined ||
        serviceType === undefined ||
        language === undefined ||
        swaggerRepo === undefined ||
        sdkRepo === undefined ||
        codegenRepo === undefined ||
        triggerType === undefined ||
        status === undefined
    ) {
        throw new Error('SdkGeneration data is empty!');
    }

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(dbConnection);
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = sdkGenerationName;
    cg.service = service;
    cg.serviceType = serviceType;
    cg.tag = tag !== undefined ? tag : '';
    cg.sdk = language;
    cg.swaggerRepo = swaggerRepo;
    cg.sdkRepo = sdkRepo;
    cg.codegenRepo = codegenRepo;
    cg.owner = owner === undefined ? '' : owner;
    cg.type = triggerType;
    cg.status = status;
    cg.lastPipelineBuildID = pipelineBuildId;
    cg.swaggerPR = swaggerPR === undefined ? '' : swaggerPR;
    cg.codePR = codePR === undefined ? '' : codePR;

    await codeGenerationDao.submitCodeGeneration(cg);
}

export async function uploadLogsAndResult(
    logsAndResultPath: string,
    pipelineBuildId: string,
    taskName: AzureSDKTaskName,
    sdkGenerationName: string,
    azureStorageBlobSasUrl: string,
    azureBlobContainerName: string
) {
    let stepName: string = taskName;
    if (logsAndResultPath === undefined) {
        throw new Error(`logsAndResultPath is empty`);
    }
    if (pipelineBuildId === undefined) {
        throw new Error(`pipelineBuildId is empty`);
    }
    if (sdkGenerationName === undefined) {
        throw new Error(`sdkGenerationName is empty`);
    }
    if (azureStorageBlobSasUrl === undefined) {
        throw new Error(`azureStorageBlobSasUrl is empty`);
    }
    if (azureBlobContainerName === undefined) {
        throw new Error(`azureBlobContainerName is empty`);
    }
    if (taskName === undefined) {
        logger.info(`taskName is undefined, set full log`);
        stepName = 'full';
    }

    const azureBlobClient = new AzureBlobClient(
        azureStorageBlobSasUrl,
        azureBlobContainerName
    );
    const logsAndResultPathArray = JSON.parse(logsAndResultPath);
    for (const file of logsAndResultPathArray) {
        if (fs.existsSync(file)) {
            let blobName: string = file.includes('.json')
                ? `${pipelineBuildId}/logs/${sdkGenerationName}-${stepName}-result.json`
                : `${pipelineBuildId}/logs/${sdkGenerationName}-${stepName}.log`;
            await azureBlobClient.publishBlob(file, blobName);
            logger.info(`Publish ${file} Success !!!`);
        } else {
            logger.error(`Publish result failed !, ${file} is missed`);
        }
    }
}

function getFileListInPackageFolder(packageFolder: string) {
    child_process.execSync('git add .', {
        encoding: 'utf8',
        cwd: packageFolder,
    });
    const files = child_process
        .execSync('git ls-files', { encoding: 'utf8', cwd: packageFolder })
        .trim()
        .split('\n');

    return files;
}

export async function uploadSourceCode(
    azureBlobClient: AzureBlobClient,
    language: string,
    pipelineBuildId: string,
    generateAndBuildOutputJson: GenerateAndBuildOutput
) {
    if (language === undefined) {
        throw new Error('language  is empty!');
    }
    if (pipelineBuildId === undefined) {
        throw new Error('pipelineBuildId  is empty!');
    }

    for (const p of generateAndBuildOutputJson.packages) {
        const result = p.result;
        if (result === 'failed') {
            logger.warn(`Build ${p.packageName} failed, skipped it`);
            continue;
        }
        const packageName = p.packageName;
        const packageFolder = p.packageFolder;

        if (fs.existsSync(packageFolder)) {
            for (const filePath of getFileListInPackageFolder(packageFolder)) {
                if (fs.existsSync(path.join(packageFolder, filePath))) {
                    await azureBlobClient.publishBlob(
                        path.join(packageFolder, filePath),
                        `${pipelineBuildId}/${language}/${packageName}/${filePath}`
                    );
                }
            }
        }
    }
}

export async function uploadArtifacts(
    azureBlobClient: AzureBlobClient,
    language: string,
    pipelineBuildId: string,
    generateAndBuildOutputJson: GenerateAndBuildOutput
) {
    if (language === undefined) {
        throw new Error('language  is empty!');
    }
    if (pipelineBuildId === undefined) {
        throw new Error('pipelineBuildId  is empty!');
    }

    for (const p of generateAndBuildOutputJson.packages) {
        const result = p.result;
        if (result === 'failed') {
            logger.warn(`Build ${p.packageName} failed, skipped it`);
            continue;
        }
        const artifacts = p.artifacts;

        for (const artifact of artifacts) {
            const artifactName = path.basename(artifact);
            await azureBlobClient.publishBlob(
                artifact,
                `${pipelineBuildId}/${language}/${artifactName}`
            );
        }
    }
}
