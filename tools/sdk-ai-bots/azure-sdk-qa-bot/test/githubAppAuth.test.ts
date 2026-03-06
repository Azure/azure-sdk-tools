import { describe, it, expect, vi, beforeEach } from 'vitest';
import { GitHubAppTokenProvider } from '../src/github/GitHubAppTokenProvider.js';
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

// Mock config to avoid env var validation at import time
vi.mock('../src/config/config.js', () => ({
  default: {},
}));

// Mock global fetch (used by Octokit internally)
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

/** Creates a Response-like object that Octokit's fetch-wrapper can process. */
function mockResponse(status: number, data?: any): Response {
  const body = data ? JSON.stringify(data) : '';
  return {
    status,
    ok: status >= 200 && status < 300,
    url: 'https://api.github.com/mock',
    headers: new Headers({ 'content-type': 'application/json' }),
    text: async () => body,
    json: async () => data,
    redirected: false,
  } as unknown as Response;
}

const mockCredential: TokenCredential = {
  getToken: vi.fn().mockResolvedValue({ token: 'mock-azure-token', expiresOnTimestamp: Date.now() + 3600000 } as AccessToken),
};

const testKeyVaultName = 'test-vault';
const testKeyName = 'test-key';
const testAppId = '12345';
const testInstallOwner = 'TestOrg';

function createProvider() {
  return new GitHubAppTokenProvider(testKeyVaultName, testKeyName, testAppId, testInstallOwner, mockCredential);
}

describe('GitHubAppTokenProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should obtain an installation token via JWT flow', async () => {
    mockFetch
      .mockResolvedValueOnce(
        mockResponse(200, [
          { id: 100, account: { login: 'TestOrg' } },
          { id: 200, account: { login: 'OtherOrg' } },
        ])
      )
      .mockResolvedValueOnce(
        mockResponse(200, {
          token: 'ghs_installation_token_123',
          expires_at: new Date(Date.now() + 3600000).toISOString(),
        })
      );

    const provider = createProvider();
    const token = await provider.getToken();

    expect(token).toBe('ghs_installation_token_123');
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it('should cache the token on subsequent calls', async () => {
    mockFetch
      .mockResolvedValueOnce(
        mockResponse(200, [{ id: 100, account: { login: 'TestOrg' } }])
      )
      .mockResolvedValueOnce(
        mockResponse(200, {
          token: 'ghs_cached_token',
          expires_at: new Date(Date.now() + 3600000).toISOString(),
        })
      );

    const provider = createProvider();

    const token1 = await provider.getToken();
    const token2 = await provider.getToken();

    expect(token1).toBe('ghs_cached_token');
    expect(token2).toBe('ghs_cached_token');
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it('should refresh the token when the cached token is near expiry', async () => {
    mockFetch
      .mockResolvedValueOnce(
        mockResponse(200, [{ id: 100, account: { login: 'TestOrg' } }])
      )
      .mockResolvedValueOnce(
        mockResponse(200, {
          token: 'ghs_short_lived_token',
          expires_at: new Date(Date.now() + 5000).toISOString(),
        })
      )
      .mockResolvedValueOnce(
        mockResponse(200, [{ id: 100, account: { login: 'TestOrg' } }])
      )
      .mockResolvedValueOnce(
        mockResponse(200, {
          token: 'ghs_refreshed_token',
          expires_at: new Date(Date.now() + 3600000).toISOString(),
        })
      );

    const provider = createProvider();

    const token1 = await provider.getToken();
    const token2 = await provider.getToken();

    expect(token1).toBe('ghs_short_lived_token');
    expect(token2).toBe('ghs_refreshed_token');
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  it('should return undefined when installation list fails (401)', async () => {
    mockFetch.mockResolvedValueOnce(mockResponse(401, { message: 'Unauthorized' }));

    const provider = createProvider();
    const token = await provider.getToken();

    expect(token).toBeUndefined();
  });

  it('should return undefined when no installation matches owner', async () => {
    mockFetch.mockResolvedValueOnce(
      mockResponse(200, [{ id: 100, account: { login: 'WrongOrg' } }])
    );

    const provider = createProvider();
    const token = await provider.getToken();

    expect(token).toBeUndefined();
  });

  it('should match installation owner case-insensitively', async () => {
    mockFetch
      .mockResolvedValueOnce(
        mockResponse(200, [{ id: 100, account: { login: 'testorg' } }])
      )
      .mockResolvedValueOnce(
        mockResponse(200, {
          token: 'ghs_case_insensitive',
          expires_at: new Date(Date.now() + 3600000).toISOString(),
        })
      );

    const provider = createProvider();
    const token = await provider.getToken();

    expect(token).toBe('ghs_case_insensitive');
  });

  it('should return undefined when token creation fails (500)', async () => {
    mockFetch
      .mockResolvedValueOnce(
        mockResponse(200, [{ id: 100, account: { login: 'TestOrg' } }])
      )
      .mockResolvedValueOnce(mockResponse(500, { message: 'Internal Server Error' }));

    const provider = createProvider();
    const token = await provider.getToken();

    expect(token).toBeUndefined();
  });

  it('should return undefined when Key Vault signing fails', async () => {
    const { CryptographyClient } = await import('@azure/keyvault-keys');
    vi.mocked(CryptographyClient).mockImplementationOnce(() => ({
      sign: vi.fn().mockRejectedValue(new Error('Access denied: missing sign permission')),
    }) as any);

    const provider = createProvider();
    const token = await provider.getToken();

    expect(token).toBeUndefined();
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('should return undefined on network failure', async () => {
    mockFetch.mockRejectedValueOnce(new Error('ECONNREFUSED'));

    const provider = createProvider();
    const token = await provider.getToken();

    expect(token).toBeUndefined();
  });
});
