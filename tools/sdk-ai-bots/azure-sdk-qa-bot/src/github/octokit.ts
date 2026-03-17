import { Octokit } from '@octokit/rest';
import { retry } from '@octokit/plugin-retry';

/**
 * Octokit subclass with automatic retry via @octokit/plugin-retry.
 *
 * Retry behavior (defaults):
 *  - Retries up to 3 times on transient errors (408, 429, 5xx).
 *  - Does NOT retry on 400, 401, 403, 404, 410, 422, 451.
 *  - Uses quadratic backoff: (retryCount+1)² × 1s → 1s, 4s, 9s.
 *  - Pass { retry: { retries: N } } to the constructor to override the retry count.
 */
export const OctokitWithRetry = Octokit.plugin(retry) as typeof Octokit;
