import { DefaultAzureCredential } from "@azure/identity";
import { SecretClient } from "@azure/keyvault-secrets";
import { readFile } from "fs/promises";
import { createKeyVaultJwtSigner, type CreateJwt } from "./signer.ts";

export interface AppConfig {
    githubAppId: string;
    webhookSecret: string;
    /** Raw private key PEM. Used for local file-based development only. */
    privateKey?: string;
    /**
     * Signer that produces the GitHub App JWT without exposing the private key.
     * Set when the key lives in Azure Key Vault.
     */
    createJwt?: CreateJwt;
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
    const credential = new DefaultAzureCredential();
    const secretClient = new SecretClient(keyVaultUrl, credential);
    const webhookSecretName = getRequiredEnv("WEBHOOK_SECRET_NAME");
    const privateKeyKeyId = getRequiredEnv("PRIVATE_KEY_KEY_ID");

    const webhookSecret = await getRequiredSecret(secretClient, webhookSecretName);

    // Sign the App JWT inside Key Vault so the private key never leaves the vault.
    const createJwt = createKeyVaultJwtSigner(privateKeyKeyId);

    return { githubAppId, webhookSecret, createJwt };
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
