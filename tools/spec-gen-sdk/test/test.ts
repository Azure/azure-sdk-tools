import {
  AzureBlobStorage,
  BlobStorage,
  BlobStorageContainer,
  createFolder,
  FakeGitHub,
  FakeHttpClient,
  FakeRepository,
  FakeRunner,
  GitHubPullRequest,
  GitHubPullRequestCommit,
  HttpClient,
  InMemoryBlobStorage,
  joinPath,
  mvnExecutable,
  NodeHttpClient,
  npmExecutable,
  writeFileContents
} from '@ts-common/azure-js-dev-tools';
import { pullRequestLabelsInfo, PullRequestLabel } from '../lib/githubUtils';

export const storageUrl = `https://sdkautomationdev.blob.core.windows.net/`;
export const deleteWorkingFolder = true;
export const deleteWorkingContainer = true;

export const testSpecificationPullRequestBaseCommit: GitHubPullRequestCommit = {
  label: 'Azure:master',
  ref: 'master',
  sha: '1b8809ee779437bc13f9af9373a5d47472a6b889'
};

export const testSpecificationPullRequestHeadCommit: GitHubPullRequestCommit = {
  label: 'pixia:master',
  ref: 'master',
  sha: 'd82d1491879729cdf44da9a664e815112acde158'
};

export const testSpecificationPullRequestMergeCommitSha = '5d204450e3ea6709a034208af441ebaaa87bd805';
export const testSpecificationPullRequestNumber = 4994;
export const testSpecificationPullRequestRepository = 'Azure/azure-rest-api-specs';

export const testSpecificationPullRequest: GitHubPullRequest = {
  base: testSpecificationPullRequestBaseCommit,
  head: testSpecificationPullRequestHeadCommit,
  merge_commit_sha: testSpecificationPullRequestMergeCommitSha,
  id: 242467286,
  labels: [],
  number: testSpecificationPullRequestNumber,
  state: 'closed',
  title: 'Fix mysql sku values',
  url: `https://api.github.com/repos/${testSpecificationPullRequestRepository}/pulls/${testSpecificationPullRequestNumber}`,
  html_url: `https://github.com/${testSpecificationPullRequestRepository}/pull/${testSpecificationPullRequestNumber}`,
  diff_url: `https://github.com/${testSpecificationPullRequestRepository}/pull/${testSpecificationPullRequestNumber}.diff`,
  milestone: undefined,
  assignees: undefined
};

export function createTestBlobStorage(real?: boolean): BlobStorage {
  return real ? new AzureBlobStorage(storageUrl) : new InMemoryBlobStorage();
}

export function getTestBlobStorageContainer(real?: boolean): BlobStorageContainer {
  const blobStorage: BlobStorage = createTestBlobStorage(real);
  return blobStorage.getContainer('abc');
}

export async function createTestBlobStorageContainer(real?: boolean): Promise<BlobStorageContainer> {
  const container: BlobStorageContainer = getTestBlobStorageContainer(real);
  await container.create({ accessPolicy: 'container' });
  return container;
}

export async function createTestGitHub(): Promise<FakeGitHub> {
  const result = new FakeGitHub();
  await result.createUser('fake_user');
  await result.setCurrentUser('fake_user');

  await result.createRepository(testSpecificationPullRequestRepository);
  await result.createCommit(
    testSpecificationPullRequestRepository,
    testSpecificationPullRequestBaseCommit.sha,
    'hello world again'
  );
  await result.createBranch(
    testSpecificationPullRequestRepository,
    testSpecificationPullRequestBaseCommit.ref,
    testSpecificationPullRequestBaseCommit.sha
  );

  const fork: FakeRepository = await result.forkRepository(testSpecificationPullRequestRepository, 'pixia');
  await result.createCommit(fork.name, testSpecificationPullRequestHeadCommit.sha, 'hello world');
  await result.createBranch(
    fork.name,
    testSpecificationPullRequestHeadCommit.ref,
    testSpecificationPullRequestHeadCommit.sha
  );

  await result.createFakePullRequest(testSpecificationPullRequestRepository, testSpecificationPullRequest);

  await result.createRepository('Azure/azure-sdk-for-python');
  await result.createCommit('Azure/azure-sdk-for-python', 'fake-python-master-commit', 'abc');
  await result.createCommit('Azure/azure-sdk-for-python', 'fake-python-non-master-commit', 'def');
  await result.createCommit('Azure/azure-sdk-for-python', 'fake-branch-commit-sha', 'def');
  await result.createBranch('Azure/azure-sdk-for-python', 'master', 'fake-python-master-commit');
  await result.createBranch('Azure/azure-sdk-for-python', 'non-master', 'fake-python-non-master-commit');
  await result.createBranch(
    'Azure/azure-sdk-for-python',
    'Azure/azure-sdk-for-python:non-master',
    'fake-python-non-master-commit'
  );
  await result.createBranch('Azure/azure-sdk-for-python', 'sdkAutomation/azure-mgmt-rdbms', 'fake-branch-commit-sha');
  await result.createBranch(
    'Azure/azure-sdk-for-python',
    'sdkAutomation/azure-mgmt-rdbms@4994',
    'fake-branch-commit-sha'
  );
  await result.createBranch('Azure/azure-sdk-for-python', 'apples/azure-mgmt-rdbms@4994', 'fake-branch-commit-sha');

  await result.forkRepository('Azure/azure-sdk-for-python', 'integration');
  await result.createCommit('integration/azure-sdk-for-python', 'fake-branch-commit-sha', 'def');
  await result.createBranch('integration/azure-sdk-for-python', 'master', 'fake-branch-commit-sha');
  await result.createBranch(
    'integration/azure-sdk-for-python',
    'sdkAutomation/azure-mgmt-rdbms',
    'fake-branch-commit-sha'
  );
  await result.createBranch(
    'integration/azure-sdk-for-python',
    'sdkAutomation/azure-mgmt-rdbms@4994',
    'fake-branch-commit-sha'
  );
  await result.createBranch('integration/azure-sdk-for-python', 'apples/azure-mgmt-rdbms', 'fake-branch-commit-sha');
  await result.createBranch(
    'integration/azure-sdk-for-python',
    'apples/azure-mgmt-rdbms@4994',
    'fake-branch-commit-sha'
  );
  await result.createBranch(
    'integration/azure-sdk-for-python',
    'sdkAutomationTest/azure-mgmt-rdbms',
    'fake-branch-commit-sha'
  );
  await result.createBranch(
    'integration/azure-sdk-for-python',
    'sdkAutomationTest/azure-mgmt-rdbms@4994',
    'fake-branch-commit-sha'
  );

  await result.forkRepository('integration/azure-sdk-for-python', 'generation');
  await result.createCommit('generation/azure-sdk-for-python', 'fake-branch-commit-sha', 'def');
  await result.createBranch('generation/azure-sdk-for-python', 'master', 'fake-branch-commit-sha');
  await result.createBranch(
    'generation/azure-sdk-for-python',
    'sdkAutomation/azure-mgmt-rdbms@4994',
    'fake-branch-commit-sha'
  );
  await result.createBranch('generation/azure-sdk-for-python', 'apples/azure-mgmt-rdbms', 'fake-branch-commit-sha');
  await result.createBranch(
    'generation/azure-sdk-for-python',
    'apples/azure-mgmt-rdbms@4994',
    'fake-branch-commit-sha'
  );

  await result.createRepository('Azure/azure-sdk-for-java');
  await result.createCommit('Azure/azure-sdk-for-java', 'fake-java-commit-sha', 'fake-message');
  await result.createBranch('Azure/azure-sdk-for-java', 'master', 'fake-java-commit-sha');

  await result.createRepository('Azure/azure-sdk-for-go');
  await result.createCommit('Azure/azure-sdk-for-go', 'fake-go-commit-sha', 'fake-message');
  await result.createBranch('Azure/azure-sdk-for-go', 'master', 'fake-go-commit-sha');

  await result.createRepository('Azure/azure-sdk-for-js');
  await result.createCommit('Azure/azure-sdk-for-js', 'fake-js-commit-sha', 'fake-message');
  await result.createBranch('Azure/azure-sdk-for-js', 'master', 'fake-js-commit-sha');

  await result.createRepository('Azure/azure-sdk-for-node');
  await result.createCommit('Azure/azure-sdk-for-node', 'fake-node-commit-sha', 'fake-message');
  await result.createBranch('Azure/azure-sdk-for-node', 'master', 'fake-node-commit-sha');

  for (const repository of [
    'Azure/azure-sdk-for-python',
    'Azure/azure-sdk-for-java',
    'Azure/azure-sdk-for-go',
    'Azure/azure-sdk-for-js',
    'Azure/azure-sdk-for-node'
  ]) {
    for (const labelName of Object.keys(pullRequestLabelsInfo) as PullRequestLabel[]) {
      const labelInfo = pullRequestLabelsInfo[labelName];
      await result.createLabel(repository, labelName, labelInfo.color);
    }
  }

  return result;
}

export function createTestHttpClient(real?: boolean): HttpClient {
  return real
    ? new NodeHttpClient()
    : new FakeHttpClient()
        .add(
          'GET',
          'https://registry.npmjs.org/-/package/autorest/dist-tags',
          200,
          undefined,
          `{"latest":"2.0.4283","last":"2.0.4215","previous":"2.0.4215","preview":"2.0.4302","beta":"3.0.5196"}`
        )
        .add(
          'GET',
          'https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags',
          200,
          undefined,
          `{"latest":"3.0.52","preview":"4.0.68"}`
        )
        .add(
          'GET',
          'https://registry.npmjs.org/-/package/@microsoft.azure/autorest.java/dist-tags',
          200,
          undefined,
          `{"latest":"2.1.0","preview":"2.1.85"}`
        )
        .add(
          'GET',
          'https://registry.npmjs.org/-/package/@microsoft.azure/autorest.go/dist-tags',
          200,
          undefined,
          `{"latest":"2.0.24","preview":"3.0.48","HEAD":"2.1.131","v3":"3.0.63"}`
        )
        .add(
          'GET',
          'https://registry.npmjs.org/-/package/@microsoft.azure/autorest.nodejs/dist-tags',
          200,
          undefined,
          `{"latest":"2.2.131","preview":"2.2.146"}`
        )
        .add(
          'GET',
          'https://registry.npmjs.org/-/package/@microsoft.azure/autorest.typescript/dist-tags',
          200,
          undefined,
          `{"latest":"4.0.0"}`
        )
        .add(
          'GET',
          'https://registry.npmjs.org/-/package/@microsoft.azure/autorest.ruby/dist-tags',
          200,
          undefined,
          `{"latest":"3.0.20","preview":"3.1.40"}`
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
          200,
          undefined,
          JSON.stringify({
            sdkRepositoryMappings: {
              'azure-sdk-for-python': 'Azure/azure-sdk-for-python',
              'azure-sdk-for-java': 'Azure/azure-sdk-for-java',
              'azure-sdk-for-go': 'Azure/azure-sdk-for-go',
              'azure-sdk-for-js': 'Azure/azure-sdk-for-js',
              'azure-sdk-for-node': 'Azure/azure-sdk-for-node'
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              autorest_options: {
                typescript: '',
                'license-header': 'MICROSOFT_MIT_NO_VERSION',
                'sdkrel:typescript-sdks-folder': '.',
                use: '@microsoft.azure/autorest.typescript@4.0.0'
              },
              advanced_options: {
                create_sdk_pull_requests: true
              }
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              autorest_options: {
                typescript: '',
                'license-header': 'MICROSOFT_MIT_NO_VERSION',
                'sdkrel:typescript-sdks-folder': '.',
                use: '@microsoft.azure/autorest.typescript@4.0.0'
              },
              advanced_options: {
                clone_dir: 'azure-sdk-for-js',
                create_sdk_pull_requests: true
              }
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              autorest_options: {
                version: 'preview',
                use: '@microsoft.azure/autorest.python@~3.0.56',
                python: '',
                'python-mode': 'update',
                multiapi: '',
                'sdkrel:python-sdks-folder': '.'
              }
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-sdk-for-python/non-master/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              autorest_options: {
                version: 'preview',
                use: '@microsoft.azure/autorest.python@~3.0.56',
                python: '',
                'python-mode': 'update',
                multiapi: '',
                'sdkrel:python-sdks-folder': '.'
              }
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              after_scripts: [
                'dep ensure',
                'go generate ./profiles/generate.go',
                'gofmt -w ./profiles/',
                'gofmt -w ./services/'
              ],
              autorest_options: {
                use: '@microsoft.azure/autorest.go@~2.1.131',
                go: '',
                verbose: '',
                'sdkrel:go-sdk-folder': '.',
                multiapi: '',
                'use-onever': '',
                'preview-chk': ''
              },
              repotag: 'azure-sdk-for-go',
              envs: {
                'sdkrel:GOPATH': '../../../..'
              },
              advanced_options: {
                clone_dir: './src/github.com/Azure/azure-sdk-for-go'
              },
              version: '0.2.0'
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-sdk-for-go/latest/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              after_scripts: [
                'dep ensure',
                'go generate ./profiles/generate.go',
                'gofmt -w ./profiles/',
                'gofmt -w ./services/'
              ],
              autorest_options: {
                use: '@microsoft.azure/autorest.go@~2.1.131',
                go: '',
                verbose: '',
                'sdkrel:go-sdk-folder': '.',
                multiapi: '',
                'use-onever': '',
                'preview-chk': ''
              },
              repotag: 'azure-sdk-for-go',
              envs: {
                'sdkrel:GOPATH': '../../../..'
              },
              advanced_options: {
                clone_dir: './src/github.com/Azure/azure-sdk-for-go'
              },
              version: '0.2.0'
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              autorest_options: {
                java: '',
                verbose: '',
                multiapi: '',
                'sdkrel:azure-libraries-for-java-folder': '.',
                use: '@microsoft.azure/autorest.java@2.1.85'
              }
            }
          })
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json',
          200,
          undefined,
          JSON.stringify({
            meta: {
              autorest_options: {
                nodejs: '',
                'license-header': 'MICROSOFT_MIT_NO_VERSION',
                use: '@microsoft.azure/autorest.nodejs@2.2.131',
                'sdkrel:node-sdks-folder': '.'
              },
              advanced_options: {
                clone_dir: 'azure-sdk-for-node'
              }
            }
          })
        )
        .add(
          'GET',
          testSpecificationPullRequest.diff_url,
          200,
          undefined,
          `
diff --git a/specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json b/specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json
diff --git a/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json b/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json
diff --git a/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json b/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json
diff --git a/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json b/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json
diff --git a/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json b/specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`
        )
        .add(
          'HEAD',
          'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md',
          200,
          undefined,
          ''
        )
        .add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md',
          200,
          undefined,
          `
\`\`\`yaml $(swagger-to-sdk)
swagger-to-sdk:
  - repo: azure-sdk-for-python
  - repo: azure-sdk-for-java
  - repo: azure-sdk-for-go
  - repo: azure-sdk-for-js
  - repo: azure-sdk-for-node
\`\`\``
        )
        .add('HEAD', 'https://github.com/Azure/azure-sdk-for-js')
        .add('HEAD', 'https://github.com/Azure/azure-sdk-for-python')
        .add('HEAD', 'https://github.com/Azure/azure-sdk-for-go')
        .add('HEAD', 'https://github.com/Azure/azure-sdk-for-java')
        .add('HEAD', 'https://github.com/Azure/azure-sdk-for-node');
}

export interface CreateTestRunnerOptions {
  specificationPullRequest: GitHubPullRequest;
  autorest: string;
  generationWorkingFolderPath: string;
  github: FakeGitHub;
  real?: boolean;
}

export function createTestRunner(options: CreateTestRunnerOptions): FakeRunner {
  const pythonFolderPath: string = joinPath(options.generationWorkingFolderPath, '1');
  const javaFolderPath: string = joinPath(options.generationWorkingFolderPath, '2');
  const goFolderPath: string = joinPath(options.generationWorkingFolderPath, 'src/github.com/Azure/azure-sdk-for-go');
  const nodeFolderPath: string = joinPath(options.generationWorkingFolderPath, 'azure-sdk-for-node');
  const jsFolderPath: string = joinPath(options.generationWorkingFolderPath, 'azure-sdk-for-js');
  const rubyFolderPath: string = joinPath(options.generationWorkingFolderPath, '6');

  const runner = new FakeRunner();

  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'generation', 'https://github.com/Azure/azure-sdk-for-python']
  });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'generation', 'https://github.com/generation/azure-sdk-for-python']
  });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'generation', 'https://github.com/integration/azure-sdk-for-python']
  });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'generation', 'https://github.com/Azure/azure-sdk-for-java']
  });
  runner.set({ executable: 'git', args: ['remote', 'add', 'generation', 'https://github.com/Azure/azure-sdk-for-go'] });
  runner.set({ executable: 'git', args: ['remote', 'add', 'generation', 'https://github.com/Azure/azure-sdk-for-js'] });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'generation', 'https://github.com/Azure/azure-sdk-for-node']
  });

  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'integration', 'https://github.com/Azure/azure-sdk-for-python']
  });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'integration', 'https://github.com/Azure/azure-sdk-for-java']
  });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'integration', 'https://github.com/Azure/azure-sdk-for-go']
  });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'integration', 'https://github.com/Azure/azure-sdk-for-js']
  });
  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'integration', 'https://github.com/Azure/azure-sdk-for-node']
  });

  runner.set({
    executable: 'git',
    args: ['remote', 'add', 'integration', 'https://github.com/integration/azure-sdk-for-python']
  });

  runner.set({ executable: 'git', args: ['remote', 'add', 'main', 'https://github.com/Azure/azure-sdk-for-python'] });
  runner.set({ executable: 'git', args: ['remote', 'add', 'main', 'https://github.com/Azure/azure-sdk-for-java'] });
  runner.set({ executable: 'git', args: ['remote', 'add', 'main', 'https://github.com/Azure/azure-sdk-for-go'] });
  runner.set({ executable: 'git', args: ['remote', 'add', 'main', 'https://github.com/Azure/azure-sdk-for-js'] });
  runner.set({ executable: 'git', args: ['remote', 'add', 'main', 'https://github.com/Azure/azure-sdk-for-node'] });

  runner.set({ executable: 'git', args: ['fetch', '--all'] });

  runner.set({ executable: 'git', args: ['checkout', '--track', 'integration/sdkAutomation/azure-mgmt-rdbms'] });
  runner.set({ executable: 'git', args: ['checkout', '--track', 'integration/sdkAutomationTest/azure-mgmt-rdbms'] });
  runner.set({ executable: `git`, args: ['checkout', 'sdkAutomationMainBranch'] });

  runner.set({ executable: `git`, args: ['pull'] });

  runner.set({ executable: `git`, args: ['push'] });
  runner.set({
    executable: `git`,
    args: ['push', '--set-upstream', 'generation', 'sdkAutomation/azure-mgmt-rdbms@4994', '--force']
  });
  runner.set({
    executable: `git`,
    args: ['push', '--set-upstream', 'generation', 'apples/azure-mgmt-rdbms@4994', '--force']
  });
  runner.set({
    executable: `git`,
    args: ['push', '--set-upstream', 'generation', 'sdkAutomationTest/azure-mgmt-rdbms@4994', '--force']
  });
  runner.set({
    executable: 'git',
    args: ['config', '--get', 'remote.origin.url'],
    executionFolderPath: pythonFolderPath,
    result: {
      exitCode: 0,
      stderr: '',
      stdout: 'https://github.com/Azure/azure-sdk-for-python'
    }
  });

  if (options.real) {
    runner.passthroughUnrecognized();
  } else {
    runner.set({ executable: `git`, args: ['branch'], result: { exitCode: 0, stdout: '* master' } });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'branch', '--remotes'],
      result: {
        exitCode: 0,
        stdout: `integration/sdkAutomation/azure-mgmt-rdbms
integration/apples/azure-mgmt-rdbms
integration/sdkAutomationTest/azure-mgmt-rdbms`
      }
    });
    runner.set({ executable: `git`, args: ['checkout', '--track', 'main/non-master', '-b', 'main-non-master'] });
    runner.set({ executable: `git`, args: ['checkout', '--track', 'main/master', '-b', 'main-master'] });
    runner.set({ executable: `git`, args: ['checkout', 'main-master'] });
    runner.set({ executable: `git`, args: ['checkout', 'main-non-master'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/azure-mgmt-rdbms@4994'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/azure-mgmt-rdbms@4994'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'apples/azure-mgmt-rdbms@4994'] });
    runner.set({ executable: `git`, args: ['checkout', 'apples/azure-mgmt-rdbms@4994'] });
    runner.set({ executable: `git`, args: ['checkout', '--track', 'integration/apples/azure-mgmt-rdbms'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomationTest/azure-mgmt-rdbms@4994'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomationTest/azure-mgmt-rdbms@4994'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/azure-mgmt-rdbms'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/azure-mgmt-rdbms'] });
    runner.set({
      executable: `git`,
      args: ['checkout', '-b', 'sdkAutomation/mysql/resource-manager/v2017_12_01@4994']
    });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/mysql/resource-manager/v2017_12_01@4994'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/mysql/resource-manager/v2017_12_01'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/mysql/resource-manager/v2017_12_01'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/mysql/mgmt/2017-12-01@4994'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/mysql/mgmt/2017-12-01@4994'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/mysql/mgmt/2017-12-01'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/mysql/mgmt/2017-12-01'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/azure-arm-mysql@4994'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/azure-arm-mysql@4994'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/azure-arm-mysql'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/azure-arm-mysql'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/@azure/arm-mysql@4994'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/@azure/arm-mysql@4994'] });
    runner.set({ executable: `git`, args: ['checkout', '-b', 'sdkAutomation/@azure/arm-mysql'] });
    runner.set({ executable: `git`, args: ['checkout', 'sdkAutomation/@azure/arm-mysql'] });
    runner.set({ executable: `git`, args: ['commit', '-m', 'Modifications after running after_scripts'] });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/Azure/azure-sdk-for-python', pythonFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(pythonFolderPath, 'azure-mgmt-rdbms');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'setup.py'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/Azure/azure-sdk-for-python', pythonFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(pythonFolderPath, 'azure-mgmt-rdbms');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'setup.py'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/integration/azure-sdk-for-python', pythonFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(pythonFolderPath, 'azure-mgmt-rdbms');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'setup.py'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/integration/azure-sdk-for-python', pythonFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(pythonFolderPath, 'azure-mgmt-rdbms');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'setup.py'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/generation/azure-sdk-for-python', pythonFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(pythonFolderPath, 'azure-mgmt-rdbms');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'setup.py'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/generation/azure-sdk-for-python', pythonFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(pythonFolderPath, 'azure-mgmt-rdbms');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'setup.py'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/Azure/azure-sdk-for-java', javaFolderPath],
      result: async () => {
        const mySqlPackageFolderPath: string = joinPath(javaFolderPath, 'mysql/resource-manager/v2017_12_01');
        await createFolder(mySqlPackageFolderPath);
        await writeFileContents(joinPath(mySqlPackageFolderPath, 'pom.xml'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/Azure/azure-sdk-for-go', goFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(goFolderPath, 'services/mysql/mgmt/2017-12-01/mysql');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'client.go'), '');
        await createFolder(joinPath(goFolderPath, 'profiles'));
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/Azure/azure-sdk-for-node', nodeFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(nodeFolderPath, 'lib/services/mysqlManagement');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'package.json'), `{"name": "azure-arm-mysql"}`);
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/Azure/azure-sdk-for-js', jsFolderPath],
      result: async () => {
        const packageFolderPath: string = joinPath(jsFolderPath, 'packages/@azure/arm-mysql');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'package.json'), `{"name":"@azure/arm-mysql"}`);
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['clone', '--quiet', 'https://github.com/Azure/azure-sdk-for-ruby', rubyFolderPath]
    });
    runner.set({
      executable: options.autorest,
      args: [
        '--version=2.0.4302',
        '--use=@microsoft.azure/autorest.python@~3.0.56',
        '--python',
        '--python-mode=update',
        '--multiapi',
        `--python-sdks-folder=${pythonFolderPath}`,
        `https://raw.githubusercontent.com/Azure/azure-rest-api-specs/${options.specificationPullRequest.merge_commit_sha}/specification/mysql/resource-manager/readme.md`
      ]
    });
    runner.set({
      executable: options.autorest,
      args: [
        '--java',
        '--verbose',
        '--multiapi',
        '--use=@microsoft.azure/autorest.java@2.1.85',
        `--azure-libraries-for-java-folder=${javaFolderPath}`,
        '--version=2.0.4283',
        `https://raw.githubusercontent.com/Azure/azure-rest-api-specs/${options.specificationPullRequest.merge_commit_sha}/specification/mysql/resource-manager/readme.md`
      ]
    });
    runner.set({
      executable: options.autorest,
      args: [
        `--use=@microsoft.azure/autorest.go@~2.1.131`,
        `--go`,
        `--verbose`,
        `--multiapi`,
        `--use-onever`,
        `--preview-chk`,
        `--go-sdk-folder=${goFolderPath}`,
        `--version=2.0.4283`,
        `https://raw.githubusercontent.com/Azure/azure-rest-api-specs/${options.specificationPullRequest.merge_commit_sha}/specification/mysql/resource-manager/readme.md`
      ]
    });
    runner.set({
      executable: options.autorest,
      args: [
        `--nodejs`,
        `--license-header=MICROSOFT_MIT_NO_VERSION`,
        `--use=@microsoft.azure/autorest.nodejs@2.2.131`,
        `--node-sdks-folder=${nodeFolderPath}`,
        `--version=2.0.4283`,
        `https://raw.githubusercontent.com/Azure/azure-rest-api-specs/${options.specificationPullRequest.merge_commit_sha}/specification/mysql/resource-manager/readme.md`
      ]
    });
    runner.set({
      executable: options.autorest,
      args: [
        '--typescript',
        '--license-header=MICROSOFT_MIT_NO_VERSION',
        '--use=@microsoft.azure/autorest.typescript@4.0.0',
        `--typescript-sdks-folder=${jsFolderPath}`,
        '--version=2.0.4283',
        `https://raw.githubusercontent.com/Azure/azure-rest-api-specs/${options.specificationPullRequest.merge_commit_sha}/specification/mysql/resource-manager/readme.md`
      ]
    });
    runner.set({
      executable: options.autorest,
      args: [
        '--version=preview',
        '--use=@microsoft.azure/autorest.ruby@3.0.20',
        '--ruby',
        '--multiapi',
        `--ruby-sdks-folder=${rubyFolderPath}`,
        `https://raw.githubusercontent.com/Azure/azure-rest-api-specs/${options.specificationPullRequest.merge_commit_sha}/specification/mysql/resource-manager/readme.md`
      ]
    });
    runner.set({
      executable: `git`,
      args: ['checkout', 'package.json']
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--ignore-all-space'],
      executionFolderPath: goFolderPath,
      result: {
        exitCode: 0,
        stdout: `diff --git a/services/mysql/mgmt/2017-12-01/mysql/locationbasedperformancetier.go b/services/mysql/mgmt/2017-12-01/mysql/locationbasedperformancetier.go
diff --git a/profiles/latest/mysql/mgmt/mysql/models.go b/profiles/latest/mysql/mgmt/mysql/models.go
diff --git a/profiles/latest/mysql/mgmt/mysql/mysqlapi/models.go b/profiles/latest/mysql/mgmt/mysql/mysqlapi/models.go
diff --git a/profiles/latest/servicebus/mgmt/servicebus/models.go b/profiles/latest/servicebus/mgmt/servicebus/models.go
diff --git a/profiles/preview/mysql/mgmt/mysql/models.go b/profiles/preview/mysql/mgmt/mysql/models.go
diff --git a/profiles/preview/mysql/mgmt/mysql/mysqlapi/models.go b/profiles/preview/mysql/mgmt/mysql/mysqlapi/models.go
diff --git a/profiles/preview/servicebus/mgmt/servicebus/models.go b/profiles/preview/servicebus/mgmt/servicebus/models.go`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--name-only', '--ignore-all-space'],
      executionFolderPath: goFolderPath,
      result: {
        exitCode: 0,
        stdout: `services/mysql/mgmt/2017-12-01/mysql/locationbasedperformancetier.go
profiles/latest/mysql/mgmt/mysql/models.go
profiles/latest/mysql/mgmt/mysql/mysqlapi/models.go
profiles/latest/servicebus/mgmt/servicebus/models.go
profiles/preview/mysql/mgmt/mysql/models.go
profiles/preview/mysql/mgmt/mysql/mysqlapi/models.go
profiles/preview/servicebus/mgmt/servicebus/models.go`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--ignore-all-space'],
      executionFolderPath: nodeFolderPath,
      result: {
        exitCode: 0,
        stdout: `diff --git a/lib/services/mysqlManagement/lib/models/firewallRuleListResult.js b/lib/services/mysqlManagement/lib/models/firewallRuleListResult.js`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--name-only', '--ignore-all-space'],
      executionFolderPath: nodeFolderPath,
      result: {
        exitCode: 0,
        stdout: ``
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--ignore-all-space'],
      executionFolderPath: pythonFolderPath,
      result: {
        exitCode: 0,
        stdout: `diff --git a/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py b/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--name-only', '--ignore-all-space'],
      executionFolderPath: pythonFolderPath,
      result: {
        exitCode: 0,
        stdout: `azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/non-master', '--staged', '--ignore-all-space'],
      executionFolderPath: pythonFolderPath,
      result: {
        exitCode: 0,
        stdout: `diff --git a/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py b/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/non-master', '--staged', '--name-only', '--ignore-all-space'],
      executionFolderPath: pythonFolderPath,
      result: {
        exitCode: 0,
        stdout: `azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`
      }
    });
    runner.set({
      executable: `git`,
      args: [
        '--no-pager',
        'diff',
        'integration/sdkAutomation/azure-mgmt-rdbms',
        '--staged',
        '--name-only',
        '--ignore-all-space'
      ],
      executionFolderPath: pythonFolderPath,
      result: {
        exitCode: 0,
        stdout: `azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`
      }
    });
    runner.set({
      executable: `git`,
      args: [
        '--no-pager',
        'diff',
        'integration/sdkAutomationTest/azure-mgmt-rdbms',
        '--staged',
        '--name-only',
        '--ignore-all-space'
      ],
      executionFolderPath: pythonFolderPath,
      result: {
        exitCode: 0,
        stdout: `azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`
      }
    });
    runner.set({
      executable: `git`,
      args: [
        '--no-pager',
        'diff',
        'integration/apples/azure-mgmt-rdbms',
        '--staged',
        '--name-only',
        '--ignore-all-space'
      ],
      executionFolderPath: pythonFolderPath,
      result: {
        exitCode: 0,
        stdout: `azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--ignore-all-space'],
      executionFolderPath: jsFolderPath,
      result: {
        exitCode: 0
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--ignore-all-space'],
      executionFolderPath: javaFolderPath,
      result: {
        exitCode: 0,
        stdout: `diff --git a/mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/CheckNameAvailabilitys.java b/mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/CheckNameAvailabilitys.java
diff --git a/mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/Configuration.java b/mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/Configuration.java`
      }
    });
    runner.set({
      executable: `git`,
      args: ['--no-pager', 'diff', 'main/master', '--staged', '--name-only', '--ignore-all-space'],
      executionFolderPath: javaFolderPath,
      result: {
        exitCode: 0,
        stdout: `mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/CheckNameAvailabilitys.java
mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/Configuration.java`
      }
    });
    runner.set({
      executable: npmExecutable(),
      args: ['pack'],
      executionFolderPath: joinPath(jsFolderPath, 'packages/@azure/arm-mysql'),
      result: async () => {
        const packageFolderPath: string = joinPath(jsFolderPath, 'packages/@azure/arm-mysql');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'azure-arm-mysql-3.2.0.tgz'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: npmExecutable(),
      args: ['pack'],
      executionFolderPath: joinPath(nodeFolderPath, 'lib/services/mysqlManagement'),
      result: async () => {
        const packageFolderPath: string = joinPath(nodeFolderPath, 'lib/services/mysqlManagement');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'azure-arm-mysql-3.2.0.tgz'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: mvnExecutable(),
      args: [
        'source:jar',
        'javadoc:jar',
        'package',
        '-f',
        joinPath(javaFolderPath, 'mysql/resource-manager/v2017_12_01'),
        '-DskipTests',
        '--batch-mode'
      ],
      result: () => {
        return { exitCode: 1 };
      }
    });
    runner.set({
      executable: `python`,
      args: ['./build_package.py', '--dest', joinPath(pythonFolderPath, 'azure-mgmt-rdbms'), 'azure-mgmt-rdbms'],
      result: async () => {
        const packageFolderPath: string = joinPath(pythonFolderPath, 'azure-mgmt-rdbms');
        await createFolder(packageFolderPath);
        await writeFileContents(joinPath(packageFolderPath, 'fake-python-package.whl'), '');
        return { exitCode: 0 };
      }
    });
    runner.set({
      executable: `git`,
      args: ['checkout', '-b', 'sdkAutomation/@azure/arm-mysql'],
      executionFolderPath: jsFolderPath
    });
    runner.set({
      executable: `git`,
      args: ['checkout', '-b', 'sdkAutomation/@azure/arm-mysql/4994'],
      executionFolderPath: jsFolderPath
    });
    runner.set({
      executable: `git`,
      args: ['checkout', '-b', 'restapi_auto_mysql/resource-manager'],
      executionFolderPath: jsFolderPath
    });
    runner.set({
      executable: `git`,
      args: ['checkout', '-b', `restapi_auto_${options.specificationPullRequest.number}`],
      executionFolderPath: jsFolderPath
    });
    runner.set({ executable: `git`, args: ['add', '*'] });
    runner.set({ executable: `git`, args: ['reset', '*'] });
    runner.set({
      executable: `git`,
      args: ['commit', '-m', `Generated from ${options.specificationPullRequest.head.sha}`, '-m', 'hello world']
    });
    runner.set({
      executable: 'git',
      args: ['rebase', '--strategy-option=theirs', 'main/master']
    });
    runner.set({
      executable: 'git',
      args: ['rebase', '--strategy-option=theirs', 'sdkAutomation/azure-mgmt-rdbms']
    });
    runner.set({
      executable: 'git',
      args: ['rebase', '--strategy-option=theirs', 'sdkAutomationTest/azure-mgmt-rdbms']
    });
    runner.set({
      executable: 'git',
      args: ['rebase', '--strategy-option=theirs', 'apples/azure-mgmt-rdbms']
    });
    runner.set({ executable: 'dep', args: ['ensure'] });
    runner.set({
      executable: 'go',
      args: ['generate', './profiles/generate.go'],
      result: async () => {
        await createFolder(joinPath(goFolderPath, 'profiles/latest/mysql/mgmt/mysql/'));
        await writeFileContents(joinPath(goFolderPath, 'profiles/latest/mysql/mgmt/mysql/models.go'), 'contents');

        await createFolder(joinPath(goFolderPath, 'profiles/latest/mysql/mgmt/mysql/mysqlapi/'));
        await writeFileContents(
          joinPath(goFolderPath, 'profiles/latest/mysql/mgmt/mysql/mysqlapi/models.go'),
          'contents'
        );

        await createFolder(joinPath(goFolderPath, 'profiles/latest/servicebus/mgmt/servicebus/'));
        await writeFileContents(
          joinPath(goFolderPath, 'profiles/latest/servicebus/mgmt/servicebus/models.go'),
          'contents'
        );

        await createFolder(joinPath(goFolderPath, 'profiles/preview/mysql/mgmt/mysql/'));
        await writeFileContents(joinPath(goFolderPath, 'profiles/preview/mysql/mgmt/mysql/models.go'), 'contents');

        await createFolder(joinPath(goFolderPath, 'profiles/preview/mysql/mgmt/mysql/mysqlapi/'));
        await writeFileContents(
          joinPath(goFolderPath, 'profiles/preview/mysql/mgmt/mysql/mysqlapi/models.go'),
          'contents'
        );

        await createFolder(joinPath(goFolderPath, 'profiles/preview/servicebus/mgmt/servicebus/'));
        await writeFileContents(
          joinPath(goFolderPath, 'profiles/preview/servicebus/mgmt/servicebus/models.go'),
          'contents'
        );

        return { exitCode: 0 };
      }
    });
    runner.set({ executable: 'gofmt', args: ['-w', './profiles/'] });
    runner.set({ executable: 'gofmt', args: ['-w', './services/'] });
  }
  return runner;
}
