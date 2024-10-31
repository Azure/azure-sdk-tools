import {
  getTestGithubClient, repoOwner, createPRWithPatch, getPRListInfo, getLoggerTextForAssert
} from '../utils';
import { fixtures } from '../fixtures';
import { prepareJsSDKTest } from './lang-common';

describe('In merged PR, SDK Automation ', () => {

  it('should push integration branch and close gen pr if spec pr merged', async () => {
    const github = await getTestGithubClient();
    const {
      specRepoName, sdkRepoName, logger, launchSDKAutomation, patchJsLog
    } = await prepareJsSDKTest('merged-pr');

    const specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch0_AddService, 'patch0');
    await github.pulls.merge({ owner: repoOwner, repo: specRepoName, pull_number: specPRNumber });

    // Launch SDK Automation for merged routine
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchJsLog(await getLoggerTextForAssert(
      logger, specRepoName, specPRNumber, sdkRepoName
    ))).toMatchSnapshot();
  });
});
