import { createHash } from 'node:crypto';
import { AzureCliCredential, ChainedTokenCredential, ManagedIdentityCredential } from '@azure/identity';
import { CryptographyClient } from '@azure/keyvault-keys';

/** Get Git authentication using an existing token or a fresh GitHub App installation token. */
export async function getGitHubEnvironment(url: string, token?: string): Promise<NodeJS.ProcessEnv> {
    if (new URL(url).origin !== 'https://github.com') throw new Error('Expected an HTTPS GitHub repository');
    if (!token) token = await acquireInstallationToken();
    return {
        ...process.env,
        GIT_TERMINAL_PROMPT: '0',
        GIT_CONFIG_COUNT: '1',
        GIT_CONFIG_KEY_0: `http.${url}.extraheader`,
        GIT_CONFIG_VALUE_0: `Authorization: Basic ${Buffer.from(`x-access-token:${token}`).toString('base64')}`
    };
}

async function acquireInstallationToken(): Promise<string> {
    const { GITHUB_APP_ID: appId, GITHUB_APP_KEY_NAME: keyName, GITHUB_APP_KEYVAULT_URL: vaultUrl } = process.env;
    const owner = process.env.GITHUB_APP_INSTALLATION_OWNER || 'Azure';
    if (!appId || !keyName || !vaultUrl) {
        throw new Error('GitHub App requires GITHUB_APP_ID, GITHUB_APP_KEY_NAME and GITHUB_APP_KEYVAULT_URL');
    }
    const credential = new ChainedTokenCredential(
        new ManagedIdentityCredential({ clientId: process.env.AZURE_CLIENT_ID }), new AzureCliCredential()
    );
    const crypto = new CryptographyClient(`${vaultUrl.replace(/\/$/, '')}/keys/${keyName}`, credential);
    const now = Math.floor(Date.now() / 1000);
    const header = Buffer.from(JSON.stringify({ alg: 'RS256', typ: 'JWT' })).toString('base64url');
    const payload = Buffer.from(JSON.stringify({ iat: now - 10, exp: now + 590, iss: appId })).toString('base64url');
    const unsigned = `${header}.${payload}`;
    let signature: Uint8Array;
    try {
        signature = (await crypto.sign('RS256', createHash('sha256').update(unsigned).digest())).result;
    } catch {
        throw new Error('GitHub App JWT signing failed; check Key Vault key signing access');
    }
    const jwt = `${unsigned}.${Buffer.from(signature).toString('base64url')}`;
    const installation = await githubRequest(`/orgs/${encodeURIComponent(owner)}/installation`, jwt);
    if (!Number.isSafeInteger(installation.id) || installation.id <= 0) throw new Error('GitHub App installation ID missing');
    const result = await githubRequest(`/app/installations/${installation.id}/access_tokens`, jwt, 'POST');
    if (typeof result.token !== 'string' || !result.token) throw new Error('GitHub installation token missing');
    return result.token;
}

async function githubRequest(route: string, jwt: string, method = 'GET'): Promise<any> {
    let response: Response;
    try {
        response = await fetch(`https://api.github.com${route}`, {
            method, redirect: 'error', signal: AbortSignal.timeout(10000),
            headers: { Authorization: `Bearer ${jwt}`, Accept: 'application/vnd.github+json',
                'X-GitHub-Api-Version': '2022-11-28', 'Content-Type': 'application/json' },
            ...(method === 'POST' ? { body: JSON.stringify({ permissions: { contents: 'read' } }) } : {})
        });
    } catch {
        throw new Error(`GitHub App ${method} request failed`);
    }
    if (!response.ok) throw new Error(`GitHub App ${method} request rejected: HTTP ${response.status}`);
    try {
        return await response.json();
    } catch {
        throw new Error('GitHub App returned invalid JSON');
    }
}
