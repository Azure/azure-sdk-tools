
import {
  createPRWithPatch, getPRListInfo, getLoggerTextForAssert
} from '../utils';
import { fixtures } from '../fixtures';
import { prepareTfSDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {

  it.skip('should create terraform/trenton sdk pr on spec pr opened', async () => {
    const {
      specRepoName, sdkRepoName, logger, launchSDKAutomation, patchTfLog
    } = await prepareTfSDKTest('open-pr');

    const specPRNumber = await createPRWithPatch(specRepoName, fixtures.specTest.patch0_AddService, 'patch0');

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchTfLog(await getLoggerTextForAssert(
      logger, specRepoName, specPRNumber, sdkRepoName
    ))).toMatchSnapshot();
  });

});
