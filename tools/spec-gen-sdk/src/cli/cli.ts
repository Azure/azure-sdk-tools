import { getRepository } from '../utils/githubUtils';
import { SDKAutomationState } from '../automation/sdkAutomationState';
import { sdkAutomationCliConfig } from './config';
import { sdkAutoMain } from '../automation/entrypoint';

// tslint:disable-next-line: no-floating-promises
(async () => {
  const start = process.hrtime();
  const config = sdkAutomationCliConfig;

  let status: SDKAutomationState | undefined = undefined;
  try {
    const repo = getRepository(config.specRepoHttpsUrl);

    process.chdir(config.workingFolder);
    status = await sdkAutoMain({
      specRepo: repo,
      localSpecRepoPath: config.localSpecRepoPath,
      localSdkRepoPath: config.localSdkRepoPath,
      tspConfigPath: config.tspConfigPath,
      readmePath: config.readmePath,
      specCommitSha: config.specCommitSha,
      specRepoHttpsUrl: config.specRepoHttpsUrl,
      pullNumber: config.prNumber,
      sdkName: config.sdkRepoName,
      workingFolder: config.workingFolder,
      github: {
        token: config.githubToken,
      },
      runEnv: config.isTriggeredByPipeline ? 'azureDevOps' : 'local',
      branchPrefix: 'sdkAuto'
    });
  } catch (e) {
    console.error(e.message);
    console.error(e.stack);
    status = 'failed';
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
