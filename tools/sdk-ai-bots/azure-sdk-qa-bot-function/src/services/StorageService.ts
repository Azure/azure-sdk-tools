import { InvocationContext} from '@azure/functions';
import { BlobServiceClient, BlobItem } from '@azure/storage-blob';
import { ChainedTokenCredential, ManagedIdentityCredential, EnvironmentCredential, AzureCliCredential } from '@azure/identity';
import * as crypto from 'crypto';

/**
 * Azure Storage service for managing blob operations
 * Uses Managed Identity for secure authentication
 */
export class StorageService {
    private blobServiceClient: BlobServiceClient;
    private containerName: string;
    
    constructor() {
        const storageAccountName = process.env.AZURE_STORAGE_ACCOUNT_NAME;
        if (!storageAccountName) {
            throw new Error('AZURE_STORAGE_ACCOUNT_NAME environment variable is required');
        }

        this.containerName = process.env.STORAGE_KNOWLEDGE_CONTAINER || 'knowledge';

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
    async putBlob(context: InvocationContext, containerName: string, blobPath: string, content: Buffer | string): Promise<void> {
        try {
            const containerClient = this.blobServiceClient.getContainerClient(containerName);
            const blockBlobClient = containerClient.getBlockBlobClient(blobPath);
            
            const uploadOptions = {
                blobHTTPHeaders: {
                    blobContentType: this.getContentType(blobPath)
                }
            };
            
            await blockBlobClient.upload(content, Buffer.byteLength(content.toString()), uploadOptions);
            context.log(`Uploaded ${blobPath} to blob storage`);
        } catch (error) {
            throw new Error(`Failed to upload blob ${blobPath}: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }
    
    /**
     * List all blobs in a container with their properties including contentMD5
     */
    async listBlobsWithProperties(containerName: string, prefix?: string): Promise<Map<string, BlobItem>> {
        try {
            const containerClient = this.blobServiceClient.getContainerClient(containerName);
            const blobs = new Map<string, BlobItem>();
            
            const listOptions = prefix ? { prefix, includeMetadata: true } : { includeMetadata: true };
            
            for await (const blob of containerClient.listBlobsFlat(listOptions)) {
                blobs.set(blob.name, blob);
            }
            
            return blobs;
        } catch (error) {
            throw new Error(`Failed to list blobs with properties: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    /**
     * List all blob names in a container
     */
    async listBlobs(containerName: string, prefix?: string): Promise<string[]> {
        try {
            const containerClient = this.blobServiceClient.getContainerClient(containerName);
            const blobs: string[] = [];
            
            const listOptions = prefix ? { prefix } : undefined;
            
            for await (const blob of containerClient.listBlobsFlat(listOptions)) {
                blobs.push(blob.name);
            }
            
            return blobs;
        } catch (error) {
            throw new Error(`Failed to list blobs: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    /**
     * Delete a blob from storage (soft delete using metadata)
     */
    async deleteBlob(context: InvocationContext, containerName: string, blobPath: string): Promise<void> {
        try {
            const containerClient = this.blobServiceClient.getContainerClient(containerName);
            const blockBlobClient = containerClient.getBlockBlobClient(blobPath);
            
            // Set metadata to mark blob as deleted (soft delete)
            await blockBlobClient.setMetadata({
                IsDeleted: 'true'
            });
            
            context.log(`Deleted blob ${blobPath}`);
        } catch (error) {
            throw new Error(`Failed to delete blob ${blobPath}: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    /**
     * Calculate MD5 hash of content
     * @param content The content to hash
     * @returns MD5 hash as base64 string (matching Azure's contentMD5 format)
     */
    private calculateContentMD5(content: string | Buffer): string {
        const buffer = typeof content === 'string' ? Buffer.from(content) : content;
        return crypto.createHash('md5').update(buffer).digest('base64');
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
        const existingMD5 = Buffer.from(existing.properties.contentMD5).toString('base64');
        
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
        const nameWithoutExtension = name.replace(/\.[^/.]+$/, '');
        return nameWithoutExtension.replace(/[/#]/g, ' ').trim();
    }
    
    /**
     * Get content type based on file extension
     */
    private getContentType(fileName: string): string {
        const extension = fileName.toLowerCase().split('.').pop();
        
        switch (extension) {
            case 'md':
            case 'mdx':
                return 'text/markdown';
            case 'txt':
                return 'text/plain';
            case 'json':
                return 'application/json';
            case 'html':
                return 'text/html';
            default:
                return 'application/octet-stream';
        }
    }
}
