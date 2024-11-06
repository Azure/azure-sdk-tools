import { getInMemoryLogger, InMemoryLogger } from '@azure/logger-js';
import {
  assertEx,
  BlobStorageBlob,
  BlobStorageContainer,
  BlobStoragePrefix,
  deleteFolder,
  FakeCompressor,
  FakeGitHub,
  HttpClient,
  ExecutableGit
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { javascript } from '../lib/langSpecs/javascript';
import { getBlobLogger } from '../lib/sdkAutomation';
import { SDKRepository } from '../lib/sdkRepository';
import {
  getSpecificationPullRequestGeneration,
  SpecificationPullRequestGeneration
} from '../lib/specificationPullRequestGeneration';
import { createTestBlobStorageContainer, createTestHttpClient, testSpecificationPullRequestNumber } from './test';

describe('specificationPullRequestGeneration.ts', function() {
  describe('getSpecificationPullRequestGeneration()', function() {
    it('with first generation', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const prefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();

      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        prefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        assertEx.defined(generation, 'generation');
        assert.deepEqual(generation.data, {
          number: 1,
          logsBlobUrl: 'https://fake.storage.com/abc/def/1/logs.txt',
          dataBlobUrl: 'https://fake.storage.com/abc/def/1/data.json',
          commentHtmlBlobUrl: 'https://fake.storage.com/abc/def/1/comment.html',
          sdkRepositories: []
        });
        assert.strictEqual(generation.specificationPullRequest, undefined);
        assert.strictEqual(await generation.logsBlob.exists(), true);
        assert.deepEqual(logger.allLogs, []);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with second generation', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const prefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      await getSpecificationPullRequestGeneration(testSpecificationPullRequestNumber, prefix, {
        httpClient,
        github: new FakeGitHub(),
        git: new ExecutableGit(),
        logger,
        deleteClonedRepositories: true,
        automationWorkingFolderPath: 'fake-automation-working-folder-path',
        compressorCreator: () => new FakeCompressor(),
        getBlobLogger,
        getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
        createGenerationPullRequests: true,
        generateLanguagesInParallel: false
      });

      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        prefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        assertEx.defined(generation, 'generation');
        assert.deepEqual(generation.data, {
          number: 2,
          logsBlobUrl: 'https://fake.storage.com/abc/def/2/logs.txt',
          dataBlobUrl: 'https://fake.storage.com/abc/def/2/data.json',
          commentHtmlBlobUrl: 'https://fake.storage.com/abc/def/2/comment.html',
          sdkRepositories: []
        });
        assert.strictEqual(generation.specificationPullRequest, undefined);
        assert.strictEqual(await generation.logsBlob.exists(), true);
        assert.deepEqual(logger.allLogs, []);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });
  });

  describe('writeGenerationData()', function() {
    it('with no data, no comment html, and no specification pull request', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const prefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        prefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.writeGenerationData();

        assert.strictEqual((await blobStorageContainer.getBlobContentsAsString('def/1/data.json')).contents, '');
        assert.strictEqual((await blobStorageContainer.getBlobContentsAsString('def/1/comment.html')).contents, '');
        assert.deepEqual(logger.allLogs, []);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with data, no comment html, and no specification pull request', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const prefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        prefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.writeGenerationData('generation data');

        assert.strictEqual(
          (await blobStorageContainer.getBlobContentsAsString('def/1/data.json')).contents,
          'generation data'
        );
        assert.strictEqual((await blobStorageContainer.getBlobContentsAsString('def/1/comment.html')).contents, '');
        assert.deepEqual(logger.allLogs, []);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with no data, comment html, and no specification pull request', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const prefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        prefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.writeGenerationData(undefined, 'comment html');

        assert.strictEqual((await blobStorageContainer.getBlobContentsAsString('def/1/data.json')).contents, '');
        assert.strictEqual(
          (await blobStorageContainer.getBlobContentsAsString('def/1/comment.html')).contents,
          'comment html'
        );
        assert.deepEqual(logger.allLogs, []);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with data, comment html, and no specification pull request', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const prefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        prefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.writeGenerationData('generation data', 'comment html');

        assert.strictEqual(
          (await blobStorageContainer.getBlobContentsAsString('def/1/data.json')).contents,
          'generation data'
        );
        assert.strictEqual(
          (await blobStorageContainer.getBlobContentsAsString('def/1/comment.html')).contents,
          'comment html'
        );
        assert.deepEqual(logger.allLogs, []);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });
  });

  describe('getSDKRepository()', function() {
    it('with SDK Repository not seen before', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const pullRequestPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        pullRequestPrefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        const sdkRepository: SDKRepository = (await generation.getSDKRepository('fake/js-sdk-repository', javascript))!;

        assertEx.defined(sdkRepository, 'sdkRepository');
        assert.deepEqual(sdkRepository.data, {
          mainRepository: 'fake/js-sdk-repository',
          mainRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
          generationRepository: 'fake/js-sdk-repository',
          generationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
          integrationRepository: 'fake/js-sdk-repository',
          integrationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
          integrationBranchPrefix: 'sdkAutomation',
          mainBranch: 'master',
          languageName: 'JavaScript',
          readmeMdFileUrlsToGenerate: [],
          status: 'pending',
          swaggerToSDKConfigFileUrl:
            'https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json'
        });
        assert.deepEqual(generation.data.sdkRepositories, [sdkRepository.data]);
        assert.deepEqual(logger.allLogs, [
          `SDK repository fake/js-sdk-repository matches programming language JavaScript.`,
          `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json"...`
        ]);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with SDK Repository previously seen', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const pullRequestPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        pullRequestPrefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        const sdkRepository1: SDKRepository = (await generation.getSDKRepository(
          'fake/js-sdk-repository',
          javascript
        ))!;

        const sdkRepository2: SDKRepository = (await generation.getSDKRepository(
          'fake/js-sdk-repository',
          javascript
        ))!;

        assert.strictEqual(sdkRepository2, sdkRepository1);
        assert.deepEqual(generation.data.sdkRepositories, [sdkRepository1.data]);
        assert.deepEqual(logger.allLogs, [
          `SDK repository fake/js-sdk-repository matches programming language JavaScript.`,
          `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json"...`
        ]);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });
  });

  describe('addReadmeMdFileToGenerateForSDKRepository()', function() {
    it('with unseen SDK repository', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const pullRequestPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        pullRequestPrefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.addReadmeMdFileToGenerateForSDKRepository(
          'fake/js-sdk-repository',
          'fake-readme.md-file-url',
          javascript
        );

        assert.deepEqual(generation.data.sdkRepositories, [
          {
            mainRepository: 'fake/js-sdk-repository',
            mainRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            generationRepository: 'fake/js-sdk-repository',
            generationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationRepository: 'fake/js-sdk-repository',
            integrationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationBranchPrefix: 'sdkAutomation',
            mainBranch: 'master',
            languageName: 'JavaScript',
            status: 'pending',
            readmeMdFileUrlsToGenerate: ['fake-readme.md-file-url'],
            swaggerToSDKConfigFileUrl:
              'https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json'
          }
        ]);
        assert.deepEqual(logger.allLogs, [
          `Mapping "fake/js-sdk-repository" to "fake/js-sdk-repository".`,
          `SDK repository fake/js-sdk-repository matches programming language JavaScript.`,
          `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json"...`,
          `Adding readme.md to generate to fake/js-sdk-repository: fake-readme.md-file-url`
        ]);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with seen SDK repository and unseen readme.md url', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const pullRequestPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        pullRequestPrefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.getSDKRepository('fake/js-sdk-repository', javascript);

        await generation.addReadmeMdFileToGenerateForSDKRepository(
          'fake/js-sdk-repository',
          'fake-readme.md-file-url',
          javascript
        );

        assert.deepEqual(generation.data.sdkRepositories, [
          {
            mainRepository: 'fake/js-sdk-repository',
            mainRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            generationRepository: 'fake/js-sdk-repository',
            generationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationRepository: 'fake/js-sdk-repository',
            integrationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationBranchPrefix: 'sdkAutomation',
            mainBranch: 'master',
            languageName: 'JavaScript',
            status: 'pending',
            readmeMdFileUrlsToGenerate: ['fake-readme.md-file-url'],
            swaggerToSDKConfigFileUrl:
              'https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json'
          }
        ]);
        assert.deepEqual(logger.allLogs, [
          `SDK repository fake/js-sdk-repository matches programming language JavaScript.`,
          `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json"...`,
          `Mapping "fake/js-sdk-repository" to "fake/js-sdk-repository".`,
          `Adding readme.md to generate to fake/js-sdk-repository: fake-readme.md-file-url`
        ]);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with seen SDK repository and seen readme.md url', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const pullRequestPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        pullRequestPrefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.addReadmeMdFileToGenerateForSDKRepository(
          'fake/js-sdk-repository',
          'fake-readme.md-file-url',
          javascript
        );

        await generation.addReadmeMdFileToGenerateForSDKRepository(
          'fake/js-sdk-repository',
          'fake-readme.md-file-url',
          javascript
        );

        assert.deepEqual(generation.data.sdkRepositories, [
          {
            mainRepository: 'fake/js-sdk-repository',
            mainRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            generationRepository: 'fake/js-sdk-repository',
            generationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationRepository: 'fake/js-sdk-repository',
            integrationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationBranchPrefix: 'sdkAutomation',
            mainBranch: 'master',
            languageName: 'JavaScript',
            status: 'pending',
            readmeMdFileUrlsToGenerate: ['fake-readme.md-file-url'],
            swaggerToSDKConfigFileUrl:
              'https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json'
          }
        ]);
        assert.deepEqual(logger.allLogs, [
          `Mapping "fake/js-sdk-repository" to "fake/js-sdk-repository".`,
          `SDK repository fake/js-sdk-repository matches programming language JavaScript.`,
          `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json"...`,
          `Adding readme.md to generate to fake/js-sdk-repository: fake-readme.md-file-url`,
          `Mapping "fake/js-sdk-repository" to "fake/js-sdk-repository".`
        ]);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });

    it('with seen SDK repository and multiple readme.md urls', async function() {
      const blobStorageContainer: BlobStorageContainer = await createTestBlobStorageContainer();
      const pullRequestPrefix: BlobStoragePrefix = blobStorageContainer.getPrefix('def/');
      const httpClient: HttpClient = createTestHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const generation: SpecificationPullRequestGeneration = await getSpecificationPullRequestGeneration(
        testSpecificationPullRequestNumber,
        pullRequestPrefix,
        {
          httpClient,
          github: new FakeGitHub(),
          git: new ExecutableGit(),
          logger,
          deleteClonedRepositories: true,
          automationWorkingFolderPath: 'fake-automation-working-folder-path',
          compressorCreator: () => new FakeCompressor(),
          getBlobLogger,
          getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL({ sasToken: false }),
          createGenerationPullRequests: true,
          generateLanguagesInParallel: false
        }
      );
      try {
        await generation.addReadmeMdFileToGenerateForSDKRepository(
          'fake/js-sdk-repository',
          'fake-readme.md-file-url1',
          javascript
        );
        await generation.addReadmeMdFileToGenerateForSDKRepository(
          'fake/js-sdk-repository',
          'fake-readme.md-file-url2',
          javascript
        );

        assert.deepEqual(generation.data.sdkRepositories, [
          {
            mainRepository: 'fake/js-sdk-repository',
            mainRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            generationRepository: 'fake/js-sdk-repository',
            generationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationRepository: 'fake/js-sdk-repository',
            integrationRepositoryUrl: 'https://github.com/fake/js-sdk-repository',
            integrationBranchPrefix: 'sdkAutomation',
            mainBranch: 'master',
            languageName: 'JavaScript',
            status: 'pending',
            readmeMdFileUrlsToGenerate: ['fake-readme.md-file-url1', 'fake-readme.md-file-url2'],
            swaggerToSDKConfigFileUrl:
              'https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json'
          }
        ]);
        assert.deepEqual(logger.allLogs, [
          `Mapping "fake/js-sdk-repository" to "fake/js-sdk-repository".`,
          `SDK repository fake/js-sdk-repository matches programming language JavaScript.`,
          `Getting swagger_to_sdk_config.json file from "https://raw.githubusercontent.com/fake/js-sdk-repository/master/swagger_to_sdk_config.json"...`,
          `Adding readme.md to generate to fake/js-sdk-repository: fake-readme.md-file-url1`,
          `Mapping "fake/js-sdk-repository" to "fake/js-sdk-repository".`,
          `Adding readme.md to generate to fake/js-sdk-repository: fake-readme.md-file-url2`
        ]);
      } finally {
        await deleteFolder('fake-automation-working-folder-path');
      }
    });
  });
});
