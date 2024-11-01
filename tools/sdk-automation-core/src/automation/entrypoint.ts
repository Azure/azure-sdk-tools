import { RestEndpointMethodTypes } from '@octokit/rest';
import { Octokit } from '@octokit/rest';
import {
  BlobServiceClient,
  ContainerClient,
} from '@azure/storage-blob';
import * as winston from 'winston';
import { getAuthenticatedOctokit, RepoKey } from '../utils/githubUtils';
import {
  FailureType,
  setFailureType,
  WorkflowContext,
  workflowFilterSdkMain,
  workflowInit,
  workflowMain
} from './workflow';
import { TriggerType } from '../types/TriggerType';
import { azureresourceschema } from '../langSpecs/langs/azureresourceschema';
import { cli } from '../langSpecs/langs/cli';
import { dotnet } from '../langSpecs/langs/dotnet';
import { go } from '../langSpecs/langs/go';
import { java } from '../langSpecs/langs/java';
import { javascript } from '../langSpecs/langs/javascript';
import { python } from '../langSpecs/langs/python';
import { pythonTrack2 } from '../langSpecs/langs/pythonTrack2';
import { trenton } from '../langSpecs/langs/trenton';
import { LanguageConfiguration } from '../langSpecs/languageConfiguration';
import {
  getBlobName,
  loggerConsoleTransport,
  loggerDevOpsTransport,
  loggerStorageAccountTransport,
  loggerTestTransport,
  loggerWaitToFinish,
  sdkAutoLogLevels
} from './logging';
import { sdkAutoReportStatus } from './reportStatus';
import { SDKAutomationState } from '../sdkAutomationState';
import { DefaultAzureCredential } from '@azure/identity';
import * as pkginfo from 'pkginfo';

interface SdkAutoOptions {
  specRepo: RepoKey;
  pullNumber: number;
  sdkName: string;
  branchPrefix: string;
  filterSwaggerToSdk?: boolean;

  github: {
    token?: string;
    id?: number;
    privateKey?: string;
    commentAuthorName?: string;
  };

  storage: {
    name: string;
    prefix: string;
    downloadCommand: string;
    isPublic: boolean;
  };

  buildID?: string;
  runEnv: 'local' | 'azureDevOps' | 'test';
}

export type SdkAutoContext = {
  config: SdkAutoOptions;
  octokit: Octokit;
  getGithubAccessToken: (owner: string) => Promise<string>;
  logger: winston.Logger;
  useMergedRoutine: boolean;
  trigger: TriggerType;
  specCommitSha: string;
  specHeadRef: string;
  specHtmlUrl: string;
  specIsPrivate: boolean;
  specPrInfo: {head: {owner: string; repo: string}, base: {owner: string; repo: string}};
  specPrBaseBranch: string;
  specPrHeadBranch: string;
  specPrTitle: string;
  specPrHtmlUrl: string;
  workingFolder: string;
  legacyLangConfig?: LanguageConfiguration;
  blobContainerClient: ContainerClient;
  logsBlobUrl?: string;
  version: string;
  autorestConfig?: string;
};

const getLegacyLanguageConfig = (sdkName: string) => {
  switch (sdkName) {
    case 'azure-resource-manager-schemas':
      return azureresourceschema;
    case 'azure-cli-extensions':
      return cli;
    case 'azure-sdk-for-net':
      return dotnet;
    case 'azure-sdk-for-go':
      return go;
    case 'azure-sdk-for-java':
      return java;
    case 'azure-sdk-for-js':
      return javascript;
    case 'azure-sdk-for-python':
      return python;
    case 'azure-sdk-for-python-track2':
      return pythonTrack2;
    case 'azure-sdk-for-trenton':
      return trenton;
  }
  return undefined;
};

const getAutorestConfigFromPRComment = async (
  octokit: Octokit,
  owner: string,
  repo: string,
  pullNumber: number,
  sdkName: string,
  logger: winston.Logger
) => {
  let comments: (string | undefined)[] | undefined = undefined;
  try {
    const res = await octokit.issues.listComments({
      owner,
      repo,
      issue_number: pullNumber
    });
    if (res.status !== 200) {
      throw new Error(`Error: Get autorest configuration from PR https://github.com/${owner}/${repo}/pull/${pullNumber} failed with status code: ${res.status}. If autorest config is requried, please re-run the failed job in pipeline run or emit '/azp run' in the PR comment to trigger the re-run if the status code is retryable.`);
    }
    comments = res.data.map(e => e.body).map(e => {
      if (!e.includes('\r\n') && e.includes('\n')) {
        return e.replace(/\n/g, '\r\n');
      }
      return e;
    });
  } catch (e) {
    logger.warn(e.message);
  }

  const regexToFilterComment = new RegExp(`#+ *${sdkName}`);
  let autorestConfigComment: string|undefined = undefined;
  for (const comment of comments!) {
    if (!comment) { continue; }
    if (comment.match(regexToFilterComment)) {
      autorestConfigComment = comment;
    }
  }
  return autorestConfigComment;
};

export const getSdkAutoContext = async (options: SdkAutoOptions): Promise<SdkAutoContext> => {
  const logger = winston.createLogger({
    levels: sdkAutoLogLevels.levels
  });

  if (options.runEnv === 'local') {
    logger.add(loggerConsoleTransport());
  } else if (options.runEnv === 'azureDevOps') {
    logger.add(loggerDevOpsTransport());
  } else if (options.runEnv === 'test') {
    logger.add(loggerTestTransport());
  }

  logger.info(
    `Working on https://github.com/${options.specRepo.owner}/${options.specRepo.name}/pull/${options.pullNumber}`
  );

  const [{ octokit, getGithubAccessToken, specPR }, { blobContainerClient, logsBlobName }] = await Promise.all([
    getGithubContext(options, logger),
    getStorageContext(options, logger)
  ]);

  const version = pkginfo.read(module).package.version;
  logger.info(`SDK Automation ${version}`);

  logger.info(
    `Getting autorest configuration from PR https://github.com/${options.specRepo.owner}/${options.specRepo.name}/pull/${options.pullNumber}`
  );

  const autorestConfig = await getAutorestConfigFromPRComment(
    octokit,
    options.specRepo.owner,
    options.specRepo.name,
    options.pullNumber,
    options.sdkName,
    logger
  );

  const useMergedRoutine =
    specPR.state === 'closed' && specPR.merged ? true : specPR.state === 'open' ? false : undefined;
  if (useMergedRoutine === undefined) {
    throw new Error(`TriggerError: PR ${options.pullNumber} is closed and isn't merged. Please re-open the PR to trigger the SDK generation.`);
  }
  if (useMergedRoutine && specPR.base.ref !== 'main') {
    throw new Error(`TriggerError: PR ${options.pullNumber} is not merged to main branch. This PR state isn't supported by the SDK Automation. Please ensure the PR is open or is merged to main branch.`);
  }
  const trigger: TriggerType = useMergedRoutine ? 'continuousIntegration' : 'pullRequest';
  logger.info(`Trigger type: ${trigger}`);

  const specCommitSha = specPR.merge_commit_sha;
  if (!specCommitSha) {
    throw new Error(`TriggerError: PR ${options.pullNumber} doesn't have merge_commit_sha. Maybe there's a conflict. Please ensure the PR is merged to main correctly in order to trigger the SDK Automation.`);
  }

  const workingFolder = '.';

  const legacyLangConfig = getLegacyLanguageConfig(options.sdkName);

  const logsBlobUrl = `${logsBlobName}`;

  return {
    config: options,
    logger,
    octokit,
    getGithubAccessToken,
    useMergedRoutine,
    trigger,
    specCommitSha,
    workingFolder,
    specIsPrivate: specPR.base.repo.private,
    specHtmlUrl: specPR.base.repo.html_url,
    specPrInfo: {
      head: {
        owner: specPR.head.repo.owner.login,
        repo: specPR.head.repo.name
      },
      base: {
        owner: specPR.base.repo.owner.login,
        repo: specPR.base.repo.name
      }
    },
    specPrBaseBranch: specPR.base.ref,
    specPrHeadBranch: specPR.head.ref,
    specPrTitle: specPR.title,
    specPrHtmlUrl: specPR.html_url,
    specHeadRef: useMergedRoutine ? specPR.base.ref : `refs/pull/${specPR.number}/merge`,
    legacyLangConfig,
    blobContainerClient,
    logsBlobUrl,
    version,
    autorestConfig
  };
};

const getStorageContext = async (options: SdkAutoOptions, logger: winston.Logger) => {
  const credential = new DefaultAzureCredential({
    loggingOptions: { allowLoggingAccountIdentifiers: true },
  });
  const serviceClient = new BlobServiceClient(
    `https://${options.storage.name}.blob.core.windows.net`,
    credential
  );
  const blobContainerClient = serviceClient.getContainerClient(options.storage.prefix);

  if (options.filterSwaggerToSdk || options.runEnv === 'test') {
    return { blobContainerClient };
  }

  logger.info(`Ensure blob container exists: ${blobContainerClient.url}`);
  await blobContainerClient.createIfNotExists();

  const blobLogger = await loggerStorageAccountTransport(
    { blobContainerClient, config: options },
    getBlobName({ config: options }, 'logs.txt')
  );
  logger.add(blobLogger.blobTransport);
  const logsBlobName = `${blobLogger.blobName}`;
  logger.info(`Log to ${logsBlobName}`);
  return { blobContainerClient, logsBlobName };
};

const getGithubContext = async (options: SdkAutoOptions, logger: winston.Logger) => {
    const {octokit, getToken: getGithubAccessToken} = getAuthenticatedOctokit(options.github, logger);

  let specPR: RestEndpointMethodTypes['pulls']['get']['response']['data'];
  do {
    const rsp = await octokit.pulls.get({
      owner: options.specRepo.owner,
      repo: options.specRepo.name,
      pull_number: options.pullNumber
    });
    specPR = rsp.data;
  } while (specPR.mergeable === null && !specPR.merged);

  return { octokit, getGithubAccessToken, specPR };
};

export const sdkAutoMain = async (options: SdkAutoOptions) => {
  const sdkContext = await getSdkAutoContext(options);
  let workflowContext: WorkflowContext | undefined = undefined;
  let workflowFilterSdkMainStatus: SDKAutomationState | undefined = undefined;
  // identify the sdkAutoMain whether to run the sdk filter or SdkGen
  const runSdkFilter = options.filterSwaggerToSdk;

  try {
    if (runSdkFilter) {
      await workflowFilterSdkMain(sdkContext);
    } else {
      workflowContext = await workflowInit(sdkContext);
      await workflowMain(workflowContext);
    }
  } catch (e) {
    if (workflowContext) {
      sdkContext.logger.error(`FatalError: ${e.message}. Please refer to the inner logs for details or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
      workflowContext.status = 'failed';
      setFailureType(workflowContext, FailureType.PipelineFrameworkFailed);
      workflowContext.messages.push(e.message);
    }
    if (e.stack) {
      sdkContext.logger.error(`ErrorStack: ${e.stack}.`);
    }
    if (runSdkFilter) { 
      workflowFilterSdkMainStatus = 'failed';
      console.log(`##vso[task.setVariable variable=SkipAll;isOutput=true]true`);
      // hardcode the skipped job name when the runSdkFilter failed. That can help customer can easily jump to devops pipeline info
      console.log(`##vso[task.setVariable variable=SkippedJobs]azure-sdk-for-go`);
      console.log(`##vso[task.complete result=Failed;]`);
    }
  } finally {
    if (workflowContext) {
      await sdkAutoReportStatus(workflowContext);
    }
    await loggerWaitToFinish(sdkContext.logger);
    if (runSdkFilter) {
      return workflowFilterSdkMainStatus;
    } else {
      return workflowContext?.status;
    }
  }
};

export const getLanguageByRepoName = (repoName: string) => {
  if (!repoName) {
    return 'unknown';
  } else if (repoName.includes('js')) {
    return 'JavaScript';
  } else if (repoName.includes('go')) {
    return 'Go';
  } else if (repoName.includes('net')) {
    return '.Net';
  } else if (repoName.includes('java')) {
    return 'Java';
  } else if (repoName.includes('python')) {
    return 'Python';
  } else {
    return repoName;
  }
};
