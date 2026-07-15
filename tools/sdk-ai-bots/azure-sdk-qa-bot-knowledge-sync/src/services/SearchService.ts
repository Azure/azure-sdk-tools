import { InvocationContext } from '@azure/functions';
import { SearchClient, SearchIndexerClient } from '@azure/search-documents';
import { RestError } from '@azure/core-rest-pipeline';
import { ChainedTokenCredential, AzureCliCredential, ManagedIdentityCredential, WorkloadIdentityCredential} from '@azure/identity';
import { StatusCodes } from 'http-status-codes';

/**
 * Document interface for AI Search operations
 */
export interface SearchDocument {
    chunk_id: string;
    title: string;
}

/**
 * Azure AI Search service for managing document chunks
 */
export class SearchService {
    private searchClient: SearchClient<SearchDocument>;
    private indexerClient: SearchIndexerClient;
    private readonly indexerName?: string;
    
    constructor() {
        const searchServiceName = process.env.AI_SEARCH_SERVICE_NAME;
        const searchIndexName = process.env.AI_SEARCH_INDEX;
        this.indexerName = process.env.AI_SEARCH_INDEXER;
        
        if (!searchServiceName || !searchIndexName) {
            throw new Error('AI_SEARCH_SERVICE_NAME and AI_SEARCH_INDEX environment variables are required');
        }

        const endpoint = `https://${searchServiceName}.search.windows.net`;

        // Use managed identity authentication
        const credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),
            new AzureCliCredential(),
            new WorkloadIdentityCredential()
        );

        this.searchClient = new SearchClient(
            endpoint,
            searchIndexName,
            credential
        );
        this.indexerClient = new SearchIndexerClient(endpoint, credential);
    }

    /**
     * Search for documents by title
     * @param title The title to search for
     * @param context Invocation context for logging
     * @returns Array of document IDs that match the title
     */
    async searchDocumentsByTitle(title: string): Promise<string[]> {
        try {
            const searchResults = await this.searchClient.search(title, {
                searchFields: ['title'],
                searchMode: 'all'
            });

            const documentIds: string[] = [];
            for await (const result of searchResults.results) {
                if (result.document.chunk_id) {
                    documentIds.push(result.document.chunk_id);
                }
            }

            console.log(`Found ${documentIds.length} documents with title: ${title}`);
            return documentIds;
        } catch (error) {
            console.error(`Error searching documents by title "${title}":`, error);
            throw new Error(`Failed to search documents by title: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    /**
     * Delete documents by their IDs
     * @param documentIds Array of document IDs to delete
     * @param context Invocation context for logging
     */
    async deleteDocuments(documentIds: string[]): Promise<void> {
        if (documentIds.length === 0) {
            console.log('No documents to delete');
            return;
        }

        try {
            // Create documents for deletion (only need the key field, but TypeScript requires all fields)
            const documentsToDelete = documentIds.map(id => ({ 
                chunk_id: id
            } as SearchDocument));
            
            const deleteResult = await this.searchClient.deleteDocuments(documentsToDelete);
            
            let successCount = 0;
            let failureCount = 0;
            
            for (const result of deleteResult.results) {
                if (result.succeeded) {
                    successCount++;
                } else {
                    failureCount++;
                    console.error(`Failed to delete document ${result.key}:`, result.errorMessage);
                }
            }

            console.log(`Successfully deleted ${successCount} documents, ${failureCount} failures`);

            if (failureCount > 0) {
                throw new Error(`Failed to delete ${failureCount} out of ${documentIds.length} documents`);
            }
        } catch (error) {
            console.error('Error deleting documents:', error);
            throw new Error(`Failed to delete documents: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    /**
     * Delete all document chunks for a given title
     * @param fileName The document name whose chunks should be deleted
     * @param context Invocation context for logging
     */
    async deleteDocumentChunksByFileName(fileName: string): Promise<void> {
        if (fileName.length === 0) {
            console.log(`No file name provided for chunk deletion: ${fileName}`);
            return;
        }
        try {
            console.log(`Deleting all chunks for document: ${fileName}`);

            // First, search for all documents with this title
            const documentIds = await this.searchDocumentsByTitle(fileName);
            
            if (documentIds.length === 0) {
                console.log(`No existing chunks found for: ${fileName}`);
                return;
            }
            
            // Delete all found documents
            await this.deleteDocuments(documentIds);

            console.log(`Successfully deleted all chunks for: ${fileName}`);
        } catch (error) {
            console.error(`Error deleting chunks for "${fileName}":`, error);
            throw error;
        }
    }

    /**
     * Trigger the Azure AI Search indexer to reindex the knowledge base.
     * This should be called after blob storage has been updated so that the
     * indexer picks up newly added/changed documents and refreshes the index.
     */
    async runIndexer(): Promise<void> {
        if (!this.indexerName) {
            console.warn('AI_SEARCH_INDEXER environment variable is not set; skipping reindex trigger');
            return;
        }

        try {
            console.log(`Triggering AI Search indexer to reindex the knowledge base: ${this.indexerName}`);
            await this.indexerClient.runIndexer(this.indexerName);
            console.log(`Successfully triggered AI Search indexer: ${this.indexerName}`);
        } catch (error) {
            // A conflict means the indexer is already running; treat as non-fatal.
            if (error instanceof RestError && error.statusCode === StatusCodes.CONFLICT) {
                console.warn(`AI Search indexer "${this.indexerName}" is already running; a new run will pick up the latest changes`);
                return;
            }
            console.error(`Error triggering AI Search indexer "${this.indexerName}":`, error);
            throw new Error(`Failed to trigger AI Search indexer: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }
}
