import { assert } from 'chai';
import { getInstallationInstructions } from '../lib/langSpecs/dotnet';

describe('dotnet.ts', function() {
  it('getInstallationInstructions()', function() {
    const instructions: string[] = getInstallationInstructions({
      packageName: 'fake-package-name',
      artifactUrls: ['fake-package-url'],
      generationRepositoryUrl: 'fake-repository-url',
      sdkRepositoryGenerationPullRequestHeadBranch: 'fake-sdk-pull-request-head-branch'
    });
    assert.deepEqual(instructions, [
      '## Installation Instructions',
      'In order to use the [generated nuget package](fake-package-url) in your app, you will have to use it from a private feed.',
      `To create a private feed, see the following link:`,
      `[https://docs.microsoft.com/en-us/nuget/hosting-packages/local-feeds](https://docs.microsoft.com/en-us/nuget/hosting-packages/local-feeds)`,
      `This will allow you to create a new local feed and add the location of the new feed as one of the sources.`,
      '## Direct Download',
      'The generated package can be directly downloaded from here:',
      '- [fake-package-name](fake-package-url)'
    ]);
  });
});
