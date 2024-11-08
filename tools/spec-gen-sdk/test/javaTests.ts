import { assert } from 'chai';
import { InstallationInstructionsOptions } from '../lib/langSpecs/installationInstructions';
import { getInstallationInstructions } from '../lib/langSpecs/java';

describe('java.ts', function() {
  describe('getInstallationInstructions()', function() {
    it('with fake-package', function() {
      const installationInstructionsOptions: InstallationInstructionsOptions = {
        packageName: 'fake-package-name',
        artifactUrls: ['fake/package/url'],
        generationRepositoryUrl: 'fake/repository/url',
        sdkRepositoryGenerationPullRequestHeadBranch: 'fake-sdk-pull-request-head-branch'
      };
      assert.deepEqual(getInstallationInstructions(installationInstructionsOptions), [
        `## Installation Instructions`,
        `You can install the package \`fake-package-name\` of this PR by downloading the artifact jar files. Then ensure that the jar files are on your project's classpath.`,
        `## Direct Download`,
        `The generated package artifacts can be directly downloaded from here:`,
        `- [url](fake/package/url)`
      ]);
    });
  });
});
