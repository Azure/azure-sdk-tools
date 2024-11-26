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
} from './workflow2';
import {
  loggerConsoleTransport,
  loggerDevOpsTransport,
  loggerTestTransport,
  loggerWaitToFinish,
  sdkAutoLogLevels
} from './logging';
//import { sdkAutoReportStatus } from './reportStatus';

interface SdkAutoOptions_New {
  specRepo: RepoKey;
  sdkName: string;
  branchPrefix: string;
  localSpecRepoPath: string;
  localSdkRepoPath: string;
  tspConfigPath?: string;
  readmePath?: string;  
  pullNumber?: number;
  specCommitSha?: string;
  specPrHttpsUrl?: string;

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

export type SdkAutoContext_New = {
  config: SdkAutoOptions_New;
  octokit: Octokit;
  getGithubAccessToken: (owner: string) => Promise<string>;
  logger: winston.Logger;
  specPrInfo: SpecPrInfo | undefined;
  specPrBaseBranch: string | undefined;
  specPrHeadBranch: string | undefined;
  workingFolder: string;
};


export const getSdkAutoContext = async (options: SdkAutoOptions_New): Promise<SdkAutoContext_New> => {
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

  const [{ octokit, getGithubAccessToken, specPR }] = await Promise.all([
    getGithubContext(options, logger),
  ]);

  const workingFolder = '.';
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
    workingFolder,
  };
};

const getGithubContext = async (options: SdkAutoOptions_New, logger: winston.Logger) => {
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

export const sdkAutoMain_New = async (options: SdkAutoOptions_New) => {
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
    //await sdkAutoReportStatus(workflowContext);
  }
  await loggerWaitToFinish(sdkContext.logger);
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
