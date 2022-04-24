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

export async function sendDB(
    server: string,
    port: number,
    username: string,
    password: string,
    database: string,
    ssl: boolean,
    changeDatabase: boolean,
    pipelineBuildId: string,
    taskResults: TaskResult[],
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
        server === undefined ||
        port === undefined ||
        username === undefined ||
        password === undefined ||
        database === undefined ||
        ssl === undefined ||
        changeDatabase === undefined
    ) {
        throw new Error('db parameter is empty!');
    }

    if (
        pipelineBuildId === undefined ||
        taskResults === undefined ||
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
        throw new Error('save data is empty!');
    }

    const mongoDbConnection: Connection = await createConnection({
        name: 'mongodb',
        type: 'mongodb',
        host: server,
        port: port,
        username: username,
        password: password,
        database: database,
        ssl: ssl,
        synchronize: changeDatabase,
        logging: true,
        entities: [TaskResultEntity, CodeGeneration],
    });

    const taskResultDao: TaskResultDao = new TaskResultDaoImpl(mongoDbConnection);
    for (const taskResult of taskResults) {
        await taskResultDao.put(pipelineBuildId, taskResult);
    }

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(
        mongoDbConnection
    );
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

    await mongoDbConnection.close();
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
                ? `${pipelineBuildId}/${sdkGenerationName}-${stepName}-result.json`
                : `${pipelineBuildId}/${sdkGenerationName}-${stepName}.log`;
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

export async function uploadGenerateAndBuildFile(
    generateAndBuildOutputFile: string,
    azureStorageBlobSasUrl: string,
    azureBlobContainerName: string,
    language: string,
    sdkGenerationName: string,
    pipelineBuildId: string,
): Promise<{ hasFailedResult: boolean }> {
    const res = { hasFailedResult: false };
    if (!fs.existsSync(generateAndBuildOutputFile)) return res;
    if (azureStorageBlobSasUrl === undefined) {
        throw new Error('azureStorageBlobSasUrl  is empty!');
    }
    if (azureBlobContainerName === undefined) {
        throw new Error('azureBlobContainerName  is empty!');
    }
    if (language === undefined) {
        throw new Error('language  is empty!');
    }
    if (sdkGenerationName === undefined) {
        throw new Error('sdkGenerationName  is empty!');
    }
    if (pipelineBuildId === undefined) {
        throw new Error('pipelineBuildId  is empty!');
    }

    const generateAndBuildOutputJson = getGenerateAndBuildOutput(
        requireJsonc(generateAndBuildOutputFile)
    );
    const allPackageFolders: string[] = [];
    for (const p of generateAndBuildOutputJson.packages) {
        const result = p.result;
        if (result === 'failed') {
            res.hasFailedResult = true;
            continue;
        }
        const packageName = p.packageName;
        const paths = p.path;
        const packageFolder = p.packageFolder;
        const changelog = p.changelog;
        const artifacts = p.artifacts;

        allPackageFolders.push(packageFolder);
        // upload generated codes in packageFolder
        const azureBlobClient = new AzureBlobClient(
            azureStorageBlobSasUrl,
            azureBlobContainerName
        );
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

        for (const artifact of artifacts) {
            const artifactName = path.basename(artifact);
            await azureBlobClient.publishBlob(
                artifact,
                `${pipelineBuildId}/${language}/${artifactName}`
            );
        }
    }

    return res;
}
