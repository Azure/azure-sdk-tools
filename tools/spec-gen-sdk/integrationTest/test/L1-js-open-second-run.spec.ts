import { createPRWithPatch, getPRListInfo, getLoggerTextForAssert, getTestGithubClient, repoOwner } from '../utils';
import { fixtures } from '../fixtures';
import { prepareJsSDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {
  it('should run on second time for open PR', async () => {
    const { specRepoName, sdkRepoName, logger, launchSDKAutomation, patchJsLog } = await prepareJsSDKTest(
      'open-second-run'
    );
    const github = await getTestGithubClient();

    const specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch0_AddService, 'patch0');

    // Simulate first SDK Automation run
    const intBranchName = 'sdkAutomation/@azure_test-service';
    const genBranchName = `${intBranchName}@${specPRNumber}`;
    await createPRWithPatch(sdkRepoName, fixtures.sdkJs.name, intBranchName);
    await createPRWithPatch(sdkRepoName, fixtures.sdkJs.patch0_AddServiceGen, genBranchName);
    await github.pulls.create({
      owner: repoOwner,
      repo: sdkRepoName,
      base: intBranchName,
      head: genBranchName,
      title: '[AutoPR] Simulate first SDK Automation'
    });

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchJsLog(await getLoggerTextForAssert(logger, specRepoName, specPRNumber, sdkRepoName))).toMatchSnapshot();
  });
});
