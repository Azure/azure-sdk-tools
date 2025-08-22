import { TableClient, TableEntity, odata } from "@azure/data-tables";
import {
    ChainedTokenCredential,
    ManagedIdentityCredential,
    EnvironmentCredential,
    AzureCliCredential,
} from "@azure/identity";

export class TableService {
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
    async queryEntities<T extends TableEntity>(queryOptions: {
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
}
