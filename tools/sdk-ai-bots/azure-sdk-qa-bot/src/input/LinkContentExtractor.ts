import { RemoteContent } from './RemoteContent.js';
import { GithubClient, PRDetails } from './GithubClient.js';
import { URLNotSupportedError } from '../error/inputErrors.js';

export class LinkContentExtractor {
  private readonly githubClient = new GithubClient();

  constructor() {
    this.githubClient = new GithubClient(undefined);
  }

  public async extract(urls: URL[], meta: object): Promise<RemoteContent[]> {
    const contents: RemoteContent[] = [];

    for (const [index, url] of urls.entries()) {
      if (!this.isGithubPullRequestUrl(url)) {
        contents.push({ text: '', url, error: new URLNotSupportedError(url) });
        continue;
      }

      const prUrl = url.href;
      let prDetails: PRDetails;
      try {
        prDetails = await this.githubClient.getPullRequestDetails(prUrl, meta);
      } catch (error) {
        contents.push({ text: '', url, error: error });
        continue;
      }

      let text = ``;
      for (const key in prDetails) {
        const detail = JSON.stringify(prDetails[key], null, 2);
        text += `### ${key}\n${detail}\n`;
      }
      contents.push({ text, url });
    }

    return contents;
  }

  private isGithubPullRequestUrl(url: URL): boolean {
    return url.hostname === 'github.com' && url.pathname.includes('/pull/');
  }
}
