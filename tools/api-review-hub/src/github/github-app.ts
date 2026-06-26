import { createHash } from "node:crypto";

import { DefaultAzureCredential } from "@azure/identity";
import { CryptographyClient, KeyClient } from "@azure/keyvault-keys";

import { getRequiredSetting } from "../config/settings.js";

interface GitHubInstallation {
    readonly id: number;
    readonly account?: {
        readonly login?: string;
    };
}

interface GitHubInstallationTokenResponse {
    readonly token: string;
    readonly expires_at: string;
}

interface InstallationTokenCacheEntry {
    readonly token: string;
    readonly expiresAtMs: number;
}

const credential = new DefaultAzureCredential();
const userAgent = "API-Review-Hub/0.1";
const tokenRefreshBufferMs = 5 * 60 * 1000;
const installationTokens = new Map<string, InstallationTokenCacheEntry>();

export async function getInstallationToken(owner: string): Promise<string> {
    const cacheKey = owner.toLowerCase();
    const cachedToken = installationTokens.get(cacheKey);
    if (cachedToken && cachedToken.expiresAtMs - tokenRefreshBufferMs > Date.now()) {
        return cachedToken.token;
    }

    const appJwt = await createGitHubAppJwt();
    const installationId = await findInstallationId(appJwt, owner);
    const installationToken = await createInstallationToken(appJwt, installationId);
    installationTokens.set(cacheKey, installationToken);
    return installationToken.token;
}

export async function getRepositoryInstallationToken(owner: string, repository: string): Promise<string> {
    const cacheKey = `${owner.toLowerCase()}/${repository.toLowerCase()}`;
    const cachedToken = installationTokens.get(cacheKey);
    if (cachedToken && cachedToken.expiresAtMs - tokenRefreshBufferMs > Date.now()) {
        return cachedToken.token;
    }

    const appJwt = await createGitHubAppJwt();
    const installationId = await findRepositoryInstallationId(appJwt, owner, repository);
    const installationToken = await createInstallationToken(appJwt, installationId);
    installationTokens.set(cacheKey, installationToken);
    return installationToken.token;
}

async function createGitHubAppJwt(): Promise<string> {
    const keyVaultUrl = await getRequiredSetting("github_app_keyvault_url");
    const keyName = await getRequiredSetting("github_app_key_name");
    const appId = await getRequiredSetting("github_app_id");

    const keyClient = new KeyClient(keyVaultUrl, credential);
    const key = await keyClient.getKey(keyName);
    const cryptoClient = new CryptographyClient(key, credential);

    const now = Math.floor(Date.now() / 1000);
    const encodedHeader = base64UrlEncodeJson({ alg: "RS256", typ: "JWT" });
    const encodedPayload = base64UrlEncodeJson({ iat: now, exp: now + 600, iss: appId });
    const unsignedToken = `${encodedHeader}.${encodedPayload}`;
    const digest = createHash("sha256").update(unsignedToken, "ascii").digest();
    const signResult = await cryptoClient.sign("RS256", digest);
    const signature = Buffer.from(signResult.result).toString("base64url");

    return `${unsignedToken}.${signature}`;
}

async function findInstallationId(appJwt: string, owner: string): Promise<number> {
    const installations = await gitHubRequest<GitHubInstallation[]>("https://api.github.com/app/installations", appJwt, "Bearer");
    const installation = installations.find((item) => item.account?.login?.toLowerCase() === owner.toLowerCase());

    if (!installation) {
        throw new Error(`No GitHub App installation found for owner '${owner}'.`);
    }

    return installation.id;
}

async function findRepositoryInstallationId(appJwt: string, owner: string, repository: string): Promise<number> {
    const installation = await gitHubRequest<GitHubInstallation>(
        `https://api.github.com/repos/${owner}/${repository}/installation`,
        appJwt,
        "Bearer",
    );
    return installation.id;
}

async function createInstallationToken(appJwt: string, installationId: number): Promise<InstallationTokenCacheEntry> {
    const response = await gitHubRequest<GitHubInstallationTokenResponse>(
        `https://api.github.com/app/installations/${installationId}/access_tokens`,
        appJwt,
        "Bearer",
        { method: "POST", body: "{}" },
    );
    return {
        token: response.token,
        expiresAtMs: Date.parse(response.expires_at),
    };
}

export async function gitHubRequest<T>(
    url: string,
    token: string,
    authorizationScheme: "Bearer",
    options: RequestInit = {},
): Promise<T> {
    const response = await fetch(url, {
        ...options,
        headers: {
            accept: "application/vnd.github+json",
            authorization: `${authorizationScheme} ${token}`,
            "content-type": "application/json",
            "user-agent": userAgent,
            "x-github-api-version": "2022-11-28",
            ...options.headers,
        },
    });

    if (!response.ok) {
        throw new Error(`GitHub API request failed with status ${response.status}: ${await response.text()}`);
    }

    return response.json() as Promise<T>;
}

function base64UrlEncodeJson(value: unknown): string {
    return Buffer.from(JSON.stringify(value)).toString("base64url");
}