import { toolError } from "./messageUtils";

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
export function getRepository(repoUrl: string): Repository {
  let result: Repository = {
    owner: '',
    name: ''
  };
  repoUrl = repoUrl.toLowerCase();
  const urlPattern = /https:\/\/github\.com\/([^/]+)\/([^/]+)/;
  if (repoUrl) {
    const match = repoUrl.match(urlPattern);
    if (!match) {
      throw new Error(toolError(`Error: Invalid spec repository URL [${repoUrl}] provided. This is a sample of correct format: https://github.com/azure/azure-rest-api-specs`));
    }
    result = {
      owner: match[1],
      name: match[2]
    };
  }
  return result;
}

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
