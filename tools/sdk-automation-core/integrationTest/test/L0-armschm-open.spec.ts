import {
  createPRWithPatch, getPRListInfo, getLoggerTextForAssert
} from '../utils';
import { fixtures } from '../fixtures';
import { prepareARMSchmSDKTest } from './lang-common';

describe('In open PR, SDK Automation', () => {

  it.skip('should create Azure resource manager schemas pr on spec pr opened', async () => {
    const {
      specRepoName, sdkRepoName, logger, launchSDKAutomation, patchARMSchmLog
    } = await prepareARMSchmSDKTest('opened-pr');

    const specPRNumber = await createPRWithPatch(
      specRepoName, fixtures.specTest.patch0_AddService, 'patch0'
    );

    // Launch SDK Automation
    await launchSDKAutomation(specPRNumber);

    // Assert
    expect(await getPRListInfo(sdkRepoName)).toMatchSnapshot();
    expect(patchARMSchmLog(await getLoggerTextForAssert(
      logger, specRepoName, specPRNumber, sdkRepoName
    ))).toMatchSnapshot();
  });

});
