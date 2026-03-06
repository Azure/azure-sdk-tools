import { CryptographyClient } from '@azure/keyvault-keys';
import { TokenCredential } from '@azure/identity';
import { OctokitWithRetry } from './octokit.js';
import { createHash } from 'crypto';
import { logger } from '../logging/logger.js';
import config from '../config/config.js';
import { getAzureCredential } from '../common/shared.js';

interface CachedToken {
  token: string;
  expiresAt: number;
}

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
 *
 * Use {@link GitHubAppTokenProvider.create} to obtain an instance; returns
 * undefined when the required GITHUB_APP_* env vars are not configured.
 */
export class GitHubAppTokenProvider {
  private readonly keyVaultName: string;
  private readonly keyName: string;
  private readonly githubAppId: string;
  private readonly installOwner: string;
  private readonly credential: TokenCredential;
  private cachedToken?: CachedToken;

  constructor(keyVaultName: string, keyName: string, githubAppId: string, installOwner: string, credential: TokenCredential) {
    this.keyVaultName = keyVaultName;
    this.keyName = keyName;
    this.githubAppId = githubAppId;
    this.installOwner = installOwner;
    this.credential = credential;
    logger.info(`GitHubAppTokenProvider initialized for app ${githubAppId}, install owner: ${installOwner}`);
  }

  /**
   * Creates a GitHubAppTokenProvider if all required GitHub App env vars are set.
   * Returns undefined otherwise (private repo access will be unavailable).
   */
  static create(): GitHubAppTokenProvider | undefined {
    const { githubAppKeyVaultName, githubAppKeyName, githubAppId, githubAppInstallOwner } = config;
    if (!githubAppKeyVaultName || !githubAppKeyName || !githubAppId || !githubAppInstallOwner) {
      logger.warn('GitHub App credentials are not fully configured; private repo access will be unavailable.');
      return undefined;
    }
    const credential = getAzureCredential(config.userManagedIdentityClientID);
    return new GitHubAppTokenProvider(githubAppKeyVaultName, githubAppKeyName, githubAppId, githubAppInstallOwner, credential);
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
      // Limit retries to 1 to keep user-facing latency low
      const octokit = new OctokitWithRetry({ retry: { retries: 1 } });
      const headers = { authorization: `Bearer ${jwt}` };

      logger.info(`Fetching installation ID for owner: ${this.installOwner}...`);
      const { data: installations } = await octokit.apps.listInstallations({ headers });
      const installation = installations.find(
        (i) => i.account?.login?.toLowerCase() === this.installOwner.toLowerCase()
      );

      if (!installation) {
        logger.error(
          `No GitHub App installation found for owner: ${this.installOwner}. ` +
          `Available installations: ${installations.map((i) => i.account?.login ?? 'unknown').join(', ')}`
        );
        return undefined;
      }

      logger.info(`Installation ID resolved: ${installation.id}. Exchanging JWT for installation token...`);
      const { data } = await octokit.apps.createInstallationAccessToken({
        installation_id: installation.id,
        headers,
      });

      this.cachedToken = {
        token: data.token,
        expiresAt: new Date(data.expires_at).getTime() - TOKEN_REFRESH_BUFFER_MS,
      };
      logger.info(`GitHub App installation token obtained, expires at ${data.expires_at}`);
      return data.token;
    } catch (error) {
      logger.error('Failed to obtain GitHub App installation token', {
        error,
        meta: { appId: this.githubAppId, installOwner: this.installOwner },
      });
      return undefined;
    }
  }

  /** Creates a short-lived JWT (10 min) signed via Azure Key Vault's remote signing API. */
  private async createAppJwt(): Promise<string> {
    const header = Buffer.from(JSON.stringify({ alg: 'RS256', typ: 'JWT' })).toString('base64url');
    const now = Math.floor(Date.now() / 1000);
    const payload = Buffer.from(
      JSON.stringify({ iat: now - 10, exp: now + 600, iss: this.githubAppId })
    ).toString('base64url');
    const unsignedToken = `${header}.${payload}`;

    const keyId = `https://${this.keyVaultName}.vault.azure.net/keys/${this.keyName}`;
    const cryptoClient = new CryptographyClient(keyId, this.credential);

    let signResult;
    try {
      const hash = createHash('sha256').update(unsignedToken).digest();
      signResult = await cryptoClient.sign('RS256', hash);
    } catch (error) {
      logger.error('Key Vault JWT signing failed — check that the managed identity has "sign" permission on the key', {
        error,
        meta: { keyVaultName: this.keyVaultName, keyName: this.keyName },
      });
      throw error;
    }
    const signature = Buffer.from(signResult.result).toString('base64url');

    logger.info('JWT signed successfully via Key Vault');
    return `${unsignedToken}.${signature}`;
  }
}
