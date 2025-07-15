import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { Octokit } from '@octokit/rest';
import { GithubClient } from '../src/input/GithubClient.js';

// Mock the Octokit client
vi.mock('@octokit/rest', () => {
  return {
    Octokit: vi.fn().mockImplementation(() => {
      return {
        pulls: {
          get: vi.fn().mockResolvedValue({
            data: {
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
                state: 'COMMENTED', // This one should be filtered out
              },
            ],
          }),
          listReviewComments: vi.fn().mockResolvedValue({
            data: [
              {
                body: 'This is a review comment on the code.',
                user: { login: 'reviewer2' },
              },
              {
                body: 'Another inline comment on the code changes.',
                user: { login: 'reviewer2' },
              },
            ],
          }),
        },
        issues: {
          listComments: vi.fn().mockResolvedValue({
            data: [
              {
                body: 'This is a general issue comment.',
                user: { login: 'reviewer2' },
              },
              {
                body: 'Another general comment.',
                user: { login: 'reviewer2' },
              },
            ],
          }),
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
      };
    }),
  };
});

describe('GitHub PR Details Fetcher', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.resetAllMocks();
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
});
