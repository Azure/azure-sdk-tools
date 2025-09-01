import * as path from 'node:path';
import * as _ from 'lodash';
import * as fs from 'node:fs';
import { repoKeyToString } from '../utils/repo';
import { runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../utils/fsUtils';
import { GenerateInput } from '../types/GenerateInput';
import { GenerateOutput, getGenerateOutput } from '../types/GenerateOutput';
import { SDKAutomationState } from './sdkAutomationState';
import { SdkSuppressionsYml, SdkPackageSuppressionsEntry, validateSdkSuppressionsFile } from '../types/sdkSuppressions';
import { parseYamlContent } from '../utils/utils';
import { SDKSuppressionContentList } from '../utils/handleSuppressionLines';
import { configError, configWarning, externalError, toolError } from '../utils/messageUtils';
import { FailureType, WorkflowContext } from '../types/Workflow';
import { setFailureType } from '../utils/workflowUtils';

const fileGenerateInput = 'generateInput.json';
const fileGenerateOutput = 'generateOutput.json';
export const workflowCallGenerateScript = async (context: WorkflowContext, changedFiles: string[], relatedReadmeMdFiles: string[], relatedTypeSpecProjectFolder: string[]) => {
  const statusContext = { status: 'succeeded' as SDKAutomationState };
  let generateOutput: GenerateOutput | undefined = undefined;
  let message = '';
  const generateInput: GenerateInput = {
    specFolder: path.relative(context.config.localSdkRepoPath, context.config.localSpecRepoPath),
    headSha: context.config.specCommitSha,
    repoHttpsUrl: context.config.specRepoHttpsUrl ?? '',
    changedFiles,
    apiVersion: context.config.apiVersion,
    runMode: context.config.runMode,
    sdkReleaseType: context.config.sdkReleaseType,
    installInstructionInput: {
      isPublic: !context.isPrivateSpecRepo,
      downloadUrlPrefix: '',
      downloadCommandTemplate: 'downloadCommand',
    },
  };

  if (context.swaggerToSdkConfig.generateOptions.generateScript === undefined) {
    message = toolError('generateScript is not configured in the swagger-to-sdk config. Please refer to the schema.');
    context.logger.error(message);
    setFailureType(context, FailureType.SpecGenSdkFailed);
    throw new Error(message);
  }

  context.logger.log('section', 'Call generateScript');

  // One of relatedTypeSpecProjectFolder and relatedReadmeMdFiles must be non-empty
  if (relatedTypeSpecProjectFolder?.length > 0) {
    generateInput.relatedTypeSpecProjectFolder = relatedTypeSpecProjectFolder;
  } else {
    generateInput.relatedReadmeMdFiles = relatedReadmeMdFiles;
  }

  writeTmpJsonFile(context, fileGenerateInput, generateInput);
  deleteTmpJsonFile(context, fileGenerateOutput);

  await runSdkAutoCustomScript(context, context.swaggerToSdkConfig.generateOptions.generateScript, {
    cwd: context.config.localSdkRepoPath,
    argTmpFileList: [fileGenerateInput, fileGenerateOutput],
    statusContext,
  });
  context.logger.log('endsection', 'Call generateScript');
  setSdkAutoStatus(context, statusContext.status);
  const generateOutputJson = readTmpJsonFile(context, fileGenerateOutput);
  if (generateOutputJson !== undefined) {
    generateOutput = getGenerateOutput(generateOutputJson);
  } else {
    message = externalError('Failed to read generateOutput.json. Please check if the generate script is configured correctly.');
    throw new Error(message);
  }

  /**
   * When the changedSpec involves multiple services, the callGenerateScript function will yield a larger set of packages in the generateOutput.
   * Within these packages, some may lack a packageName, exemplified by {"path": [ ""],"result": "failed"}, leading to a failed status.
   * However, it's crucial to proceed with processing the remaining packages.
   * Therefore, we should exclude any packages with empty or invalid packageName entries, log the error, and continue with the process.
   * Conversely, if the generateOutput package is an empty array, we should halt the process and log the error.
   */
  generateOutput.packages = (generateOutput.packages ?? []).filter((item) => !!item.packageName);

  return { ...statusContext, generateInput, generateOutput };
};

export const workflowDetectChangedPackages = (context: WorkflowContext) => {
  context.logger.log('section', 'Detect changed packages');
  context.logger.info(`${context.pendingPackages.length} packages found after generation:`);
  for (const pkg of context.pendingPackages) {
    context.logger.info(`\t${pkg.relativeFolderPath}`);
    for (const extraPath of pkg.extraRelativeFolderPaths) {
      context.logger.info(`\t- ${extraPath}`);
    }
  }

  if (context.pendingPackages.length === 0) {
    context.logger.warn(`Warning: No package detected after generation. Please refer to the above logs to understand why the package hasn't been generated. `);
  }
  context.logger.log('endsection', 'Detect changed packages');
};

export const workflowInitGetSdkSuppressionsYml = async (
  context: WorkflowContext,
  filterSuppressionFileMap: Map<string, string | undefined>,
): Promise<SDKSuppressionContentList> => {
  context.logger.info(
    `Get file content from ${repoKeyToString(context.config.specRepo)} ` + `for following SDK suppression files: ${Array.from(filterSuppressionFileMap.values()).join(',')} `,
  );

  const suppressionFileMap: SDKSuppressionContentList = new Map();
  let suppressionFileParseResult;
  let message = '';
  for (const [changedSpecFilePath, sdkSuppressionFilePath] of filterSuppressionFileMap) {
    context.logger.info(`${changedSpecFilePath} corresponding SDK suppression files ${sdkSuppressionFilePath}.`);
    if (!sdkSuppressionFilePath) {
      suppressionFileMap.set(changedSpecFilePath, { content: null, sdkSuppressionFilePath, errors: ['No suppression file added.'] });
      continue;
    }
    // Retrieve the data of all the files that suppress certain content for a language
    const sdkSuppressionFilesContentTotal: SdkSuppressionsYml = { suppressions: {} };
    // Use file parsing to obtain yaml content and check if the suppression file has any grammar errors
    const sdkSuppressionFilesParseErrorTotal: string[] = [];
    let suppressionFileData: string = '';
    try {
      suppressionFileData = fs.readFileSync(sdkSuppressionFilePath).toString();
    } catch (error) {
      const message = configError(
        `Fails to read SDK suppressions file with path of '${sdkSuppressionFilePath}'. Assuming no suppressions are present. Please ensure the suppression file exists in the right path in order to load the suppressions for the SDK breaking changes. Error: ${error.message}`,
      );
      context.logger.error(message);
      continue;
    }
    // parse file both to get yaml content and validate the suppression file has grammar error
    try {
      suppressionFileParseResult = parseYamlContent(suppressionFileData);
    } catch (error) {
      message = configError(`The file parsing failed in the ${sdkSuppressionFilePath}. Details: ${error}`);
      context.logger.error(message);
      sdkSuppressionFilesParseErrorTotal.push(message);
      continue;
    }
    if (!suppressionFileParseResult) {
      message = configWarning(`Ignore the suppressions as the file at ${sdkSuppressionFilePath} is empty.`);
      context.logger.warn(message);
      sdkSuppressionFilesParseErrorTotal.push(message);
      continue;
    }
    const suppressionFileContent = suppressionFileParseResult as SdkSuppressionsYml;
    // Check if the suppression file content has any schema error
    const validateSdkSuppressionsFileResult = validateSdkSuppressionsFile(suppressionFileContent);
    if (!validateSdkSuppressionsFileResult.result) {
      sdkSuppressionFilesParseErrorTotal.push(validateSdkSuppressionsFileResult.message);
      context.logger.error(
        configError(
          `ContentError: ${validateSdkSuppressionsFileResult.message}. The SDK suppression file is malformed. Please refer to the https://aka.ms/azsdk/sdk-suppression to fix the content.`,
        ),
      );
      continue;
    }
    sdkSuppressionFilesContentTotal.suppressions = _.mergeWith(suppressionFileContent.suppressions, sdkSuppressionFilesContentTotal.suppressions, function customizer(
      originSdkPackageSuppression: SdkPackageSuppressionsEntry,
      othersSdkPackageSuppression: SdkPackageSuppressionsEntry,
    ) {
      if (_.isArray(originSdkPackageSuppression)) {
        return originSdkPackageSuppression.concat(othersSdkPackageSuppression);
      }
    });
    suppressionFileMap.set(changedSpecFilePath, { content: sdkSuppressionFilesContentTotal, sdkSuppressionFilePath, errors: sdkSuppressionFilesParseErrorTotal });
  }

  return suppressionFileMap;
};
