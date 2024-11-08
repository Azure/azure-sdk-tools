import { getInMemoryLogger, InMemoryLogger } from '@azure/logger-js';
import {
  autorestExecutable,
  BlobStorageAppendBlob,
  BlobStorageBlob,
  BlobStorageContainer,
  BlobStoragePrefix,
  Command,
  Compressor,
  deleteFolder,
  FakeCompressor,
  FakeGitHub,
  FakeHttpClient,
  FakeRunner,
  GitHubPullRequest,
  HttpClient,
  InMemoryBlobStorage,
  joinPath,
  ExecutableGit
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { go } from '../lib/langSpecs/go';
import { python } from '../lib/langSpecs/python';
import {
  ensureStateImagesExist,
  getAutomationWorkingFolderPath,
  getBlobLogger,
  getGenerationWorkingFolderPath,
  getLogsBlob,
  getOpenAPISDKAutomationVersion,
  getRootFolderPath,
  SDKAutomation
} from '../lib/sdkAutomation';
import {
  createCommandProperties,
  replaceCommandVariables,
  replaceStringVariables,
  SDKRepository,
  SDKRepositoryContext,
  SDKRepositoryData
} from '../lib/sdkRepository';
import { getSpecificationPullRequest, SpecificationPullRequest } from '../lib/specificationPullRequest';
import { SDKGenerationPullRequestBaseOptions, SwaggerToSDKConfiguration } from '../lib/swaggerToSDKConfiguration';
import {
  createTestBlobStorageContainer,
  createTestGitHub,
  createTestHttpClient,
  createTestRunner,
  testSpecificationPullRequest
} from './test';

describe('sdkRepository.ts', function() {
  it('constructor()', function() {
    const logger: InMemoryLogger = getInMemoryLogger();
    const swaggerToSDKConfiguration: SwaggerToSDKConfiguration = {};
    const blobStorage = new InMemoryBlobStorage();
    const specPRIterationPrefix: BlobStoragePrefix = blobStorage.getPrefix('abc/def/blah');
    const specPRPrefix: BlobStoragePrefix = blobStorage.getPrefix('abc/blah');
    const logsBlob: BlobStorageAppendBlob = getLogsBlob(specPRIterationPrefix);
    const sdkRepositoryContext: SDKRepositoryContext = {
      writeGenerationData(): Promise<unknown> {
        return Promise.resolve();
      },
      deleteClonedRepositories: true,
      createCompressor(): Compressor {
        return new FakeCompressor();
      },
      httpClient: createTestHttpClient(),
      github: new FakeGitHub(),
      git: new ExecutableGit(),
      specificationPullRequest: {
        baseRepository: 'Azure/azure-rest-api-specs',
        headRepository: 'Azure/azure-rest-api-specs',
        headCommit: 'fake-head-commit',
        number: 235,
        title: 'fake-title',
        htmlUrl: 'fake-html-url'
      },
      getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
      getBlobLogger,
      createGenerationPullRequests: true
    };
    const sdkRepositoryData: SDKRepositoryData = {
      mainRepository: 'fake-repository-name',
      mainRepositoryUrl: 'blah',
      generationRepository: 'fake-repository-name',
      generationRepositoryUrl: 'blah',
      integrationRepository: 'fake-repository-name',
      integrationRepositoryUrl: 'blah',
      integrationBranchPrefix: 'sdkAutomation',
      mainBranch: 'master',
      languageName: 'Go',
      readmeMdFileUrlsToGenerate: [],
      status: 'pending',
      logsBlobUrl: logsBlob.getURL(),
      swaggerToSDKConfigFileUrl: 'blah/swagger_to_sdk_config.json'
    };
    const sdkRepository = new SDKRepository(
      logsBlob,
      logger,
      go,
      swaggerToSDKConfiguration,
      specPRIterationPrefix,
      specPRPrefix,
      sdkRepositoryContext,
      sdkRepositoryData
    );
    assert.strictEqual(sdkRepository.logger, logger);
    assert.strictEqual(sdkRepository.data, sdkRepositoryData);
  });

  describe('generate()', function() {
    describe('with sdk_generation_pull_request_base property set to undefined', function() {
      it('with the same main, integration, and generation repositories', async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: HttpClient = createTestHttpClient();
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, undefined);

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" to "Azure/azure-sdk-for-python".`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if the SDK integration branch "sdkAutomation/azure-mgmt-rdbms" already exists in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager branch --remotes`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - SDK integration branch exists: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing existing SDK integration branch "sdkAutomation/azure-mgmt-rdbms" onto main branch "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout --track integration/sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs main/master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git pull`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" onto SDK integration branch "sdkAutomation/azure-mgmt-rdbms" from "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff integration/sdkAutomation/azure-mgmt-rdbms --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "sdkAutomation/azure-mgmt-rdbms@4994" to Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation sdkAutomation/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "Azure/azure-sdk-for-python" from "sdkAutomation/azure-mgmt-rdbms@4994" to "sdkAutomation/azure-mgmt-rdbms"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in Azure/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);
            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('Azure/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'sdkAutomation/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'sdkAutomation/azure-mgmt-rdbms');
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });

      it('with the main repository different from integration and generation repositories', async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: FakeHttpClient = createTestHttpClient() as FakeHttpClient;
            httpClient.add(
              'GET',
              'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
              200,
              undefined,
              JSON.stringify({
                sdkRepositoryMappings: {
                  'azure-sdk-for-python': {
                    mainRepository: 'Azure/azure-sdk-for-python',
                    integrationRepository: 'integration/azure-sdk-for-python',
                    generationRepository: 'integration/azure-sdk-for-python',
                    integrationBranchPrefix: 'sdkAutomationTest'
                  },
                  'azure-sdk-for-java': 'Azure/azure-sdk-for-java',
                  'azure-sdk-for-go': 'Azure/azure-sdk-for-go',
                  'azure-sdk-for-js': 'Azure/azure-sdk-for-js',
                  'azure-sdk-for-node': 'Azure/azure-sdk-for-node'
                }
              })
            );
            httpClient.add('HEAD', 'https://github.com/integration/azure-sdk-for-python', 200);
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            runner.set({
              executable: 'git',
              args: ['config', '--get', 'remote.origin.url'],
              executionFolderPath: pythonFolderPath,
              result: {
                exitCode: 0,
                stderr: '',
                stdout: 'https://github.com/integration/azure-sdk-for-python'
              }
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, undefined);

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" generation repository to "integration/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" integration repository to "integration/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" main repository to "Azure/azure-sdk-for-python".`,
              `Using "sdkAutomationTest" as the integration branch prefix.`,
              `Using "master" as the main branch in the main repository.`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/integration/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/integration/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/integration/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomationTest/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomationTest/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomationTest/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if the SDK integration branch "sdkAutomationTest/azure-mgmt-rdbms" already exists in "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager branch --remotes`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - SDK integration branch exists: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing existing SDK integration branch "sdkAutomationTest/azure-mgmt-rdbms" onto main branch "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout --track integration/sdkAutomationTest/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs main/master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git pull`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing SDK generation branch "sdkAutomationTest/azure-mgmt-rdbms@4994" onto SDK integration branch "sdkAutomationTest/azure-mgmt-rdbms" from "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomationTest/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs sdkAutomationTest/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff integration/sdkAutomationTest/azure-mgmt-rdbms --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "sdkAutomationTest/azure-mgmt-rdbms@4994" to integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation sdkAutomationTest/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "integration/azure-sdk-for-python" from "sdkAutomationTest/azure-mgmt-rdbms@4994" to "sdkAutomationTest/azure-mgmt-rdbms"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label GenerationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label IntegrationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRInProgress in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRClosed in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRMerged in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in integration/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of integration/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of integration/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);

            assert.deepEqual(await github.getPullRequests('Azure/azure-sdk-for-python'), []);
            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('integration/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'sdkAutomationTest/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'sdkAutomationTest/azure-mgmt-rdbms');
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });

      it('with different main, integration, and generation repositories', async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: FakeHttpClient = createTestHttpClient() as FakeHttpClient;
            httpClient.add(
              'GET',
              'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
              200,
              undefined,
              JSON.stringify({
                sdkRepositoryMappings: {
                  'azure-sdk-for-python': {
                    mainRepository: 'Azure/azure-sdk-for-python',
                    integrationRepository: 'integration/azure-sdk-for-python',
                    generationRepository: 'generation/azure-sdk-for-python',
                    integrationBranchPrefix: 'apples'
                  },
                  'azure-sdk-for-java': 'Azure/azure-sdk-for-java',
                  'azure-sdk-for-go': 'Azure/azure-sdk-for-go',
                  'azure-sdk-for-js': 'Azure/azure-sdk-for-js',
                  'azure-sdk-for-node': 'Azure/azure-sdk-for-node'
                }
              })
            );
            httpClient.add('HEAD', 'https://github.com/integration/azure-sdk-for-python', 200);
            httpClient.add('HEAD', 'https://github.com/generation/azure-sdk-for-python', 200);
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            runner.set({
              executable: 'git',
              args: ['config', '--get', 'remote.origin.url'],
              executionFolderPath: pythonFolderPath,
              result: {
                exitCode: 0,
                stderr: '',
                stdout: 'https://github.com/integration/azure-sdk-for-python'
              }
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, undefined);

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" generation repository to "generation/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" integration repository to "integration/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" main repository to "Azure/azure-sdk-for-python".`,
              `Using "apples" as the integration branch prefix.`,
              `Using "master" as the main branch in the main repository.`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/generation/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/generation/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/integration/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "apples/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b apples/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout apples/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if the SDK integration branch "apples/azure-mgmt-rdbms" already exists in "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager branch --remotes`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - SDK integration branch exists: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing existing SDK integration branch "apples/azure-mgmt-rdbms" onto main branch "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout --track integration/apples/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs main/master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git pull`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing SDK generation branch "apples/azure-mgmt-rdbms@4994" onto SDK integration branch "apples/azure-mgmt-rdbms" from "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout apples/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs apples/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff integration/apples/azure-mgmt-rdbms --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "apples/azure-mgmt-rdbms@4994" to generation/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation apples/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "integration/azure-sdk-for-python" from "generation:apples/azure-mgmt-rdbms@4994" to "apples/azure-mgmt-rdbms"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: false`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label GenerationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label IntegrationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRInProgress in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRClosed in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRMerged in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in integration/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of generation/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of generation/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);

            assert.deepEqual(await github.getPullRequests('Azure/azure-sdk-for-python'), []);
            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('integration/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'apples/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'apples/azure-mgmt-rdbms');
            assert.deepEqual(await github.getPullRequests('generation/azure-sdk-for-python'), []);
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });
    });

    describe(`with sdk_generation_pull_request_base property set to "integration_branch"`, function() {
      it(`with the same main, integration, and generation repositories"`, async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: HttpClient = createTestHttpClient();
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, 'integration_branch');

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" to "Azure/azure-sdk-for-python".`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if the SDK integration branch "sdkAutomation/azure-mgmt-rdbms" already exists in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager branch --remotes`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - SDK integration branch exists: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing existing SDK integration branch "sdkAutomation/azure-mgmt-rdbms" onto main branch "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout --track integration/sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs main/master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git pull`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" onto SDK integration branch "sdkAutomation/azure-mgmt-rdbms" from "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff integration/sdkAutomation/azure-mgmt-rdbms --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "sdkAutomation/azure-mgmt-rdbms@4994" to Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation sdkAutomation/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "Azure/azure-sdk-for-python" from "sdkAutomation/azure-mgmt-rdbms@4994" to "sdkAutomation/azure-mgmt-rdbms"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in Azure/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });

      it('with the main repository different from integration and generation repositories', async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: FakeHttpClient = createTestHttpClient() as FakeHttpClient;
            httpClient.add(
              'GET',
              'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
              200,
              undefined,
              JSON.stringify({
                sdkRepositoryMappings: {
                  'azure-sdk-for-python': {
                    mainRepository: 'Azure/azure-sdk-for-python',
                    integrationRepository: 'integration/azure-sdk-for-python',
                    generationRepository: 'integration/azure-sdk-for-python'
                  },
                  'azure-sdk-for-java': 'Azure/azure-sdk-for-java',
                  'azure-sdk-for-go': 'Azure/azure-sdk-for-go',
                  'azure-sdk-for-js': 'Azure/azure-sdk-for-js',
                  'azure-sdk-for-node': 'Azure/azure-sdk-for-node'
                }
              })
            );
            httpClient.add('HEAD', 'https://github.com/integration/azure-sdk-for-python', 200);
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            runner.set({
              executable: 'git',
              args: ['config', '--get', 'remote.origin.url'],
              executionFolderPath: pythonFolderPath,
              result: {
                exitCode: 0,
                stderr: '',
                stdout: 'https://github.com/integration/azure-sdk-for-python'
              }
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, 'integration_branch');

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" generation repository to "integration/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" integration repository to "integration/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" main repository to "Azure/azure-sdk-for-python".`,
              `Using "sdkAutomation" as the integration branch prefix.`,
              `Using "master" as the main branch in the main repository.`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/integration/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/integration/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/integration/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if the SDK integration branch "sdkAutomation/azure-mgmt-rdbms" already exists in "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager branch --remotes`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - SDK integration branch exists: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing existing SDK integration branch "sdkAutomation/azure-mgmt-rdbms" onto main branch "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout --track integration/sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs main/master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git pull`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" onto SDK integration branch "sdkAutomation/azure-mgmt-rdbms" from "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff integration/sdkAutomation/azure-mgmt-rdbms --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "sdkAutomation/azure-mgmt-rdbms@4994" to integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation sdkAutomation/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "integration/azure-sdk-for-python" from "sdkAutomation/azure-mgmt-rdbms@4994" to "sdkAutomation/azure-mgmt-rdbms"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label GenerationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label IntegrationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRInProgress in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRClosed in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRMerged in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in integration/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of integration/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of integration/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);

            assert.deepEqual(await github.getPullRequests('Azure/azure-sdk-for-python'), []);
            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('integration/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'sdkAutomation/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'sdkAutomation/azure-mgmt-rdbms');
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });

      it('with different main, integration, and generation repositories', async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: FakeHttpClient = createTestHttpClient() as FakeHttpClient;
            httpClient.add(
              'GET',
              'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
              200,
              undefined,
              JSON.stringify({
                sdkRepositoryMappings: {
                  'azure-sdk-for-python': {
                    mainRepository: 'Azure/azure-sdk-for-python',
                    integrationRepository: 'integration/azure-sdk-for-python',
                    generationRepository: 'generation/azure-sdk-for-python'
                  },
                  'azure-sdk-for-java': 'Azure/azure-sdk-for-java',
                  'azure-sdk-for-go': 'Azure/azure-sdk-for-go',
                  'azure-sdk-for-js': 'Azure/azure-sdk-for-js',
                  'azure-sdk-for-node': 'Azure/azure-sdk-for-node'
                }
              })
            );
            httpClient.add('HEAD', 'https://github.com/integration/azure-sdk-for-python', 200);
            httpClient.add('HEAD', 'https://github.com/generation/azure-sdk-for-python', 200);
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            runner.set({
              executable: 'git',
              args: ['config', '--get', 'remote.origin.url'],
              executionFolderPath: pythonFolderPath,
              result: {
                exitCode: 0,
                stderr: '',
                stdout: 'https://github.com/integration/azure-sdk-for-python'
              }
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, 'integration_branch');

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" generation repository to "generation/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" integration repository to "integration/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" main repository to "Azure/azure-sdk-for-python".`,
              `Using "sdkAutomation" as the integration branch prefix.`,
              `Using "master" as the main branch in the main repository.`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/generation/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/generation/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/integration/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if the SDK integration branch "sdkAutomation/azure-mgmt-rdbms" already exists in "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager branch --remotes`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - SDK integration branch exists: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing existing SDK integration branch "sdkAutomation/azure-mgmt-rdbms" onto main branch "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout --track integration/sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs main/master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git pull`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Rebasing SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" onto SDK integration branch "sdkAutomation/azure-mgmt-rdbms" from "integration/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git rebase --strategy-option=theirs sdkAutomation/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff integration/sdkAutomation/azure-mgmt-rdbms --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "sdkAutomation/azure-mgmt-rdbms@4994" to generation/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation sdkAutomation/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "integration/azure-sdk-for-python" from "generation:sdkAutomation/azure-mgmt-rdbms@4994" to "sdkAutomation/azure-mgmt-rdbms"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: false`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label GenerationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label IntegrationPR in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRInProgress in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRClosed in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Didn't find label SpecPRMerged in integration/azure-sdk-for-python. Creating it...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in integration/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of generation/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of generation/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);

            assert.deepEqual(await github.getPullRequests('Azure/azure-sdk-for-python'), []);
            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('integration/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'sdkAutomation/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'sdkAutomation/azure-mgmt-rdbms');
            assert.deepEqual(await github.getPullRequests('generation/azure-sdk-for-python'), []);
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });
    });

    describe(`with sdk_generation_pull_request_base property set to "main_branch" but no "main_branch" specified`, function() {
      it(`with the same main, integration, and generation repositories"`, async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: HttpClient = createTestHttpClient();
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, 'main_branch');

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" to "Azure/azure-sdk-for-python".`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "sdkAutomation/azure-mgmt-rdbms@4994" to Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation sdkAutomation/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "Azure/azure-sdk-for-python" from "sdkAutomation/azure-mgmt-rdbms@4994" to "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in Azure/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);

            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('Azure/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'sdkAutomation/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'master');
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });
    });

    describe(`with sdk_generation_pull_request_base property set to "main_branch" and "main_branch" set to "master"`, function() {
      it(`with the same main, integration, and generation repositories"`, async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: FakeHttpClient = createTestHttpClient() as FakeHttpClient;
            httpClient.add(
              'GET',
              'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
              200,
              undefined,
              JSON.stringify({
                sdkRepositoryMappings: {
                  'azure-sdk-for-python': {
                    mainRepository: 'Azure/azure-sdk-for-python',
                    integrationRepository: 'Azure/azure-sdk-for-python',
                    generationRepository: 'Azure/azure-sdk-for-python',
                    integrationBranchPrefix: 'sdkAutomation',
                    mainBranch: 'master'
                  },
                  'azure-sdk-for-java': 'Azure/azure-sdk-for-java',
                  'azure-sdk-for-go': 'Azure/azure-sdk-for-go',
                  'azure-sdk-for-js': 'Azure/azure-sdk-for-js',
                  'azure-sdk-for-node': 'Azure/azure-sdk-for-node'
                }
              })
            );
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, 'main_branch');

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" generation repository to "Azure/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" integration repository to "Azure/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" main repository to "Azure/azure-sdk-for-python".`,
              `Using "sdkAutomation" as the integration branch prefix.`,
              `Using "master" as the main branch in the main repository.`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "sdkAutomation/azure-mgmt-rdbms@4994" to Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation sdkAutomation/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "Azure/azure-sdk-for-python" from "sdkAutomation/azure-mgmt-rdbms@4994" to "master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in Azure/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);
            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('Azure/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'sdkAutomation/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'master');
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });

      it(`with the same main, integration, and generation repositories but only main repository available"`, async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: FakeHttpClient = createTestHttpClient() as FakeHttpClient;
            httpClient.add(
              'GET',
              'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
              200,
              undefined,
              JSON.stringify({
                sdkRepositoryMappings: {
                  'azure-sdk-for-python': {
                    mainRepository: 'Azure/azure-sdk-for-python',
                    integrationRepository: 'integration/azure-sdk-for-python',
                    generationRepository: 'generation/azure-sdk-for-python',
                    integrationBranchPrefix: 'sdkAutomation',
                    mainBranch: 'master'
                  },
                  'azure-sdk-for-java': 'Azure/azure-sdk-for-java'
                }
              })
            );
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, 'main_branch');

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" generation repository to "generation/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" integration repository to "integration/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" main repository to "Azure/azure-sdk-for-python".`,
              `Using "sdkAutomation" as the integration branch prefix.`,
              `Using "master" as the main branch in the main repository.`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `SDK repository azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Could not find a swagger_to_sdk_config.json file at https://raw.githubusercontent.com/azure-sdk-for-go/master/swagger_to_sdk_config.json.`,
              `SDK repository azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Could not find a swagger_to_sdk_config.json file at https://raw.githubusercontent.com/azure-sdk-for-js/master/swagger_to_sdk_config.json.`,
              `SDK repository azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Could not find a swagger_to_sdk_config.json file at https://raw.githubusercontent.com/azure-sdk-for-node/master/swagger_to_sdk_config.json.`,
              `Azure/azure-sdk-for-python - Integration repository https://github.com/integration/azure-sdk-for-python doesn't exist. Using fallback https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - Generation repository https://github.com/generation/azure-sdk-for-python doesn't exist. Using fallback https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/master -b main-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "sdkAutomation/azure-mgmt-rdbms@4994" based off of "master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout sdkAutomation/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - No SDK repository artifact files detected.`,
              `Azure/azure-sdk-for-python - Deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);

            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('Azure/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 0);
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });
    });

    describe(`with sdk_generation_pull_request_base property set to "main_branch" and "main_branch" set to "non-master"`, function() {
      it(`with the same main, integration, and generation repositories`, async function() {
        const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
        try {
          const automationWorkingPrefix: BlobStoragePrefix = workingContainer.getPrefix('');
          await ensureStateImagesExist(automationWorkingPrefix);

          const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
          const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
          await deleteFolder(generationWorkingFolderPath);
          try {
            const github: FakeGitHub = await createTestGitHub();
            const logger: InMemoryLogger = getInMemoryLogger();
            const httpClient: FakeHttpClient = createTestHttpClient() as FakeHttpClient;
            httpClient.add(
              'GET',
              'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
              200,
              undefined,
              JSON.stringify({
                sdkRepositoryMappings: {
                  'azure-sdk-for-python': {
                    mainRepository: 'Azure/azure-sdk-for-python',
                    integrationBranchPrefix: 'apples',
                    mainBranch: 'non-master'
                  },
                  'azure-sdk-for-java': 'Azure/azure-sdk-for-java',
                  'azure-sdk-for-go': 'Azure/azure-sdk-for-go',
                  'azure-sdk-for-js': 'Azure/azure-sdk-for-js',
                  'azure-sdk-for-node': 'Azure/azure-sdk-for-node'
                }
              })
            );
            const autorest: string = autorestExecutable({
              autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
            });
            const runner: FakeRunner = createTestRunner({
              specificationPullRequest: testSpecificationPullRequest,
              autorest,
              generationWorkingFolderPath: generationWorkingFolderPath,
              github
            });
            const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
              github,
              logger,
              runner,
              httpClient,
              createGenerationPullRequests: true
            });
            const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
              sdkAutomation,
              automationWorkingPrefix,
              testSpecificationPullRequest,
              true
            );
            await specificationPullRequest.populateSDKRepositoryReadmeMdFilesToGenerate();
            const sdkRepository: SDKRepository = (await specificationPullRequest.generation.getSDKRepository(
              'Azure/azure-sdk-for-python',
              python
            ))!;
            setSDKGenerationPullRequestBase(sdkRepository.swaggerToSDKConfiguration, 'main_branch');

            await sdkRepository.generate(generationWorkingFolderPath, 1);

            const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
            assert.deepEqual(logger.allLogs, [
              `Using provided GitHub.`,
              `No provided Git. Using ExecutableGit instance.`,
              `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
              `Using provided HttpClient.`,
              `No BlobProxy provided. Using a FakeBlobProxy.`,
              `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
              `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
              `Using FakeGitHub client.`,
              `Getting generation state from https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/data.json...`,
              `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
              `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
              `Specification repository configuration response status code: 200`,
              `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
              `diff_url response status code is 200.`,
              `diff_url response body contains 5 changed files:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `diff_url response body contains 5 changed files in the specification folder:`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
              `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`,
              `Found 1 readme.md files to generate:`,
              `specification/mysql/resource-manager/readme.md`,
              `Looking for repositories to generate in "specification/mysql/resource-manager/readme.md"...`,
              `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md"...`,
              `Merged readme.md response status code is 200.`,
              `Found 5 requested SDK repositories:`,
              `  azure-sdk-for-python`,
              `  azure-sdk-for-java`,
              `  azure-sdk-for-go`,
              `  azure-sdk-for-js`,
              `  azure-sdk-for-node`,
              `Mapping "azure-sdk-for-python" generation repository to "Azure/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" integration repository to "Azure/azure-sdk-for-python".`,
              `Mapping "azure-sdk-for-python" main repository to "Azure/azure-sdk-for-python".`,
              `Using "apples" as the integration branch prefix.`,
              `Using "non-master" as the main branch in the main repository.`,
              `SDK repository Azure/azure-sdk-for-python matches programming language Python.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/non-master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-python: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-java" to "Azure/azure-sdk-for-java".`,
              `SDK repository Azure/azure-sdk-for-java matches programming language Java.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-java/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-java: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-go" to "Azure/azure-sdk-for-go".`,
              `SDK repository Azure/azure-sdk-for-go matches programming language Go.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-go: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-js" to "Azure/azure-sdk-for-js".`,
              `SDK repository Azure/azure-sdk-for-js matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-js/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-js: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Mapping "azure-sdk-for-node" to "Azure/azure-sdk-for-node".`,
              `SDK repository Azure/azure-sdk-for-node matches programming language JavaScript.`,
              `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-node/master/swagger_to_sdk_config.json"...`,
              `Adding readme.md to generate to Azure/azure-sdk-for-node: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-python ${pythonFolderPath}`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-python`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git fetch --all`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git checkout --track main/non-master -b main-non-master`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
              `Azure/azure-sdk-for-python - Resolving "autorest" version "preview" to "2.0.4302".`,
              `Azure/azure-sdk-for-python - Getting dist-tags for "@microsoft.azure/autorest.python" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.python/dist-tags"...`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
              `Azure/azure-sdk-for-python - "@microsoft.azure/autorest.python@~3.0.56" does not have a dist-tag to resolve to.`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: ${autorest} --version=2.0.4302 --use=@microsoft.azure/autorest.python@~3.0.56 --python --python-mode=update --multiapi --python-sdks-folder=${pythonFolderPath} https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
              `Azure/azure-sdk-for-python - Getting diff after AutoRest ran...`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git add *`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git --no-pager diff main/non-master --staged --ignore-all-space`,
              `Azure/azure-sdk-for-python - ${pythonFolderPath}: git reset *`,
              `Azure/azure-sdk-for-python - The following files were changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - Found 1 package folder that changed:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Package name for "${pythonFolderPath}/azure-mgmt-rdbms" is "azure-mgmt-rdbms".`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation branch "apples/azure-mgmt-rdbms@4994" based off of "non-master" in "Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout main-non-master`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout -b apples/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}/azure-mgmt-rdbms: git add *`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/non-master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - 1 files staged for commit:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git checkout apples/azure-mgmt-rdbms@4994`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - No after_scripts to run.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git --no-pager diff main/non-master --staged --name-only --ignore-all-space`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Output:`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - azure-mgmt-rdbms/azure/mgmt/rdbms/mysql/operations/servers_operations.py`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Found 1 files that are different in the generation branch than its parent branch.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: python ./build_package.py --dest ${pythonFolderPath}/azure-mgmt-rdbms azure-mgmt-rdbms`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Exit Code: 0`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Done uploading ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/fake-python-package.whl.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating package installation instructions...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure-mgmt-rdbms/instructions.md...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Pushing generation branch "apples/azure-mgmt-rdbms@4994" to Azure/azure-sdk-for-python"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - ${pythonFolderPath}: git push --set-upstream generation apples/azure-mgmt-rdbms@4994 --force`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Checking if generation pull request exists...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Creating SDK generation pull request in "Azure/azure-sdk-for-python" from "apples/azure-mgmt-rdbms@4994" to "non-master"...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - maintainerCanModify: true`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Created generation pull request at fake-html-url.`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Adding installation instructions comment to generation pull request...`,
              `Azure/azure-sdk-for-python - azure-mgmt-rdbms - Label changes for PR 1 in Azure/azure-sdk-for-python: +GenerationPR, +SpecPRInProgress`,
              `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
              `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
              `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
              `Azure/azure-sdk-for-python - Deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}...`,
              `Azure/azure-sdk-for-python - Finished deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}.`
            ]);
            const pullRequests: GitHubPullRequest[] = await github.getPullRequests('Azure/azure-sdk-for-python');
            assert.strictEqual(pullRequests.length, 1);
            assert.strictEqual(pullRequests[0].head.ref, 'apples/azure-mgmt-rdbms@4994');
            assert.strictEqual(pullRequests[0].base.ref, 'non-master');
          } finally {
            await deleteFolder(generationWorkingFolderPath);
          }
        } finally {
          await workingContainer.delete();
        }
      });
    });
  });

  describe('createCommandProperties()', function() {
    it('with empty object', function() {
      assert.deepEqual(createCommandProperties({}), {});
    });

    it('with object with string properties', function() {
      assert.deepEqual(createCommandProperties({ a: 'A', b: 'B' }), { a: 'A', b: 'B' });
    });

    it('with object with number properties', function() {
      assert.deepEqual(createCommandProperties({ a: 1, b: 2 }), { a: '1', b: '2' });
    });

    it('with object with boolean properties', function() {
      assert.deepEqual(createCommandProperties({ a: true, b: false }), { a: 'true', b: 'false' });
    });

    it('with object with mixed properties', function() {
      assert.deepEqual(
        createCommandProperties({
          a: true,
          b: false,
          c: 1,
          d: 'D',
          e: {},
          f: [],
          g: undefined
        }),
        {
          a: 'true',
          b: 'false',
          c: '1',
          d: 'D'
        }
      );
    });
  });

  describe('replaceStringVariables()', function() {
    it('with empty source string', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(replaceStringVariables('', {}, logger), '');
      assert.deepEqual(logger.allLogs, []);
    });

    it('with source string with no property references', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(replaceStringVariables('hello there', {}, logger), 'hello there');
      assert.deepEqual(logger.allLogs, []);
    });

    it('with source string with not found property reference', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(replaceStringVariables('hello $(NAME)', {}, logger), 'hello $(NAME)');
      assert.deepEqual(logger.allLogs, [`Found no property replacement for "NAME" in "hello $(NAME)".`]);
    });

    it('with source string with found property reference', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME)',
          {
            FIRST_NAME: 'Dan'
          },
          logger
        ),
        'hello Dan'
      );
      assert.deepEqual(logger.allLogs, []);
    });

    it('with source string with different-cased found property reference', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME)',
          {
            first_name: 'Dan'
          },
          logger
        ),
        'hello Dan'
      );
      assert.deepEqual(logger.allLogs, []);
    });

    it('with source string with removed underscore found property reference', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME)',
          {
            FIRSTNAME: 'Dan'
          },
          logger
        ),
        'hello Dan'
      );
      assert.deepEqual(logger.allLogs, []);
    });

    it('with source string with removed dash found property reference', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST-NAME)',
          {
            FIRSTNAME: 'Dan'
          },
          logger
        ),
        'hello Dan'
      );
      assert.deepEqual(logger.allLogs, []);
    });

    it('with source string with removed symbol and different-cased found property reference', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME)',
          {
            firstName: 'Dan'
          },
          logger
        ),
        'hello Dan'
      );
      assert.deepEqual(logger.allLogs, []);
    });

    it('with multiple property references', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME) $(LAST_NAME)!',
          {
            firstName: 'Abe',
            lastName: 'Lincoln'
          },
          logger
        ),
        'hello Abe Lincoln!'
      );
      assert.deepEqual(logger.allLogs, []);
    });

    it("with multiple property references where first reference isn't found", function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME) $(LAST_NAME)!',
          {
            lastName: 'Lincoln'
          },
          logger
        ),
        'hello $(FIRST_NAME) Lincoln!'
      );
      assert.deepEqual(logger.allLogs, [
        `Found no property replacement for "FIRST_NAME" in "hello $(FIRST_NAME) $(LAST_NAME)!".`
      ]);
    });

    it("with multiple property references where middle reference isn't found", function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME) $(MIDDLE_NAME) $(LAST_NAME)!',
          {
            firstName: 'Abe',
            lastName: 'Lincoln'
          },
          logger
        ),
        'hello Abe $(MIDDLE_NAME) Lincoln!'
      );
      assert.deepEqual(logger.allLogs, [
        `Found no property replacement for "MIDDLE_NAME" in "hello $(FIRST_NAME) $(MIDDLE_NAME) $(LAST_NAME)!".`
      ]);
    });

    it("with multiple property references where last reference isn't found", function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FIRST_NAME) $(LAST_NAME)!',
          {
            firstName: 'Abe'
          },
          logger
        ),
        'hello Abe $(LAST_NAME)!'
      );
      assert.deepEqual(logger.allLogs, [
        `Found no property replacement for "LAST_NAME" in "hello $(FIRST_NAME) $(LAST_NAME)!".`
      ]);
    });

    it('with recursive property references', function() {
      const logger: InMemoryLogger = getInMemoryLogger();
      assert.strictEqual(
        replaceStringVariables(
          'hello $(FULL_NAME)!',
          {
            fullName: '$(FIRST_NAME) $(LAST_NAME)',
            firstName: 'Abe',
            lastName: 'Lincoln'
          },
          logger
        ),
        'hello Abe Lincoln!'
      );
      assert.deepEqual(logger.allLogs, []);
    });
  });

  it('replaceCommandVariables()', function() {
    const logger: InMemoryLogger = getInMemoryLogger();
    const command: Command = {
      executable: 'hello/$(apples)',
      args: ['banan$(as)', '$(or)anges']
    };
    replaceCommandVariables(command, { apples: 'APPLES', as: 'AS', or: 'OR' }, logger);
    assert.deepEqual(command, {
      executable: 'hello/APPLES',
      args: ['bananAS', 'ORanges']
    });
    assert.deepEqual(logger.allLogs, []);
  });
});

function setSDKGenerationPullRequestBase(
  configuration: SwaggerToSDKConfiguration,
  value: SDKGenerationPullRequestBaseOptions | undefined
): void {
  if (!configuration.meta) {
    configuration.meta = {};
  }
  if (!configuration.meta.advanced_options) {
    configuration.meta.advanced_options = {};
  }
  configuration.meta.advanced_options.create_sdk_pull_requests = true;
  configuration.meta.advanced_options.sdk_generation_pull_request_base = value;
}
