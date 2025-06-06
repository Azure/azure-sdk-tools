import { RemoteContent } from './RemoteContent.js';
import { GithubClient, PRDetails } from './GithubClient.js';
import { URLNotSupportedError } from '../error/inputErrors.js';

export class LinkContentExtractor {
  private readonly githubClient = new GithubClient();
  private logMeta: object;

  constructor(logMeta: object = {}) {
    this.logMeta = logMeta;
    this.githubClient = new GithubClient(undefined, logMeta);
  }

  public async extract(urls: URL[]): Promise<RemoteContent[]> {
    const contents: RemoteContent[] = [];

    for (const [index, url] of urls.entries()) {
      const id = `link-${index}`;
      if (!this.isGithubPullRequestUrl(url)) {
        contents.push({ text: '', url, id, error: new URLNotSupportedError(url) });
        continue;
      }

      const prUrl = url.href;
      let prDetails: PRDetails;
      try {
        prDetails = await this.githubClient.getPullRequestDetails(prUrl);
      } catch (error) {
        contents.push({ text: '', url, id: `link-${index}`, error: error });
        continue;
      }

      let text = ``;
      for (const key in prDetails) {
        const detail = JSON.stringify(prDetails[key], null, 2);
        text += `### ${key}\n${detail}\n`;
      }
      contents.push({ text, url, id });
    }

    return contents;
  }

  private isGithubPullRequestUrl(url: URL): boolean {
    return url.hostname === 'github.com' && url.pathname.includes('/pull/');
  }
}
