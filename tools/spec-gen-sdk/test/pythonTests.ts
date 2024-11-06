import { assert } from 'chai';
import { InstallationInstructionsOptions } from '../lib/langSpecs/installationInstructions';
import { getInstallationInstructions, getPackageName } from '../lib/langSpecs/python';

describe('python.ts', function() {
  it('getPackageName()', function() {
    assert.strictEqual(getPackageName('/1/1/', 'sdk/network/azure-mgmt-dns'), 'azure-mgmt-dns');
  });

  describe('getInstallationInstructions()', function() {
    it('with azure-graphrbac', function() {
      const expectedInstallationInstructions: string[] = [
        '(message created by the CI based on PR content)',
        '# Installation instruction',
        '## Package azure-graphrbac',
        'You can install the package `azure-graphrbac` of this PR using the following command:',
        '\t`pip install "git+https://github.com/Azure/azure-sdk-for-python@restapi_auto_graphrbac/data-plane#egg=azure-graphrbac&subdirectory=azure-graphrbac"`',
        '',
        'You can build a wheel to distribute for test using the following command:',
        '\t`pip wheel --no-deps "git+https://github.com/Azure/azure-sdk-for-python@restapi_auto_graphrbac/data-plane#egg=azure-graphrbac&subdirectory=azure-graphrbac"`',
        '',
        'If you have a local clone of this repository, you can also do:',
        '',
        '- `git checkout restapi_auto_graphrbac/data-plane`',
        '- `pip install -e ./azure-graphrbac`',
        '',
        '',
        'Or build a wheel file to distribute for testing:',
        '',
        '- `git checkout restapi_auto_graphrbac/data-plane`',
        '- `pip wheel --no-deps ./azure-graphrbac`',
        '',
        '',
        '# Direct download',
        '',
        'Your files can be directly downloaded here:',
        '',
        '- [azure_graphrbac-0.52.0-py2.py3-none-any.whl](http://azuresdkinfrajobstore1.blob.core.windows.net/azure/azure-sdk-for-python/pullrequests/4574/dist/azure_graphrbac-0.52.0-py2.py3-none-any.whl)',
        ''
      ];
      const options: InstallationInstructionsOptions = {
        packageName: 'azure-graphrbac',
        artifactUrls: [
          'http://azuresdkinfrajobstore1.blob.core.windows.net/azure/azure-sdk-for-python/pullrequests/4574/dist/azure_graphrbac-0.52.0-py2.py3-none-any.whl'
        ],
        generationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-python',
        sdkRepositoryGenerationPullRequestHeadBranch: 'restapi_auto_graphrbac/data-plane'
      };
      assert.deepEqual(getInstallationInstructions(options), expectedInstallationInstructions);
    });

    it('with azure-mgmt-alertsmanagement', function() {
      const expectedInstallationInstructions: string[] = [
        '(message created by the CI based on PR content)',
        '# Installation instruction',
        '## Package azure-mgmt-alertsmanagement',
        'You can install the package `azure-mgmt-alertsmanagement` of this PR using the following command:',
        '\t`pip install "git+https://github.com/Azure/azure-sdk-for-python@restapi_auto_5384#egg=azure-mgmt-alertsmanagement&subdirectory=azure-mgmt-alertsmanagement"`',
        '',
        'You can build a wheel to distribute for test using the following command:',
        '\t`pip wheel --no-deps "git+https://github.com/Azure/azure-sdk-for-python@restapi_auto_5384#egg=azure-mgmt-alertsmanagement&subdirectory=azure-mgmt-alertsmanagement"`',
        '',
        'If you have a local clone of this repository, you can also do:',
        '',
        '- `git checkout restapi_auto_5384`',
        '- `pip install -e ./azure-mgmt-alertsmanagement`',
        '',
        '',
        'Or build a wheel file to distribute for testing:',
        '',
        '- `git checkout restapi_auto_5384`',
        '- `pip wheel --no-deps ./azure-mgmt-alertsmanagement`',
        '',
        '',
        '# Direct download',
        '',
        'Your files can be directly downloaded here:',
        '',
        '- [azure_mgmt_alertsmanagement-2018_05_05-py2.py3-none-any.whl](http://azuresdkinfrajobstore1.blob.core.windows.net/azure/azure-sdk-for-python/pullrequests/4572/dist/azure_mgmt_alertsmanagement-2018_05_05-py2.py3-none-any.whl)',
        ''
      ];
      const options: InstallationInstructionsOptions = {
        packageName: 'azure-mgmt-alertsmanagement',
        artifactUrls: [
          'http://azuresdkinfrajobstore1.blob.core.windows.net/azure/azure-sdk-for-python/pullrequests/4572/dist/azure_mgmt_alertsmanagement-2018_05_05-py2.py3-none-any.whl'
        ],
        generationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-python',
        sdkRepositoryGenerationPullRequestHeadBranch: 'restapi_auto_5384'
      };
      assert.deepEqual(getInstallationInstructions(options), expectedInstallationInstructions);
    });
  });
});
