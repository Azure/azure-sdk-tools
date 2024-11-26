import * as path from 'path';
import { PackageResult } from './GenerateOutput';
import { WorkflowContext } from '../automation/workflow2';
import { repoKeyToString } from '../utils/githubUtils';
import { SDKAutomationState } from '../automation/sdkAutomationState';
import { parseSemverVersionString } from '../utils/parseSemverVersionString';
import { getSuppressionLines, SDKSuppressionContentList } from '../utils/handleSuppressionLines2';

/**
 * The data that describes an SDK repository package.
 */
export interface SDKRepositoryPackageData {
  /**
   * The name of the package.
   */
  readonly name: string;
  /**
   * The relative path to the root of the package folder from the root of the SDK repository.
   */
  readonly relativeFolderPath: string;
  /**
   * The relative path to the root of the extra package folders from the root of the SDK repository.
   */
  extraRelativeFolderPaths: string[];
  /**
   * The current status of creating and uploading this package.
   */
  status: SDKAutomationState;
  /**
   * Message to be shown for this package.
   */
  messages: string[];
  /**
   * Does this package has breaking change.
   */
  hasBreakingChange?: boolean;
  /**
   * The URL of the blob where this SDK repository package's logs will be written to.
   */
  logsBlobUrl: string;
  /**
   * The URLs of the blobs where this SDK repository package's artifacts will be written to.
   */
  artifactBlobUrls?: string[];
  /**
   * The URLs of the generated apiView.
   */
  apiViewUrl?: string;
  /**
   * Used to indicate whether the package should be released as public
   */
  isPrivatePackage: boolean;
  /**
   * The installation instructions for this package.
   */
  installationInstructions?: string;
  /**
   * Lite installation instruction for this package.
   */
  liteInstallationInstruction?: string | undefined;
  /**
   * The URL of the blob where this SDK repository package's installation instructions will be
   * written to.
   */
  installationInstructionsBlobUrl?: string;
  /**
   * The URL to the created generation pull request for this SDK repository package.
   */
  generationPullRequestUrl?: string;
  /**
   * The URL to the diff page of the created generation pull request for this SDK repository package.
   */
  generationPullRequestDiffUrl?: string;
  /**
   * The URL to the integration pull request for this SDK repository package.
   */
  integrationPullRequestUrl?: string;
  /**
   * The files in this package that have changed.
   */
  readonly changedFilePaths: string[];
  /**
   * The name of the generation branch for this package.
   */
  readonly generationBranch: string;
  /**
   * The repository where the SDK's generation pull request and branch will be created. This is the
   * first repository where the automatically generated SDK will appear.
   */
  readonly generationRepository: string;
  /**
   * The URL to this package's generation repository.
   */
  readonly generationRepositoryUrl: string;
  /**
   * The name of the integration branch for this package.
   */
  readonly integrationBranch: string;
  /**
   * The repository where the SDK's integration/staging pull request and branch will be created. The
   * SDK's integration branch and pull request are where merged SDK generation pull requests will be
   * staged before they are merged and published in the main repository.
   */
  readonly integrationRepository: string;
  /**
   * Whether or not this SDK repository package's generation branch should be based off of the
   * integration branch or the main branch.
   */
  readonly useIntegrationBranch: boolean;
  /**
   * The main repository for the SDK. This is where the SDK packages are published from.
   */
  readonly mainRepository: string;
}

export type PackageData = SDKRepositoryPackageData & {
  artifactPaths: string[];
  apiViewArtifactPath?: string;
  language?: string;
  changelogs: string[];
  breakingChangeItems?: string[];
  version?: string;
  presentSuppressionLines: string[];
  absentSuppressionLines: string[];
  parseSuppressionLinesErrors: string[];
  sdkSuppressionFilePath: string | undefined;

  /**
   * Is the package a beta version of a management plane SDK
   */
  isBetaMgmtSdk?: boolean;
  isDataPlane: boolean;
};

export const getGenerationBranchName = (context: WorkflowContext, packageName: string) => {
  return `${context.config.branchPrefix}/${context.config.pullNumber}/${packageName.replace('/', '_')}`;
};
export const getIntegrationBranchName = (context: WorkflowContext, packageName: string) => {
  return `${context.config.branchPrefix}/${packageName.replace('/', '_')}`;
};

// tslint:disable-next-line: max-line-length
export const getPackageData = (context: WorkflowContext, result: PackageResult, suppressionContentList?: SDKSuppressionContentList): PackageData => {
  const relativeFolderPath = result.path[0];
  if (!relativeFolderPath) {
    // Allow empty package for go sdk
    // throw new Error('Empty path array in package result');
  }
  const name = result.packageName ?? path.basename(relativeFolderPath);
  const extraRelativeFolderPaths = result.path.slice(1);

  const integrationRepository = repoKeyToString(context.sdkRepoConfig.integrationRepository);

  const hasBreakingChange = result.changelog?.hasBreakingChange;
  const breakingChangeItems = result.changelog?.breakingChangeItems;

  /**
  * If the identified readme files only include changes to the management plane (resource manager),
  * the pull request will be considered a management plane PR and
  * the beta version of the SDK will be excluded from flagging any SDK breaking changes.
  * However, if the identified readme files include changes to other areas (such as the data plane),
  * the beta version will not be excluded from flagging SDK breaking changes.
  * In this case, SDK reviewers will need to perform a manual review of the pull request.
  */

  const readmeMdIsManagementPlane = result.readmeMd && result.readmeMd.every(
    item => item.split('/').includes('resource-manager')
  );

  // same logic as src/utils/utils.ts/removeDuplicatesFromRelatedFiles
  const typespecProjectIsManagementPlane = result.typespecProject && result.typespecProject.every(
    item => item.endsWith('.Management')
  );

  const isDataPlane = !(readmeMdIsManagementPlane || typespecProjectIsManagementPlane);

  const isBetaMgmtSdk = !isDataPlane && parseSemverVersionString(result.version, result.language)?.versionType?.toLowerCase() === 'beta';

  let presentSuppressionLines: string[] = [];
  let absentSuppressionLines: string[] = [];
  let parseSuppressionLinesErrors: string[] = [];
  let sdkSuppressionFilePath: string | undefined = undefined;

  const packageTSForReadmeMdKey = result.typespecProject ? result.typespecProject[0] : result.readmeMd ? `${context.config.sdkName == 'azure-sdk-for-go' ? 'specification/' : ''}${result.readmeMd[0]}` : null;
  const suppressionContent = packageTSForReadmeMdKey ? suppressionContentList?.get(packageTSForReadmeMdKey) : undefined;
  if ((suppressionContent !== undefined) && !isBetaMgmtSdk) {
    if (breakingChangeItems && breakingChangeItems.length > 0) {
      ({ presentSuppressionLines, absentSuppressionLines } = getSuppressionLines(suppressionContent.content, name, breakingChangeItems, context));
    }
    parseSuppressionLinesErrors = suppressionContent.errors;
    sdkSuppressionFilePath = suppressionContent.sdkSuppressionFilePath;
  }

  if(context.specPrInfo) {
    sdkSuppressionFilePath = `https://github.com/${context.specPrInfo.head.owner}/${context.specPrInfo.head.repo}/blob/${context.specPrHeadBranch}/${sdkSuppressionFilePath}`;
  }
  return {
    name,
    isDataPlane,
    relativeFolderPath,
    extraRelativeFolderPaths,
    status: result.result,
    messages: [],
    absentSuppressionLines,
    presentSuppressionLines,
    parseSuppressionLinesErrors,
    sdkSuppressionFilePath: sdkSuppressionFilePath,
    isBetaMgmtSdk,
    logsBlobUrl: '',
    isPrivatePackage: false,
    changedFilePaths: [],
    generationBranch: getGenerationBranchName(context, name),
    generationRepository: integrationRepository,
    generationRepositoryUrl: integrationRepository,
    integrationBranch: getIntegrationBranchName(context, name),
    integrationRepository,
    useIntegrationBranch: true,
    mainRepository: repoKeyToString(context.sdkRepoConfig.mainRepository),
    installationInstructions: result.installInstructions?.full,
    liteInstallationInstruction: result.installInstructions?.lite,
    hasBreakingChange,
    artifactPaths: result.artifacts ?? [],
    apiViewArtifactPath: result.apiViewArtifact ?? undefined,
    artifactBlobUrls: [],
    changelogs: result.changelog?.content.split('\n') ?? [],
    breakingChangeItems,
    version: result.version
  };
};
