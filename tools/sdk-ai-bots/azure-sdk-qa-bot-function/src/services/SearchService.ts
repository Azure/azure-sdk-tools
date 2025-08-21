import { InvocationContext } from '@azure/functions';
import { SearchClient, AzureKeyCredential } from '@azure/search-documents';
import { ChainedTokenCredential, DefaultAzureCredential, AzureCliCredential, EnvironmentCredential, ManagedIdentityCredential } from '@azure/identity';

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
    
    constructor() {
        const searchServiceName = process.env.AZURE_SEARCH_SERVICE_NAME;
        const searchIndexName = process.env.AZURE_SEARCH_INDEX_NAME;
        const searchApiKey = process.env.AZURE_SEARCH_API_KEY;
        
        if (!searchServiceName || !searchIndexName) {
            throw new Error('AZURE_SEARCH_SERVICE_NAME and AZURE_SEARCH_INDEX_NAME environment variables are required');
        }

        const endpoint = `https://${searchServiceName}.search.windows.net`;

        if (searchApiKey) {
            // Use API key authentication
            this.searchClient = new SearchClient(
                endpoint,
                searchIndexName,
                new AzureKeyCredential(searchApiKey)
            );
        } else {
            // Use managed identity authentication
            const credential = new ChainedTokenCredential(
                new ManagedIdentityCredential(),
                new EnvironmentCredential(),
                new AzureCliCredential()
            );
            
            this.searchClient = new SearchClient(
                endpoint,
                searchIndexName,
                credential
            );
        }
    }

    /**
     * Search for documents by title
     * @param title The title to search for
     * @param context Invocation context for logging
     * @returns Array of document IDs that match the title
     */
    async searchDocumentsByTitle(title: string, context: InvocationContext): Promise<string[]> {
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

            context.log(`Found ${documentIds.length} documents with title: ${title}`);
            return documentIds;
        } catch (error) {
            context.error(`Error searching documents by title "${title}":`, error);
            throw new Error(`Failed to search documents by title: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    /**
     * Delete documents by their IDs
     * @param documentIds Array of document IDs to delete
     * @param context Invocation context for logging
     */
    async deleteDocuments(documentIds: string[], context: InvocationContext): Promise<void> {
        if (documentIds.length === 0) {
            context.log('No documents to delete');
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
                    context.error(`Failed to delete document ${result.key}:`, result.errorMessage);
                }
            }
            
            context.log(`Successfully deleted ${successCount} documents, ${failureCount} failures`);
            
            if (failureCount > 0) {
                throw new Error(`Failed to delete ${failureCount} out of ${documentIds.length} documents`);
            }
        } catch (error) {
            context.error('Error deleting documents:', error);
            throw new Error(`Failed to delete documents: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    /**
     * Delete all document chunks for a given title
     * @param title The document title whose chunks should be deleted
     * @param context Invocation context for logging
     */
    async deleteDocumentChunksByTitle(title: string, context: InvocationContext): Promise<void> {
        try {
            context.log(`Deleting all chunks for document title: ${title}`);
            
            // First, search for all documents with this title
            const documentIds = await this.searchDocumentsByTitle(title, context);
            
            if (documentIds.length === 0) {
                context.log(`No existing chunks found for title: ${title}`);
                return;
            }
            
            // Delete all found documents
            await this.deleteDocuments(documentIds, context);
            
            context.log(`Successfully deleted all chunks for title: ${title}`);
        } catch (error) {
            context.error(`Error deleting chunks for title "${title}":`, error);
            throw error;
        }
    }
}
