import {
  getRunIdPrefix,
  initializeGithubRepoFromLocalFixture,
  launchTestSDKAutomation,
  repoOwner,
  createPRWithPatch,
  getTestGithubClient
} from '../utils';
import { fixtures } from '../fixtures';
import { getSdkAutoContext } from '../../src/automation/entrypoint';
import { sdkAutomationCliConfig } from '../../src/cli/config';
import { workflowInit, workflowMain } from '../../src/automation/workflow';
import { sdkAutoReportStatus } from '../../src/automation/reportStatus';
import { CommentCaptureTransport, sdkAutoLogLevels } from '../../src/automation/logging';

export const prepareSDKTestCommon = async (
  langAlias: string,
  langFull: string,
  sdkFixtureName: string,
  repoNameParam: string,
  sdkBaseBranch?: string,
  isPrivate?: boolean
) => {
  const namePrefix = `${getRunIdPrefix()}-${langAlias}-${repoNameParam}`;
  let specOriginName = 'azure-rest-api-specs';
  let sdkOriginName = `${langFull}-test`;
  let specRepoName = `${namePrefix}-spec`;
  let sdkRepoName = `${namePrefix}-sdk`;
  if (isPrivate) {
    specOriginName += '-pr';
    sdkOriginName += '-pr';
    specRepoName += '-pr';
    sdkRepoName += '-pr';
  }

  await Promise.all([
    initializeGithubRepoFromLocalFixture(fixtures.specTest.name, specRepoName, {
      'specificationRepositoryConfiguration.json': [
        {
          search: `"${sdkOriginName}"`,
          replace: `"${sdkRepoName}"`
        },
        {
          search: `"${specOriginName}"`,
          replace: `"${specRepoName}"`
        }
      ]
    }),
    initializeGithubRepoFromLocalFixture(
      sdkFixtureName,
      sdkRepoName,
      {
        'swagger_to_sdk_config.json': [
          {
            search: `"${langFull}-test"`,
            replace: `"${sdkRepoName}"`
          }
        ]
      },
      sdkBaseBranch
    )
  ]);

  const logger = {
    allLogs: [] as string[]
  };

  const launchSDKAutomation = (specPRNumber: number) => {
    const config = sdkAutomationCliConfig;
    return launchTestSDKAutomation(
      (async () => {
        const cwd = process.cwd();
        process.chdir(config.workingFolder);
        try {
          const sdkAutoContext = await getSdkAutoContext({
            specRepo: { owner: repoOwner, name: specRepoName },
            pullNumber: specPRNumber,
            sdkName: langFull,
            filterSwaggerToSdk: false,
            github: {
              token: config.githubToken,
              id: config.githubApp.id,
              privateKey: config.githubApp.privateKey
            },
            storage: {
              name: 'sdkautotest',
              prefix: 'sdkautocontainer',
              downloadCommand: 'DOWNLOAD {URL} TO {FILENAME}',
              isPublic: !isPrivate
            },
            runEnv: 'test',
            branchPrefix: 'sdkAuto'
          });

          const captureTransport = new CommentCaptureTransport({
            extraLevelFilter: Object.keys(sdkAutoLogLevels.levels) as any[],
            output: logger.allLogs
          });
          sdkAutoContext.logger.add(captureTransport);
          // sdkAutoContext.logger.add(new winston.transports.Console());

          const context = await workflowInit(sdkAutoContext);
          await workflowMain(context);
          await sdkAutoReportStatus(context);
        } finally {
          process.chdir(cwd);
        }
      })(),
      `${langAlias}-${repoNameParam}`
    );
  };

  return { namePrefix, specRepoName, sdkRepoName, logger, launchSDKAutomation };
};

export const prepareJsSDKTest = async (repoNameParam: string, isPrivate?: boolean) => {
  const context = await prepareSDKTestCommon(
    'js',
    'azure-sdk-for-js',
    fixtures.sdkJs.name,
    repoNameParam,
    undefined,
    isPrivate
  );

  const patchJsLog = (text: string) => {
    const regexToMatchLine = /^cmderr\t\[npmPack\]/g;
    text = text
      .split('\n')
      .filter((line) => line.match(regexToMatchLine) === null)
      .join('\n');
    return text;
  };

  return { ...context, patchJsLog };
};

export const preparePySDKTest = async (repoNameParam: string) => {
  const context = await prepareSDKTestCommon(
    'py',
    'azure-sdk-for-python',
    fixtures.sdkPy.name,
    repoNameParam,
    'release/v3'
  );

  const patchPyLog = (text: string) => {
    const regexToMatchLine = /\[changelog/g;
    text = text
      .split('\n')
      .filter((line) => line.match(regexToMatchLine) === null)
      .join('\n');
    return text;
  };

  return { ...context, patchPyLog };
};

export const preparePyT2SDKTest = async (repoNameParam: string) => {
  const context = await prepareSDKTestCommon('pyt2', 'azure-sdk-for-python-track2', fixtures.sdkPy.name, repoNameParam);
  const githubClient = await getTestGithubClient();

  const prNumber = await createPRWithPatch(context.sdkRepoName, fixtures.sdkPy.patch0_Track2, 'enable-track2', {
    toReplace: {
      'swagger_to_sdk_custom_config.json': [
        {
          search: 'azure-sdk-for-python-track2-test',
          replace: context.sdkRepoName
        }
      ]
    }
  });
  await githubClient.pulls.merge({
    owner: repoOwner,
    repo: context.sdkRepoName,
    pull_number: prNumber
  });

  const patchPyT2Log = (text: string) => {
    const regexToMatchLine = /\[changelog/g;
    text = text
      .split('\n')
      .filter((line) => line.match(regexToMatchLine) === null)
      .join('\n');
    return text;
  };

  return { ...context, patchPyT2Log };
};

export const prepareTfSDKTest = async (repoNameParam: string) => {
  const context = await prepareSDKTestCommon('tf', 'azure-sdk-for-trenton', fixtures.sdkTf.name, repoNameParam);

  const patchTfLog = (text: string) => text;

  return { ...context, patchTfLog };
};

export const prepareARMSchmSDKTest = async (repoNameParam: string) => {
  const context = await prepareSDKTestCommon(
    'armschm',
    'azure-resource-manager-schemas',
    fixtures.schmARM.name,
    repoNameParam
  );

  const patchARMSchmLog = (text: string) => text;

  return { ...context, patchARMSchmLog };
};
