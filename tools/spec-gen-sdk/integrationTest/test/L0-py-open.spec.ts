
import {
  createPRWithPatch, getPRListInfo, getLoggerTextForAssert
} from '../utils';
import { fixtures } from '../fixtures';
import { preparePySDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {

  it.skip('should create Python sdk pr on spec pr opened', async () => {
    const {
      specRepoName, sdkRepoName, logger, launchSDKAutomation, patchPyLog
    } = await preparePySDKTest('opened-pr');

    const specPRNumber = await createPRWithPatch(
      specRepoName, fixtures.specTest.patch0_AddService, 'patch0'
    );

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchPyLog(await getLoggerTextForAssert(
      logger, specRepoName, specPRNumber, sdkRepoName
    ))).toMatchSnapshot();
  });

});
