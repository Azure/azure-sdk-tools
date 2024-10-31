import { Logger } from '@azure/logger-js';
import {
  a,
  any,
  b,
  BlobStorageBlob,
  BlobStorageBlockBlob,
  BlobStoragePrefix,
  body,
  BuilderActions,
  contains,
  details,
  getParentFolderPath,
  getPathName,
  getRepository,
  getRepositoryBranch,
  getRepositoryFullName,
  GitHubComment,
  GitHubPullRequest,
  h3,
  html,
  img,
  joinPath,
  LIBuilder,
  Repository,
  RepositoryBranch,
  RepositoryConfiguration,
  StringMap,
  URLBuilder,
  where,
  h4,
  getGithubRepositoryUrl,
  first,
  RealGitHub,
  getFilesChangedFromFullDiff
} from '@ts-common/azure-js-dev-tools';
import { executeAndLog, getDataBlob, trimNewLine, SDKAutomationContext } from './sdkAutomation';
import { getSDKAutomationStateImageBlob, getSDKAutomationStateString, SDKAutomationState } from './sdkAutomationState';
import { SDKRepositoryData } from './sdkRepository';
import { SDKRepositoryPackageData } from './langSpecs/sdkRepositoryPackage';
import {
  getSpecificationPullRequestGeneration,
  SpecificationPullRequestGeneration,
  SpecificationPullRequestGenerationData,
  getLanguageConfigurationForRepository
} from './specificationPullRequestGeneration';
import { SpecificationReadmeMdFile } from './specificationReadmeMdFile';
import {
  SpecificationRepositoryConfiguration,
  getSpecificationRepositoryConfiguration
} from './specificationRepositoryConfiguration';
import { addPullRequestLabel, removePullRequestLabel } from './utils/githubUtils';
import { existsSync, readFileSync } from 'fs';
import * as Handlebars from 'handlebars';
import * as format from '@azure/swagger-validation-common';
import * as fs from 'fs-extra';

/**
 * The data that describes a OpenAPI specification pull request.
 */
export interface SpecificationPullRequestData {
  /**
   * The GitHub repository that should be used for getting files.
   */
  readonly specPRRepository: Repository;
  /**
   * The SHA hash for that commit that should be used for getting files.
   */
  readonly specPRCommit: string;
  /**
   * The GitHub repository that the pull request exists in.
   */
  readonly baseRepository: Repository;
  /**
   * The pull request's number.
   */
  readonly number: number;
  /**
   * The GitHub repository that the pull request is attempting to merge from.
   */
  readonly headRepository: Repository;
  /**
   * The branch that the pull request is attempting to merge from.
   */
  readonly headBranch: RepositoryBranch;
  /**
   * The SHA hash for the commit that is trying to merge.
   */
  readonly headCommit: string;
  /**
   * The branch that the pull request is attempting to merge into.
   */
  readonly baseBranch: RepositoryBranch;
  /**
   * The URL to the html version of the pull request.
   */
  readonly htmlUrl: string;
  /**
   * The temporary SHA hash for the commit that will result if the pull request is merged. The
   * initial event that GitHub sends when a pull request is created will not have a merge commit, so
   * in those cases this will be undefined.
   */
  readonly mergeCommit: string | undefined;

  readonly merged: boolean | undefined;
  /**
   * The title of the pull request.
   */
  readonly title: string;
  /**
   * The id of the comment that SDK Automation will write its status to.
   */
  commentId?: number;
  /**
   * The URL of the blob where this pull request's data will be written to.
   */
  readonly dataBlobUrl: string;
  /**
   * Data pertaining to this specific generation.
   */
  readonly generation: SpecificationPullRequestGenerationData;
}

/**
 * An object that can be used to interact with a OpenAPI specification pull request.
 */
export class SpecificationPullRequest {
  private readonly specRepoFolder: string;

  /**
   * Create a new SpecificationPullRequest object that can be used to interact with the existing
   * GitHub OpenAPI specification pull request.
   * @param context The SDKAutomation context that the SpecificationPullRequest should use.
   * @param automationWorkingPrefix The working prefix for the SDK Automation application.
   * @param dataBlob The blob storage blob that this pull request's generation data will be written
   * to.
   * @param generation The SpecificationPullRequestGeneration object that contains information about
   * this specification generation.
   * @param data The data that describes this OpenAPI specification pull request.
   * @param isTriggeredByUP Whether the SDKAutomation is triggered by unified pipeline or not.
   * @param sdkRepoName Selected SDK repo name to generate. Used in pipeline.
   */
  constructor(
    public readonly context: SDKAutomationContext,
    private readonly automationWorkingPrefix: BlobStoragePrefix,
    public readonly dataBlob: BlobStorageBlockBlob,
    public readonly generation: SpecificationPullRequestGeneration,
    public readonly data: SpecificationPullRequestData,
    public readonly isTriggeredByUP: boolean,
    private readonly sdkRepoName?: string
  ) {
    this.specRepoFolder = joinPath(context.automationWorkingFolderPath, this.data.specPRRepository.name);
  }

  /**
   * Generate SDKs and create pull requests for any services that were modified or added in this
   * specification pull request.
   */
  public async generateModifiedServices(): Promise<void> {
    await this.cloneSpecRepo();
    await this.populateSDKRepositoryReadmeMdFilesToGenerate();
    await this.generation.generateSDKRepositories();
    await this.manipulateBreakingChangeLabel();
    await this.prepareUnifiedPipelineMessage();
    this.throwErrorIfFailed();
  }

  public async getSDKReposToTrigger(): Promise<string[]> {
    await this.cloneSpecRepo();
    const readmeMdFilesToGenerate: SpecificationReadmeMdFile[] = await this.getReadmeMdFilesToGenerate();
    return this.getSDKReposFromReadmeMdFiles(readmeMdFilesToGenerate);
  }

  public async getSDKReposFromReadmeMdFiles(readmeMdFilesToGenerate: SpecificationReadmeMdFile[]): Promise<string[]> {
    const logger: Logger = this.generation.logger;
    const readmeMdRepositories: string[] = [];
    for (const readmeMdFileToGenerate of readmeMdFilesToGenerate) {
      await logger.logSection(
        `Looking for repositories to generate in "${readmeMdFileToGenerate.relativeFilePath}"...`
      );
      const readmeMdRepositoryConfigurations:
        | RepositoryConfiguration[]
        | undefined = await readmeMdFileToGenerate.getSwaggerToSDKRepositoryConfigurations();

      if (readmeMdRepositoryConfigurations) {
        for (const readmeMdRepositoryConfiguration of readmeMdRepositoryConfigurations) {
          if (readmeMdRepositories.indexOf(readmeMdRepositoryConfiguration.repo) === -1) {
            await this.context.logger.logInfo(
              `Add new SDK repo task to trigger ${readmeMdRepositoryConfiguration.repo}`
            );
            readmeMdRepositories.push(readmeMdRepositoryConfiguration.repo);
          }
        }
      }
    }
    return readmeMdRepositories;
  }

  public async prepareUnifiedPipelineMessage(): Promise<void> {
    if (!this.isTriggeredByUP) {
      return;
    }
    let isShowLiteInstruction: boolean = false;
    if (this.data.generation.sdkRepositories
      .filter(repo => repo.changedPackages
        ?.filter(pack => pack.liteInstallationInstruction !== undefined).length
        ).length > 0) {
      isShowLiteInstruction = true;
    }

    const subtitle = getPullRequestCommentSubtitleForSDK(
      this.automationWorkingPrefix,
      this.sdkRepoName!,
      this.data,
      isShowLiteInstruction,
      this.context.sdkAutomationVersion
    );

    const commentBody = getPullRequestCommentBodyForSDK(
      this.automationWorkingPrefix,
      this.sdkRepoName!,
      this.data,
      this.context.sdkAutomationVersion
    );

    const statusMap: Map<string, string> = new Map([
      ['pending', 'Error'],
      ['inprogress', 'Error'],
      ['failed', 'Error'],
      ['warning', 'Warning'],
      ['succeeded', 'Info']
    ]);

    const type = statusMap.get(String(this.generation.sdkRepositories[0].data.status).toLowerCase());

    const pipelineResultData: format.MessageRecord = {
      type: 'Markdown',
      mode: 'replace',
      level: type as format.MessageLevel,
      message: commentBody,
      time: new Date()
    };

    const decode = (str: string): string => Buffer.from(str, 'base64').toString('binary');
    const encode = (str: string): string => Buffer.from(str, 'binary').toString('base64');
    console.log(`##vso[task.setVariable variable=SubTitle]${encode(subtitle)}`);
    console.log(decode(encode(subtitle)));

    await this.context.logger.logInfo('Writing unified pipeline message to pipe.log');
    fs.writeFileSync('output/pipe.log', JSON.stringify(pipelineResultData)
      .replace(/\\n/g, '')
      .replace(/\\\"/g, '\'') + '\n');
    return;
  }

  public throwErrorIfFailed(): void {
    for (const sdkRepository of this.generation.sdkRepositories) {
      if (sdkRepository.data.status === 'failed') {
        if (this.isTriggeredByUP) {
          console.log('##vso[task.setVariable variable=ValidationResult]failure');
        }
        throw new Error('ResultFailure: The result is marked as failure due to at least one required step fails. Please refer to the detail log in pipeline run or local console for more information.');
      }
    }
    if (this.isTriggeredByUP) {
      console.log('##vso[task.setVariable variable=ValidationResult]success');
    }
    return;
  }

  public async manipulateBreakingChangeLabel(): Promise<void> {
    const sdkRepositories = this.generation.sdkRepositories;
    const specRepository: Repository = this.data.baseRepository;
    const specPRNumber: number = this.data.number;
    const specPullRequest = await this.context.github.getPullRequest(specRepository, specPRNumber);
    if (sdkRepositories.length > 0) {
      for (const sdkRepository of sdkRepositories) {
        const breakingChangeLabel = sdkRepository.language.breakingChangeLabel;
        if (breakingChangeLabel) {
          if (sdkRepository.data.hasBreakingChange) {
            await addPullRequestLabel(
              this.context.github,
              specRepository,
              specPullRequest,
              breakingChangeLabel,
              this.context.logger
            );
          } else {
            await removePullRequestLabel(
              this.context.github,
              specRepository,
              specPullRequest,
              breakingChangeLabel,
              this.context.logger
            );
          }
        }
      }
    } else {
      if (this.sdkRepoName) {
        const languageConfiguration = getLanguageConfigurationForRepository(
          this.sdkRepoName,
          this.context.supportedLanguageConfigurations
        );
        if (languageConfiguration?.breakingChangeLabel) {
          await removePullRequestLabel(this.context.github,
            specRepository,
            specPullRequest,
            languageConfiguration.breakingChangeLabel,
            this.context.logger
          );
        }
      }
    }
  }

  public async cloneSpecRepo(): Promise<void> {
    const git = this.context.git.scope({
      executionFolderPath: this.specRepoFolder,
      log: (text: string) => this.context.logger.logInfo(trimNewLine(text)),
      showCommand: true,
      throwOnError: true,
      runner: this.context.runner
    });

    await git.resetRepoFolder();

    await git.addRemote('origin', getGithubRepositoryUrl(this.data.specPRRepository));
    if (this.data.merged) {
      // Cannot fetch commit directly if PR is merged and is not the latest commit in master
      await git.fetch({ remoteName: 'origin' });
    } else {
      // Cannot checkout merge commit if we only fetch origin when PR is not merged
      await git.fetch({ remoteName: 'origin', refSpec: this.data.specPRCommit });
    }
    await git.checkout(this.data.specPRCommit, { localBranchName: 'sdkAutomation' });
  }

  /**
   * Populate the readme.md file lists to generate in each of the SDK repositories.
   */
  public async populateSDKRepositoryReadmeMdFilesToGenerate(): Promise<void> {
    let specificationRepositoryConfiguration =
      await getSpecificationRepositoryConfiguration(this.context, this.data.specPRRepository);
    if (specificationRepositoryConfiguration !== undefined) {
      specificationRepositoryConfiguration.specRepoOwner = this.data.specPRRepository.owner;
      const overrides = specificationRepositoryConfiguration.overrides;
      if (overrides !== undefined) {
        const repo = this.data.specPRRepository;
        const overrideDetail = overrides[`${repo.owner}/${repo.name}`] || overrides[repo.name];
        if (overrideDetail !== undefined) {
          specificationRepositoryConfiguration = { ...specificationRepositoryConfiguration, ...overrideDetail };
        }
      }
    }
    const readmeMdFilesToGenerate: SpecificationReadmeMdFile[] = await this.getReadmeMdFilesToGenerate();
    await this.addReadmeMdFilesToGenerateToSDKRepositories(
      readmeMdFilesToGenerate,
      specificationRepositoryConfiguration
    );
  }

  /**
   * Update the generation data blobs with the current state of the generation.
   */
  public async writeGenerationData(): Promise<void> {
    if (this.isTriggeredByUP) {
      return;
    }
    const specificationRepository: Repository = this.data.baseRepository;
    const specPRNumber: number = this.data.number;

    if (this.sdkRepoName === undefined) {
      const generationData: string = JSON.stringify(this.data, undefined, '  ');
      const generationHtml: string = getPullRequestCommentHTML(this.automationWorkingPrefix, this.data);
      await this.dataBlob.setContentsFromString(generationData, { contentType: 'application/json' });
      const specPRComments: GitHubComment[] = await this.context.github.getPullRequestComments(
        specificationRepository,
        specPRNumber
      );

      if (contains(specPRComments, (comment: GitHubComment) => comment.id === this.data.commentId)) {
        await this.context.github.updatePullRequestComment(
          specificationRepository,
          specPRNumber,
          this.data.commentId!,
          generationHtml
        );
      }

      await this.generation.writeGenerationData(generationData, generationHtml);
    } else {
      // Triggered from pipeline
      let isShowLiteInstruction: boolean = false;
      if (this.data.generation.sdkRepositories
        .filter(repo => repo.changedPackages
          ?.filter(pack => pack.liteInstallationInstruction !== undefined).length
        ).length > 0) {
        isShowLiteInstruction = true;
      }
      const generationHTML = getPullRequestCommentHTMLForSDK(
        this.sdkRepoName,
        this.data,
        isShowLiteInstruction,
        this.context.sdkAutomationVersion
      );

      if (this.data.commentId === undefined) {
        const specPRComments: GitHubComment[] = await this.context.github.getPullRequestComments(
          specificationRepository,
          specPRNumber
        );
        const targetComment = first(specPRComments, (comment: GitHubComment) => {
          if (comment.user.login !== this.context.githubCommentAuthorName) {
            return false;
          }
          const firstLine = comment.body.split('\n')[0];
          // tslint:disable-next-line: no-use-before-declare
          if (firstLine.indexOf(handleBarHelpers.renderSDKTitleMapping(this.sdkRepoName!)) === -1) {
            return false;
          }
          return true;
        });
        if (targetComment !== undefined) {
          this.data.commentId = targetComment.id;
        }
      }

      if (this.data.commentId !== undefined) {
        await this.context.github.updatePullRequestComment(
          specificationRepository,
          specPRNumber,
          this.data.commentId,
          generationHTML
        );
      } else {
        const githubComment = await this.context.github.createPullRequestComment(
          specificationRepository,
          specPRNumber,
          generationHTML
        );
        this.data.commentId = githubComment.id;
      }
    }
  }

  /**
   * Get the file paths relative to the root of the repository of the files that were modified in
   * this pull request.
   */
  public async getChangedFilesRelativePaths(): Promise<string[]> {
    const logger: Logger = this.generation.logger;
    const githubClient = await (this.context.github as RealGitHub).getClient(this.data.specPRRepository);

    let changedFilesRelativePaths: string[] = [];
    await logger.logSection(`Getting PR diff contents...`);
    const diffResponse = await githubClient.pulls.get({
      pull_number: this.data.number,
      repo: this.data.specPRRepository.name,
      owner: this.data.specPRRepository.owner,
      mediaType: {
        format: 'diff'
      }
    });
    const diffContent = diffResponse.data as unknown as string;
    changedFilesRelativePaths = getFilesChangedFromFullDiff(diffContent);
    await logger.logInfo(`PR diff response body contains ${changedFilesRelativePaths.length} changed files:`);
    for (const changedFileRelativePath of changedFilesRelativePaths) {
      await logger.logInfo(`  ${changedFileRelativePath}`);
    }
    return changedFilesRelativePaths;
  }

  /**
   * Get the file paths relative to the root of the repository of the readme.md files that are
   * related to this pull request's specification changes.
   */
  public async getReadmeMdFilesToGenerate(): Promise<SpecificationReadmeMdFile[]> {
    const logger: Logger = this.generation.logger;

    const result: SpecificationReadmeMdFile[] = [];
    const changedFilesRelativePaths: string[] = await this.getChangedFilesRelativePaths();
    if (changedFilesRelativePaths.length > 0) {
      const specificationChangedFileRelativePaths: string[] = where(changedFilesRelativePaths, (path: string) =>
        path.startsWith('specification/') && path.indexOf('/scenarios/') === -1
      );
      const readmeCache: StringMap<SpecificationReadmeMdFile | undefined> = {};

      for (const changedFileRelativePath of specificationChangedFileRelativePaths) {
        let folderRelativePath: string = getParentFolderPath(changedFileRelativePath);

        while (folderRelativePath && folderRelativePath !== '.') {
          const readmeMdRelativePath: string = joinPath(folderRelativePath, 'readme.md');
          const specificationReadmeMdFile = new SpecificationReadmeMdFile(
            this.specRepoFolder,
            logger,
            readmeMdRelativePath
          );
          if (specificationReadmeMdFile.contentsPath in readmeCache) {
            const cachedValue: SpecificationReadmeMdFile | undefined =
              readmeCache[specificationReadmeMdFile.contentsPath];
            if (!cachedValue) {
              folderRelativePath = getParentFolderPath(folderRelativePath);
            } else {
              break;
            }
          } else {
            const foundReadmeMdFile: boolean = existsSync(specificationReadmeMdFile.contentsPath);
            readmeCache[specificationReadmeMdFile.contentsPath] = foundReadmeMdFile
              ? specificationReadmeMdFile
              : undefined;
            if (!foundReadmeMdFile) {
              folderRelativePath = getParentFolderPath(folderRelativePath);
            } else {
              break;
            }
          }
        }
      }
      for (const readmeMdFile of Object.values(readmeCache)) {
        if (readmeMdFile) {
          result.push(readmeMdFile);
        }
      }

      await logger.logInfo(`Found ${result.length} readme.md files to generate:`);
      for (const readmeMdFileToGenerate of result) {
        await logger.logInfo(`  ${readmeMdFileToGenerate.relativeFilePath}`);
      }
    }

    return result;
  }

  /**
   * Get the proxy URL for the provided blob relative to this specification pull request's working
   * prefix.
   * @param blob The blob to get the proxy URL for.
   * @returns The proxy URL for the provided blob relative to this specification pull request's
   * working prefix.
   */
  public getBlobProxyUrl = (blob: BlobStorageBlob): string => {
    return this.context.blobProxy.getProxyURL(this.automationWorkingPrefix, blob);
  }

  /**
   * Add the provided readme.md relative file paths to the SDK repositories that they should be
   * generated for.
   * @param specificationRepositoryConfiguration The configuration for the specification repository.
   * @param readmeMdFilesToGenerate The readme.md relative file paths.
   */
  private async addReadmeMdFilesToGenerateToSDKRepositories(
    readmeMdFilesToGenerate: SpecificationReadmeMdFile[],
    specificationRepositoryConfiguration?: SpecificationRepositoryConfiguration
  ): Promise<void> {
    const logger: Logger = this.generation.logger;
    for (const readmeMdFileToGenerate of readmeMdFilesToGenerate) {
      await logger.logSection(
        `Looking for repositories to generate in "${readmeMdFileToGenerate.relativeFilePath}"...`
      );
      const readmeMdRepositoryConfigurations:
        | RepositoryConfiguration[]
        | undefined = await readmeMdFileToGenerate.getSwaggerToSDKRepositoryConfigurations();

      if (readmeMdRepositoryConfigurations) {
        for (const readmeMdRepositoryConfiguration of readmeMdRepositoryConfigurations) {
          if (this.sdkRepoName !== undefined && readmeMdRepositoryConfiguration.repo !== this.sdkRepoName) {
            await this.context.logger.logInfo(`Skip unselected SDK repo ${readmeMdRepositoryConfiguration.repo}`);
            continue;
          }
          await this.generation.addReadmeMdFileToGenerateForSDKRepository(
            readmeMdRepositoryConfiguration,
            readmeMdFileToGenerate,
            this.context.supportedLanguageConfigurations,
            specificationRepositoryConfiguration
          );
        }
      }
    }
    await this.writeGenerationData();
  }
}

/**
 * Get a new SpecificationPullRequest object for the provided details.
 * @param sdkAutomation The SDKAutomation object that is the current context.
 * @param automationWorkingPrefix The blob storage prefix that all of the pull request's data will be under.
 * @param pullRequestUrl The URL to the specification pull request.
 * @param newIteration Whether or not this request is for a new pull request iteration.
 * @param sdkRepoName Selected sdk repo to generate. Used in pipeline only.
 */
export async function getSpecificationPullRequest(
  context: SDKAutomationContext,
  automationWorkingPrefix: BlobStoragePrefix,
  pullRequest: GitHubPullRequest,
  newIteration: boolean,
  isTriggeredByUP: boolean,
  sdkRepoName?: string
): Promise<SpecificationPullRequest> {
  const isPipelineTriggered = sdkRepoName !== undefined;
  const pullRequestRepository: Repository = getPullRequestRepository(pullRequest);
  const headBranch: RepositoryBranch = getRepositoryBranch(pullRequest.head.label);
  let headRepository: Repository;
  if (!headBranch.owner || headBranch.owner === pullRequestRepository.owner) {
    headRepository = pullRequestRepository;
  } else {
    headRepository = {
      owner: headBranch.owner,
      name: pullRequestRepository.name
    };
  }
  const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
    automationWorkingPrefix,
    pullRequestRepository,
    pullRequest.number,
    sdkRepoName
  );
  const pullRequestDataBlob: BlobStorageBlockBlob = getDataBlob(pullRequestPrefix);

  let pullRequestCommentId: number | undefined;
  let pullRequestData: SpecificationPullRequestData | undefined;

  if (!isPipelineTriggered && (await pullRequestDataBlob.exists())) {
    const pullRequestDataBlobContents: string | undefined = (await pullRequestDataBlob.getContentsAsString()).contents;
    if (pullRequestDataBlobContents) {
      pullRequestData = JSON.parse(pullRequestDataBlobContents) as SpecificationPullRequestData;
      pullRequestCommentId = pullRequestData.commentId;
    }
  }

  await context.logger.logInfo(`Retrieving pull request from ${pullRequest.html_url} ...`);

  const getBlobProxyUrl = (blob: BlobStorageBlob) => context.blobProxy.getProxyURL(automationWorkingPrefix, blob);

  const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
    pullRequest.number,
    pullRequestPrefix,
    {
      ...context,
      generationWorkingFolderPath: context.automationWorkingFolderPath,
      isPipelineTriggered,
      getBlobProxyUrl
    },
    context.buildID,
    !newIteration && pullRequestData ? pullRequestData.generation.number : undefined
  );

  const logger: Logger = generation.logger;
  await logger.logSection(`Received pull request change webhook request from GitHub for "${pullRequest.html_url}".`);
  await context.logContext(logger);

  return executeAndLog(logger, undefined, async () => {
    await logger.logSection(
      `Getting generation state from ${getBlobProxyUrl(pullRequestDataBlob)}...`
    );

    let specPRRepository: Repository;
    let specPRCommit: string;
    if (pullRequest.merge_commit_sha) {
      specPRRepository = pullRequestRepository;
      specPRCommit = pullRequest.merge_commit_sha;
    } else {
      specPRRepository = headRepository;
      specPRCommit = pullRequest.head.sha;
    }
    await logger.logInfo(`Using Commit: ${specPRCommit}`);

    if (pullRequestCommentId === undefined && !isPipelineTriggered) {
      const commentText: string = getPullRequestCommentHTML(automationWorkingPrefix);
      const comment: GitHubComment = await context.github.createPullRequestComment(
        pullRequestRepository,
        pullRequest.number,
        commentText
      );
      pullRequestCommentId = comment.id;
    }

    if (!pullRequestData || newIteration) {
      pullRequestData = {
        specPRRepository,
        specPRCommit,
        baseRepository: pullRequestRepository,
        headRepository,
        number: pullRequest.number,
        headBranch: getRepositoryBranch(pullRequest.head.label),
        headCommit: pullRequest.head.sha,
        baseBranch: getRepositoryBranch(pullRequest.base.label),
        htmlUrl: pullRequest.html_url,
        mergeCommit: pullRequest.merge_commit_sha,
        merged: pullRequest.merged,
        title: pullRequest.title,
        commentId: pullRequestCommentId,
        dataBlobUrl: getBlobProxyUrl(pullRequestDataBlob),
        generation: generation.data
      };
    } else {
      generation.data = pullRequestData.generation;
    }

    const result = new SpecificationPullRequest(
      context,
      automationWorkingPrefix,
      pullRequestDataBlob,
      generation,
      pullRequestData,
      isTriggeredByUP,
      sdkRepoName
    );
    await result.writeGenerationData();

    generation.specificationPullRequest = result;

    return result;
  });
}

const pullRequestUrlRepositoryNameRegex: RegExp = /https:\/\/api\.github\.com\/repos\/(.*)\/pulls\/.*/;
/**
 * Get the GitHub repository from the provided pull request URL.
 * @param pullRequestUrl The URL to get the GitHub repository from.
 */
export function getPullRequestRepository(pullRequestUrl: string | GitHubPullRequest): Repository {
  if (typeof pullRequestUrl !== 'string') {
    pullRequestUrl = pullRequestUrl.url;
  }
  return getRepository(pullRequestUrl.match(pullRequestUrlRepositoryNameRegex)![1]);
}

/**
 * Get the prefix that all blobs related to the pull request will be put in.
 * @param workingPrefix The prefix that this service operates under.
 * @param repository The repository that the pull request exists in.
 * @param pullRequestNumber The pull request number.
 */
export function getPullRequestPrefix(
  workingPrefix: BlobStoragePrefix,
  repository: string | Repository,
  pullRequestNumber: number,
  sdkRepoName: string | undefined
): BlobStoragePrefix {
  let prefix = workingPrefix
    .getPrefix(getRepositoryFullName(repository) + '/')
    .getPrefix(pullRequestNumber.toString() + '/');
  if (sdkRepoName !== undefined) {
    prefix = prefix.getPrefix(sdkRepoName + '/');
  }

  return prefix;
}

/**
 * Get the comment HTML for the provided data.
 * @param automationWorkingPrefix The working prefix for the SDK Automation application.
 * @param specificationPullRequestData The data that describes the specification pull request.
 */
function getPullRequestCommentHTML(
  automationWorkingPrefix: BlobStoragePrefix,
  specificationPullRequestData?: SpecificationPullRequestData
): string {
  return html(
    body([
      h4(['In Testing, Please Ignore']),
      getPullRequestCommentHeaderHTML(specificationPullRequestData),
      getPullRequestCommentMessageHTML(specificationPullRequestData),
      getPullRequestCommentSDKRepositoriesHTML(automationWorkingPrefix, specificationPullRequestData)
    ])
  );
}

function getPullRequestCommentHeaderHTML(specificationPullRequestData?: SpecificationPullRequestData): string {
  return h3([
    specificationPullRequestData && ` ${getLogsLink(specificationPullRequestData.generation.logsBlobUrl)}`,
    specificationPullRequestData && ` (Generated from ${getHeadCommitLink(specificationPullRequestData)}, `,
    specificationPullRequestData && `Iteration ${specificationPullRequestData.generation.number})`
  ]);
}

function getPullRequestCommentMessageHTML(
  specificationPullRequestData?: SpecificationPullRequestData
): string | undefined {
  return specificationPullRequestData && specificationPullRequestData.generation.message;
}

function getPullRequestCommentSDKRepositoriesHTML(
  automationWorkingPrefix: BlobStoragePrefix,
  specificationPullRequestData?: SpecificationPullRequestData
): string {
  let result = '';
  const sdkRepositories: SDKRepositoryData[] | undefined =
    specificationPullRequestData && specificationPullRequestData.generation.sdkRepositories;
  if (any(sdkRepositories)) {
    for (const sdkRepository of sdkRepositories) {
      result += getPullRequestCommentSDKRepositoryHTML(automationWorkingPrefix, sdkRepository);
    }
  }
  return result;
}

function getPullRequestCommentSDKRepositoryHTML(
  automationWorkingPrefix: BlobStoragePrefix,
  sdkRepositoryData: SDKRepositoryData
): BuilderActions<LIBuilder> {
  return details(repositoryDetails => {
    repositoryDetails.summary(
      b([
        getStateImgElement(automationWorkingPrefix, sdkRepositoryData.status),
        ` ${sdkRepositoryData.languageName}: `,
        ` ${getLink(sdkRepositoryData.mainRepositoryUrl, sdkRepositoryData.mainRepository)}`,
        sdkRepositoryData.logsBlobUrl && ` ${getLogsLink(sdkRepositoryData.logsBlobUrl)}`,
        sdkRepositoryData.diffBlobUrl && ` ${getDiffLink(sdkRepositoryData.diffBlobUrl)}`
      ])
    );
    repositoryDetails.ul(packageList => {
      if (!any(sdkRepositoryData.changedPackages)) {
        if (sdkRepositoryData.status === 'pending') {
          packageList.li('Package generation pending.');
        } else if (sdkRepositoryData.status === 'inProgress') {
          packageList.li('Package generation in progress.');
        } else {
          packageList.li('No packages generated.');
        }
      } else {
        for (const sdkPackage of sdkRepositoryData.changedPackages) {
          packageList.li(getPullRequestCommentSDKPackageHTML(automationWorkingPrefix, sdkPackage));
        }
      }
    });
  });
}

function getPullRequestCommentSDKPackageHTML(
  automationWorkingPrefix: BlobStoragePrefix,
  sdkPackage: SDKRepositoryPackageData
): BuilderActions<LIBuilder> {
  return [
    getStateImgElement(automationWorkingPrefix, sdkPackage.status),
    ` ${sdkPackage.name}`,
    sdkPackage.logsBlobUrl && ` ${getLogsLink(sdkPackage.logsBlobUrl)}`,
    sdkPackage.installationInstructionsBlobUrl && ` ${getInstructionsLink(sdkPackage.installationInstructionsBlobUrl)}`,
    sdkPackage.generationPullRequestUrl && ` ${getGenerationPullRequestLink(sdkPackage.generationPullRequestUrl)}`,
    sdkPackage.integrationPullRequestUrl && ` ${getIntegrationPullRequestLink(sdkPackage.integrationPullRequestUrl)}`,
    packageArtifacts => {
      const artifactBlobUrls: string[] | undefined = sdkPackage.artifactBlobUrls;
      if (artifactBlobUrls && artifactBlobUrls.length > 0) {
        packageArtifacts.ul(artifactList => {
          for (const artifactBlobUrl of artifactBlobUrls) {
            artifactList.li(getArtifactLink(artifactBlobUrl));
          }
        });
      }
    }
  ];
}

function getLogsLink(logsUrl: string): string {
  return `[${getLink(logsUrl, 'Logs')}]`;
}

function getDiffLink(diffUrl: string): string {
  return `[${getLink(diffUrl, 'Diff')}]`;
}

function getGenerationPullRequestLink(generationPullRequestUrl: string): string {
  return `[${getLink(generationPullRequestUrl, 'Generation PR')}]`;
}

function getIntegrationPullRequestLink(integrationPullRequestUrl: string): string {
  return `[${getLink(integrationPullRequestUrl, 'Integration PR')}]`;
}

function getHeadCommitLink(specificationPRData: SpecificationPullRequestData): string {
  const generationCommitUrl = `${specificationPRData.htmlUrl}/commits/${specificationPRData.headCommit}`;
  return getLink(generationCommitUrl, specificationPRData.headCommit.substring(0, 7));
}

function getInstructionsLink(instructionsUrl: string): string {
  return `[${getLink(instructionsUrl, 'Instructions')}]`;
}

function getArtifactLink(artifactUrl: string): string {
  return getLink(artifactUrl, getPathName(URLBuilder.parse(artifactUrl).getPath()!));
}

function getLink(url: string, content: string): string {
  return a(ah => ah.href(url).content(content));
}

function getStateImgElement(automationWorkingPrefix: BlobStoragePrefix, state: SDKAutomationState): string {
  const imagePixelSize = 16;
  return img(stageImage =>
    stageImage
      .src(getSDKAutomationStateImageBlob(automationWorkingPrefix, state).getURL())
      .alt(getSDKAutomationStateString(state))
      .width(imagePixelSize)
      .height(imagePixelSize)
  );
}

const generationViewTemplate = readFileSync(`${__dirname}/templates/generationView.handlebars`).toString();
const commentSubtitleTemplate = readFileSync(`${__dirname}/templates/commentSubtitle.handlebars`).toString();
const commentDetailTemplate = readFileSync(`${__dirname}/templates/commentDetail.handlebars`).toString();
const generationView = Handlebars.compile(generationViewTemplate, { noEscape: true });
const commentSubtitle = Handlebars.compile(commentSubtitleTemplate, { noEscape: true });
const commentDetail = Handlebars.compile(commentDetailTemplate, { noEscape: true });
const githubStateEmoji: { [key in SDKAutomationState]: string } = {
  pending: '⌛',
  failed: '❌',
  inProgress: '🔄',
  succeeded: '️✔️',
  warning: '⚠️'
};
const handleBarHelpers = {
  renderFilename: (url: string) => getPathName(url),
  renderStatus: (status: SDKAutomationState) => `<code>${githubStateEmoji[status]}</code>`,
  renderMessages: (messages: string[]) => {
    if (messages.length > 100) {
      return `Only show 100 items here, please refer to log for details.<br>
      <pre>${messages.slice(0, 100).map(trimNewLine).join('\n')}</pre>`;
    } else {
      return `<pre>${messages.slice(0, 100).map(trimNewLine).join('\n')}</pre>`;
    }
  },
  renderMessagesUnifiedPipeline: (messages: string[]) => {
    if (messages.length > 50) {
      return `Only show 50 items here, please refer to log for details.<br>
      <pre>${messages.slice(0, 50).map(trimNewLine).join('<br>')}</pre>`;
    } else {
      return `<pre>${messages.slice(0, 50).map(trimNewLine).join('<br>')}</pre>`;
    }
  },
  renderSDKTitleMapping: (sdkRepoName: string) => {
    switch (sdkRepoName) {
      case 'azure-cli-extensions':
        return 'Azure CLI Extension Generation';
      case 'azure-sdk-for-trenton':
        return 'Trenton Generation';
      default:
        return sdkRepoName;
    }
  },
  isPublicRelease: (changedPackage: SDKRepositoryPackageData) => {
    return !changedPackage.isPrivatePackage;
  },
  renderSDKNameMapping: (sdkRepoName: string) => {
    switch (sdkRepoName) {
      case 'azure-cli-extensions':
        return 'Azure CLI';
      case 'azure-sdk-for-trenton':
        return 'Trenton';
      case 'azure-resource-manager-schemas':
        return 'Schema';
      default:
        return 'SDK';
    }
  }
};
Handlebars.registerHelper(handleBarHelpers);
function getPullRequestCommentHTMLForSDK(
  sdkRepoName: string,
  data: SpecificationPullRequestData,
  isShowInstruction: boolean,
  version?: string
): string {
  const commentBody = generationView({
    ...data,
    version,
    sdkRepoName,
    isShowInstruction
  })
    .replace(/>\s+</g, '><')
    .replace(/\\n\\n/g, '\n');

  return commentBody;
}

Handlebars.registerHelper(handleBarHelpers);
function getPullRequestCommentSubtitleForSDK(
  prefix: BlobStoragePrefix,
  sdkRepoName: string,
  data: SpecificationPullRequestData,
  isShowInstruction: boolean,
  version?: string
): string {
  const commentBody = commentSubtitle({
    ...data,
    version,
    sdkRepoName,
    isShowInstruction
  })
    .replace(/>\s+</g, '><')
    .replace(/\\n\\n/g, '\n');

  return commentBody;
}

Handlebars.registerHelper(handleBarHelpers);
function getPullRequestCommentBodyForSDK(
  prefix: BlobStoragePrefix,
  sdkRepoName: string,
  data: SpecificationPullRequestData,
  version?: string
): string {
  const commentBody = commentDetail({
    ...data,
    version,
    sdkRepoName
  })
    .replace(/>\s+</g, '><')
    .replace(/\\n\\n/g, '\n');

  return commentBody;
}
