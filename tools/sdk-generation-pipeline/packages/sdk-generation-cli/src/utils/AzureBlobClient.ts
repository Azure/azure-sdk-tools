// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License in the project root for license information.
import { BlobServiceClient } from '@azure/storage-blob';
import {logger} from "@azure-tools/sdk-generation-lib";

export class AzureBlobClient {
  private blobServiceClient: BlobServiceClient;
  private containerName: string;
  constructor(sasURL: string, containerName: string) {
    this.blobServiceClient = new BlobServiceClient(sasURL);
    this.containerName = containerName;
  }

  public async uploadLocal(
    path: string,
    blobName: string,
    containerName: string = this.containerName,
    createContainer: boolean = true
  ) {
    const containerClient = this.blobServiceClient.getContainerClient(
      containerName
    );
    if (createContainer && !(await containerClient.exists())) {
      const resp = await containerClient.create();
      if (resp.errorCode) {
        throw new Error(
          `Failed to create container for ${containerName}:${resp}`
        );
      }
    }

    const blockBlobClient = containerClient.getBlockBlobClient(blobName);
    logger.info(`Uploading ${path} to ${containerName}/${blobName}`);
    const resp = await blockBlobClient.uploadFile(path);
    if (resp.errorCode) {
      throw new Error(`Failed to upload ${path}:${resp}`);
    }

    return resp;
  }
}
