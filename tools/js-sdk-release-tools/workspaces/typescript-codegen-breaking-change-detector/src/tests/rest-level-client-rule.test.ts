// import { describe, expect, test } from 'vitest';
import { mkdirp, pathExists } from 'fs-extra';

import { detectBreakingChangesBetweenPackages } from '../azure/detect-breaking-changes.js';
import { join } from 'node:path';

function getFormattedDate(): string {
  const today = new Date();

  const year = today.getFullYear();
  const month = String(today.getMonth() + 1).padStart(2, '0');
  const day = String(today.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

async function createTempFolder(tempFolderPrefix: string): Promise<string> {
  const maxRetry = 1000;
  let tempFolder = '';
  for (let i = 0; i < maxRetry; i++) {
    tempFolder = `${tempFolderPrefix}-${Math.round(Math.random() * 1000)}`;
    if (await pathExists(tempFolder)) continue;

    await mkdirp(tempFolder);
    return tempFolder;
  }
  throw new Error(`Failed to create temp folder at "${tempFolder}" for ${maxRetry} times`);
}

// describe('detect rest level client breaking changes', async () => {
//   test('should ignore operation rename', async () => {
    // const testCaseDir = '../../misc/test-cases/rest-level-client-to-rest-level-client/';
    // const currentPackageFolder = join(__dirname, testCaseDir, 'current-package');
    // const baselinePackageFolder = join(__dirname, testCaseDir, 'baseline-package');
async function test() {
    console.log('--------------start -1')
    const currentPackageFolder = 'C:/Users/wanl/workspace/test-azure-sdk-for-js/sdk/loadtesting/load-testing-rest';
    const baselinePackageFolder = 'C:/Users/wanl/workspace/wanl-fork-azure-sdk-tools/tools/js-sdk-release-tools/azure-rest-load-testing-1.0.0/package';
    const date = getFormattedDate();
    const tempFolder = await createTempFolder(`./tmp/temp-${date}`);
    const messagesMap = await detectBreakingChangesBetweenPackages(
      baselinePackageFolder,
      currentPackageFolder,
      tempFolder,
      true
    );
    // expect(messagesMap.size).toBe(1);
    // TODO: add more checks
}

test()
//   });
// });
