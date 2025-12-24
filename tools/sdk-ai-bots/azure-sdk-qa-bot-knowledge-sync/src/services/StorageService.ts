import {
    ContainerClient,
    BlobServiceClient,
    BlobItem,
    BlobDownloadResponseParsed,
} from "@azure/storage-blob";
import {
    ChainedTokenCredential,
    ManagedIdentityCredential,
    AzureCliCredential,
    WorkloadIdentityCredential,
} from "@azure/identity";
import * as crypto from "crypto";

/**
 * Azure Storage service for managing blob operations
 * Uses Managed Identity for secure authentication
 */
export class BlobService {
    private blobServiceClient: BlobServiceClient;
    private blobContainerClient: ContainerClient;

    constructor() {
        const storageAccountName = process.env.STORAGE_ACCOUNT_NAME;
        if (!storageAccountName) {
            throw new Error(
                "STORAGE_ACCOUNT_NAME environment variable is required"
            );
        }

        // Use ChainedTokenCredential for better fallback options
        const credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),
            new AzureCliCredential(),
            new WorkloadIdentityCredential()
        );

        const accountUrl = `https://${storageAccountName}.blob.core.windows.net`;

        this.blobServiceClient = new BlobServiceClient(accountUrl, credential);

        const containerName = process.env.STORAGE_KNOWLEDGE_CONTAINER;
        if (!containerName) {
            throw new Error(
                "STORAGE_KNOWLEDGE_CONTAINER environment variable is required"
            );
        }

        this.blobContainerClient =
            this.blobServiceClient.getContainerClient(containerName);
    }

    /**
     * Upload content to blob storage
     */
    async putBlob(
        blobPath: string,
        content: Buffer | string
    ): Promise<void> {
        try {
            const blockBlobClient =
                this.blobContainerClient.getBlockBlobClient(blobPath);

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
            console.log(`Uploaded ${blobPath} to blob storage`);
        } catch (error) {
            throw new Error(
                `Failed to upload blob ${blobPath}: ${
                    error instanceof Error ? error.message : "Unknown error"
                }`
            );
        }
    }

    /**
     * List all blobs in a container
     */
    async listBlobs(
        prefix?: string
    ): Promise<Map<string, BlobItem>> {
        try {
            const containerClient = this.blobContainerClient;
            const blobs = new Map<string, BlobItem>();

            const listOptions = prefix
                ? { prefix, includeMetadata: true }
                : { includeMetadata: true };

            for await (const blob of containerClient.listBlobsFlat(
                listOptions
            )) {
                blobs.set(blob.name, blob);
            }
            console.log(`Listed ${blobs.size} blobs with properties`);
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
     * Delete a blob from storage (soft delete using metadata)
     */
    async deleteBlob(
        blobPath: string
    ): Promise<void> {
        try {
            const blockBlobClient =
                this.blobContainerClient.getBlockBlobClient(blobPath);

            // Set metadata to mark blob as deleted (soft delete)
            await blockBlobClient.setMetadata({
                IsDeleted: "true",
            });

            console.log(`Deleted blob ${blobPath}`);
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
        blobName: string
    ): Promise<BlobDownloadResponseParsed> {
        // Get blob client
        const containerClient = this.blobContainerClient;

        // Use the latest version if found, otherwise use the default
        const blobClient = containerClient.getBlobClient(blobName);

        // Check if blob exists
        const exists = await blobClient.exists();
        if (!exists) {
            throw new Error(
                `Blob ${blobName} not found in container ${this.blobContainerClient.containerName}`
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
        return crypto.createHash("md5").update(content).digest("base64");
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
            console.log(`Blob ${blobPath} is new or missing MD5, needs upload`);
            return true;
        }

        // Check if the blob has a deletion flag in metadata
        if (existing.metadata && existing.metadata.IsDeleted === "true") {
            console.log(`Blob ${blobPath} has deletion flag, needs re-upload to override`);
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
