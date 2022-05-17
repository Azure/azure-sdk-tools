import * as fs from 'fs';

import { Connection, createConnection } from 'typeorm';
import { AzureBlobClient } from '../utils/blob/AzureBlobClient';
import { AzureSDKTaskName, SDKPipelineStatus } from '../types/commonType';
import { CodeGeneration } from '../types/codeGeneration';
import { CodeGenerationDao } from '../utils/db/codeGenerationDao';
import { CodeGenerationDaoImpl } from '../utils/db/codeGenerationDaoImpl';
import { EventHubProducer } from '../utils/eventhub/EventHubProducer';
import { logger } from '../utils/logger';
import { PipelineRunEvent } from '../types/events';
import { TaskResult, TaskResultEntity } from '../types/taskResult';
import { TaskResultDao } from '../utils/db/taskResultDao';
import { TaskResultDaoImpl } from '../utils/db/taskResultDaoImpl';

export type MongoConnectContext = {
    name: string;
    type: string;
    host: string;
    port: number;
    username: string;
    password: string;
    database: string;
    ssl: boolean;
    synchronize: boolean;
    logging: boolean;
};

export class ResultDBPublisher {
    private connection: Connection;

    public async close() {
        await this.connection.close();
    }

    public async connectDB(context: MongoConnectContext) {
        this.connection = await createConnection({
            name: 'mongodb',
            type: 'mongodb',
            host: context.host,
            port: context.port,
            username: context.username,
            password: context.password,
            database: context.database,
            ssl: context.ssl,
            synchronize: context.synchronize,
            logging: context.logging,
            entities: [TaskResultEntity, CodeGeneration],
        });
    }

    public async sendSdkTaskResultToDB(pipelineBuildId: string, taskResults: TaskResult[]) {
        if (pipelineBuildId === undefined) {
            throw new Error('pipelineBuildId is empty!');
        }

        const taskResultDao: TaskResultDao = new TaskResultDaoImpl(this.connection);
        for (const taskResult of taskResults) {
            await taskResultDao.put(pipelineBuildId, taskResult);
        }
    }

    public async sendSdkGenerationToDB(cg: CodeGeneration) {
        const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(this.connection);

        await codeGenerationDao.submitCodeGeneration(cg);
    }
}

export type BlobBasicContext = {
    pipelineBuildId: string;
    sdkGenerationName: string;
    azureStorageBlobSasUrl: string;
    azureBlobContainerName: string;
};

export class ResultBlobPublisher {
    private pipelineBuildId: string;
    private sdkGenerationName: string;
    private azureBlobClient: AzureBlobClient;

    constructor(conetxt: BlobBasicContext) {
        this.pipelineBuildId = conetxt.pipelineBuildId;
        this.sdkGenerationName = conetxt.sdkGenerationName;
        this.azureBlobClient = new AzureBlobClient(conetxt.azureStorageBlobSasUrl, conetxt.azureBlobContainerName);
    }

    public async uploadLogsAndResult(logsAndResultPath: string, taskNameInput: AzureSDKTaskName) {
        let taskName: string = taskNameInput;

        if (!taskNameInput) {
            logger.info(`taskName is undefined or null, set full`);
            taskName = 'full';
        }

        const logsAndResultPathArray = JSON.parse(logsAndResultPath);
        for (const file of logsAndResultPathArray) {
            if (fs.existsSync(file)) {
                let blobName: string = file.includes('.json')
                    ? `${this.pipelineBuildId}/logs/${this.sdkGenerationName}-${taskName}-result.json`
                    : `${this.pipelineBuildId}/logs/${this.sdkGenerationName}-${taskName}.log`;
                await this.azureBlobClient.publishBlob(file, blobName);
                logger.info(`Publish ${file} Success !!!`);
            } else {
                logger.error(`Publish result failed !, ${file} is missed`);
            }
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
