import {
  createPRWithPatch, getPRListInfo, getLoggerTextForAssert
} from '../utils';
import { fixtures } from '../fixtures';
import { prepareJsSDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {

  it('should handle two readme update', async () => {
    const {
      specRepoName, sdkRepoName, logger, launchSDKAutomation, patchJsLog
    } = await prepareJsSDKTest('open-two-readme');

    const specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch3_TwoReadme, 'patch3');

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchJsLog(await getLoggerTextForAssert(
      logger, specRepoName, specPRNumber, sdkRepoName
    ))).toMatchSnapshot();
  });

});
