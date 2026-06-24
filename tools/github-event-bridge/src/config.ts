import { DefaultAzureCredential } from "@azure/identity";
import { SecretClient } from "@azure/keyvault-secrets";
import { readFile } from "fs/promises";

export interface AppConfig {
    githubAppId: string;
    webhookSecret: string;
    privateKey: string;
}

export async function loadConfig(): Promise<AppConfig> {
    const githubAppId = getRequiredEnv("GITHUB_APP_ID");

    if (process.env.KEY_VAULT_URL) {
        return loadKeyVaultConfig(githubAppId, process.env.KEY_VAULT_URL);
    }

    const webhookSecret = getRequiredEnv("WEBHOOK_SECRET");
    const privateKeyPath = getRequiredEnv("PRIVATE_KEY_PATH");
    const privateKey = normalizePrivateKey(await readFile(privateKeyPath, "utf8"));

    return { githubAppId, webhookSecret, privateKey };
}

async function loadKeyVaultConfig(githubAppId: string, keyVaultUrl: string): Promise<AppConfig> {
    const secretClient = new SecretClient(keyVaultUrl, new DefaultAzureCredential());
    const webhookSecretName = getRequiredEnv("WEBHOOK_SECRET_NAME");
    const privateKeySecretName = getRequiredEnv("PRIVATE_KEY_SECRET_NAME");

    const [webhookSecret, privateKey] = await Promise.all([
        getRequiredSecret(secretClient, webhookSecretName),
        getRequiredSecret(secretClient, privateKeySecretName),
    ]);

    return { githubAppId, webhookSecret, privateKey: normalizePrivateKey(privateKey) };
}

function normalizePrivateKey(privateKey: string): string {
    return privateKey.trim().replaceAll("\\n", "\n");
}

async function getRequiredSecret(secretClient: SecretClient, secretName: string): Promise<string> {
    const secret = await secretClient.getSecret(secretName);
    if (!secret.value) {
        throw new Error(`Key Vault secret ${secretName} has no value`);
    }
    return secret.value;
}

function getRequiredEnv(name: string): string {
    const value = process.env[name];
    if (!value) {
        throw new Error(`${name} is not set`);
    }
    return value;
}
