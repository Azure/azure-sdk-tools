const config = {
    MicrosoftAppId: process.env.BOT_ID,
    MicrosoftAppType: process.env.BOT_TYPE,
    MicrosoftAppTenantId: process.env.BOT_TENANT_ID,
    MicrosoftAppPassword: process.env.BOT_PASSWORD,
    azureOpenAIKey: process.env.AZURE_OPENAI_API_KEY,
    azureOpenAIEndpoint: process.env.AZURE_OPENAI_ENDPOINT,
    azureOpenAIDeploymentName: process.env.AZURE_OPENAI_DEPLOYMENT_NAME,

    icmUrl: process.env.ICM_URL,

    feedbackEndpoint: process.env.FEEDBACK_ENDPOINT,
    feedbackApiKey: process.env.FEEDBACK_API_KEY,
    feedbackTenantId: process.env.FEEDBACK_TENANT_ID,
};

export default config;
