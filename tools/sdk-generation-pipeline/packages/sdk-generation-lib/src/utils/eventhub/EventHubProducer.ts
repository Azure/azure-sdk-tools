// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License in the project root for license information.
import {
    CreateBatchOptions,
    EventDataBatch,
    EventHubProducerClient
} from '@azure/event-hubs';

import { logger } from '../logger';

export class EventHubProducer {
    private producer: EventHubProducerClient;
    constructor(sasUrl: string) {
        this.producer = new EventHubProducerClient(sasUrl);
    }

    private async createBatch(partitionKey?: string) {
        const batchOptions: CreateBatchOptions = {};
        if (partitionKey) {
            batchOptions.partitionKey = partitionKey;
        }

        return await this.producer.createBatch(batchOptions);
    }

    private async* getBatchIterator(events: string[], partitionKey?: string) {
        let toAddIndex = 0;
        if (toAddIndex >= events.length) {
            return;
        }
        const batch = await this.createBatch(partitionKey);

        while (toAddIndex < events.length) {
            const success = batch.tryAdd({ body: events[toAddIndex] });
            if (!success) {
                if (batch.count > 0) {
                    // batch is full
                    break;
                } else {
                    // none event in batch, maybe the event is too large
                    throw new Error(
                        `Failed to add ${JSON.stringify(
                            events[toAddIndex]
                        )} to batch`
                    );
                }
            }
            ++toAddIndex;
        }
        yield batch;
    }

    public async send(events: string[], partitionKey?: string) {
        logger.info(`events to send: ${events}`);
        const batchIterator = this.getBatchIterator(events, partitionKey);
        let next = await batchIterator.next();
        while (!next.done) {
            if (next.value !== undefined) {
                const batch: EventDataBatch = next.value as EventDataBatch;
                await this.producer.sendBatch(batch);
            }
            next = await batchIterator.next();
        }
        logger.info('Send events done');
    }

    public async close() {
        try {
            await this.producer.close();
        } catch (err) {
            logger.error('Error when closing client: ', err);
        } // swallow the error
    }
}
