import { RestEndpointMethodTypes } from '@octokit/rest';
import { Octokit } from '@octokit/rest';
import * as winston from 'winston';
import { getAuthenticatedOctokit, RepoKey } from '../utils/githubUtils';
import {
  FailureType,
  setFailureType,
  WorkflowContext,
  workflowInit,
  workflowMain
} from './workflow';
import {
  loggerConsoleTransport,
  loggerDevOpsTransport,
  loggerFileTransport,
  loggerTestTransport,
  sdkAutoLogLevels
} from './logging';
import path from 'path';
import { generateReport } from './reportStatus';

interface SdkAutoOptions {
  specRepo: RepoKey;
  sdkName: string;
  branchPrefix: string;
  localSpecRepoPath: string;
  localSdkRepoPath: string;
  tspConfigPath?: string;
  readmePath?: string;  
  pullNumber?: number;
  specCommitSha?: string;
  specRepoHttpsUrl?: string;
  workingFolder: string;

  github: {
    token?: string;
    id?: number;
    privateKey?: string;
    commentAuthorName?: string;
  };

  runEnv: 'local' | 'azureDevOps' | 'test';
}

type SpecPrInfo = {
  head: {owner: string; repo: string};
  base: {owner: string; repo: string};
}

export type SdkAutoContext = {
  config: SdkAutoOptions;
  octokit: Octokit;
  getGithubAccessToken: (owner: string) => Promise<string>;
  logger: winston.Logger;
  specPrInfo: SpecPrInfo | undefined;
  specPrBaseBranch: string | undefined;
  specPrHeadBranch: string | undefined;
  fullLogFileName: string;
  filterLogFileName: string;
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

  const fullLogFileName = path.join(options.workingFolder, 'full.log');
  const filterLogFileName = path.join(options.workingFolder, 'filter.log');
  logger.info(`Log to ${fullLogFileName}`);
  logger.add(loggerFileTransport(fullLogFileName));

  const [{ octokit, getGithubAccessToken, specPR }] = await Promise.all([
    getGithubContext(options, logger),
  ]);

  let specPrInfo: SpecPrInfo | undefined
  if (specPR) {
    specPrInfo = {
      head: {
        owner: specPR.head.repo.owner.login,
        repo: specPR.head.repo.name
      },
      base: {
        owner: specPR.base.repo.owner.login,
        repo: specPR.base.repo.name
      }
    };
  }
  return {
    config: options,
    octokit,
    getGithubAccessToken,
    logger,
    specPrInfo,
    specPrBaseBranch: specPR?.base.ref,
    specPrHeadBranch: specPR?.head.ref,
    fullLogFileName,
    filterLogFileName
  };
};

const getGithubContext = async (options: SdkAutoOptions, logger: winston.Logger) => {
  const {octokit, getToken: getGithubAccessToken} = getAuthenticatedOctokit(options.github, logger);

  if (!options.pullNumber) {
    return { octokit, getGithubAccessToken, specPR: undefined };
  }
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

  try {
    workflowContext = await workflowInit(sdkContext);
    await workflowMain(workflowContext);
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
  }
  if (workflowContext) {
    await generateReport(workflowContext);
  }
  return workflowContext?.status;
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
