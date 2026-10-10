import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createHash } from 'node:crypto';
import { getGitHubEnvironment } from '../src/services/GitHubAuthentication';

const { sign, cryptoClient } = vi.hoisted(() => ({ sign: vi.fn(), cryptoClient: vi.fn() }));
vi.mock('@azure/keyvault-keys', () => ({ CryptographyClient: class {
    constructor(...args: unknown[]) { cryptoClient(...args); }
    sign = sign;
} }));
const fetchMock = vi.fn();
const url = 'https://github.com/Azure/typespec-cpp.git';

describe('GitHub checkout authentication', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        fetchMock.mockReset();
        vi.stubGlobal('fetch', fetchMock);
        vi.stubEnv('GITHUB_APP_ID', '123');
        vi.stubEnv('GITHUB_APP_KEY_NAME', 'github-key');
        vi.stubEnv('GITHUB_APP_KEYVAULT_URL', 'https://test.vault.azure.net/');
        vi.stubEnv('GITHUB_APP_INSTALLATION_OWNER', '');
        vi.stubEnv('BOT_CLIENT_ID', 'bot-signing-identity');
        sign.mockResolvedValue({ result: Buffer.from('signature') });
        fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ id: 456 })))
            .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'installation-token' })));
    });
    afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals(); });

    it('signs the JWT digest in Key Vault and exchanges it for read-only Git credentials', async () => {
        const env = await getGitHubEnvironment(url);
        expect(cryptoClient.mock.calls[0][0]).toBe('https://test.vault.azure.net/keys/github-key');
        expect(fetchMock.mock.calls[0][0]).toBe('https://api.github.com/orgs/Azure/installation');
        const jwt = fetchMock.mock.calls[0][1].headers.Authorization.slice(7);
        const [header, payload, signature] = jwt.split('.');
        expect(JSON.parse(Buffer.from(header, 'base64url').toString())).toEqual({ alg: 'RS256', typ: 'JWT' });
        const claims = JSON.parse(Buffer.from(payload, 'base64url').toString());
        expect(claims.iss).toBe('123');
        expect(claims.exp - claims.iat).toBe(600);
        expect(sign).toHaveBeenCalledWith('RS256', createHash('sha256').update(`${header}.${payload}`).digest());
        expect(signature).toBe(Buffer.from('signature').toString('base64url'));
        expect(fetchMock.mock.calls[1][0]).toBe('https://api.github.com/app/installations/456/access_tokens');
        expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({ permissions: { contents: 'read' } });
        expect(env.GIT_CONFIG_KEY_0).toBe(`http.${url}.extraheader`);
        expect(env.GIT_CONFIG_VALUE_0).toBe(`Authorization: Basic ${Buffer.from('x-access-token:installation-token').toString('base64')}`);
        expect(env.GIT_TERMINAL_PROMPT).toBe('0');
    });

    it('preserves an explicitly supplied token without signing or making API calls', async () => {
        const env = await getGitHubEnvironment(url, 'existing-token');
        expect(env.GIT_CONFIG_VALUE_0).toContain(Buffer.from('x-access-token:existing-token').toString('base64'));
        expect(sign).not.toHaveBeenCalled();
        expect(fetchMock).not.toHaveBeenCalled();
    });

    it('mints a fresh token for each private repository checkout', async () => {
        await getGitHubEnvironment(url);
        fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ id: 456 })))
            .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'fresh-token' })));
        const env = await getGitHubEnvironment(url);
        expect(sign).toHaveBeenCalledTimes(2);
        expect(env.GIT_CONFIG_VALUE_0).toContain(Buffer.from('x-access-token:fresh-token').toString('base64'));
    });

    it('supports the configured installation owner', async () => {
        vi.stubEnv('GITHUB_APP_INSTALLATION_OWNER', 'other-org');
        await getGitHubEnvironment(url);
        expect(fetchMock.mock.calls[0][0]).toBe('https://api.github.com/orgs/other-org/installation');
    });

    it('requires GitHub App configuration when no token is provided', async () => {
        vi.stubEnv('GITHUB_APP_ID', '');
        await expect(getGitHubEnvironment(url)).rejects.toThrow('GitHub App requires');
        expect(sign).not.toHaveBeenCalled();
    });

    it('requires BOT_CLIENT_ID without falling back to AZURE_CLIENT_ID', async () => {
        vi.stubEnv('BOT_CLIENT_ID', '');
        vi.stubEnv('AZURE_CLIENT_ID', 'other-identity');
        await expect(getGitHubEnvironment(url)).rejects.toThrow('BOT_CLIENT_ID is required');
        expect(cryptoClient).not.toHaveBeenCalled();
        expect(fetchMock).not.toHaveBeenCalled();
    });

    it('does not expose Key Vault signing errors', async () => {
        sign.mockRejectedValueOnce(new Error('private signing details'));
        await expect(getGitHubEnvironment(url)).rejects.toThrow('GitHub App JWT signing failed; check Key Vault key signing access');
    });

    it.each([401, 403, 404, 500])('reports HTTP %i without exposing GitHub response contents', async (status) => {
        fetchMock.mockReset().mockResolvedValueOnce(new Response('private token details', { status }));
        await expect(getGitHubEnvironment(url)).rejects.toThrow(`GitHub App GET request rejected: HTTP ${status}`);
    });

    it('rejects repository hosts other than GitHub before acquiring credentials', async () => {
        await expect(getGitHubEnvironment('https://example.com/repo.git')).rejects.toThrow('Expected an HTTPS GitHub repository');
        expect(sign).not.toHaveBeenCalled();
    });
});
