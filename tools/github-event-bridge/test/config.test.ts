import { mkdtemp, rm, writeFile } from "fs/promises";
import { tmpdir } from "os";
import { join } from "path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const secretValues = vi.hoisted(() => new Map<string, string>());
const getSecretMock = vi.hoisted(() => vi.fn());
const signDataMock = vi.hoisted(() => vi.fn());
const cryptographyClientCtor = vi.hoisted(() => vi.fn());

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

vi.mock("@azure/keyvault-keys", () => ({
    CryptographyClient: vi.fn(function CryptographyClient(keyId: string) {
        cryptographyClientCtor(keyId);
        return {
            signData: signDataMock,
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
            "PRIVATE_KEY_KEY_ID",
        ]) {
            delete process.env[key];
        }

        secretValues.clear();
        getSecretMock.mockReset();
        getSecretMock.mockImplementation((name: string) => ({ value: secretValues.get(name) }));
        signDataMock.mockReset();
        cryptographyClientCtor.mockReset();
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

    it("loads webhook secret from Key Vault and signs the App JWT inside the vault", async () => {
        process.env.GITHUB_APP_ID = "123456";
        process.env.KEY_VAULT_URL = "https://example.vault.azure.net/";
        process.env.WEBHOOK_SECRET_NAME = "webhook-secret";
        process.env.PRIVATE_KEY_KEY_ID =
            "https://example.vault.azure.net/keys/github-app-signing-key";
        secretValues.set("webhook-secret", "vault-webhook-secret");
        signDataMock.mockResolvedValue({ result: Buffer.from("signature-bytes") });

        const { loadConfig } = await import("../src/config.ts");
        const config = await loadConfig();

        expect(config.githubAppId).toBe("123456");
        expect(config.webhookSecret).toBe("vault-webhook-secret");
        expect(config.privateKey).toBeUndefined();
        expect(config.createJwt).toBeTypeOf("function");
        expect(cryptographyClientCtor).toHaveBeenCalledWith(
            "https://example.vault.azure.net/keys/github-app-signing-key",
        );

        const { jwt, expiresAt } = await config.createJwt!("123456");

        // signData is invoked with RS256 over the header.payload signing input.
        expect(signDataMock).toHaveBeenCalledTimes(1);
        const [algorithm, signingInput] = signDataMock.mock.calls[0] as [string, Buffer];
        expect(algorithm).toBe("RS256");

        const [header, payload, signature] = jwt.split(".");
        expect(`${header}.${payload}`).toBe(Buffer.from(signingInput).toString());
        expect(signature).toBe(Buffer.from("signature-bytes").toString("base64url"));

        const decodedHeader = JSON.parse(
            Buffer.from(header, "base64url").toString(),
        ) as Record<string, unknown>;
        expect(decodedHeader).toEqual({ alg: "RS256", typ: "JWT" });
        const decodedPayload = JSON.parse(
            Buffer.from(payload, "base64url").toString(),
        ) as Record<string, unknown>;
        expect(decodedPayload["iss"]).toBe("123456");
        expect(typeof expiresAt).toBe("string");
    });
});
