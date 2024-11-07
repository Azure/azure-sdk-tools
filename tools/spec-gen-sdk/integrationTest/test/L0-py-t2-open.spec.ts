
import {
  createPRWithPatch, getPRListInfo, getLoggerTextForAssert
} from '../utils';
import { fixtures } from '../fixtures';
import { preparePyT2SDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {

  it.skip('should create Python Track2 sdk pr on spec pr opened', async () => {
    const {
      specRepoName, sdkRepoName, logger, launchSDKAutomation, patchPyT2Log
    } = await preparePyT2SDKTest('opened-pr');

    const specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch0_AddService, 'patch0');

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchPyT2Log(await getLoggerTextForAssert(
      logger, specRepoName, specPRNumber, sdkRepoName
    ))).toMatchSnapshot();
  });

});
