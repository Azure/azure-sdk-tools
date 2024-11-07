import { assert } from 'chai';
import { getInstallationInstructions } from '../lib/langSpecs/go';

describe('go.ts', function() {
  it('getInstallationInstructions()', function() {
    const instructions: string[] = getInstallationInstructions({
      packageName: 'fake-package-name',
      artifactUrls: ['fake-package-url'],
      generationRepositoryUrl: 'fake-repository-url',
      sdkRepositoryGenerationPullRequestHeadBranch: 'fake-sdk-pull-request-head-branch'
    });
    assert.deepEqual(instructions, [
      '## Installation Instructions',
      'You can install the package `fake-package-name` of this PR by downloading the [package](fake-package-url) and extracting it to the root of your azure-sdk-for-go directory.',
      '## Direct Download',
      'The generated package can be directly downloaded from here:',
      '- [fake-package-name](fake-package-url)'
    ]);
  });
});
