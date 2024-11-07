import { getInMemoryLogger, InMemoryLogger } from '@azure/logger-js';
import {
  assertEx,
  autorestExecutable,
  BlobPath,
  BlobStorageAppendBlob,
  BlobStorageBlockBlob,
  BlobStorageContainer,
  BlobStoragePrefix,
  deleteFolder,
  FakeGitHub,
  FakeRunner,
  getRepository,
  getRepositoryBranch,
  GitHubLabel,
  GitHubPullRequest,
  HttpClient,
  InMemoryBlobStorage,
  joinPath,
  map,
  mvnExecutable,
  URLBuilder,
  FakeHttpClient
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { pullRequestLabelsInfo } from '../lib/githubUtils';
import {
  ensureStateImagesExist,
  getAutomationWorkingFolderPath,
  getDataBlob,
  getGenerationWorkingFolderPath,
  getLogsBlob,
  getOpenAPISDKAutomationVersion,
  getRootFolderPath,
  SDKAutomation
} from '../lib/sdkAutomation';
import {
  getPullRequestPrefix,
  getSpecificationPullRequest,
  SpecificationPullRequest,
  SpecificationPullRequestData
} from '../lib/specificationPullRequest';
import {
  getCommentHtmlBlob,
  getGenerationPrefix,
  getSDKRepositoryPrefix,
  getSwaggerToSDKConfigurationURL
} from '../lib/specificationPullRequestGeneration';
import { SpecificationReadmeMdFile } from '../lib/specificationReadmeMdFile';
import {
  createTestBlobStorageContainer,
  createTestGitHub,
  createTestHttpClient,
  createTestRunner,
  deleteWorkingContainer,
  deleteWorkingFolder,
  storageUrl,
  testSpecificationPullRequest,
  testSpecificationPullRequestMergeCommitSha,
  testSpecificationPullRequestRepository
} from './test';

describe('specificationPullRequest.ts', function() {
  describe('getSpecificationPullRequest()', function() {
    it('with first generation', async function() {
      const workingFolderPath: string = joinPath(process.cwd(), 'fakeAutomationWorkingFolderPath');
      const github: FakeGitHub = await createTestGitHub();
      const logger: InMemoryLogger = getInMemoryLogger();
      const sdkAutomation = new SDKAutomation(workingFolderPath, {
        github,
        logger
      });
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const workingPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');

      const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
        sdkAutomation,
        workingPrefix,
        testSpecificationPullRequest,
        true
      );
      try {
        assertEx.defined(specificationPullRequest, 'specificationPullRequest');
        assert.strictEqual(
          await blobStorageContainer.blobExists(
            `def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/1/logs.txt`
          ),
          true
        );
        assert.deepEqual(specificationPullRequest.data, {
          specPRRepository: {
            owner: 'Azure',
            name: 'azure-rest-api-specs'
          },
          specPRCommit: testSpecificationPullRequestMergeCommitSha,
          baseBranch: {
            owner: 'Azure',
            name: 'master'
          },
          commentId: 1,
          dataBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/data.json`,
          diffUrl: testSpecificationPullRequest.diff_url,
          generation: {
            commentHtmlBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/1/comment.html`,
            dataBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/1/data.json`,
            logsBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/1/logs.txt`,
            number: 1,
            sdkRepositories: []
          },
          headBranch: {
            owner: 'pixia',
            name: 'master'
          },
          headCommit: testSpecificationPullRequest.head.sha,
          htmlUrl: testSpecificationPullRequest.html_url,
          mergeCommit: testSpecificationPullRequestMergeCommitSha,
          number: testSpecificationPullRequest.number,
          baseRepository: {
            owner: 'Azure',
            name: 'azure-rest-api-specs'
          },
          headRepository: {
            owner: 'pixia',
            name: 'azure-rest-api-specs'
          },
          title: testSpecificationPullRequest.title
        });
        assert.deepEqual(logger.allLogs, [
          `Using provided GitHub.`,
          `No provided Git. Using ExecutableGit instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `No HttpClient provided. Using NodeHttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
          `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
          `Using FakeGitHub client.`,
          `Getting generation state from https://fake.storage.com/abc/def/Azure/azure-rest-api-specs/4994/data.json...`,
          `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`
        ]);
      } finally {
        await deleteFolder(workingFolderPath);
      }
    });

    it('with second generation', async function() {
      const workingFolderPath: string = joinPath(process.cwd(), 'apples');
      const github: FakeGitHub = await createTestGitHub();
      const logger: InMemoryLogger = getInMemoryLogger();
      const sdkAutomation = new SDKAutomation(workingFolderPath, {
        github,
        logger
      });
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const workingPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      await getSpecificationPullRequest(sdkAutomation, workingPrefix, testSpecificationPullRequest, true);

      const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
        sdkAutomation,
        workingPrefix,
        testSpecificationPullRequest,
        true
      );
      try {
        assertEx.defined(specificationPullRequest, 'specificationPullRequest');
        assert.strictEqual(
          await blobStorageContainer.blobExists(
            `def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/2/logs.txt`
          ),
          true
        );
        assert.deepEqual(specificationPullRequest.data, {
          specPRRepository: {
            owner: 'Azure',
            name: 'azure-rest-api-specs'
          },
          specPRCommit: testSpecificationPullRequestMergeCommitSha,
          baseBranch: {
            owner: 'Azure',
            name: 'master'
          },
          commentId: 1,
          dataBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/data.json`,
          diffUrl: testSpecificationPullRequest.diff_url,
          generation: {
            commentHtmlBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/2/comment.html`,
            dataBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/2/data.json`,
            logsBlobUrl: `https://fake.storage.com/abc/def/${testSpecificationPullRequestRepository}/${testSpecificationPullRequest.number}/2/logs.txt`,
            number: 2,
            sdkRepositories: []
          },
          headBranch: {
            owner: 'pixia',
            name: 'master'
          },
          headCommit: testSpecificationPullRequest.head.sha,
          htmlUrl: testSpecificationPullRequest.html_url,
          mergeCommit: testSpecificationPullRequestMergeCommitSha,
          number: testSpecificationPullRequest.number,
          baseRepository: {
            name: 'azure-rest-api-specs',
            owner: 'Azure'
          },
          headRepository: {
            name: 'azure-rest-api-specs',
            owner: 'pixia'
          },
          title: testSpecificationPullRequest.title
        });
        assert.deepEqual(logger.allLogs, [
          `Using provided GitHub.`,
          `No provided Git. Using ExecutableGit instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `No HttpClient provided. Using NodeHttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
          `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
          `Using FakeGitHub client.`,
          `Getting generation state from https://fake.storage.com/abc/def/Azure/azure-rest-api-specs/4994/data.json...`,
          `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
          `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
          `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
          `Using FakeGitHub client.`,
          `Getting generation state from https://fake.storage.com/abc/def/Azure/azure-rest-api-specs/4994/data.json...`,
          `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`
        ]);
      } finally {
        await deleteFolder(workingFolderPath);
      }
    });
  });

  it('getChangedFilesRelativePaths()', async function() {
    this.timeout(5000);

    const workingFolderPath: string = joinPath(process.cwd(), 'grapes');
    const github: FakeGitHub = await createTestGitHub();
    const logger: InMemoryLogger = getInMemoryLogger();
    const sdkAutomation = new SDKAutomation(workingFolderPath, {
      github,
      logger
    });
    const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
    const workingPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
    const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
      sdkAutomation,
      workingPrefix,
      testSpecificationPullRequest,
      true
    );
    try {
      const changedFilesRelativePaths: string[] = await specificationPullRequest.getChangedFilesRelativePaths();

      assert.deepEqual(changedFilesRelativePaths, [
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`
      ]);
      assert.deepEqual(logger.allLogs, [
        `Using provided GitHub.`,
        `No provided Git. Using ExecutableGit instance.`,
        `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
        `No HttpClient provided. Using NodeHttpClient.`,
        `No BlobProxy provided. Using a FakeBlobProxy.`,
        `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
        `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
        `Using FakeGitHub client.`,
        `Getting generation state from https://fake.storage.com/abc/def/Azure/azure-rest-api-specs/4994/data.json...`,
        `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
        `Getting diff_url (https://github.com/Azure/azure-rest-api-specs/pull/4994.diff) contents...`,
        `diff_url response status code is 200.`,
        `diff_url response body contains 5 changed files:`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/preview/2017-12-01-preview/examples/ServerCreate.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreate.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateGeoRestoreMode.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreatePointInTimeRestore.json`,
        `specification/mysql/resource-manager/Microsoft.DBforMySQL/stable/2017-12-01/examples/ServerCreateReplicaMode.json`
      ]);
    } finally {
      await deleteFolder(workingFolderPath);
    }
  });

  it('getReadmeMdRelativeFilePathsToGenerate()', async function() {
    const workingFolderPath: string = joinPath(process.cwd(), 'kiwi');
    const github: FakeGitHub = await createTestGitHub();
    const httpClient: HttpClient = createTestHttpClient();
    const logger: InMemoryLogger = getInMemoryLogger();
    const sdkAutomation = new SDKAutomation(workingFolderPath, {
      github,
      httpClient,
      logger
    });
    const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
    const workingPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
    const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
      sdkAutomation,
      workingPrefix,
      testSpecificationPullRequest,
      true
    );
    try {
      const readmeMdRelativeFilePathsToGenerate: SpecificationReadmeMdFile[] = await specificationPullRequest.getReadmeMdFilesToGenerate();

      assert.deepEqual(
        map(
          readmeMdRelativeFilePathsToGenerate,
          (readmeFile: SpecificationReadmeMdFile) => readmeFile.relativeFilePath
        ),
        ['specification/mysql/resource-manager/readme.md']
      );
      assert.deepEqual(logger.allLogs, [
        `Using provided GitHub.`,
        `No provided Git. Using ExecutableGit instance.`,
        `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
        `Using provided HttpClient.`,
        `No BlobProxy provided. Using a FakeBlobProxy.`,
        `Received pull request change webhook request from GitHub for "https://github.com/Azure/azure-rest-api-specs/pull/4994".`,
        `Using openapi-sdk-automation version ${await getOpenAPISDKAutomationVersion()}.`,
        `Using FakeGitHub client.`,
        `Getting generation state from https://fake.storage.com/abc/def/Azure/azure-rest-api-specs/4994/data.json...`,
        `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`,
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
        `specification/mysql/resource-manager/readme.md`
      ]);
    } finally {
      await deleteFolder(workingFolderPath);
    }
  });

  describe('generateModifiedServices()', function() {
    it('fake', function() {
      return generateModifiedServicesTest(false);
    });

    (URLBuilder.parse(storageUrl).getQueryParameterValue('sig') ? it : it.skip)('real', function() {
      this.timeout(1000 * 60 * 9);
      return generateModifiedServicesTest(true);
    });

    async function generateModifiedServicesTest(real: boolean): Promise<void> {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer(real);
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient: HttpClient = createTestHttpClient(real);
          const autorest: string = autorestExecutable({
            autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest')
          });
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest,
            generationWorkingFolderPath: generationWorkingFolderPath,
            github,
            real
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            true
          );

          await specificationPullRequest.generateModifiedServices();

          const pythonFolderPath: string = joinPath(generationWorkingFolderPath, '1');
          const javaFolderPath: string = joinPath(generationWorkingFolderPath, '2');
          const goFolderPath: string = joinPath(generationWorkingFolderPath, 'src/github.com/Azure/azure-sdk-for-go');
          const jsFolderPath: string = joinPath(generationWorkingFolderPath, 'azure-sdk-for-js');
          const nodeFolderPath: string = joinPath(generationWorkingFolderPath, 'azure-sdk-for-node');
          const mvn: string = mvnExecutable();
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
            `Azure/azure-sdk-for-python - Creating SDK repository artifact zip file (${pythonFolderPath}/azure.azure-sdk-for-python.artifacts.zip) from:`,
            `Azure/azure-sdk-for-python -   ${pythonFolderPath}/azure-mgmt-rdbms/fake-python-package.whl`,
            `Azure/azure-sdk-for-python - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-python/azure.azure-sdk-for-python.artifacts.zip...`,
            `Azure/azure-sdk-for-python - Deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}...`,
            `Azure/azure-sdk-for-python - Finished deleting clone of Azure/azure-sdk-for-python at folder ${pythonFolderPath}.`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-java ${javaFolderPath}`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-java`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-java`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-java`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git fetch --all`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git checkout --track main/master -b main-master`,
            `Azure/azure-sdk-for-java - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
            `Azure/azure-sdk-for-java - No version specified for "autorest". Defaulting to "latest".`,
            `Azure/azure-sdk-for-java - Resolving "autorest" version "latest" to "2.0.4283".`,
            `Azure/azure-sdk-for-java - Getting dist-tags for "@microsoft.azure/autorest.java" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.java/dist-tags"...`,
            `Azure/azure-sdk-for-java - "@microsoft.azure/autorest.java@2.1.85" does not have a dist-tag to resolve to.`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: ${autorest} --java --verbose --multiapi --use=@microsoft.azure/autorest.java@2.1.85 --azure-libraries-for-java-folder=${javaFolderPath} --version=2.0.4283 https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
            `Azure/azure-sdk-for-java - Getting diff after AutoRest ran...`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git add *`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
            `Azure/azure-sdk-for-java - ${javaFolderPath}: git reset *`,
            `Azure/azure-sdk-for-java - The following files were changed:`,
            `Azure/azure-sdk-for-java -   ${javaFolderPath}/mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/CheckNameAvailabilitys.java`,
            `Azure/azure-sdk-for-java -   ${javaFolderPath}/mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/Configuration.java`,
            `Azure/azure-sdk-for-java - Found 1 package folder that changed:`,
            `Azure/azure-sdk-for-java -   ${javaFolderPath}/mysql/resource-manager/v2017_12_01`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - Package name for "${javaFolderPath}/mysql/resource-manager/v2017_12_01" is "mysql/resource-manager/v2017_12_01".`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - Creating SDK generation branch "sdkAutomation/mysql/resource-manager/v2017_12_01@4994" based off of "master" in "Azure/azure-sdk-for-java"...`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}: git checkout main-master`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}: git checkout -b sdkAutomation/mysql/resource-manager/v2017_12_01@4994`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}/mysql/resource-manager/v2017_12_01: git add *`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - 2 files staged for commit:`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/CheckNameAvailabilitys.java`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/Configuration.java`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}: git checkout sdkAutomation/mysql/resource-manager/v2017_12_01@4994`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - No after_scripts to run.`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - Exit Code: 0`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - Output:`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/CheckNameAvailabilitys.java`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - mysql/resource-manager/v2017_12_01/src/main/java/com/microsoft/azure/management/mysql/v2017_12_01/Configuration.java`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - Found 2 files that are different in the generation branch than its parent branch.`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - ${javaFolderPath}/mysql/resource-manager/v2017_12_01: ${mvn} source:jar javadoc:jar package -f ${javaFolderPath}/mysql/resource-manager/v2017_12_01 -DskipTests --batch-mode`,
            `Azure/azure-sdk-for-java - mysql/resource-manager/v2017_12_01 - Exit Code: 1`,
            `Azure/azure-sdk-for-java - No SDK repository artifact files detected.`,
            `Azure/azure-sdk-for-java - Deleting clone of Azure/azure-sdk-for-java at folder ${javaFolderPath}...`,
            `Azure/azure-sdk-for-java - Finished deleting clone of Azure/azure-sdk-for-java at folder ${javaFolderPath}.`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-go ${goFolderPath}`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-go`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-go`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-go`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git fetch --all`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git checkout --track main/master -b main-master`,
            `Azure/azure-sdk-for-go - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
            `Azure/azure-sdk-for-go - No version specified for "autorest". Defaulting to "latest".`,
            `Azure/azure-sdk-for-go - Resolving "autorest" version "latest" to "2.0.4283".`,
            `Azure/azure-sdk-for-go - Getting dist-tags for "@microsoft.azure/autorest.go" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.go/dist-tags"...`,
            `Azure/azure-sdk-for-go - "@microsoft.azure/autorest.go@~2.1.131" contains version range symbols (such as "^", "~", and "*"). Version range symbols are not resolved by SDK Automation.`,
            `Azure/azure-sdk-for-go - "@microsoft.azure/autorest.go@~2.1.131" does not have a dist-tag to resolve to.`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: ${autorest} --use=@microsoft.azure/autorest.go@~2.1.131 --go --verbose --multiapi --use-onever --preview-chk --go-sdk-folder=${goFolderPath} --version=2.0.4283 https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
            `Azure/azure-sdk-for-go - Getting diff after AutoRest ran...`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git add *`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
            `Azure/azure-sdk-for-go - ${goFolderPath}: git reset *`,
            `Azure/azure-sdk-for-go - The following files were changed:`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql/locationbasedperformancetier.go`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/profiles/latest/mysql/mgmt/mysql/models.go`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/profiles/latest/mysql/mgmt/mysql/mysqlapi/models.go`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/profiles/latest/servicebus/mgmt/servicebus/models.go`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/profiles/preview/mysql/mgmt/mysql/models.go`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/profiles/preview/mysql/mgmt/mysql/mysqlapi/models.go`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/profiles/preview/servicebus/mgmt/servicebus/models.go`,
            `Azure/azure-sdk-for-go - Found 1 package folder that changed:`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Package name for "${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql" is "mysql/mgmt/2017-12-01".`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Creating SDK generation branch "sdkAutomation/mysql/mgmt/2017-12-01@4994" based off of "master" in "Azure/azure-sdk-for-go"...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git checkout main-master`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git checkout -b sdkAutomation/mysql/mgmt/2017-12-01@4994`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql: git add *`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - 7 files staged for commit:`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - services/mysql/mgmt/2017-12-01/mysql/locationbasedperformancetier.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/latest/mysql/mgmt/mysql/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/latest/mysql/mgmt/mysql/mysqlapi/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/latest/servicebus/mgmt/servicebus/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/preview/mysql/mgmt/mysql/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/preview/mysql/mgmt/mysql/mysqlapi/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/preview/servicebus/mgmt/servicebus/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git checkout sdkAutomation/mysql/mgmt/2017-12-01@4994`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Running after_scripts...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: dep ensure`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Exit Code: 0`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: go generate ./profiles/generate.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Exit Code: 0`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: gofmt -w ./profiles/`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Exit Code: 0`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: gofmt -w ./services/`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Exit Code: 0`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git add *`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git commit -m "Modifications after running after_scripts"`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - ${goFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Exit Code: 0`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Output:`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - services/mysql/mgmt/2017-12-01/mysql/locationbasedperformancetier.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/latest/mysql/mgmt/mysql/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/latest/mysql/mgmt/mysql/mysqlapi/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/latest/servicebus/mgmt/servicebus/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/preview/mysql/mgmt/mysql/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/preview/mysql/mgmt/mysql/mysqlapi/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - profiles/preview/servicebus/mgmt/servicebus/models.go`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Found 7 files that are different in the generation branch than its parent branch.`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Creating folder ${goFolderPath}/zip...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Not copying anything from the profiles folder since no profile files were changed.`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Copying ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql to ${goFolderPath}/zip/services/mysql/mgmt/2017-12-01/mysql...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Compressing ${goFolderPath}/zip to ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql/mysql.mgmt.2017-12-01.zip...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Deleting ${goFolderPath}/zip...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Uploading ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql/mysql.mgmt.2017-12-01.zip to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-go/mysql/mgmt/2017-12-01/mysql.mgmt.2017-12-01.zip...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Done uploading ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql/mysql.mgmt.2017-12-01.zip to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-go/mysql/mgmt/2017-12-01/mysql.mgmt.2017-12-01.zip.`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Creating package installation instructions...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-go/mysql/mgmt/2017-12-01/instructions.md...`,
            `Azure/azure-sdk-for-go - mysql/mgmt/2017-12-01 - Uploading package installation instructions to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-go/mysql/mgmt/2017-12-01/instructions.md...`,
            `Azure/azure-sdk-for-go - Creating SDK repository artifact zip file (${goFolderPath}/azure.azure-sdk-for-go.artifacts.zip) from:`,
            `Azure/azure-sdk-for-go -   ${goFolderPath}/services/mysql/mgmt/2017-12-01/mysql/mysql.mgmt.2017-12-01.zip`,
            `Azure/azure-sdk-for-go - Uploading SDK repository artifact zip file to https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/Azure/azure-sdk-for-go/azure.azure-sdk-for-go.artifacts.zip...`,
            `Azure/azure-sdk-for-go - Deleting clone of Azure/azure-sdk-for-go at folder ${goFolderPath}...`,
            `Azure/azure-sdk-for-go - Finished deleting clone of Azure/azure-sdk-for-go at folder ${goFolderPath}.`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-js ${jsFolderPath}`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-js`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-js`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-js`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git fetch --all`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git checkout --track main/master -b main-master`,
            `Azure/azure-sdk-for-js - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
            `Azure/azure-sdk-for-js - No version specified for "autorest". Defaulting to "latest".`,
            `Azure/azure-sdk-for-js - Resolving "autorest" version "latest" to "2.0.4283".`,
            `Azure/azure-sdk-for-js - Getting dist-tags for "@microsoft.azure/autorest.typescript" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.typescript/dist-tags"...`,
            `Azure/azure-sdk-for-js - "@microsoft.azure/autorest.typescript@4.0.0" does not have a dist-tag to resolve to.`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: ${autorest} --typescript --license-header=MICROSOFT_MIT_NO_VERSION --use=@microsoft.azure/autorest.typescript@4.0.0 --typescript-sdks-folder=${jsFolderPath} --version=2.0.4283 https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
            `Azure/azure-sdk-for-js - Getting diff after AutoRest ran...`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git add *`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
            `Azure/azure-sdk-for-js - ${jsFolderPath}: git reset *`,
            `Azure/azure-sdk-for-js - No changes were detected after AutoRest ran.`,
            `Azure/azure-sdk-for-js - Deleting clone of Azure/azure-sdk-for-js at folder ${jsFolderPath}...`,
            `Azure/azure-sdk-for-js - Finished deleting clone of Azure/azure-sdk-for-js at folder ${jsFolderPath}.`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git clone --quiet https://github.com/Azure/azure-sdk-for-node ${nodeFolderPath}`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git remote add generation https://github.com/Azure/azure-sdk-for-node`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git remote add integration https://github.com/Azure/azure-sdk-for-node`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git remote add main https://github.com/Azure/azure-sdk-for-node`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git fetch --all`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git checkout --track main/master -b main-master`,
            `Azure/azure-sdk-for-node - Getting dist-tags for "autorest" from "https://registry.npmjs.org/-/package/autorest/dist-tags"...`,
            `Azure/azure-sdk-for-node - No version specified for "autorest". Defaulting to "latest".`,
            `Azure/azure-sdk-for-node - Resolving "autorest" version "latest" to "2.0.4283".`,
            `Azure/azure-sdk-for-node - Getting dist-tags for "@microsoft.azure/autorest.nodejs" from "https://registry.npmjs.org/-/package/@microsoft.azure/autorest.nodejs/dist-tags"...`,
            `Azure/azure-sdk-for-node - "@microsoft.azure/autorest.nodejs@2.2.131" does not have a dist-tag to resolve to.`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: ${autorest} --nodejs --license-header=MICROSOFT_MIT_NO_VERSION --use=@microsoft.azure/autorest.nodejs@2.2.131 --node-sdks-folder=${nodeFolderPath} --version=2.0.4283 https://raw.githubusercontent.com/Azure/azure-rest-api-specs/5d204450e3ea6709a034208af441ebaaa87bd805/specification/mysql/resource-manager/readme.md`,
            `Azure/azure-sdk-for-node - Getting diff after AutoRest ran...`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git add *`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git --no-pager diff main/master --staged --ignore-all-space`,
            `Azure/azure-sdk-for-node - ${nodeFolderPath}: git reset *`,
            `Azure/azure-sdk-for-node - The following files were changed:`,
            `Azure/azure-sdk-for-node -   ${nodeFolderPath}/lib/services/mysqlManagement/lib/models/firewallRuleListResult.js`,
            `Azure/azure-sdk-for-node - Found 1 package folder that changed:`,
            `Azure/azure-sdk-for-node -   ${nodeFolderPath}/lib/services/mysqlManagement`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - Package name for "${nodeFolderPath}/lib/services/mysqlManagement" is "azure-arm-mysql".`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - Creating SDK generation branch "sdkAutomation/azure-arm-mysql@4994" based off of "master" in "Azure/azure-sdk-for-node"...`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - ${nodeFolderPath}: git checkout main-master`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - ${nodeFolderPath}: git checkout -b sdkAutomation/azure-arm-mysql@4994`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - ${nodeFolderPath}/lib/services/mysqlManagement: git add *`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - ${nodeFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - 0 files staged for commit:`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - ${nodeFolderPath}: git commit -m "Generated from d82d1491879729cdf44da9a664e815112acde158" -m "hello world"`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - ${nodeFolderPath}: git checkout sdkAutomation/azure-arm-mysql@4994`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - No after_scripts to run.`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - ${nodeFolderPath}: git --no-pager diff main/master --staged --name-only --ignore-all-space`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - Exit Code: 0`,
            `Azure/azure-sdk-for-node - azure-arm-mysql - No differences were detected between the generation branch and its parent branch after the after_scripts were run.`,
            `Azure/azure-sdk-for-node - No SDK repository artifact files detected.`,
            `Azure/azure-sdk-for-node - Deleting clone of Azure/azure-sdk-for-node at folder ${nodeFolderPath}...`,
            `Azure/azure-sdk-for-node - Finished deleting clone of Azure/azure-sdk-for-node at folder ${nodeFolderPath}.`
          ]);
          assert.strictEqual(await specificationPullRequest.dataBlob.exists(), true);
          assert.strictEqual(await specificationPullRequest.generation.dataBlob.exists(), true);
          assert.strictEqual(await specificationPullRequest.generation.commentHtmlBlob.exists(), true);
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    }
  });

  describe('closeSDKGenerationPullRequests()', function() {
    it('with no open generation pull requests', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient: HttpClient = createTestHttpClient();
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.closeSDKGenerationPullRequests();

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
            `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`
          ]);
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });

    it('with open non-generation pull request', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const fakePullRequest: GitHubPullRequest = {
            base: {
              label: 'Azure:fake-base',
              ref: 'fake-base',
              sha: 'fake-base-sha'
            },
            diff_url: 'fake-diff-url',
            head: {
              label: 'Azure:fake-head',
              ref: 'fake-head',
              sha: 'fake-head-sha'
            },
            html_url: 'fake-html-url',
            id: 1,
            labels: [],
            merge_commit_sha: 'fake-merge-commit-sha',
            number: 2,
            state: 'open',
            title: 'fake-title',
            url: 'fake-url'
          };
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.head.sha, 'hello');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.head.ref, fakePullRequest.head.sha);
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.base.sha, 'there');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.base.ref, fakePullRequest.base.sha);
          await github.createFakePullRequest('Azure/azure-sdk-for-go', fakePullRequest);
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient: HttpClient = createTestHttpClient();
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.closeSDKGenerationPullRequests();

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
            `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`
          ]);
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });

    it('with open generation pull request', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
          automationWorkingPrefix,
          testSpecificationPullRequestRepository,
          testSpecificationPullRequest.number
        );
        const dataBlob: BlobStorageBlockBlob = getDataBlob(pullRequestPrefix);
        const iterationNumber = 5;
        const generationPrefix: BlobStoragePrefix = getGenerationPrefix(pullRequestPrefix, iterationNumber);
        const generationDataBlob: BlobStorageBlockBlob = getDataBlob(generationPrefix);
        const generationLogsBlob: BlobStorageAppendBlob = getLogsBlob(generationPrefix);
        await generationLogsBlob.create();
        const generationCommentBlob: BlobStorageBlockBlob = getCommentHtmlBlob(generationPrefix);
        const specificationPullRequestData: SpecificationPullRequestData = {
          baseBranch: getRepositoryBranch(testSpecificationPullRequest.base.label),
          baseRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          dataBlobUrl: dataBlob.getURL(),
          diffUrl: 'fake-diff-url',
          headBranch: getRepositoryBranch(testSpecificationPullRequest.head.label),
          headCommit: testSpecificationPullRequest.head.sha,
          headRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.head.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          htmlUrl: 'fake-html-url',
          mergeCommit: testSpecificationPullRequestMergeCommitSha,
          number: testSpecificationPullRequest.number,
          specPRCommit: testSpecificationPullRequestMergeCommitSha,
          specPRRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          title: 'fake-title',
          generation: {
            dataBlobUrl: generationDataBlob.getURL(),
            logsBlobUrl: generationLogsBlob.getURL(),
            commentHtmlBlobUrl: generationCommentBlob.getURL(),
            number: iterationNumber,
            sdkRepositories: [
              {
                generationRepository: 'Azure/azure-sdk-for-go',
                generationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                integrationBranchPrefix: 'sdkAutomationTest/',
                integrationRepository: 'Azure/azure-sdk-for-go',
                integrationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                languageName: 'Go',
                mainBranch: 'latest',
                mainRepository: 'Azure/azure-sdk-for-go',
                mainRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                readmeMdFileUrlsToGenerate: [],
                status: 'succeeded',
                swaggerToSDKConfigFileUrl: getSwaggerToSDKConfigurationURL('Azure/azure-sdk-for-go', 'latest')
              }
            ]
          }
        };
        await dataBlob.setContentsFromString(JSON.stringify(specificationPullRequestData));

        const goSDKRepositoryPrefix: BlobStoragePrefix = getSDKRepositoryPrefix(
          generationPrefix,
          'Azure/azure-sdk-for-go'
        );
        const goLogsBlob: BlobStorageAppendBlob = getLogsBlob(goSDKRepositoryPrefix);
        await goLogsBlob.create();

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const specPRInProgressLabel: GitHubLabel = await github.createLabel(
            'Azure/azure-sdk-for-go',
            'SpecPRInProgress',
            pullRequestLabelsInfo['SpecPRInProgress'].color
          );
          const fakePullRequest: GitHubPullRequest = {
            base: {
              label: 'Azure:fake-base',
              ref: 'fake-base',
              sha: 'fake-base-sha'
            },
            diff_url: 'fake-diff-url',
            head: {
              label: `Azure:fake-head@${testSpecificationPullRequest.number}`,
              ref: `fake-head@${testSpecificationPullRequest.number}`,
              sha: 'fake-head-sha'
            },
            html_url: `https://github.com/Azure/azure-sdk-for-go/pull/1`,
            id: 1,
            labels: [specPRInProgressLabel],
            merge_commit_sha: 'fake-merge-commit-sha',
            number: 2,
            state: 'open',
            title: 'fake-title',
            url: `https://api.github.com/repos/Azure/azure-sdk-for-go/pulls/1`
          };
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.head.sha, 'hello');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.head.ref, fakePullRequest.head.sha);
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.base.sha, 'there');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.base.ref, fakePullRequest.base.sha);
          await github.createFakePullRequest('Azure/azure-sdk-for-go', fakePullRequest);
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient: HttpClient = createTestHttpClient();
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.closeSDKGenerationPullRequests();

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
            `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/latest/swagger_to_sdk_config.json"...`,
            `Azure/azure-sdk-for-go - Getting generation pull requests for specification pull request fake-html-url...`,
            `Azure/azure-sdk-for-go - Closing pull request https://github.com/Azure/azure-sdk-for-go/pull/1...`,
            `Azure/azure-sdk-for-go - Deleting branch "fake-head@4994" in Azure/azure-sdk-for-go...`,
            `Azure/azure-sdk-for-go - Label changes for PR 2 in Azure/azure-sdk-for-go: +GenerationPR, +SpecPRClosed, -SpecPRInProgress`
          ]);

          const fakePullRequestAfterClosing: GitHubPullRequest = await github.getPullRequest(
            'Azure/azure-sdk-for-go',
            fakePullRequest.number
          );
          assert.strictEqual(fakePullRequestAfterClosing.state, 'closed');
          assert.deepEqual(
            map(fakePullRequestAfterClosing.labels, (label: GitHubLabel) => label.name),
            ['GenerationPR', 'SpecPRClosed']
          );
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });

    it('with open generation pull request but disabled pull requestion automation', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
          automationWorkingPrefix,
          testSpecificationPullRequestRepository,
          testSpecificationPullRequest.number
        );
        const dataBlob: BlobStorageBlockBlob = getDataBlob(pullRequestPrefix);
        const iterationNumber = 5;
        const generationPrefix: BlobStoragePrefix = getGenerationPrefix(pullRequestPrefix, iterationNumber);
        const generationDataBlob: BlobStorageBlockBlob = getDataBlob(generationPrefix);
        const generationLogsBlob: BlobStorageAppendBlob = getLogsBlob(generationPrefix);
        await generationLogsBlob.create();
        const generationCommentBlob: BlobStorageBlockBlob = getCommentHtmlBlob(generationPrefix);
        const specificationPullRequestData: SpecificationPullRequestData = {
          baseBranch: getRepositoryBranch(testSpecificationPullRequest.base.label),
          baseRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          dataBlobUrl: dataBlob.getURL(),
          diffUrl: 'fake-diff-url',
          headBranch: getRepositoryBranch(testSpecificationPullRequest.head.label),
          headCommit: testSpecificationPullRequest.head.sha,
          headRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.head.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          htmlUrl: 'fake-html-url',
          mergeCommit: testSpecificationPullRequestMergeCommitSha,
          number: testSpecificationPullRequest.number,
          specPRCommit: testSpecificationPullRequestMergeCommitSha,
          specPRRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          title: 'fake-title',
          generation: {
            dataBlobUrl: generationDataBlob.getURL(),
            logsBlobUrl: generationLogsBlob.getURL(),
            commentHtmlBlobUrl: generationCommentBlob.getURL(),
            number: iterationNumber,
            sdkRepositories: [
              {
                generationRepository: 'Azure/azure-sdk-for-go',
                generationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                integrationBranchPrefix: 'sdkAutomationTest/',
                integrationRepository: 'Azure/azure-sdk-for-go',
                integrationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                languageName: 'Go',
                mainBranch: 'latest',
                mainRepository: 'Azure/azure-sdk-for-go',
                mainRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                readmeMdFileUrlsToGenerate: [],
                status: 'succeeded',
                swaggerToSDKConfigFileUrl: getSwaggerToSDKConfigurationURL('Azure/azure-sdk-for-go', 'latest')
              }
            ]
          }
        };
        await dataBlob.setContentsFromString(JSON.stringify(specificationPullRequestData));

        const goSDKRepositoryPrefix: BlobStoragePrefix = getSDKRepositoryPrefix(
          generationPrefix,
          'Azure/azure-sdk-for-go'
        );
        const goLogsBlob: BlobStorageAppendBlob = getLogsBlob(goSDKRepositoryPrefix);
        await goLogsBlob.create();

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const specPRInProgressLabel: GitHubLabel = await github.createLabel(
            'Azure/azure-sdk-for-go',
            'SpecPRInProgress',
            pullRequestLabelsInfo['SpecPRInProgress'].color
          );
          const fakePullRequest: GitHubPullRequest = {
            base: {
              label: 'Azure:fake-base',
              ref: 'fake-base',
              sha: 'fake-base-sha'
            },
            diff_url: 'fake-diff-url',
            head: {
              label: `Azure:fake-head@${testSpecificationPullRequest.number}`,
              ref: `fake-head@${testSpecificationPullRequest.number}`,
              sha: 'fake-head-sha'
            },
            html_url: `https://github.com/Azure/azure-sdk-for-go/pull/1`,
            id: 1,
            labels: [specPRInProgressLabel],
            merge_commit_sha: 'fake-merge-commit-sha',
            number: 2,
            state: 'open',
            title: 'fake-title',
            url: `https://api.github.com/repos/Azure/azure-sdk-for-go/pulls/1`
          };
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.head.sha, 'hello');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.head.ref, fakePullRequest.head.sha);
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.base.sha, 'there');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.base.ref, fakePullRequest.base.sha);
          await github.createFakePullRequest('Azure/azure-sdk-for-go', fakePullRequest);
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient = createTestHttpClient() as FakeHttpClient;
          httpClient.add(
            'GET',
            'https://raw.githubusercontent.com/Azure/azure-sdk-for-go/latest/swagger_to_sdk_config.json',
            200,
            undefined,
            JSON.stringify({
              meta: {
                advanced_options: {
                  disable_generation_pr_automation: true
                }
              }
            })
          );
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.closeSDKGenerationPullRequests();

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
            `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/latest/swagger_to_sdk_config.json"...`,
            `Azure/azure-sdk-for-go - Getting generation pull requests for specification pull request fake-html-url...`,
            `Azure/azure-sdk-for-go - Skip to close generation pull request at https://github.com/Azure/azure-sdk-for-go/pull/1 as it's disabled in config.`,
            `Azure/azure-sdk-for-go - Label changes for PR 2 in Azure/azure-sdk-for-go: +GenerationPR, +SpecPRClosed, -SpecPRInProgress`
          ]);

          const fakePullRequestAfterClosing: GitHubPullRequest = await github.getPullRequest(
            'Azure/azure-sdk-for-go',
            fakePullRequest.number
          );
          assert.strictEqual(fakePullRequestAfterClosing.state, 'open');
          assert.deepEqual(
            map(fakePullRequestAfterClosing.labels, (label: GitHubLabel) => label.name),
            ['GenerationPR', 'SpecPRClosed']
          );
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });
  });

  describe('mergeSDKGenerationPullRequests()', function() {
    it('with no open generation pull requests', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient: HttpClient = createTestHttpClient();
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.mergeSDKGenerationPullRequests();

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
            `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`
          ]);
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });

    it('with open non-generation pull request', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const fakePullRequest: GitHubPullRequest = {
            base: {
              label: 'Azure:fake-base',
              ref: 'fake-base',
              sha: 'fake-base-sha'
            },
            diff_url: 'fake-diff-url',
            head: {
              label: 'Azure:fake-head',
              ref: 'fake-head',
              sha: 'fake-head-sha'
            },
            html_url: 'fake-html-url',
            id: 1,
            labels: [],
            merge_commit_sha: 'fake-merge-commit-sha',
            number: 2,
            state: 'open',
            title: 'fake-title',
            url: 'fake-url'
          };
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.head.sha, 'hello');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.head.ref, fakePullRequest.head.sha);
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.base.sha, 'there');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.base.ref, fakePullRequest.base.sha);
          await github.createFakePullRequest('Azure/azure-sdk-for-go', fakePullRequest);
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient: HttpClient = createTestHttpClient();
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.mergeSDKGenerationPullRequests();

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
            `Pull request event contained mergeCommit: 5d204450e3ea6709a034208af441ebaaa87bd805`
          ]);
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });

    it('with open generation pull request', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
          automationWorkingPrefix,
          testSpecificationPullRequestRepository,
          testSpecificationPullRequest.number
        );
        const dataBlob: BlobStorageBlockBlob = getDataBlob(pullRequestPrefix);
        const iterationNumber = 5;
        const generationPrefix: BlobStoragePrefix = getGenerationPrefix(pullRequestPrefix, iterationNumber);
        const generationDataBlob: BlobStorageBlockBlob = getDataBlob(generationPrefix);
        const generationLogsBlob: BlobStorageAppendBlob = getLogsBlob(generationPrefix);
        await generationLogsBlob.create();
        const generationCommentBlob: BlobStorageBlockBlob = getCommentHtmlBlob(generationPrefix);
        const specificationPullRequestData: SpecificationPullRequestData = {
          baseBranch: getRepositoryBranch(testSpecificationPullRequest.base.label),
          baseRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          dataBlobUrl: dataBlob.getURL(),
          diffUrl: 'fake-diff-url',
          headBranch: getRepositoryBranch(testSpecificationPullRequest.head.label),
          headCommit: testSpecificationPullRequest.head.sha,
          headRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.head.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          htmlUrl: 'fake-html-url',
          mergeCommit: testSpecificationPullRequestMergeCommitSha,
          number: testSpecificationPullRequest.number,
          specPRCommit: testSpecificationPullRequestMergeCommitSha,
          specPRRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          title: 'fake-title',
          generation: {
            dataBlobUrl: generationDataBlob.getURL(),
            logsBlobUrl: generationLogsBlob.getURL(),
            commentHtmlBlobUrl: generationCommentBlob.getURL(),
            number: iterationNumber,
            sdkRepositories: [
              {
                generationRepository: 'Azure/azure-sdk-for-go',
                generationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                integrationBranchPrefix: 'sdkAutomationTest/',
                integrationRepository: 'Azure/azure-sdk-for-go',
                integrationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                languageName: 'Go',
                mainBranch: 'latest',
                mainRepository: 'Azure/azure-sdk-for-go',
                mainRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                readmeMdFileUrlsToGenerate: [],
                status: 'succeeded',
                swaggerToSDKConfigFileUrl: getSwaggerToSDKConfigurationURL('Azure/azure-sdk-for-go', 'latest')
              }
            ]
          }
        };
        await dataBlob.setContentsFromString(JSON.stringify(specificationPullRequestData));

        const goSDKRepositoryPrefix: BlobStoragePrefix = getSDKRepositoryPrefix(
          generationPrefix,
          'Azure/azure-sdk-for-go'
        );
        const goLogsBlob: BlobStorageAppendBlob = getLogsBlob(goSDKRepositoryPrefix);
        await goLogsBlob.create();

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const specPRInProgressLabel: GitHubLabel = await github.createLabel(
            'Azure/azure-sdk-for-go',
            'SpecPRInProgress',
            pullRequestLabelsInfo['SpecPRInProgress'].color
          );
          const fakePullRequest: GitHubPullRequest = {
            base: {
              label: 'Azure:fake-base',
              ref: 'fake-base',
              sha: 'fake-base-sha'
            },
            diff_url: 'fake-diff-url',
            head: {
              label: `Azure:fake-head@${testSpecificationPullRequest.number}`,
              ref: `fake-head@${testSpecificationPullRequest.number}`,
              sha: 'fake-head-sha'
            },
            html_url: `https://github.com/Azure/azure-sdk-for-go/pull/1`,
            id: 1,
            labels: [specPRInProgressLabel],
            merge_commit_sha: 'fake-merge-commit-sha',
            number: 1,
            state: 'open',
            title: 'fake-title',
            url: `https://api.github.com/repos/Azure/azure-sdk-for-go/pulls/1`
          };
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.head.sha, 'hello');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.head.ref, fakePullRequest.head.sha);
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.base.sha, 'there');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.base.ref, fakePullRequest.base.sha);
          await github.createCommit('Azure/azure-sdk-for-go', 'fake-latest-sha', 'test');
          await github.createBranch('Azure/azure-sdk-for-go', 'latest', 'fake-latest-sha');
          await github.createFakePullRequest('Azure/azure-sdk-for-go', fakePullRequest);
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient: HttpClient = createTestHttpClient();
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.mergeSDKGenerationPullRequests();

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
            `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/latest/swagger_to_sdk_config.json"...`,
            `Azure/azure-sdk-for-go - Getting generation pull requests for specification pull request fake-html-url...`,
            `Azure/azure-sdk-for-go - Merging https://github.com/Azure/azure-sdk-for-go/pull/1...`,
            `Azure/azure-sdk-for-go - Deleting generation branch "fake-head@4994" from "Azure/azure-sdk-for-go"...`,
            `Azure/azure-sdk-for-go - Label changes for PR 1 in Azure/azure-sdk-for-go: +GenerationPR, +SpecPRMerged, -SpecPRInProgress`,
            `Azure/azure-sdk-for-go - Looking for integration pull requests that use "Azure:fake-base" as their head label...`,
            `Azure/azure-sdk-for-go - Found no open integration pull requests that match. Creating a new integration pull request in "Azure/azure-sdk-for-go" from "Azure:fake-base" to "latest"...`,
            `Azure/azure-sdk-for-go - maintainerCanModify: true`,
            `Azure/azure-sdk-for-go - Created integration pull request at fake-html-url.`,
            `Azure/azure-sdk-for-go - Label changes for PR 2 in Azure/azure-sdk-for-go: +IntegrationPR`,
            `Azure/azure-sdk-for-go - Adding integration pull request link to generation pull request comment...`
          ]);

          const fakePullRequestAfterMerging: GitHubPullRequest = await github.getPullRequest(
            'Azure/azure-sdk-for-go',
            fakePullRequest.number
          );
          assert.strictEqual(fakePullRequestAfterMerging.state, 'closed');
          assert.deepEqual(
            map(fakePullRequestAfterMerging.labels, (label: GitHubLabel) => label.name),
            ['GenerationPR', 'SpecPRMerged']
          );
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });

    it('with open generation pull request but disabled generation pr automation', async function() {
      const workingContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      try {
        const automationWorkingPrefix: BlobStoragePrefix = workingContainer;
        await ensureStateImagesExist(automationWorkingPrefix);

        const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
          automationWorkingPrefix,
          testSpecificationPullRequestRepository,
          testSpecificationPullRequest.number
        );
        const dataBlob: BlobStorageBlockBlob = getDataBlob(pullRequestPrefix);
        const iterationNumber = 5;
        const generationPrefix: BlobStoragePrefix = getGenerationPrefix(pullRequestPrefix, iterationNumber);
        const generationDataBlob: BlobStorageBlockBlob = getDataBlob(generationPrefix);
        const generationLogsBlob: BlobStorageAppendBlob = getLogsBlob(generationPrefix);
        await generationLogsBlob.create();
        const generationCommentBlob: BlobStorageBlockBlob = getCommentHtmlBlob(generationPrefix);
        const specificationPullRequestData: SpecificationPullRequestData = {
          baseBranch: getRepositoryBranch(testSpecificationPullRequest.base.label),
          baseRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          dataBlobUrl: dataBlob.getURL(),
          diffUrl: 'fake-diff-url',
          headBranch: getRepositoryBranch(testSpecificationPullRequest.head.label),
          headCommit: testSpecificationPullRequest.head.sha,
          headRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.head.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          htmlUrl: 'fake-html-url',
          mergeCommit: testSpecificationPullRequestMergeCommitSha,
          number: testSpecificationPullRequest.number,
          specPRCommit: testSpecificationPullRequestMergeCommitSha,
          specPRRepository: {
            owner: getRepositoryBranch(testSpecificationPullRequest.base.label).owner,
            name: getRepository(testSpecificationPullRequestRepository).name
          },
          title: 'fake-title',
          generation: {
            dataBlobUrl: generationDataBlob.getURL(),
            logsBlobUrl: generationLogsBlob.getURL(),
            commentHtmlBlobUrl: generationCommentBlob.getURL(),
            number: iterationNumber,
            sdkRepositories: [
              {
                generationRepository: 'Azure/azure-sdk-for-go',
                generationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                integrationBranchPrefix: 'sdkAutomationTest/',
                integrationRepository: 'Azure/azure-sdk-for-go',
                integrationRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                languageName: 'Go',
                mainBranch: 'latest',
                mainRepository: 'Azure/azure-sdk-for-go',
                mainRepositoryUrl: 'https://github.com/Azure/azure-sdk-for-go',
                readmeMdFileUrlsToGenerate: [],
                status: 'succeeded',
                swaggerToSDKConfigFileUrl: getSwaggerToSDKConfigurationURL('Azure/azure-sdk-for-go', 'latest')
              }
            ]
          }
        };
        await dataBlob.setContentsFromString(JSON.stringify(specificationPullRequestData));

        const goSDKRepositoryPrefix: BlobStoragePrefix = getSDKRepositoryPrefix(
          generationPrefix,
          'Azure/azure-sdk-for-go'
        );
        const goLogsBlob: BlobStorageAppendBlob = getLogsBlob(goSDKRepositoryPrefix);
        await goLogsBlob.create();

        const automationWorkingFolderPath: string = getAutomationWorkingFolderPath(getRootFolderPath());
        const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(automationWorkingFolderPath);
        await deleteFolder(generationWorkingFolderPath);
        try {
          const github: FakeGitHub = await createTestGitHub();
          const specPRInProgressLabel: GitHubLabel = await github.createLabel(
            'Azure/azure-sdk-for-go',
            'SpecPRInProgress',
            pullRequestLabelsInfo['SpecPRInProgress'].color
          );
          const fakePullRequest: GitHubPullRequest = {
            base: {
              label: 'Azure:fake-base',
              ref: 'fake-base',
              sha: 'fake-base-sha'
            },
            diff_url: 'fake-diff-url',
            head: {
              label: `Azure:fake-head@${testSpecificationPullRequest.number}`,
              ref: `fake-head@${testSpecificationPullRequest.number}`,
              sha: 'fake-head-sha'
            },
            html_url: `https://github.com/Azure/azure-sdk-for-go/pull/1`,
            id: 1,
            labels: [specPRInProgressLabel],
            merge_commit_sha: 'fake-merge-commit-sha',
            number: 1,
            state: 'open',
            title: 'fake-title',
            url: `https://api.github.com/repos/Azure/azure-sdk-for-go/pulls/1`
          };
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.head.sha, 'hello');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.head.ref, fakePullRequest.head.sha);
          await github.createCommit('Azure/azure-sdk-for-go', fakePullRequest.base.sha, 'there');
          await github.createBranch('Azure/azure-sdk-for-go', fakePullRequest.base.ref, fakePullRequest.base.sha);
          await github.createCommit('Azure/azure-sdk-for-go', 'fake-latest-sha', 'test');
          await github.createBranch('Azure/azure-sdk-for-go', 'latest', 'fake-latest-sha');
          await github.createFakePullRequest('Azure/azure-sdk-for-go', fakePullRequest);
          const logger: InMemoryLogger = getInMemoryLogger();
          const httpClient = createTestHttpClient() as FakeHttpClient;
          httpClient.add(
            'GET',
            'https://raw.githubusercontent.com/Azure/azure-sdk-for-go/latest/swagger_to_sdk_config.json',
            200,
            undefined,
            JSON.stringify({
              meta: {
                advanced_options: {
                  disable_generation_pr_automation: true
                }
              }
            })
          );
          const runner: FakeRunner = createTestRunner({
            specificationPullRequest: testSpecificationPullRequest,
            autorest: autorestExecutable({ autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest') }),
            generationWorkingFolderPath: generationWorkingFolderPath,
            github
          });
          const sdkAutomation = new SDKAutomation(automationWorkingFolderPath, {
            github,
            logger,
            runner,
            httpClient
          });
          const specificationPullRequest: SpecificationPullRequest = await getSpecificationPullRequest(
            sdkAutomation,
            automationWorkingPrefix,
            testSpecificationPullRequest,
            false
          );

          await specificationPullRequest.mergeSDKGenerationPullRequests();

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
            `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/Azure/azure-sdk-for-go/latest/swagger_to_sdk_config.json"...`,
            `Azure/azure-sdk-for-go - Getting generation pull requests for specification pull request fake-html-url...`,
            `Azure/azure-sdk-for-go - Skip to merge generation pull request at https://github.com/Azure/azure-sdk-for-go/pull/1 as it's disabled in config.`,
            `Azure/azure-sdk-for-go - Label changes for PR 1 in Azure/azure-sdk-for-go: +GenerationPR, +SpecPRMerged, -SpecPRInProgress`,
            `Azure/azure-sdk-for-go - Looking for integration pull requests that use "Azure:fake-base" as their head label...`,
            `Azure/azure-sdk-for-go - Found no open integration pull requests that match. Creating a new integration pull request in "Azure/azure-sdk-for-go" from "Azure:fake-base" to "latest"...`,
            `Azure/azure-sdk-for-go - maintainerCanModify: true`,
            `Azure/azure-sdk-for-go - Created integration pull request at fake-html-url.`,
            `Azure/azure-sdk-for-go - Label changes for PR 2 in Azure/azure-sdk-for-go: +IntegrationPR`,
            `Azure/azure-sdk-for-go - Adding integration pull request link to generation pull request comment...`
          ]);

          const fakePullRequestAfterMerging: GitHubPullRequest = await github.getPullRequest(
            'Azure/azure-sdk-for-go',
            fakePullRequest.number
          );
          assert.strictEqual(fakePullRequestAfterMerging.state, 'open');
          assert.deepEqual(
            map(fakePullRequestAfterMerging.labels, (label: GitHubLabel) => label.name),
            ['GenerationPR', 'SpecPRMerged']
          );
        } finally {
          if (deleteWorkingFolder) {
            await deleteFolder(generationWorkingFolderPath);
          }
        }
      } finally {
        if (deleteWorkingContainer) {
          await workingContainer.delete();
        }
      }
    });
  });

  describe('getPullRequestPrefix()', function() {
    it('with container prefix without trailing slash', function() {
      const blobStorage = new InMemoryBlobStorage();
      const workingPrefix: BlobStoragePrefix = blobStorage.getPrefix('abc');
      const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
        workingPrefix,
        'Azure/azure-rest-api-specs',
        4994
      );
      assert.strictEqual(pullRequestPrefix.storage, blobStorage);
      assert.deepEqual(pullRequestPrefix.path, new BlobPath('abc', 'Azure/azure-rest-api-specs/4994/'));
      assert.strictEqual(pullRequestPrefix.getURL(), 'https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/');
    });

    it('with container prefix with trailing slash', function() {
      const blobStorage = new InMemoryBlobStorage();
      const workingPrefix: BlobStoragePrefix = blobStorage.getPrefix('abc/');
      const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
        workingPrefix,
        'Azure/azure-rest-api-specs',
        4994
      );
      assert.strictEqual(pullRequestPrefix.storage, blobStorage);
      assert.deepEqual(pullRequestPrefix.path, new BlobPath('abc', 'Azure/azure-rest-api-specs/4994/'));
      assert.strictEqual(pullRequestPrefix.getURL(), 'https://fake.storage.com/abc/Azure/azure-rest-api-specs/4994/');
    });

    it('with container and blob prefix without trailing slash', function() {
      const blobStorage = new InMemoryBlobStorage();
      const workingPrefix: BlobStoragePrefix = blobStorage.getPrefix('abc/def');
      const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
        workingPrefix,
        'Azure/azure-rest-api-specs',
        4994
      );
      assert.strictEqual(pullRequestPrefix.storage, blobStorage);
      assert.deepEqual(pullRequestPrefix.path, new BlobPath('abc', 'defAzure/azure-rest-api-specs/4994/'));
      assert.strictEqual(
        pullRequestPrefix.getURL(),
        'https://fake.storage.com/abc/defAzure/azure-rest-api-specs/4994/'
      );
    });

    it('with container and blob prefix with trailing slash', function() {
      const blobStorage = new InMemoryBlobStorage();
      const workingPrefix: BlobStoragePrefix = blobStorage.getPrefix('abc/def/');
      const pullRequestPrefix: BlobStoragePrefix = getPullRequestPrefix(
        workingPrefix,
        'Azure/azure-rest-api-specs',
        4994
      );
      assert.strictEqual(pullRequestPrefix.storage, blobStorage);
      assert.deepEqual(pullRequestPrefix.path, new BlobPath('abc', 'def/Azure/azure-rest-api-specs/4994/'));
      assert.strictEqual(
        pullRequestPrefix.getURL(),
        'https://fake.storage.com/abc/def/Azure/azure-rest-api-specs/4994/'
      );
    });
  });
});
