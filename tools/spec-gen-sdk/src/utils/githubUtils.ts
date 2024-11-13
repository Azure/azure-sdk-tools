import { Octokit } from '@octokit/rest';
import { createAppAuth } from '@octokit/auth-app';
import * as winston from 'winston';
import { SdkAutoContext } from '../automation/entrypoint';


/**
 * The name and optional organization that the repository belongs to.
 */
export interface Repository {
  /**
   * The entity that owns the repository.
   */
  owner: string;
  /**
   * The name of the repository.
   */
  name: string;
}

/**
 * Get a GitHubRepository object from the provided string or GitHubRepository object.
 * @param repository The repository name or object.
 */
export function getRepository(repository: string | Repository): Repository {
  let result: Repository;
  if (!repository) {
    result = {
      name: repository,
      owner: ""
    };
  } else if (typeof repository === "string") {
    let slashIndex: number = repository.indexOf("/");
    if (slashIndex === -1) {
      slashIndex = repository.indexOf("\\");
    }
    result = {
      name: repository.substr(slashIndex + 1),
      owner: slashIndex === -1 ? "" : repository.substr(0, slashIndex)
    };
  } else {
    result = repository;
  }
  return result;
}

/**
 * Label that can be added to a pull request
 */
export interface GithubLabel {
  name: string;
  color: string;
}

export interface PullRequestLabelInfo {
  color: string;
  description?: string;
}

/**
 * The labels that can be added to an SDK repository's pull request.
 */
export const pullRequestLabelsInfo = {
  GenerationPR: {
    color: 'ff5900'
  },
  IntegrationPR: {
    color: '008cff'
  },
  SpecPRInProgress: {
    color: 'fbca04'
  },
  SpecPRClosed: {
    color: 'b60205'
  },
  SpecPRMerged: {
    color: '0e8a16'
  }
};

export type PullRequestLabel = keyof typeof pullRequestLabelsInfo;

export interface RepoKey {
  /**
   * The entity that owns the repository.
   */
  owner: string;
  /**
   * The name of the repository.
   */
  name: string;
}

/**
 * Get a GitHubRepository object from the provided string or GitHubRepository object.
 * @param repository The repository name or object.
 */
export function getRepoKey(repository: string | RepoKey): RepoKey {
  let result: RepoKey;
  if (!repository) {
    result = {
      name: repository,
      owner: ''
    };
  } else if (typeof repository === 'string') {
    let slashIndex: number = repository.indexOf('/');
    if (slashIndex === -1) {
      slashIndex = repository.indexOf('\\');
    }
    result = {
      name: repository.substr(slashIndex + 1),
      owner: slashIndex === -1 ? '' : repository.substr(0, slashIndex)
    };
  } else {
    result = repository;
  }
  return result;
}

export function repoKeyToString(repoKey: RepoKey): string {
  return `${repoKey.owner}/${repoKey.name}`;
}

interface AuthenticatedOctokit {
  octokit: Octokit;
  getToken: (owner: string) => Promise<string>;
}

export const getAuthenticatedOctokit = (
  opts: { id?: number; privateKey?: string; token?: string },
  logger: winston.Logger
): AuthenticatedOctokit => {
  if (opts.token) {
    return {
      octokit: new Octokit({
        auth: opts.token,
        log: logger
      }),
      getToken: () => Promise.resolve(opts.token!)
    };
  }

  if (opts.id && opts.privateKey) {
    const appAuthOctokit = new Octokit({
      authStrategy: createAppAuth,
      auth: {
        appId: opts.id,
        privateKey: opts.privateKey
      },
      log: logger
    });
    const installationIdCache: { [owner: string]: number } = {};
    const getInstallationId = async (owner: string) => {
      let installationId = installationIdCache[owner];
      if (installationId === undefined) {
        try {
          const {
            data: { id }
          } = await appAuthOctokit.apps.getOrgInstallation({ org: owner });
          installationId = id;
          installationIdCache[owner] = id;
        } catch (e) {
          try {
            logger.warn(`Retrying to get installation app from user. Error details: ${JSON.stringify(e)}.`);
            const {
              data: { id }
            } = await appAuthOctokit.apps.getUserInstallation({ username: owner });
            installationId = id;
            installationIdCache[owner] = id;            
          } catch (e) {
            logger.error(`ConfigError: GitHubApp ${opts.id} doesn't have installation for: ${owner}. Please report this issue through https://aka.ms/azsdk/support/specreview-channel or reach out the SDK Automation owner to set up the GitHub application correctly.`);
            logger.error(`Error details: ${JSON.stringify(e)}.`);
            return undefined;
          }
        }
      }
      return installationId;
    };

    const getAccessToken = async (owner: string) => {
      const installationId = await getInstallationId(owner);
      const auth = await appAuthOctokit.auth({
        type: 'installation',
        installationId
      }) as { token: string };
      return auth.token;
    };

    const octokit = new Octokit({
      log: logger
    });
    octokit.hook.wrap('request', async (request, options) => {
      const owner = options.owner ?? options.org;
      if (typeof owner !== 'string') {
        return request(options);
      }
      const token = await getAccessToken(owner);
      options.headers.Authorization = `token ${token}`;
      return request(options);
    });
    return {
      octokit,
      getToken: getAccessToken
    };
  }

  throw new Error('ConfigError: Invalid GitHub auth config. Please report this issue through https://aka.ms/azsdk/support/specreview-channel.');
};

export const getGithubFileContent = async (
  context: SdkAutoContext, repo: RepoKey, path: string, branch?: string
) => {
  try {
    const rsp = await context.octokit.repos.getContent({
      owner: repo.owner,
      repo: repo.name,
      path,
      ref: branch === undefined ? undefined : `refs/heads/${branch}`,
      mediaType: {
        format: 'raw'
      }
    });

    const result = JSON.parse(rsp.data as unknown as string);
    return result;
  } catch (error) {
    context.logger.error(`Error: exception is thrown when trying to get ${path} file content from ${repo.owner}/${repo.name}/refs/heads/${branch}. Please ensure the file exist in the ref commit. Details: ${error}`)
  }
  return undefined;
};

export const getPullRequestLabelsOctokit = async (
    context: SdkAutoContext, repo: RepoKey, pullRequestNumber: number
  ) => {
    const rsp = await context.octokit.issues.listLabelsOnIssue({
      owner: repo.owner,
      repo: repo.name,
      issue_number: pullRequestNumber
    });

    return rsp.data.map(l => l.name);
};

export async function removePullRequestLabelOctokit(
    context: SdkAutoContext,
    repo: RepoKey,
    pullRequestNumber: number,
    label: string
  ): Promise<string[]> {
    try {
        const rsp = await context.octokit.issues.removeLabel({
            owner: repo.owner,
            repo: repo.name,
            issue_number: pullRequestNumber,
            name: label
          });
          return rsp.data.map(l => l.name);
    } catch (e) {
        context.logger.error(`Error: exception is thrown when trying to remove labels from the PR. Error details: ${JSON.stringify(e)}. Please try to re-run this CI check or report this issue through https://aka.ms/azsdk/support/specreview-channel`);
    }
    return [];
}

export async function addPullRequestLabelOctokit(
    context: SdkAutoContext,
    repo: RepoKey,
    pullRequestNumber: number,
    labels: string[]
  ): Promise<string[]> {
    try {
        const rsp = await context.octokit.issues.addLabels({
            owner: repo.owner,
            repo: repo.name,
            issue_number: pullRequestNumber,
            labels
          });
        return rsp.data.map(l => l.name);
    } catch (e) {
        context.logger.error(`Error: exception is thrown when trying to add labels to the PR. Error details: ${JSON.stringify(e)}. Please try to re-run this CI check or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    }
    return [];
}
