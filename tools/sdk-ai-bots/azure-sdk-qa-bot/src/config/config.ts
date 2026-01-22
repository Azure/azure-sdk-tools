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
  isLocal: process.env.IS_LOCAL === 'true',
  // Backend service endpoint used in local env
  localBackendEndpoint: process.env.LOCAL_BACKEND_ENDPOINT,
  // RAG tenant used in local env
  localRagTenant: process.env.LOCAL_RAG_TENANT,
};

export const ragApiPaths = {
  completion: '/completion',
  feedback: '/feedback',
};

export const contactCardVersion = `1.0.0`;

export default config;
