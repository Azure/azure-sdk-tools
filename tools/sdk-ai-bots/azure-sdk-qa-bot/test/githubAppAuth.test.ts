import { describe, it, expect, vi, beforeEach } from 'vitest';
import { GitHubAppTokenProvider, GitHubAppAuthConfig } from '../src/github/GitHubAppTokenProvider.js';
import { TokenCredential, AccessToken } from '@azure/identity';

// Mock @azure/keyvault-keys
vi.mock('@azure/keyvault-keys', () => {
  return {
    CryptographyClient: vi.fn().mockImplementation(() => ({
      sign: vi.fn().mockResolvedValue({
        result: Buffer.from('mock-signature'),
      }),
    })),
  };
});

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

const mockCredential: TokenCredential = {
  getToken: vi.fn().mockResolvedValue({ token: 'mock-azure-token', expiresOnTimestamp: Date.now() + 3600000 } as AccessToken),
};

const testConfig: GitHubAppAuthConfig = {
  keyVaultName: 'test-vault',
  keyName: 'test-key',
  appId: '12345',
  installOwner: 'TestOrg',
};

describe('GitHubAppTokenProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should obtain an installation token via JWT flow', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => [
          { id: 100, account: { login: 'TestOrg' } },
          { id: 200, account: { login: 'OtherOrg' } },
        ],
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          token: 'ghs_installation_token_123',
          expires_at: new Date(Date.now() + 3600000).toISOString(),
        }),
      });

    const provider = new GitHubAppTokenProvider(testConfig, mockCredential);
    const token = await provider.getToken();

    expect(token).toBe('ghs_installation_token_123');
    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect(mockFetch.mock.calls[0][0]).toBe('https://api.github.com/app/installations');
    expect(mockFetch.mock.calls[1][0]).toBe('https://api.github.com/app/installations/100/access_tokens');
    expect(mockFetch.mock.calls[1][1].method).toBe('POST');
  });

  it('should cache the token on subsequent calls', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => [{ id: 100, account: { login: 'TestOrg' } }],
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          token: 'ghs_cached_token',
          expires_at: new Date(Date.now() + 3600000).toISOString(),
        }),
      });

    const provider = new GitHubAppTokenProvider(testConfig, mockCredential);

    const token1 = await provider.getToken();
    const token2 = await provider.getToken();

    expect(token1).toBe('ghs_cached_token');
    expect(token2).toBe('ghs_cached_token');
    // fetch should only be called twice total (once for installations, once for token)
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it('should return undefined when installation list fails', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
    });

    const provider = new GitHubAppTokenProvider(testConfig, mockCredential);
    const token = await provider.getToken();

    expect(token).toBeUndefined();
  });

  it('should return undefined when no installation matches owner', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [{ id: 100, account: { login: 'WrongOrg' } }],
    });

    const provider = new GitHubAppTokenProvider(testConfig, mockCredential);
    const token = await provider.getToken();

    expect(token).toBeUndefined();
  });

  it('should match installation owner case-insensitively', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => [{ id: 100, account: { login: 'testorg' } }],
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          token: 'ghs_case_insensitive',
          expires_at: new Date(Date.now() + 3600000).toISOString(),
        }),
      });

    const provider = new GitHubAppTokenProvider(testConfig, mockCredential);
    const token = await provider.getToken();

    expect(token).toBe('ghs_case_insensitive');
  });

  it('should return undefined when token creation fails', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => [{ id: 100, account: { login: 'TestOrg' } }],
      })
      .mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
      });

    const provider = new GitHubAppTokenProvider(testConfig, mockCredential);
    const token = await provider.getToken();

    expect(token).toBeUndefined();
  });
});
