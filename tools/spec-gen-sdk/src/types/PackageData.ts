import { SDKRepositoryPackageData } from '../langSpecs/sdkRepositoryPackage';
import { PackageResult } from './GenerateOutput';
import * as path from 'path';
import { WorkflowContext } from '../automation/workflow';
import { repoKeyToString } from '../utils/githubUtils';
import { getSuppressionLines, SDKSuppressionContentList } from '../utils/handleSuppressionLines';
import { parseSemverVersionString } from '../utils/parseSemverVersionString';

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
    sdkSuppressionFilePath: sdkSuppressionFilePath && `https://github.com/${context.specPrInfo.head.owner}/${context.specPrInfo.head.repo}/blob/${context.specPrHeadBranch}/${sdkSuppressionFilePath}`,
    isBetaMgmtSdk,
    logsBlobUrl: '',
    isPrivatePackage: !context.config.storage.isPublic,
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
