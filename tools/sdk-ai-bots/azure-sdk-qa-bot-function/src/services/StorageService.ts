import { InvocationContext } from "@azure/functions";
import {
    BlobServiceClient,
    BlobItem,
    BlobDownloadResponseParsed,
} from "@azure/storage-blob";
import {
    ChainedTokenCredential,
    ManagedIdentityCredential,
    EnvironmentCredential,
    AzureCliCredential,
} from "@azure/identity";
import * as crypto from "crypto";
import { TableClient, TableEntity } from "@azure/data-tables";

/**
 * Azure Storage service for managing blob operations
 * Uses Managed Identity for secure authentication
 */
export class BlobService {
    private blobServiceClient: BlobServiceClient;
    private containerName: string;

    constructor() {
        const storageAccountName = process.env.AZURE_STORAGE_ACCOUNT_NAME;
        if (!storageAccountName) {
            throw new Error(
                "AZURE_STORAGE_ACCOUNT_NAME environment variable is required"
            );
        }

        this.containerName =
            process.env.STORAGE_KNOWLEDGE_CONTAINER || "knowledge";

        // Use ChainedTokenCredential for better fallback options
        const credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),
            new EnvironmentCredential(),
            new AzureCliCredential()
        );

        const accountUrl = `https://${storageAccountName}.blob.core.windows.net`;

        this.blobServiceClient = new BlobServiceClient(accountUrl, credential);
    }

    /**
     * Upload content to blob storage
     */
    async putBlob(
        context: InvocationContext,
        containerName: string,
        blobPath: string,
        content: Buffer | string
    ): Promise<void> {
        try {
            const containerClient =
                this.blobServiceClient.getContainerClient(containerName);
            const blockBlobClient =
                containerClient.getBlockBlobClient(blobPath);

            const uploadOptions = {
                blobHTTPHeaders: {
                    blobContentType: this.getContentType(blobPath),
                },
            };

            await blockBlobClient.upload(
                content,
                Buffer.byteLength(content.toString()),
                uploadOptions
            );
            context.log(`Uploaded ${blobPath} to blob storage`);
        } catch (error) {
            throw new Error(
                `Failed to upload blob ${blobPath}: ${
                    error instanceof Error ? error.message : "Unknown error"
                }`
            );
        }
    }

    /**
     * List all blobs in a container with their properties including contentMD5
     */
    async listBlobsWithProperties(
        containerName: string,
        prefix?: string
    ): Promise<Map<string, BlobItem>> {
        try {
            const containerClient =
                this.blobServiceClient.getContainerClient(containerName);
            const blobs = new Map<string, BlobItem>();

            const listOptions = prefix
                ? { prefix, includeMetadata: true }
                : { includeMetadata: true };

            for await (const blob of containerClient.listBlobsFlat(
                listOptions
            )) {
                blobs.set(blob.name, blob);
            }

            return blobs;
        } catch (error) {
            throw new Error(
                `Failed to list blobs with properties: ${
                    error instanceof Error ? error.message : "Unknown error"
                }`
            );
        }
    }

    /**
     * List all blob names in a container
     */
    async listBlobs(containerName: string, prefix?: string): Promise<string[]> {
        try {
            const containerClient =
                this.blobServiceClient.getContainerClient(containerName);
            const blobs: string[] = [];

            const listOptions = prefix ? { prefix } : undefined;

            for await (const blob of containerClient.listBlobsFlat(
                listOptions
            )) {
                blobs.push(blob.name);
            }

            return blobs;
        } catch (error) {
            throw new Error(
                `Failed to list blobs: ${
                    error instanceof Error ? error.message : "Unknown error"
                }`
            );
        }
    }

    /**
     * Delete a blob from storage (soft delete using metadata)
     */
    async deleteBlob(
        context: InvocationContext,
        containerName: string,
        blobPath: string
    ): Promise<void> {
        try {
            const containerClient =
                this.blobServiceClient.getContainerClient(containerName);
            const blockBlobClient =
                containerClient.getBlockBlobClient(blobPath);

            // Set metadata to mark blob as deleted (soft delete)
            await blockBlobClient.setMetadata({
                IsDeleted: "true",
            });

            context.log(`Deleted blob ${blobPath}`);
        } catch (error) {
            throw new Error(
                `Failed to delete blob ${blobPath}: ${
                    error instanceof Error ? error.message : "Unknown error"
                }`
            );
        }
    }

    /**
     * Download a blob from Azure Blob Storage
     * @param containerName The name of the container
     * @param blobName The name of the blob
     * @returns The downloaded blob content
     */
    async downloadBlob(
        containerName: string,
        blobName: string
    ): Promise<BlobDownloadResponseParsed> {
        // Get blob client
        const containerClient =
            this.blobServiceClient.getContainerClient(containerName);

        // Use the latest version if found, otherwise use the default
        const blobClient = containerClient.getBlobClient(blobName);

        // Check if blob exists
        const exists = await blobClient.exists();
        if (!exists) {
            throw new Error(
                `Blob ${blobName} not found in container ${containerName}`
            );
        }

        // Download blob content
        const downloadResponse = await blobClient.download(0, undefined, {
            conditions: {},
            customerProvidedKey: undefined,
        });
        if (!downloadResponse.readableStreamBody) {
            throw new Error("Failed to download blob content");
        }

        return downloadResponse;
    }

    /**
     * Calculate MD5 hash of content
     * @param content The content to hash
     * @returns MD5 hash as base64 string (matching Azure's contentMD5 format)
     */
    private calculateContentMD5(content: string | Buffer): string {
        const buffer =
            typeof content === "string" ? Buffer.from(content) : content;
        return crypto.createHash("md5").update(buffer).digest("base64");
    }

    /**
     * Check if document content has changed by comparing MD5 hashes
     * @param blobPath The blob path to check
     * @param content The current content
     * @param existingBlobs Map of existing blob items with their properties
     * @returns True if content has changed or is new, false if unchanged
     */
    hasContentChanged(
        blobPath: string,
        content: string | Buffer,
        existingBlobs: Map<string, BlobItem>
    ): boolean {
        const currentMD5 = this.calculateContentMD5(content);
        const existing = existingBlobs.get(blobPath);

        if (!existing || !existing.properties.contentMD5) {
            // New blob or blob without MD5 (needs update)
            return true;
        }

        // Convert Uint8Array to base64 string for comparison
        const existingMD5 = Buffer.from(
            existing.properties.contentMD5
        ).toString("base64");

        // Check if content MD5 has changed
        return existingMD5 !== currentMD5;
    }

    /**
     * Get title from blob name or metadata
     * @param blobItem The blob item
     * @returns The title extracted from metadata or derived from name
     */
    getBlobTitle(blobItem: BlobItem): string | undefined {
        // Try to get title from metadata first
        if (blobItem.metadata && blobItem.metadata.title) {
            return blobItem.metadata.title;
        }

        // Fallback to deriving title from blob name
        // Remove file extension and replace path separators with spaces
        const name = blobItem.name;
        const nameWithoutExtension = name.replace(/\.[^/.]+$/, "");
        return nameWithoutExtension.replace(/[/#]/g, " ").trim();
    }

    /**
     * Get content type based on file extension
     */
    private getContentType(fileName: string): string {
        const extension = fileName.toLowerCase().split(".").pop();

        switch (extension) {
            case "md":
            case "mdx":
                return "text/markdown";
            case "txt":
                return "text/plain";
            case "json":
                return "application/json";
            case "html":
                return "text/html";
            default:
                return "application/octet-stream";
        }
    }
}

export class TableService {
    private readonly batchSize = 10000; // Azure Table Storage batch limit
    private readonly batchDelayMs = 1000; // 1 second delay between batches
    private tableClient: TableClient;

    constructor(tableName: string) {
        const storageAccountName = process.env.AZURE_STORAGE_ACCOUNT_NAME;
        if (!storageAccountName) {
            throw new Error(
                "AZURE_STORAGE_ACCOUNT_NAME environment variable is required"
            );
        }

        // Use ChainedTokenCredential for better fallback options
        const credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),
            new EnvironmentCredential(),
            new AzureCliCredential()
        );

        const accountUrl = `https://${storageAccountName}.table.core.windows.net`;

        this.tableClient = new TableClient(accountUrl, tableName, credential);
    }

    // Generic method to query entities with more complex filters
    public async queryEntities<T extends TableEntity>(queryOptions: {
        filter?: string;
        select?: string[];
        top?: number;
    }): Promise<T[]> {
        try {
            const entities: T[] = [];
            const entitiesIter = this.tableClient.listEntities<T>({
                queryOptions,
            });

            for await (const entity of entitiesIter) {
                entities.push(entity);
            }

            return entities;
        } catch (error) {
            console.error("Error querying entities:", error);
            throw error;
        }
    }

    public async batchUploadEntities<TEntity extends TableEntity>(
        entities: TEntity[]
    ): Promise<void> {
        const totalBatches = Math.ceil(entities.length / this.batchSize);

        for (let i = 0; i < entities.length; i += this.batchSize) {
            const batch = entities.slice(i, i + this.batchSize);
            const batchNumber = Math.floor(i / this.batchSize) + 1;

            console.log(
                `Uploading batch ${batchNumber}/${totalBatches} (${batch.length} entities)...`
            );

            // Group entities by partitionKey for batch transactions
            const partitionGroups = new Map<string, TEntity[]>();
            for (const entity of batch) {
                const key = entity.partitionKey;
                if (!partitionGroups.has(key)) {
                    partitionGroups.set(key, []);
                }
                partitionGroups.get(key)!.push(entity);
            }

            // Process each partition group as a separate transaction
            const transactionPromises: Promise<void>[] = [];
            for (const [partitionKey, groupEntities] of partitionGroups) {
                const transactionPromise = (async () => {
                    try {
                        await this.tableClient.submitTransaction(
                            groupEntities.map((entity) => ["upsert", entity])
                        );
                        // Transaction completed successfully
                    } catch (error) {
                        throw new Error(
                            `Failed to submit transaction for partition ${partitionKey}: ${
                                error instanceof Error
                                    ? error.message
                                    : "Unknown error"
                            }`
                        );
                    }
                })();
                transactionPromises.push(transactionPromise);
            }

            // Wait for all transactions in this batch to complete
            await Promise.all(transactionPromises);

            console.log(`Batch ${batchNumber} completed`);

            // Add delay between batches (except for the last batch)
            if (batchNumber < totalBatches) {
                console.log(
                    `Waiting ${this.batchDelayMs}ms before next batch...`
                );
                await new Promise((resolve) =>
                    setTimeout(resolve, this.batchDelayMs)
                );
            }
        }
    }
}
