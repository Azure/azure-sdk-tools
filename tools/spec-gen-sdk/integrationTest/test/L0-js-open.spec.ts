import {
  createPRWithPatch, getPRListInfo, getLoggerTextForAssert
} from '../utils';
import { fixtures } from '../fixtures';
import { prepareJsSDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {

  it('should create JS sdk pr on spec pr opened', async () => {
    const {
      specRepoName, sdkRepoName, logger, launchSDKAutomation, patchJsLog
    } = await prepareJsSDKTest('opened-pr');

    const specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch0_AddService, 'patch0');

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchJsLog(await getLoggerTextForAssert(
      logger, specRepoName, specPRNumber, sdkRepoName
    ))).toMatchSnapshot();
  });

});
