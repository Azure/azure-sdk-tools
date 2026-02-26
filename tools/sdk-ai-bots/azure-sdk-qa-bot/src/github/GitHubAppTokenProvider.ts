import { CryptographyClient } from '@azure/keyvault-keys';
import { TokenCredential } from '@azure/identity';
import { createHash } from 'crypto';
import { logger } from '../logging/logger.js';

export interface GitHubAppAuthConfig {
  keyVaultName: string;
  keyName: string;
  appId: string;
  installOwner: string;
}

interface CachedToken {
  token: string;
  expiresAt: number;
}

const GITHUB_API_BASE = 'https://api.github.com';
const GITHUB_API_VERSION = '2022-11-28';
const TOKEN_REFRESH_BUFFER_MS = 10 * 60 * 1000; // Refresh 10 minutes before expiry

/**
 * Provides GitHub API tokens via GitHub App authentication.
 *
 * Flow:
 *  1. Build an RS256 JWT with the GitHub App ID as issuer
 *  2. Sign the JWT using a non-exportable RSA key in Azure Key Vault
 *  3. Exchange the JWT for a short-lived installation access token (~1 hour)
 *  4. Cache the token and refresh automatically before expiry
 *
 * The managed identity (e.g. azsdkqabotdev) must have "sign" permission
 * on the Key Vault key for this to work.
 */
export class GitHubAppTokenProvider {
  private readonly config: GitHubAppAuthConfig;
  private readonly credential: TokenCredential;
  private cachedToken?: CachedToken;

  constructor(config: GitHubAppAuthConfig, credential: TokenCredential) {
    this.config = config;
    this.credential = credential;
    logger.info(`GitHubAppTokenProvider initialized for app ${config.appId}, install owner: ${config.installOwner}`);
  }

  /** Returns a cached or fresh installation token. Returns undefined on failure. */
  public async getToken(): Promise<string | undefined> {
    if (this.cachedToken && Date.now() < this.cachedToken.expiresAt) {
      logger.info('Using cached GitHub App installation token');
      return this.cachedToken.token;
    }

    try {
      logger.info('Generating new GitHub App JWT via Key Vault signing...');
      const jwt = await this.createAppJwt();

      logger.info(`Fetching installation ID for owner: ${this.config.installOwner}...`);
      const installationId = await this.getInstallationId(jwt);
      if (installationId === undefined) {
        return undefined;
      }

      logger.info(`Installation ID resolved: ${installationId}. Exchanging JWT for installation token...`);
      const result = await this.createInstallationToken(jwt, installationId);
      if (!result) {
        return undefined;
      }

      this.cachedToken = { token: result.token, expiresAt: result.expiresAt - TOKEN_REFRESH_BUFFER_MS };
      logger.info(`GitHub App installation token obtained, expires at ${new Date(result.expiresAt).toISOString()}`);
      return result.token;
    } catch (error) {
      logger.error(`Failed to obtain GitHub App installation token: ${error}`);
      return undefined;
    }
  }

  /** Creates a short-lived JWT (10 min) signed via Azure Key Vault's remote signing API. */
  private async createAppJwt(): Promise<string> {
    const header = Buffer.from(JSON.stringify({ alg: 'RS256', typ: 'JWT' })).toString('base64url');
    const now = Math.floor(Date.now() / 1000);
    const payload = Buffer.from(
      JSON.stringify({ iat: now - 10, exp: now + 600, iss: this.config.appId })
    ).toString('base64url');
    const unsignedToken = `${header}.${payload}`;

    const keyId = `https://${this.config.keyVaultName}.vault.azure.net/keys/${this.config.keyName}`;
    const cryptoClient = new CryptographyClient(keyId, this.credential);

    const hash = createHash('sha256').update(unsignedToken).digest();
    const signResult = await cryptoClient.sign('RS256', hash);
    const signature = Buffer.from(signResult.result).toString('base64url');

    logger.info('JWT signed successfully via Key Vault');
    return `${unsignedToken}.${signature}`;
  }

  /** Finds the GitHub App installation ID for the configured org/owner. */
  private async getInstallationId(jwt: string): Promise<number | undefined> {
    const response = await fetch(`${GITHUB_API_BASE}/app/installations`, {
      headers: {
        Authorization: `Bearer ${jwt}`,
        Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': GITHUB_API_VERSION,
        'User-Agent': 'azure-sdk-qa-bot',
      },
    });

    if (!response.ok) {
      logger.error(`Failed to list GitHub App installations: ${response.status} ${response.statusText}`);
      return undefined;
    }

    const installations = (await response.json()) as { id: number; account: { login: string } }[];
    const installation = installations.find(
      (i) => i.account.login.toLowerCase() === this.config.installOwner.toLowerCase()
    );

    if (!installation) {
      logger.error(
        `No GitHub App installation found for owner: ${this.config.installOwner}. ` +
        `Available installations: ${installations.map((i) => i.account.login).join(', ')}`
      );
      return undefined;
    }

    return installation.id;
  }

  /** Exchanges the App JWT for an installation access token (valid ~1 hour). */
  private async createInstallationToken(
    jwt: string,
    installationId: number
  ): Promise<{ token: string; expiresAt: number } | undefined> {
    const response = await fetch(`${GITHUB_API_BASE}/app/installations/${installationId}/access_tokens`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${jwt}`,
        Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': GITHUB_API_VERSION,
        'User-Agent': 'azure-sdk-qa-bot',
      },
    });

    if (!response.ok) {
      logger.error(`Failed to create installation token for installation ${installationId}: ${response.status} ${response.statusText}`);
      return undefined;
    }

    const data = (await response.json()) as { token: string; expires_at: string };
    if (!data.token) {
      logger.error('GitHub API response does not contain a token');
      return undefined;
    }

    return {
      token: data.token,
      expiresAt: new Date(data.expires_at).getTime(),
    };
  }
}
