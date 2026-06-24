import { mkdtemp, rm, writeFile } from "fs/promises";
import { tmpdir } from "os";
import { join } from "path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const secretValues = vi.hoisted(() => new Map<string, string>());
const getSecretMock = vi.hoisted(() => vi.fn());

vi.mock("@azure/identity", () => ({
    DefaultAzureCredential: vi.fn(function DefaultAzureCredential() {}),
}));

vi.mock("@azure/keyvault-secrets", () => ({
    SecretClient: vi.fn(function SecretClient() {
        return {
            getSecret: getSecretMock,
        };
    }),
}));

describe("loadConfig", () => {
    const originalEnv = process.env;

    beforeEach(() => {
        vi.resetModules();
        process.env = { ...originalEnv };
        for (const key of [
            "GITHUB_APP_ID",
            "WEBHOOK_SECRET",
            "PRIVATE_KEY_PATH",
            "KEY_VAULT_URL",
            "WEBHOOK_SECRET_NAME",
            "PRIVATE_KEY_SECRET_NAME",
        ]) {
            delete process.env[key];
        }

        secretValues.clear();
        getSecretMock.mockReset();
        getSecretMock.mockImplementation((name: string) => ({ value: secretValues.get(name) }));
    });

    afterEach(() => {
        process.env = originalEnv;
    });

    it("loads local webhook settings from environment and private key file", async () => {
        const tempDir = await mkdtemp(join(tmpdir(), "github-event-bridge-"));

        try {
            const privateKeyPath = join(tempDir, "private-key.pem");
            await writeFile(privateKeyPath, "local-private-key");
            process.env.GITHUB_APP_ID = "123456";
            process.env.WEBHOOK_SECRET = "local-webhook-secret";
            process.env.PRIVATE_KEY_PATH = privateKeyPath;

            const { loadConfig } = await import("../src/config.ts");

            await expect(loadConfig()).resolves.toEqual({
                githubAppId: "123456",
                webhookSecret: "local-webhook-secret",
                privateKey: "local-private-key",
            });
        } finally {
            await rm(tempDir, { recursive: true, force: true });
        }
    });

    it("loads webhook secret and private key from Key Vault when configured", async () => {
        process.env.GITHUB_APP_ID = "123456";
        process.env.KEY_VAULT_URL = "https://example.vault.azure.net/";
        process.env.WEBHOOK_SECRET_NAME = "webhook-secret";
        process.env.PRIVATE_KEY_SECRET_NAME = "webhook-private-key";
        secretValues.set("webhook-secret", "vault-webhook-secret");
        secretValues.set("webhook-private-key", "-----BEGIN RSA PRIVATE KEY-----\\nkey\\n-----END RSA PRIVATE KEY-----");

        const { loadConfig } = await import("../src/config.ts");

        await expect(loadConfig()).resolves.toEqual({
            githubAppId: "123456",
            webhookSecret: "vault-webhook-secret",
            privateKey: "-----BEGIN RSA PRIVATE KEY-----\nkey\n-----END RSA PRIVATE KEY-----",
        });
    });
});
