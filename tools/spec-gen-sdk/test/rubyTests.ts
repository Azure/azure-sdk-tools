import { assert } from 'chai';
import { getPackageNameToGenerate, getInstallationInstructions } from '../lib/langSpecs/ruby';
import { InstallationInstructionsOptions } from '../lib/langSpecs/installationInstructions';

describe('ruby.ts', function() {
  it('getPackageNameToGenerate()', function() {
    const readmeRubyMdFileContents = `## Ruby

These settings apply only when \`--ruby\` is specified on the command line.

\`\`\`yaml
package-name: azure_mgmt_customproviders
package-version: 2018-09-01-preview
azure-arm: true
\`\`\`

### Tag: package-2018-09-01-preview and ruby

These settings apply only when \`--tag=package-2018-09-01-preview --ruby\` is specified on the command line.
Please also specify \`--ruby-sdks-folder=<path to the root directory of your azure-sdk-for-ruby clone>\`.

\`\`\`yaml $(tag) == 'package-2018-09-01-preview' && $(ruby)
namespace: Microsoft.CustomProviders
output-folder: $(ruby-sdks-folder)/customproviders
\`\`\``;
    assert.strictEqual(getPackageNameToGenerate(readmeRubyMdFileContents), 'customproviders');
  });

  it('getInstallationInstructions()', function() {
    const options: InstallationInstructionsOptions = {
      artifactUrls: [
        'https://openapistoragetest.blob.core.windows.net/sdkautomation/test/test-repo-billy/azure-rest-api-specs/113/28/test-repo-billy/azure-sdk-for-ruby/azure_sdk/azure_sdk-0.25.0.gem'
      ],
      generationRepositoryUrl: 'fake-generation-repository-url',
      packageName: 'fake-package-name',
      sdkRepositoryGenerationPullRequestHeadBranch: 'fake-head-branch'
    };
    assert.deepEqual(getInstallationInstructions(options), [
      `## Installation Instructions`,
      `The Gem file for \`azure_sdk\` can be downloaded [here](https://openapistoragetest.blob.core.windows.net/sdkautomation/test/test-repo-billy/azure-rest-api-specs/113/28/test-repo-billy/azure-sdk-for-ruby/azure_sdk/azure_sdk-0.25.0.gem).`,
      `After downloading the gem file, you can add it to your Ruby project by running the following command:`,
      `\`\`\`gem 'azure_sdk', '0.25.0', '<download-folder-path>/azure_sdk-0.25.0.gem'\`\`\``,
      `## Direct Download`,
      `The generated gem can be directly downloaded from here:`,
      `- [azure_sdk-0.25.0.gem](https://openapistoragetest.blob.core.windows.net/sdkautomation/test/test-repo-billy/azure-rest-api-specs/113/28/test-repo-billy/azure-sdk-for-ruby/azure_sdk/azure_sdk-0.25.0.gem)`
    ]);
  });
});
