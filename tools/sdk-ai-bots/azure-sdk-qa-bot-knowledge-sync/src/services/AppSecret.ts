import { AzureCliCredential, ChainedTokenCredential, ManagedIdentityCredential, WorkloadIdentityCredential} from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';

/**
 * Initialize secrets by loading them from Azure Key Vault and setting to environment variables
 */
export async function initSecrets(): Promise<void> {
    // Get the Key Vault endpoint from environment variable
    const keyVaultEndpoint = process.env.KEYVAULT_ENDPOINT;
    
    if (!keyVaultEndpoint) {
        throw new Error('KEYVAULT_ENDPOINT environment variable is required');
    }

    try {
        console.log('Loading secrets from Azure Key Vault...');
        
        // Create a credential
        const credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),
            new AzureCliCredential(),
            new WorkloadIdentityCredential()
        );

        // Establish a connection to the Key Vault client
        const client = new SecretClient(keyVaultEndpoint, credential);

        // Get AI Search API Key
        const aiSearchSecretResponse = await client.getSecret('AI-SEARCH-APIKEY');
        if (!aiSearchSecretResponse.value) {
            throw new Error('Failed to get AI-SEARCH-APIKEY secret value');
        }

        process.env.AI_SEARCH_API_KEY = aiSearchSecretResponse.value;
        console.log('Set AI_SEARCH_API_KEY from Key Vault');

        // Get AOAI Chat Completions API Key
        const aoaiSecretResponse = await client.getSecret('AOAI-CHAT-COMPLETIONS-API-KEY');
        if (!aoaiSecretResponse.value) {
            throw new Error('Failed to get AOAI-CHAT-COMPLETIONS-API-KEY secret value');
        }

        process.env.AOAI_CHAT_COMPLETIONS_API_KEY = aoaiSecretResponse.value;
        console.log('Set AOAI_CHAT_COMPLETIONS_API_KEY from Key Vault');

        // Get SSH private key
        const sshPrivateKeySecret = await client.getSecret('SSH-PRIVATE-KEY');
        if (!sshPrivateKeySecret.value) {
            throw new Error('Failed to get SSH-PRIVATE-KEY secret value');
        }
        
        process.env.SSH_PRIVATE_KEY = sshPrivateKeySecret.value;

        console.log('Successfully loaded secrets from Azure Key Vault');

    } catch (error) {
        console.error('Failed to load secrets from Azure Key Vault:', error);
        throw error;
    }
}
