// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License in the project root for license information.
import { BlobServiceClient } from '@azure/storage-blob';

import { logger } from '../logger';

export class AzureBlobClient {
    private blobServiceClient: BlobServiceClient;
    private containerName: string;
    constructor(sasURL: string, containerName: string) {
        this.blobServiceClient = new BlobServiceClient(sasURL);
        this.containerName = containerName;
    }

    public async publishBlob(
        filePath: string,
        blobName: string,
        createContainer: boolean = true
    ) {
        const containerClient =
            this.blobServiceClient.getContainerClient(this.containerName);
        if (createContainer && !(await containerClient.exists())) {
            const resp = await containerClient.create();
            if (resp.errorCode) {
                throw new Error(
                    `Failed to create container for ${this.containerName}:${resp}`
                );
            }
        }

        const blockBlobClient = containerClient.getBlockBlobClient(blobName);
        logger.info(`Uploading ${filePath} to ${this.containerName}/${blobName}`);
        const resp = await blockBlobClient.uploadFile(filePath);
        if (resp.errorCode) {
            throw new Error(`Failed to upload ${filePath}:${resp}`);
        }

        return resp;
    }
}
