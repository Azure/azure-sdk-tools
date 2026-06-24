import { DefaultAzureCredential } from "@azure/identity";
import { CryptographyClient } from "@azure/keyvault-keys";

export type CreateJwt = (
    appId: string | number,
    timeDifference?: number,
) => Promise<{ jwt: string; expiresAt: string }>;

function base64url(input: string | Buffer): string {
    return Buffer.from(input).toString("base64url");
}

/**
 * Builds a GitHub App JWT signer that delegates the RS256 signature to Azure Key
 * Vault. The private key never leaves the vault: only the data-to-be-signed is
 * sent and only the signature bytes are returned.
 *
 * @param keyId Full Key Vault key identifier, e.g.
 *   `https://my-vault.vault.azure.net/keys/github-app-signing-key`.
 */
export function createKeyVaultJwtSigner(keyId: string): CreateJwt {
    const cryptoClient = new CryptographyClient(keyId, new DefaultAzureCredential());

    return async function createJwt(appId, timeDifference = 0) {
        const now = Math.floor(Date.now() / 1000) + timeDifference;
        const iat = now - 10; // allow for clock skew
        const exp = now + 600; // GitHub max lifetime is 10 minutes

        const header = base64url(JSON.stringify({ alg: "RS256", typ: "JWT" }));
        const payload = base64url(JSON.stringify({ iat, exp, iss: appId }));
        const signingInput = `${header}.${payload}`;

        // Key Vault hashes and signs inside the vault; only the signature comes back.
        const { result } = await cryptoClient.signData("RS256", Buffer.from(signingInput));

        return {
            jwt: `${signingInput}.${base64url(Buffer.from(result))}`,
            expiresAt: new Date(exp * 1000).toISOString(),
        };
    };
}
