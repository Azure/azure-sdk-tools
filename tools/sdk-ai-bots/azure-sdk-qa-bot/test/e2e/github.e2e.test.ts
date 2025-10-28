import { it, expect } from 'vitest';
import { GithubClient } from '../../src/input/GithubClient.js';

// TODO: Add more tests to cover all branches and edge cases
it('e2e test', async () => {
  const prUrl = 'https://github.com/Azure/azure-rest-api-specs/pull/34201';
  const details = await new GithubClient().getPullRequestDetails(prUrl, {});
  expect(details.basic.labels).contains('TypeSpec');
});
