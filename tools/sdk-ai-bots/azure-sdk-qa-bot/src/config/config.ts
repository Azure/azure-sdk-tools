const config = {
  // Teams app
  MicrosoftAppId: process.env.BOT_ID,
  MicrosoftAppType: process.env.BOT_TYPE,
  MicrosoftAppTenantId: process.env.BOT_TENANT_ID,
  MicrosoftAppPassword: process.env.BOT_PASSWORD,
  // RAG
  ragApiKey: process.env.RAG_API_KEY,
  // auth
  userManagedIdentityClientID: process.env.BOT_ID,
  ragScope: process.env.RAG_SERVICE_SCOPE, 
  // Computer Vision
  azureComputerVisionEndpoint: process.env.AZURE_COMPUTER_VISION_ENDPOINT,
  azureComputerVisionApiKey: process.env.AZURE_COMPUTER_VISION_API_KEY,
  // Azure Table Storage
  azureStorageUrl: process.env.AZURE_STORAGE_URL,
  azureTableNameForConversation: process.env.AZURE_TABLE_NAME_FOR_CONVERSATION,
  // Azure Blob Storage
  azureBlobStorageUrl: process.env.AZURE_BLOB_STORAGE_URL,
  blobContainerName: process.env.BLOB_CONTAINER_NAME,
  channelConfigBlobName: process.env.CHANNEL_CONFIG_BLOB_NAME,
  tenantConfigBlobName: process.env.TENANT_CONFIG_BLOB_NAME,
  // Fallback RAG config
  fallbackRagEndpoint: process.env.FALLBACK_RAG_ENDPOINT,
  fallbackRagTenant: process.env.FALLBACK_RAG_TENANT,
  // Local config
  isLocal: process.env.IS_LOCAL === 'true',
  // Local RAG config
  localBackendEndpoint: process.env.LOCAL_BACKEND_ENDPOINT,
  localRagTenant: process.env.LOCAL_RAG_TENANT,
};

// Validate required environment variables at startup
const requiredEnvVars = [
  { name: 'AZURE_BLOB_STORAGE_URL', value: config.azureBlobStorageUrl },
  { name: 'BLOB_CONTAINER_NAME', value: config.blobContainerName },
  { name: 'CHANNEL_CONFIG_BLOB_NAME', value: config.channelConfigBlobName },
  { name: 'TENANT_CONFIG_BLOB_NAME', value: config.tenantConfigBlobName },
  { name: 'FALLBACK_RAG_ENDPOINT', value: config.fallbackRagEndpoint },
  { name: 'FALLBACK_RAG_TENANT', value: config.fallbackRagTenant },
];

const missingVars = requiredEnvVars.filter((v) => !v.value).map((v) => v.name);
if (missingVars.length > 0) {
  throw new Error(`Missing required environment variables: ${missingVars.join(', ')}`);
}

export const ragApiPaths = {
  completion: '/completion',
  feedback: '/feedback',
};

export const contactCardVersion = `1.0.0`;

export default config;
