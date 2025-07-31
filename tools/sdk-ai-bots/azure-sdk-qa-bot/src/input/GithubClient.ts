import { Octokit } from '@octokit/rest';
import { components } from '@octokit/openapi-types';
import { logger } from '../logging/logger.js';

export interface PRDetails {
  comments: {
    review: CommentEx[];
    issue: CommentEx[];
  };
  reviews: {
    state: string;
    // TODO: check when it's null
    reviewer?: string;
  }[];
  basic: {
    labels: string[];
    title: string;
  };
  diff: string;
}

type User = components['schemas']['nullable-simple-user'];

interface CommentEx {
  name: string;
  type: string;
  comment: string;
}

export class GithubClient {
  private readonly authToken?: string;
  private readonly perPage = 100;
  private readonly octokit: Octokit;

  constructor(authToken?: string) {
    this.authToken = authToken;
    this.octokit = new Octokit({ auth: this.authToken });
  }

  public async getPullRequestDetails(prUrl: string, meta: object): Promise<PRDetails> {
    // 1. Parse owner, repo, pull_number from URL
    const match = prUrl.match(/github\.com\/([^/]+)\/([^/]+)\/pull\/(\d+)/);
    if (!match) {
      logger.warn(`Invalid PR URL: ${prUrl}. Ignore`, { meta });
      return { comments: { review: [], issue: [] }, reviews: [], basic: { labels: [], title: '' }, diff: '' };
    }

    const [, owner, repo, pullNumberStr] = match;
    const pullNumber = Number(pullNumberStr);

    const [basicInfo, issueComments, reviewComments, reviews, diff] = await Promise.all([
      this.tryGetBasicInfo(owner, repo, pullNumber, prUrl),
      this.tryListIssueComments(owner, repo, pullNumber, prUrl),
      this.tryListReviewComments(owner, repo, pullNumber, prUrl),
      this.tryListReviews(owner, repo, pullNumber, prUrl),
      this.tryGetPullDiff(owner, repo, pullNumber, prUrl),
    ]);

    return {
      comments: { review: reviewComments, issue: issueComments },
      reviews,
      basic: basicInfo,
      diff,
    };
  }

  private getCommentWithUser(commentUser: User, commentBody: string): CommentEx {
    return { name: commentUser.login, type: commentUser.type, comment: commentBody };
  }

  // TODO: add retry
  private async tryGetBasicInfo(
    owner: string,
    repo: string,
    id: number,
    prUrl: string
  ): Promise<{ labels: string[]; title: string } | undefined> {
    try {
      const parameters = { owner, repo, pull_number: id };
      const response = await this.octokit.pulls.get(parameters);
      const pr = response.data;
      const labels = pr.labels.map((lbl) => lbl.name);
      const title = pr.title;
      return { labels, title };
    } catch (error) {
      // TODO: use logger instead
      console.error(`Failed to get basic info for pull request ${prUrl}: ${error}`);
      return undefined;
    }
  }

  // TODO: add retry
  private async tryListIssueComments(
    owner: string,
    repo: string,
    id: number,
    prUrl: string
  ): Promise<CommentEx[] | undefined> {
    try {
      const parameters = { owner, repo, issue_number: id, per_page: this.perPage };
      const response = await this.octokit.issues.listComments(parameters);
      const comments = response.data
        .filter((d) => d.user.type !== 'Bot')
        .map((d) => this.getCommentWithUser(d.user, d.body));
      return comments;
    } catch (error) {
      console.error(`Failed to list comments for pull request ${prUrl}: ${error}`);
      return undefined;
    }
  }

  // TODO: add retry
  private async tryListReviewComments(
    owner: string,
    repo: string,
    id: number,
    prUrl: string
  ): Promise<CommentEx[] | undefined> {
    try {
      const parameters = { owner, repo, pull_number: id, per_page: this.perPage };
      const response = await this.octokit.pulls.listReviewComments(parameters);
      const comments = response.data
        .filter((d) => d.user.type !== 'Bot')
        .map((d) => this.getCommentWithUser(d.user, d.body));
      return comments;
    } catch (error) {
      console.error(`Failed to list review comments for pull request ${prUrl}: ${error}`);
      return undefined;
    }
  }
  // TODO: add retry
  private async tryListReviews(
    owner: string,
    repo: string,
    id: number,
    prUrl: string
  ): Promise<{ state: string; reviewer?: string }[] | undefined> {
    try {
      const parameters = { owner, repo, pull_number: id, per_page: this.perPage };
      const response = await this.octokit.pulls.listReviews(parameters);
      const reviews = response.data.map((r) => ({
        state: r.state,
        reviewer: r.user?.login,
      }));
      return reviews;
    } catch (error) {
      console.error(`Failed to list reviews for pull request ${prUrl}: ${error}`);
      return undefined;
    }
  }

  // TODO: add retry
  private async tryGetPullDiff(owner: string, repo: string, id: number, prUrl: string): Promise<string | undefined> {
    try {
      const parameters = { owner, repo, pull_number: id, mediaType: { format: 'diff' } };
      const response = await this.octokit.rest.pulls.get(parameters);
      const diff = response.data as unknown as string;
      return diff;
    } catch (error) {
      console.error(`Failed to get diff for pull request ${prUrl}: ${error}`);
      return undefined;
    }
  }
}
