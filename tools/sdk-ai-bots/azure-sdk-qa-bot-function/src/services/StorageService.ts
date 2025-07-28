import { BlobServiceClient, ContainerClient } from '@azure/storage-blob';
import { DefaultAzureCredential } from '@azure/identity';

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
        
        // Use Managed Identity for authentication (Azure best practice)
        const credential = new DefaultAzureCredential();
        const accountUrl = `https://${storageAccountName}.blob.core.windows.net`;
        
        this.blobServiceClient = new BlobServiceClient(accountUrl, credential);
    }
    
    /**
     * Upload content to blob storage
     */
    async putBlob(containerName: string, blobPath: string, content: Buffer | string): Promise<void> {
        // try {
        //     const containerClient = this.blobServiceClient.getContainerClient(containerName);
        //     const blockBlobClient = containerClient.getBlockBlobClient(blobPath);
            
        //     const uploadOptions = {
        //         blobHTTPHeaders: {
        //             blobContentType: this.getContentType(blobPath)
        //         }
        //     };
            
        //     await blockBlobClient.upload(content, Buffer.byteLength(content.toString()), uploadOptions);
        // } catch (error) {
        //     throw new Error(`Failed to upload blob ${blobPath}: ${error instanceof Error ? error.message : 'Unknown error'}`);
        // }
    }
    
    /**
     * List all blobs in a container
     */
    async listBlobs(containerName: string, prefix?: string): Promise<string[]> {
        try {
            const containerClient = this.blobServiceClient.getContainerClient(containerName);
            const blobs: string[] = [];
            
            const listOptions = prefix ? { prefix } : undefined;
            
            for await (const blob of containerClient.listBlobsFlat(listOptions)) {
                // blobs.push(blob.name);
            }
            
            return blobs;
        } catch (error) {
            throw new Error(`Failed to list blobs: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }
    
    /**
     * Delete a blob from storage
     */
    async deleteBlob(containerName: string, blobPath: string): Promise<void> {
        // try {
        //     const containerClient = this.blobServiceClient.getContainerClient(containerName);
        //     const blockBlobClient = containerClient.getBlockBlobClient(blobPath);
            
        //     await blockBlobClient.deleteIfExists();
        // } catch (error) {
        //     throw new Error(`Failed to delete blob ${blobPath}: ${error instanceof Error ? error.message : 'Unknown error'}`);
        // }
    }
    
    /**
     * Delete multiple blobs that are not in the current files list
     */
    async deleteExpiredBlobs(containerName: string, currentFiles: string[]): Promise<number> {
        try {
            const allBlobs = await this.listBlobs(containerName);
            const currentFileSet = new Set(currentFiles);
            
            let deletedCount = 0;
            for (const blobPath of allBlobs) {
                // Skip static files (matching Go logic)
                if (blobPath.startsWith('static_')) {
                    continue;
                }
                
                // Delete if not in current files
                if (!currentFileSet.has(blobPath)) {
                    await this.deleteBlob(containerName, blobPath);
                    deletedCount++;
                }
            }
            
            return deletedCount;
        } catch (error) {
            throw new Error(`Failed to delete expired blobs: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }
    
    /**
     * Ensure container exists
     */
    async ensureContainer(containerName: string): Promise<void> {
        try {
            const containerClient = this.blobServiceClient.getContainerClient(containerName);
            await containerClient.createIfNotExists();
        } catch (error) {
            throw new Error(`Failed to create container ${containerName}: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
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
