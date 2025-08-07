import { InvocationContext} from '@azure/functions';
import { BlobServiceClient, ContainerClient } from '@azure/storage-blob';
import { ChainedTokenCredential, DefaultAzureCredential, AzureCliCredential, EnvironmentCredential, ManagedIdentityCredential } from '@azure/identity';

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
     * List all blobs in a container
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
     * Delete multiple blobs that are not in the current files list
     */
    async deleteExpiredBlobs(context: InvocationContext, containerName: string, currentFiles: string[]): Promise<number> {
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
                    await this.deleteBlob(context, containerName, blobPath);
                    deletedCount++;
                }
            }
            
            return deletedCount;
        } catch (error) {
            throw new Error(`Failed to delete expired blobs: ${error instanceof Error ? error.message : 'Unknown error'}`);
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
