import { RemoteContent } from './RemoteContent.js';
import { GithubClient, PRDetails, IssueDetails } from './GithubClient.js';
import { URLNotSupportedError } from '../error/inputErrors.js';

export class LinkContentExtractor {
  private readonly githubClient = new GithubClient();

  constructor() {
    this.githubClient = new GithubClient(undefined);
  }

  public async extract(urls: URL[], meta: object): Promise<RemoteContent[]> {
    const contents: RemoteContent[] = [];

    for (const [index, url] of urls.entries()) {
      const isPR = this.isGithubPullRequestUrl(url);
      const isIssue = this.isGithubIssueUrl(url);

      if (!isPR && !isIssue) {
        contents.push({ text: '', url, error: new URLNotSupportedError(url) });
        continue;
      }

      try {
        let details: PRDetails | IssueDetails | undefined;
        if (isPR) {
          details = await this.githubClient.getPullRequestDetails(url.href, meta);
        } else {
          details = await this.githubClient.getIssueDetails(url.href, meta);
        }

        if (!details) {
          contents.push({ text: '', url, error: new Error('Failed to fetch details') });
          continue;
        }

        let text = ``;
        for (const key in details) {
          const detail = JSON.stringify(details[key as keyof typeof details], null, 2);
          text += `### ${key}\n${detail}\n`;
        }
        contents.push({ text, url });
      } catch (error) {
        contents.push({ text: '', url, error: error });
        continue;
      }
    }

    return contents;
  }

  private isGithubPullRequestUrl(url: URL): boolean {
    return url.hostname === 'github.com' && url.pathname.includes('/pull/');
  }

  private isGithubIssueUrl(url: URL): boolean {
    return url.hostname === 'github.com' && url.pathname.includes('/issues/');
  }
}
