const config = {
  // Teams app
  MicrosoftAppId: process.env.BOT_ID,
  MicrosoftAppType: process.env.BOT_TYPE,
  MicrosoftAppTenantId: process.env.BOT_TENANT_ID,
  MicrosoftAppPassword: process.env.BOT_PASSWORD,
  // RAG
  ragApiKey: process.env.RAG_API_KEY,
  ragEndpoint: process.env.RAG_ENDPOINT,
  // Computer Vision
  azureComputerVisionEndpoint: process.env.AZURE_COMPUTER_VISION_ENDPOINT,
  azureComputerVisionApiKey: process.env.AZURE_COMPUTER_VISION_API_KEY,
  // Azure Table Storage
  azureStorageUrl: process.env.AZURE_STORAGE_URL,
  azureTableNameForConversation: process.env.AZURE_TABLE_NAME_FOR_CONVERSATION,
};

export const channelToRagTanent = {
  [process.env.CHANNEL_ID_FOR_PYTHON]: process.env.RAG_TANENT_ID_FOR_PYTHON,
  default: process.env.RAG_TANENT_ID,
  [process.env.CHANNEL_ID_FOR_PYTHON_DEV_INTERNAL]: process.env.RAG_TANENT_ID_FOR_PYTHON,
};

export const ragApiPaths = {
  completion: '/completion',
  feedback: '/feedback',
};

export default config;
