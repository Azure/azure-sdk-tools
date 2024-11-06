import { getRepository } from '@ts-common/azure-js-dev-tools';
import { sdkAutoMain } from '../automation/entrypoint';
import { SDKAutomationState } from '../sdkAutomationState';
import { sdkAutomationCliConfig } from './config';
// import { cliMain } from './main';

// tslint:disable-next-line: no-floating-promises
// cliMain(sdkAutomationCliConfig).catch(e => {
//   console.error(e);
//   process.exit(-1);
// });

// tslint:disable-next-line: no-floating-promises
(async () => {
  const start = process.hrtime();
  const config = sdkAutomationCliConfig;

  let status: SDKAutomationState | undefined = undefined;
  try {
    const repo = getRepository(config.specRepo);

    process.chdir(config.workingFolder);
    status = await sdkAutoMain({
      specRepo: repo,
      pullNumber: config.prNumber,
      sdkName: config.sdkRepoName,
      filterSwaggerToSdk: config.executionMode === 'SDK_FILTER',
      github: {
        token: config.githubToken,
        id: config.githubApp.id,
        privateKey: config.githubApp.privateKey
      },
      storage: config.blobStorage,
      runEnv: config.isTriggeredByUP ? 'azureDevOps' : 'local',
      branchPrefix: 'sdkAuto'
    });
  } catch (e) {
    console.error(e.message);
    console.error(e.stack);
    status = 'failed';
    if (config.executionMode === 'SDK_FILTER') {
      console.log(`##vso[task.setVariable variable=SkipAll;isOutput=true]true`);
    }
  }

  const elapsed = process.hrtime(start);
  console.log(`Execution time: ${elapsed[0]}s`);

  console.log(`Exit with status ${status}`);
  if (status !== undefined && !['warning', 'succeeded'].includes(status)) {
    process.exit(-1);
  } else {
    process.exit(0);
  }
})();
