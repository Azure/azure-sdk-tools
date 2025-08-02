export interface ResourceRequirement {
    type: string; // ARM resource type, e.g. "Microsoft.Storage/storageAccounts"
    apiVersion: string; // e.g. "2022-09-01"
    // any ARM template defaults
    defaults?: Record<string, any>;
}

export const clientToResource: Record<string, ResourceRequirement[]> = {
    // Azure Storage
    BlobServiceClient: [
        {
            type: "Microsoft.Storage/storageAccounts",
            apiVersion: "2022-09-01",
            defaults: { sku: "Standard_LRS", kind: "StorageV2" },
        },
        {
            type: "Microsoft.Storage/storageAccounts/blobServices/containers",
            apiVersion: "2022-09-01",
        },
    ],
    // Cosmos DB
    CosmosClient: [
        {
            type: "Microsoft.DocumentDB/databaseAccounts",
            apiVersion: "2021-07-01-preview",
        },
    ],
    TextAnalysisClient: [
        {
            type: "Microsoft.CognitiveServices/accounts",
            apiVersion: "2023-05-01",
            defaults: { sku: "S0", kind: "TextAnalytics" },
        },
    ],
    // Add more mappings as you support more SDK clients...
};
