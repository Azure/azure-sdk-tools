import * as fs from 'fs';
import { Connection, createConnection } from 'typeorm';

import { CodeGeneration } from '../types/codeGeneration';
import { AzureSDKTaskName } from '../types/commonType';
import { PipelineRunEvent } from '../types/events';
import { TaskResult, TaskResultEntity } from '../types/taskResult';
import { AzureBlobClient } from '../utils/blob/AzureBlobClient';
import { CodeGenerationDao } from '../utils/db/codeGenerationDao';
import { CodeGenerationDaoImpl } from '../utils/db/codeGenerationDaoImpl';
import { TaskResultDao } from '../utils/db/taskResultDao';
import { TaskResultDaoImpl } from '../utils/db/taskResultDaoImpl';
import { EventHubProducer } from '../utils/eventhub/EventHubProducer';
import { logger } from '../utils/logger';

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
    private context: MongoConnectContext;

    constructor(conetxt: MongoConnectContext) {
        this.context = conetxt;
    }

    public async close() {
        await this.connection.close();
    }

    public async connectDB() {
        this.connection = await createConnection({
            name: 'mongodb',
            type: 'mongodb',
            host: this.context.host,
            port: this.context.port,
            username: this.context.username,
            password: this.context.password,
            database: this.context.database,
            ssl: this.context.ssl,
            synchronize: this.context.synchronize,
            logging: this.context.logging,
            entities: [TaskResultEntity, CodeGeneration]
        });
    }

    public async sendSdkTaskResultToDB(pipelineBuildId: string, taskResults: TaskResult[]) {
        if (!pipelineBuildId) {
            throw new Error('Invalid pipelineBuildId!');
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
                const blobName: string = file.includes('.json') ?
                    `${this.pipelineBuildId}/logs/${this.sdkGenerationName}-${taskName}-result.json` :
                    `${this.pipelineBuildId}/logs/${this.sdkGenerationName}-${taskName}.log`;
                await this.azureBlobClient.publishBlob(file, blobName);
                logger.info(`Publish ${file} Success !!!`);
            } else {
                logger.error(`Publish result failed !, ${file} is missed`);
            }
        }
    }
}

export class ResultEventhubPublisher {
    private producer: EventHubProducer;

    constructor(eventHubConnectionString: string) {
        this.producer = new EventHubProducer(eventHubConnectionString);
    }

    public async publishEvent(event: PipelineRunEvent, partitionKey?: string): Promise<void> {
        try {
            await this.producer.send([JSON.stringify(event)], partitionKey);
        } catch (e) {
            await this.producer.close();
            logger.error('Failed to send pipeline result:', JSON.stringify(event), e);
            throw e;
        }
    }

    public async close() {
        await this.producer.close();
    }
}
