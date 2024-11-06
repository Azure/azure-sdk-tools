import { SDKAutomation } from '../sdkAutomation';
import {
  getRepository,
  AzureBlobStorage,
  RealGitHub,
  ExecutableGit,
  StringMap,
  getGitHubRepositoryFromUrl
} from '@ts-common/azure-js-dev-tools';
import { getAzureDevOpsLogger, Logger, timestamps } from '@azure/logger-js';
import { Octokit } from '@octokit/rest';
import { createAppAuth } from '@octokit/auth-app';
import { App as GithubApp } from '@octokit/app';
import { SDKAutomationCliConfig } from './config/schema';
import { RealBlobProxy } from '../realBlobProxy';

interface Clients {
  github: RealGitHub;
  git: ExecutableGit;
}

export const getClientsWithAccessToken = (config: SDKAutomationCliConfig, logger: Logger): Clients => {
  const { githubToken, githubApp: appConfig } = config;

  if (githubToken) {
    return {
      github: RealGitHub.fromToken(githubToken),
      git: new ExecutableGit({
        authentication: githubToken
      })
    };
  }

  const githubApp = new GithubApp(appConfig);

  const installationIdCache: StringMap<number> = {};
  const getInstallationId = async (scope: string) => {
    let installationId = installationIdCache[scope];
    if (installationId === undefined) {
      const jwtToken = githubApp.getSignedJsonWebToken();
      const octokit = new Octokit({
        auth: `Bearer ${jwtToken}`,
        previews: ['machine-man']
      });
      try {
        const {
          data: { id }
        } = await octokit.apps.getOrgInstallation({ org: scope });
        installationId = id;
        installationIdCache[scope] = id;
      } catch (e) {
        try {
          const {
            data: { id }
          } = await octokit.apps.getUserInstallation({ username: scope });
          installationId = id;
          installationIdCache[scope] = id;
          await logger.logError(JSON.stringify(e));
        } catch (e) {
          await logger.logError(`GithubApp ${appConfig.id} doesn't have installation for: ${scope}`);
          await logger.logError(JSON.stringify(e));
          return undefined;
        }
      }
    }

    return installationId;
  };
  const getAccessToken = async (scope: string) => {
    const installationId = await getInstallationId(scope);
    return installationId === undefined ? undefined : githubApp.getInstallationAccessToken({ installationId });
  };

  return {
    github: RealGitHub.fromOctokit(
      async (scope: string): Promise<Octokit> => {
        const installationId = await getInstallationId(scope);
        return new Octokit({
          authStrategy: createAppAuth,
          auth: {
            appId: appConfig.id,
            privateKey: appConfig.privateKey,
            installationId
          }
        }) as Octokit;
      }
    ),
    git: new ExecutableGit({
      authentication: async (url: string) => {
        const repo = getGitHubRepositoryFromUrl(url);
        const token = await getAccessToken(repo!.owner);
        return token ? `x-access-token:${token}` : undefined;
      }
    })
  };
};

export const cliMain = async (config: SDKAutomationCliConfig) => {
  const workingFolder = 'work';

  const {
    executionMode,
    isTriggeredByUP,
    prNumber,
    sdkRepoName,
    specRepo,
    blobStorage: blobConfig,
    githubCommentAuthorName
  } = config;

  const logger = timestamps(getAzureDevOpsLogger());

  const blobStorage = new AzureBlobStorage(blobConfig.name);
  const blobStoragePrefix = blobStorage.getPrefix(blobConfig.prefix);
  const clients = getClientsWithAccessToken(config, logger);

  const repo = getRepository(specRepo);
  // Refresh PR ahead of time to refresh merge commit to avoid unadvertised object error
  await logger.logInfo(`Refreshing merge commit for PR ${prNumber}`);
  await clients.github.getPullRequest(repo, prNumber);
  let launchAutomation: () => Promise<void>;

  const sdkAutomation = new SDKAutomation(workingFolder, {
    buildID: config.azureCliArgs.buildId,
    logger,
    githubCommentAuthorName,
    deleteClonedRepositories: false,
    createGenerationPullRequests: true,
    blobProxy: new RealBlobProxy(config.blobStorage.name),
    downloadCommandTemplate: config.blobStorage.downloadCommand,
    isPublic: config.blobStorage.isPublic,
    ...clients
  });

  switch (executionMode) {
    case 'SDK_FILTER': {
      launchAutomation = () =>
      sdkAutomation.filterSDKReposToTrigger(repo, prNumber, blobStoragePrefix, isTriggeredByUP);
      break;
    }
    default: {
      launchAutomation = () =>
      sdkAutomation.pipelineTrigger(repo, prNumber, blobStoragePrefix, sdkRepoName, isTriggeredByUP);
      break;
    }
  }

  try {
    await launchAutomation();
  } catch (e) {
    if (e.message && e.message.indexOf('Server does not allow request for unadvertised object') > -1) {
      await logger.logWarning('Failed to retrieve merge commit. Retrying...');
      await launchAutomation();
    } else {
      throw e;
    }
  }
};
