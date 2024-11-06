import { assert } from 'chai';
import { getInstallationInstructions } from '../lib/langSpecs/javascript';

describe('javascript.ts', function() {
  it('getInstallationInstructions()', function() {
    const instructions: string[] = getInstallationInstructions({
      packageName: 'fake-package-name',
      artifactUrls: ['fake/package/url'],
      generationRepositoryUrl: 'fake-repository-url',
      sdkRepositoryGenerationPullRequestHeadBranch: 'fake-sdk-pull-request-head-branch'
    });
    assert.deepEqual(instructions, [
      '## Installation Instructions',
      'You can install the package `fake-package-name` of this PR using the following command:',
      '```bash',
      'npm install fake/package/url',
      '```',
      '## Direct Download',
      'The generated package can be directly downloaded from here:',
      '- [url](fake/package/url)'
    ]);
  });
});
