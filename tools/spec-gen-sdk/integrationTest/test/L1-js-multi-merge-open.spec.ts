import { createPRWithPatch, getPRListInfo, getLoggerTextForAssert, getTestGithubClient, repoOwner } from '../utils';
import { fixtures } from '../fixtures';
import { prepareJsSDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {
  it('should run in open pr after one merged pr', async () => {
    const { specRepoName, sdkRepoName, logger, launchSDKAutomation, patchJsLog } = await prepareJsSDKTest(
      'multi-merge-open'
    );
    const github = await getTestGithubClient();

    // Simulate spec PR merged and integration branch updated
    let specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch0_AddService, 'patch0');
    await github.pulls.merge({ owner: repoOwner, repo: specRepoName, pull_number: specPRNumber });
    await createPRWithPatch(sdkRepoName, fixtures.sdkJs.patch0_AddServiceGen, 'sdkAutomation/@azure_test-service');

    // New open PR
    specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch1_Add_02_01, 'patch1', {
      fetchBase: true
    });

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchJsLog(await getLoggerTextForAssert(logger, specRepoName, specPRNumber, sdkRepoName))).toMatchSnapshot();
  });
});
