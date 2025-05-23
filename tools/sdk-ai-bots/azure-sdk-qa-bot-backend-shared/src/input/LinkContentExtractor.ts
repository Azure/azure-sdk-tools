import { RemoteContent } from './RemoteContent.js';
import { GithubClient, PRDetails } from './GithubClient.js';
import { URLNotSupportedError } from '../error/inputErrors.js';

// TODO: add logging
export class LinkContentExtractor {
  private readonly githubClient = new GithubClient();
  private logMeta: object;

  constructor(logMeta: object = {}) {
    this.logMeta = logMeta;
    this.githubClient = new GithubClient(undefined, logMeta);
  }

  // TODO: make batch process
  public async extract(urls: URL[]): Promise<RemoteContent[]> {
    return Promise.all(
      urls.map(async (url, index) => {
        const id = `link-${index}`;
        if (!this.isGithubPullRequestUrl(url)) {
          return { text: '', url, id, error: new URLNotSupportedError(url) };
        }

        const prUrl = url.href;
        let prDetails: PRDetails;
        try {
          prDetails = await this.githubClient.getPullRequestDetails(prUrl);
        } catch (error) {
          return { text: '', url, id: `link-${index}`, error: error as Error };
        }

        let text = ``;
        for (const key in prDetails) {
          const detail = JSON.stringify(prDetails[key as keyof PRDetails], null, 2);
          text += `### ${key}\n\n${detail}\n\n`;
        }
        return { text, url, id };
      })
    );
  }

  private isGithubPullRequestUrl(url: URL): boolean {
    return url.hostname === 'github.com' && url.pathname.includes('/pull/');
  }
}
