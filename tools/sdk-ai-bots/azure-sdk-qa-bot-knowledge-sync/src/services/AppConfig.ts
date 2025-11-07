import { DefaultAzureCredential } from '@azure/identity';
import { AppConfigurationClient } from '@azure/app-configuration';
import * as dotenv from 'dotenv';

/**
 * Load environment variables from .env file if it exists
 */
function loadEnvFile(): void {
    try {
        // Go to project root (3 levels up from dist/src/services when compiled)
        const path = require('path');
        const projectRoot = path.resolve(__dirname, '../../..');
        const envPath = path.join(projectRoot, '.env');
        
        dotenv.config({ path: envPath });
        console.log(`Loaded environment variables from .env file at: ${envPath}`);
    } catch (error) {
        console.log(`No .env file found or error loading it: ${error}`);
    }
}

/**
 * Initialize configuration by loading from local .env file first, then Azure App Configuration
 */
export async function initConfiguration(): Promise<void> {
    
    // Load .env file first
    loadEnvFile();

    // Get the endpoint from environment variable (could be from .env or already set)
    const endpoint = process.env.AZURE_APPCONFIG_ENDPOINT;
    
    if (!endpoint) {
        throw new Error('AZURE_APPCONFIG_ENDPOINT environment variable is required');
    }

    try {
        console.log('Loading configuration from Azure App Configuration...');
        
        // Create a credential using DefaultAzureCredential
        const credential = new DefaultAzureCredential();

        // Create the App Configuration client
        const client = new AppConfigurationClient(endpoint, credential);

        // Load all configuration settings
        const settings = client.listConfigurationSettings();

        // Iterate through all settings and set them as environment variables
        // Only set if not already defined (giving priority to .env file)
        for await (const setting of settings) {
            if (setting.key && setting.value !== undefined) {
                if (!process.env[setting.key]) {
                    process.env[setting.key] = setting.value;
                    console.log(`Set ${setting.key} from App Configuration`);
                } else {
                    console.log(`Skipping ${setting.key} - already set in environment (likely from .env file)`);
                }
            }
        }

        console.log('Successfully loaded configuration from Azure App Configuration');

    } catch (error) {
        console.error('Failed to load configuration from Azure App Configuration:', error);
        throw error;
    }
}
