// Storage account name
const storageAccountName = process.env.STORAGE_ACCOUNT_NAME;

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
  azureStorageUrl: storageAccountName ? `https://${storageAccountName}.table.core.windows.net/` : undefined,
  azureTableNameForConversation: process.env.AZURE_TABLE_NAME_FOR_CONVERSATION,
  // Azure Blob Storage
  azureBlobStorageUrl: storageAccountName ? `https://${storageAccountName}.blob.core.windows.net/` : undefined,
  blobContainerName: process.env.BLOB_CONTAINER_NAME,
  channelConfigBlobName: process.env.CHANNEL_CONFIG_BLOB_NAME,
  tenantConfigBlobName: process.env.TENANT_CONFIG_BLOB_NAME,
  // Local config
  isLocal: process.env.IS_LOCAL === 'true',
  // Local RAG config
  localBackendEndpoint: process.env.LOCAL_BACKEND_ENDPOINT,
  localRagTenant: process.env.LOCAL_RAG_TENANT,
};

// Validate required environment variables at startup
const requiredEnvVars = [
  { name: 'STORAGE_ACCOUNT_NAME', value: storageAccountName },
  { name: 'BLOB_CONTAINER_NAME', value: config.blobContainerName },
  { name: 'CHANNEL_CONFIG_BLOB_NAME', value: config.channelConfigBlobName },
  { name: 'TENANT_CONFIG_BLOB_NAME', value: config.tenantConfigBlobName },
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
