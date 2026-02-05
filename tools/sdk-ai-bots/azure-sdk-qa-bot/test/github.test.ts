import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { Octokit } from '@octokit/rest';
import { GithubClient } from '../src/input/GithubClient.js';

// Mock the Octokit client
vi.mock('@octokit/rest', () => {
  const createMockOctokit = () => ({
    pulls: {
      get: vi.fn().mockResolvedValue({
        data: {
          title: 'Test PR Title',
          head: {
            sha: 'abc123sha',
          },
          labels: [{ name: 'TypeSpec' }, { name: 'Service PR' }],
        },
      }),
      listReviews: vi.fn().mockResolvedValue({
        data: [
          {
            user: { login: 'reviewer1' },
            state: 'APPROVED',
          },
          {
            user: { login: 'reviewer2' },
            state: 'CHANGES_REQUESTED',
          },
          {
            user: { login: 'reviewer3' },
            state: 'COMMENTED',
          },
        ],
      }),
      listReviewComments: vi.fn().mockResolvedValue({
        data: [
          {
            body: 'This is a review comment on the code.',
            user: { login: 'reviewer2', type: 'User' },
          },
          {
            body: 'Another inline comment on the code changes.',
            user: { login: 'reviewer2', type: 'User' },
          },
          {
            body: 'Automated code review bot comment.',
            user: { login: 'codecov', type: 'Bot' },
          },
        ],
      }),
    },
    issues: {
      get: vi.fn().mockResolvedValue({
        data: {
          title: 'Test Issue Title',
          body: 'This is the issue body content.',
          state: 'open',
          labels: [{ name: 'bug' }, { name: 'help wanted' }],
        },
      }),
      listComments: vi.fn().mockResolvedValue({
        data: [
          {
            body: 'This is a general issue comment.',
            user: { login: 'reviewer2', type: 'User' },
          },
          {
            body: 'Another general comment.',
            user: { login: 'reviewer2', type: 'User' },
          },
          {
            body: 'This is an automated bot comment.',
            user: { login: 'github-actions', type: 'Bot' },
          },
        ],
      }),
    },
    rest: {
      pulls: {
        get: vi.fn().mockResolvedValue({
          data: 'diff --git a/file.txt b/file.txt\n+added line',
        }),
      },
    },
    checks: {
      listForRef: vi.fn().mockResolvedValue({
        data: {
          check_runs: [
            {
              name: 'TypeSpec Linter',
              conclusion: 'success',
            },
            {
              name: 'Build and Test',
              conclusion: 'failure',
            },
            { name: 'In Progress Check', conclusion: null },
          ],
        },
      }),
    },
  });

  return {
    Octokit: vi.fn().mockImplementation(() => createMockOctokit()),
  };
});

describe('GitHub PR Details Fetcher', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch details for Azure/azure-rest-api-specs PR #34286', async () => {
    // The specific PR URL from the prompt
    const prUrl = 'https://github.com/Azure/azure-rest-api-specs/pull/34286';

    const githubClient = new GithubClient();
    const details = await githubClient.getPullRequestDetails(prUrl, {});

    // Verify Octokit was called with correct parameters
    expect(Octokit).toHaveBeenCalledWith({ auth: undefined });

    // Check the returned data structure
    expect(details).toHaveProperty('reviews');
    expect(details).toHaveProperty('comments');
    expect(details).toHaveProperty('basic');
    expect(details.basic).toHaveProperty('labels');

    // Validate conversations array
    expect(details.comments.issue.map((r) => r.comment)).toEqual([
      'This is a general issue comment.',
      'Another general comment.',
    ]);

    expect(details.comments.review.map((r) => r.comment)).toEqual([
      'This is a review comment on the code.',
      'Another inline comment on the code changes.',
    ]);

    // Validate reviewers array (only approved or changes requested)
    expect(details.reviews.map((r) => r.reviewer)).toEqual(['reviewer1', 'reviewer2', 'reviewer3']);

    // Validate labels array
    expect(details.basic.labels).toEqual(['TypeSpec', 'Service PR']);
  });

  it('should throw an error for invalid GitHub PR URL', async () => {
    const invalidUrl = 'https://github.com/invalid/url';

    const githubClient = new GithubClient();
    const res = await githubClient.getPullRequestDetails(invalidUrl, {});
    expect(res).toEqual({
      comments: { review: [], issue: [] },
      reviews: [],
      basic: { labels: [], title: '' },
      diff: '',
    });
  });

  it('should filter out Bot comments from PR comments', async () => {
    const prUrl = 'https://github.com/Azure/azure-rest-api-specs/pull/34286';

    const githubClient = new GithubClient();
    const details = await githubClient.getPullRequestDetails(prUrl, {});

    // Verify Bot comments are filtered out from review comments (mock includes 3 comments, 1 is Bot)
    expect(details.comments.review).toHaveLength(2);
    expect(details.comments.review.map((c) => c.name)).not.toContain('codecov');
    expect(details.comments.review.every((c) => c.type !== 'Bot')).toBe(true);

    // Verify Bot comments are filtered out from issue comments (mock includes 3 comments, 1 is Bot)
    expect(details.comments.issue).toHaveLength(2);
    expect(details.comments.issue.map((c) => c.name)).not.toContain('github-actions');
    expect(details.comments.issue.every((c) => c.type !== 'Bot')).toBe(true);
  });
});

describe('GitHub Issue Details Fetcher', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch details for a GitHub issue', async () => {
    const issueUrl = 'https://github.com/Azure/azure-sdk-for-js/issues/12345';

    const githubClient = new GithubClient();
    const details = await githubClient.getIssueDetails(issueUrl, {});

    // Verify Octokit was called with correct parameters
    expect(Octokit).toHaveBeenCalledWith({ auth: undefined });

    // Check the returned data structure
    expect(details).toHaveProperty('title');
    expect(details).toHaveProperty('body');
    expect(details).toHaveProperty('state');
    expect(details).toHaveProperty('labels');
    expect(details).toHaveProperty('comments');

    // Validate basic info
    expect(details.title).toBe('Test Issue Title');
    expect(details.body).toBe('This is the issue body content.');
    expect(details.state).toBe('open');
    expect(details.labels).toEqual(['bug', 'help wanted']);

    // Validate comments
    expect(details.comments.map((c) => c.comment)).toEqual([
      'This is a general issue comment.',
      'Another general comment.',
    ]);
  });

  it('should return empty details for invalid GitHub issue URL', async () => {
    const invalidUrl = 'https://github.com/invalid/url';

    const githubClient = new GithubClient();
    const res = await githubClient.getIssueDetails(invalidUrl, {});
    expect(res).toEqual({
      title: '',
      body: '',
      state: '',
      labels: [],
      comments: [],
    });
  });

  it('should handle issue URL with different repo format', async () => {
    const issueUrl = 'https://github.com/microsoft/TypeScript/issues/999';

    const githubClient = new GithubClient();
    const details = await githubClient.getIssueDetails(issueUrl, {});

    // Should successfully parse and fetch
    expect(details.title).toBe('Test Issue Title');
    expect(details.state).toBe('open');
  });

  it('should filter out Bot comments from issue comments', async () => {
    const issueUrl = 'https://github.com/Azure/azure-sdk-for-js/issues/12345';

    const githubClient = new GithubClient();
    const details = await githubClient.getIssueDetails(issueUrl, {});

    // Verify Bot comments are filtered out (mock includes 3 comments, 1 is Bot)
    expect(details.comments).toHaveLength(2);
    expect(details.comments.map((c) => c.name)).not.toContain('github-actions');
    expect(details.comments.every((c) => c.type !== 'Bot')).toBe(true);
  });
});
