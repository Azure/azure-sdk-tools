import * as child_process from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { Connection } from 'typeorm';

import { AzureBlobClient } from '../utils/blob/AzureBlobClient';
import { AzureSDKTaskName, SDKPipelineStatus } from '../types/commonType';
import { CodeGeneration } from '../types/codeGeneration';
import { CodeGenerationDao } from '../utils/db/codeGenerationDao';
import { CodeGenerationDaoImpl } from '../utils/db/codeGenerationDaoImpl';
import { EventHubProducer } from '../utils/eventhub/EventHubProducer';
import { GenerateAndBuildOutput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildOutput';
import { logger } from '../utils/logger';
import { PipelineRunEvent } from '../types/events';
import { TaskResult } from '../types/taskResult';
import { TaskResultDao } from '../utils/db/taskResultDao';
import { TaskResultDaoImpl } from '../utils/db/taskResultDaoImpl';

export async function sendSdkTaskResultToDB(
    pipelineBuildId: string,
    dbConnection: Connection,
    taskResults: TaskResult[]
) {
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
    status: SDKPipelineStatus,
    owner?: string,
    tag?: string,
    swaggerPR?: string,
    codePR?: string
) {
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
    taskNameInput: AzureSDKTaskName,
    sdkGenerationName: string,
    azureStorageBlobSasUrl: string,
    azureBlobContainerName: string
) {
    let taskName: string = taskNameInput;

    if (!taskNameInput) {
        logger.info(`taskName is undefined or null, set full`);
        taskName = 'full';
    }

    const azureBlobClient = new AzureBlobClient(azureStorageBlobSasUrl, azureBlobContainerName);
    const logsAndResultPathArray = JSON.parse(logsAndResultPath);
    for (const file of logsAndResultPathArray) {
        if (fs.existsSync(file)) {
            let blobName: string = file.includes('.json')
                ? `${pipelineBuildId}/logs/${sdkGenerationName}-${taskName}-result.json`
                : `${pipelineBuildId}/logs/${sdkGenerationName}-${taskName}.log`;
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
    const files = child_process.execSync('git ls-files', { encoding: 'utf8', cwd: packageFolder }).trim().split('\n');

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

        if (packageFolder && fs.existsSync(packageFolder)) {
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
        if (!artifacts) {
            // artifacts is optional
            continue;
        }

        for (const artifact of artifacts) {
            const artifactName = path.basename(artifact);
            await azureBlobClient.publishBlob(artifact, `${pipelineBuildId}/${language}/${artifactName}`);
        }
    }
}

export async function publishEvent(
    eventHubConnectionString: string,
    event: PipelineRunEvent,
    partitionKey?: string
): Promise<void> {
    try {
        const producer = new EventHubProducer(eventHubConnectionString);
        await producer.send([JSON.stringify(event)], partitionKey);
        await producer.close();
    } catch (e) {
        logger.error('Failed to send pipeline result:', JSON.stringify(event), e);
        throw e;
    }
}
