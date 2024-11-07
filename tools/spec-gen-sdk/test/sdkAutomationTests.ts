import { getInMemoryLogger, InMemoryLogger } from '@azure/logger-js';
import { SharedKeyCredential } from '@azure/storage-blob';
import {
  assertEx,
  BlobStorageContainer,
  BlobStoragePrefix,
  deleteFolder,
  FakeGitHub,
  FakeHttpClient,
  folderExists,
  getRootPath,
  GitHub,
  InMemoryBlobStorage,
  joinPath,
  NodeHttpClient,
  URLBuilder
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { SpecificationPREvent } from '../lib';
import { go } from '../lib/langSpecs/go';
import { java } from '../lib/langSpecs/java';
import { javascript } from '../lib/langSpecs/javascript';
import { RealBlobProxy } from '../lib/realBlobProxy';
import {
  getAllLanguages,
  getAutomationWorkingFolderPath,
  getGenerationWorkingFolderPath,
  getGitHub,
  getRootFolderPath,
  getSupportedLanguages,
  SDKAutomation,
  trimNewLine
} from '../lib/sdkAutomation';
import { createTestBlobStorageContainer, testSpecificationPullRequest } from './test';

describe('sdkAutomation.ts', function() {
  describe('SDKAutomation', function() {
    describe('constructor()', function() {
      it('with no options', function() {
        const sdkAutomation = new SDKAutomation('fake-automation-working-folder-path');
        assert.strictEqual(sdkAutomation.automationWorkingFolderPath, 'fake-automation-working-folder-path');
        assertEx.defined(sdkAutomation.compressorCreator, 'sdkAutomation.compressorCreator');
        assert.strictEqual(sdkAutomation.deleteClonedRepositories, true);
        assert(sdkAutomation.github instanceof FakeGitHub, 'sdkAutomation.github instanceof FakeGitHub');
        assert(
          sdkAutomation.httpClient instanceof NodeHttpClient,
          'sdkAutomation.httpClient instanceof NodeHttpClient'
        );
        assert.strictEqual(sdkAutomation.logger, undefined);
        assert.strictEqual(sdkAutomation.runner, undefined);
        assert.deepEqual(sdkAutomation.supportedLanguageConfigurations, getAllLanguages());
      });
    });

    describe('resolveBlobProxyUrl()', function() {
      it('with workingPrefix with non-existing container', async function() {
        const blobStorage = new InMemoryBlobStorage(
          'https://fake.storage.com/',
          new SharedKeyCredential('fake-account-name', 'fake-account-key')
        );
        const workingPrefix: BlobStoragePrefix = blobStorage.getPrefix('privatecontainer/prefix/');
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          blobProxy: new RealBlobProxy('https://kebab.com/sdkAutomation/')
        });
        const sasUrl: string = (await sdkAutomation.resolveBlobProxyUrl(
          workingPrefix,
          'https://kebab.com/sdkAutomation/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-node/azure-arm-mysql/azure-arm-mysql-3.2.0.tgz'
        ))!;
        assertEx.definedAndNotEmpty(sasUrl, 'sasUrl');
        const sasUrlBuilder: URLBuilder = URLBuilder.parse(sasUrl);
        assert.strictEqual(sasUrlBuilder.getScheme(), 'https');
        assert.strictEqual(sasUrlBuilder.getHost(), 'fake.storage.com');
        assert.strictEqual(sasUrlBuilder.getPort(), undefined);
        assert.strictEqual(
          sasUrlBuilder.getPath(),
          '/privatecontainer/prefix/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-node/azure-arm-mysql/azure-arm-mysql-3.2.0.tgz'
        );
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('se') as string, 'se');
        assert.strictEqual(sasUrlBuilder.getQueryParameterValue('sp'), 'r');
        assert.strictEqual(sasUrlBuilder.getQueryParameterValue('sr'), 'b');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('sig') as string, 'sig');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('st') as string, 'st');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('sv') as string, 'sv');
      });

      it('with workingPrefix with non-existing blob', async function() {
        const blobStorage = new InMemoryBlobStorage(
          'https://fake.storage.com/',
          new SharedKeyCredential('fake-account-name', 'fake-account-key')
        );
        const container: BlobStorageContainer = blobStorage.getContainer('privatecontainer');
        await container.create();
        const workingPrefix: BlobStoragePrefix = container.getPrefix('prefix/');
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          blobProxy: new RealBlobProxy('https://kebab.com/sdkAutomation/')
        });
        const sasUrl: string = (await sdkAutomation.resolveBlobProxyUrl(
          workingPrefix,
          'https://kebab.com/sdkAutomation/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-node/azure-arm-mysql/azure-arm-mysql-3.2.0.tgz'
        ))!;
        assertEx.definedAndNotEmpty(sasUrl, 'sasUrl');
        const sasUrlBuilder: URLBuilder = URLBuilder.parse(sasUrl);
        assert.strictEqual(sasUrlBuilder.getScheme(), 'https');
        assert.strictEqual(sasUrlBuilder.getHost(), 'fake.storage.com');
        assert.strictEqual(sasUrlBuilder.getPort(), undefined);
        assert.strictEqual(
          sasUrlBuilder.getPath(),
          '/privatecontainer/prefix/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-node/azure-arm-mysql/azure-arm-mysql-3.2.0.tgz'
        );
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('se') as string, 'se');
        assert.strictEqual(sasUrlBuilder.getQueryParameterValue('sp'), 'r');
        assert.strictEqual(sasUrlBuilder.getQueryParameterValue('sr'), 'b');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('sig') as string, 'sig');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('st') as string, 'st');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('sv') as string, 'sv');
      });

      it('with workingPrefix with existing blob', async function() {
        const blobStorage = new InMemoryBlobStorage(
          'https://fake.storage.com/',
          new SharedKeyCredential('fake-account-name', 'fake-account-key')
        );
        const container: BlobStorageContainer = blobStorage.getContainer('privatecontainer');
        await container.create();
        const workingPrefix: BlobStoragePrefix = container.getPrefix('prefix/');
        const blobRelativePath =
          'Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-node/azure-arm-mysql/azure-arm-mysql-3.2.0.tgz';
        await workingPrefix.createBlockBlob(blobRelativePath);
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          blobProxy: new RealBlobProxy('https://kebab.com/sdkAutomation/')
        });
        const sasUrl: string = (await sdkAutomation.resolveBlobProxyUrl(
          workingPrefix,
          `https://kebab.com/sdkAutomation/${blobRelativePath}`
        ))!;
        assertEx.definedAndNotEmpty(sasUrl, 'sasUrl');
        const sasUrlBuilder: URLBuilder = URLBuilder.parse(sasUrl);
        assert.strictEqual(sasUrlBuilder.getScheme(), 'https');
        assert.strictEqual(sasUrlBuilder.getHost(), 'fake.storage.com');
        assert.strictEqual(sasUrlBuilder.getPort(), undefined);
        assert.strictEqual(
          sasUrlBuilder.getPath(),
          '/privatecontainer/prefix/Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-node/azure-arm-mysql/azure-arm-mysql-3.2.0.tgz'
        );
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('se') as string, 'se');
        assert.strictEqual(sasUrlBuilder.getQueryParameterValue('sp'), 'r');
        assert.strictEqual(sasUrlBuilder.getQueryParameterValue('sr'), 'b');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('sig') as string, 'sig');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('st') as string, 'st');
        assertEx.definedAndNotEmpty(sasUrlBuilder.getQueryParameterValue('sv') as string, 'sv');
      });

      it('with proxyBlobUrl with incorrect path format', async function() {
        const blobStorage = new InMemoryBlobStorage(
          'https://fake.storage.com/',
          new SharedKeyCredential('fake-account-name', 'fake-account-key')
        );
        const container: BlobStorageContainer = blobStorage.getContainer('privatecontainer');
        await container.create();
        const workingPrefix: BlobStoragePrefix = container.getPrefix('prefix/');
        const blobRelativePath =
          'Azure/azure-rest-api-specs/4994/1/Azure/azure-sdk-for-node/azure-arm-mysql/azure-arm-mysql-3.2.0.tgz';
        await workingPrefix.createBlockBlob(blobRelativePath);
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          blobProxy: new RealBlobProxy('https://kebab.com/sdkAutomation/')
        });
        const sasUrl: string | undefined = await sdkAutomation.resolveBlobProxyUrl(
          workingPrefix,
          `https://kebab.com/blah/${blobRelativePath}`
        );
        assert.strictEqual(sasUrl, undefined);
      });
    });

    describe('handleEvent()', function() {
      it('with undefined event', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger
        });
        let value = 0;
        const result: number | undefined = await sdkAutomation.handleEvent(undefined, () => {
          ++value;
          return value + 5;
        });
        assertEx.containsAll(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `No HttpClient provided. Using NodeHttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Processing pull request event because the event is undefined.`,
          `Using FakeGitHub client.`
        ]);
        assert.strictEqual(value, 1);
        assert.strictEqual(result, 6);
      });

      it('with non-GitHub repository pull request url', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const container: BlobStorageContainer = await createTestBlobStorageContainer();
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger
        });
        const event: SpecificationPREvent | undefined = {
          workingPrefix: container,
          logger,
          webhookBody: {
            action: 'opened',
            number: 50,
            pull_request: {
              ...testSpecificationPullRequest,
              html_url: 'https://not.a.github/repository/pull/request/url'
            }
          }
        };
        let value = 0;
        const result: number | undefined = await sdkAutomation.handleEvent(event, () => {
          ++value;
          return value + 5;
        });
        assert.deepEqual(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `No provided Git. Using ExecutableGit instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `No HttpClient provided. Using NodeHttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Failed to get the pull request repository from the pull request url (https://not.a.github/repository/pull/request/url).`
        ]);
        assert.strictEqual(value, 0);
        assert.strictEqual(result, undefined);
      });

      it('with no specificationRepositoryConfiguration.json file and PR base branch is master', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const container: BlobStorageContainer = await createTestBlobStorageContainer();
        const httpClient = new FakeHttpClient();
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger,
          httpClient
        });
        let value = 0;
        const event: SpecificationPREvent | undefined = {
          workingPrefix: container,
          logger,
          webhookBody: {
            action: 'opened',
            number: 50,
            pull_request: testSpecificationPullRequest
          }
        };
        const result: number | undefined = await sdkAutomation.handleEvent(event, () => {
          ++value;
          return value + 5;
        });
        assertEx.containsAll(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `Using provided HttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
          `Specification repository configuration response status code: 404`,
          `Could not find a specificationRepositoryConfiguration.json. Ignoring event.`
        ]);
        assert.strictEqual(value, 0);
        assert.strictEqual(result, undefined);
      });

      it('with no specificationRepositoryConfiguration.json file and PR base branch is not master', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const container: BlobStorageContainer = await createTestBlobStorageContainer();
        const httpClient = new FakeHttpClient();
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger,
          httpClient
        });
        let value = 0;
        const event: SpecificationPREvent | undefined = {
          workingPrefix: container,
          logger,
          webhookBody: {
            action: 'opened',
            number: 50,
            pull_request: {
              ...testSpecificationPullRequest,
              base: {
                label: 'fake-label',
                ref: 'fake-ref',
                sha: 'fake-sha'
              }
            }
          }
        };
        const result: number | undefined = await sdkAutomation.handleEvent(event, () => {
          ++value;
          return value + 5;
        });
        assert.deepEqual(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `No provided Git. Using ExecutableGit instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `Using provided HttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/fake-ref/specificationRepositoryConfiguration.json"...`,
          `Specification repository configuration response status code: 404`,
          `Could not find a specificationRepositoryConfiguration.json. Ignoring event.`
        ]);
        assert.strictEqual(value, 0);
        assert.strictEqual(result, undefined);
      });

      it('with no sdkAutomationBaseBranch property and PR base branch is master', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const container: BlobStorageContainer = await createTestBlobStorageContainer();
        const httpClient = new FakeHttpClient();
        httpClient.add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
          200,
          undefined,
          JSON.stringify({})
        );
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger,
          httpClient
        });
        let value = 0;
        const event: SpecificationPREvent | undefined = {
          workingPrefix: container,
          logger,
          webhookBody: {
            action: 'opened',
            number: 50,
            pull_request: testSpecificationPullRequest
          }
        };
        const result: number | undefined = await sdkAutomation.handleEvent(event, () => {
          ++value;
          return value + 5;
        });
        assertEx.containsAll(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `Using provided HttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
          `Specification repository configuration response status code: 200`,
          `No sdkAutomationBaseBranch property specified in the specificationRepositoryConfiguration.json. Defaulting sdkAutomationBaseBranch to "master".`,
          `Processing pull request event because the pull request base branch (master) is equal to the sdkAutomationBaseBranch (master).`,
          `Updating logger...`,
          `Using FakeGitHub client.`
        ]);
        assert.strictEqual(value, 1);
        assert.strictEqual(result, 6);
      });

      it('with sdkAutomationBaseBranch property and PR base branch is not equal', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const container: BlobStorageContainer = await createTestBlobStorageContainer();
        const httpClient = new FakeHttpClient();
        httpClient.add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
          200,
          undefined,
          JSON.stringify({
            sdkAutomationBaseBranch: 'fake-ref'
          })
        );
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger,
          httpClient
        });
        let value = 0;
        const event: SpecificationPREvent | undefined = {
          workingPrefix: container,
          logger,
          webhookBody: {
            action: 'opened',
            number: 50,
            pull_request: testSpecificationPullRequest
          }
        };
        const result: number | undefined = await sdkAutomation.handleEvent(event, () => {
          ++value;
          return value + 5;
        });
        assert.deepEqual(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `No provided Git. Using ExecutableGit instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `Using provided HttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
          `Specification repository configuration response status code: 200`,
          `Ignoring pull request event because the pull request base branch (master) is not equal to the sdkAutomationBaseBranch (fake-ref).`
        ]);
        assert.strictEqual(value, 0);
        assert.strictEqual(result, undefined);
      });

      it('with sdkAutomationBaseBranch property and PR base branch is equal', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const container: BlobStorageContainer = await createTestBlobStorageContainer();
        const httpClient = new FakeHttpClient();
        httpClient.add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/fake-ref/specificationRepositoryConfiguration.json',
          200,
          undefined,
          JSON.stringify({
            sdkAutomationBaseBranch: 'fake-ref'
          })
        );
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger,
          httpClient
        });
        let value = 0;
        const event: SpecificationPREvent | undefined = {
          workingPrefix: container,
          logger,
          webhookBody: {
            action: 'opened',
            number: 50,
            pull_request: {
              ...testSpecificationPullRequest,
              base: {
                label: 'fake-label',
                ref: 'fake-ref',
                sha: 'fake-sha'
              }
            }
          }
        };
        const result: number | undefined = await sdkAutomation.handleEvent(event, () => {
          ++value;
          return value + 5;
        });
        assertEx.containsAll(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `Using provided HttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/fake-ref/specificationRepositoryConfiguration.json"...`,
          `Specification repository configuration response status code: 200`,
          `Processing pull request event because the pull request base branch (fake-ref) is equal to the sdkAutomationBaseBranch (fake-ref).`,
          `Updating logger...`,
          `Using FakeGitHub client.`
        ]);
        assert.strictEqual(value, 1);
        assert.strictEqual(result, 6);
      });

      it('with no sdkAutomationBaseBranch property and PR base branch is not master', async function() {
        const logger: InMemoryLogger = getInMemoryLogger();
        const container: BlobStorageContainer = await createTestBlobStorageContainer();
        const httpClient = new FakeHttpClient();
        httpClient.add(
          'GET',
          'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/fake-ref/specificationRepositoryConfiguration.json',
          200,
          undefined,
          JSON.stringify({})
        );
        const sdkAutomation = new SDKAutomation('fake-working-folder-path', {
          logger,
          httpClient
        });
        let value = 0;
        const event: SpecificationPREvent | undefined = {
          workingPrefix: container,
          logger,
          webhookBody: {
            action: 'opened',
            number: 50,
            pull_request: {
              ...testSpecificationPullRequest,
              base: {
                label: 'fake-label',
                ref: 'fake-ref',
                sha: 'fake-sha'
              }
            }
          }
        };
        const result: number | undefined = await sdkAutomation.handleEvent(event, () => {
          ++value;
          return value + 5;
        });
        assert.deepEqual(logger.allLogs, [
          `No provided GitHub. Using FakeGitHub instance.`,
          `No provided Git. Using ExecutableGit instance.`,
          `Using supported languages: [".NET","Go","Java","JavaScript","Python","Ruby"]`,
          `Using provided HttpClient.`,
          `No BlobProxy provided. Using a FakeBlobProxy.`,
          `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/fake-ref/specificationRepositoryConfiguration.json"...`,
          `Specification repository configuration response status code: 200`,
          `No sdkAutomationBaseBranch property specified in the specificationRepositoryConfiguration.json. Defaulting sdkAutomationBaseBranch to "master".`,
          `Ignoring pull request event because the pull request base branch (fake-ref) is not equal to the sdkAutomationBaseBranch (master).`
        ]);
        assert.strictEqual(value, 0);
        assert.strictEqual(result, undefined);
      });
    });
  });

  describe('getGitHub()', function() {
    it('with no arguments', function() {
      const github: GitHub = getGitHub();
      assertEx.defined(github, 'github');
      assert(github instanceof FakeGitHub);
    });

    it('with undefined', function() {
      const github: GitHub = getGitHub(undefined);
      assertEx.defined(github, 'github');
      assert(github instanceof FakeGitHub);
    });

    it('with null', function() {
      // tslint:disable-next-line:no-null-keyword
      const github: GitHub = getGitHub(null as any);
      assertEx.defined(github, 'github');
      assert(github instanceof FakeGitHub);
    });

    it('with FakeGitHub', function() {
      const github = new FakeGitHub();
      assert.strictEqual(getGitHub(github), github);
    });
  });

  describe('getSupportedLanguages()', function() {
    it('with no arguments', function() {
      assert.deepEqual(getSupportedLanguages(), getAllLanguages());
    });

    it('with undefined', function() {
      assert.deepEqual(getSupportedLanguages(undefined), getAllLanguages());
    });

    it('with null', function() {
      // tslint:disable-next-line:no-null-keyword
      assert.deepEqual(getSupportedLanguages(null as any), getAllLanguages());
    });

    it('with LanguageConfiguration', function() {
      assert.deepEqual(getSupportedLanguages(go), [go]);
    });

    it('with LanguageConfiguration[]', function() {
      assert.deepEqual(getSupportedLanguages([java]), [java]);
    });

    it("with function that doesn't return anything", function() {
      assert.deepEqual(
        getSupportedLanguages(() => {}),
        getAllLanguages()
      );
    });

    it('with function that returns a LanguageConfiguration', function() {
      assert.deepEqual(
        getSupportedLanguages(() => javascript),
        [javascript]
      );
    });

    it('with function that returns a LanguageConfiguration[]', function() {
      assert.deepEqual(
        getSupportedLanguages(() => [go, javascript]),
        [go, javascript]
      );
    });
  });

  describe('getAutomationWorkingFolderPath()', function() {
    it('with no arguments', function() {
      assert.strictEqual(getAutomationWorkingFolderPath(), process.cwd());
    });

    it('with undefined', function() {
      assert.strictEqual(getAutomationWorkingFolderPath(undefined), process.cwd());
    });

    it('with null', function() {
      // tslint:disable-next-line:no-null-keyword
      assert.strictEqual(getAutomationWorkingFolderPath(null as any), process.cwd());
    });

    it(`with ""`, function() {
      assert.strictEqual(getAutomationWorkingFolderPath(''), process.cwd());
    });

    it(`with "apples/and/bananas"`, function() {
      assert.strictEqual(
        getAutomationWorkingFolderPath('apples/and/bananas'),
        joinPath(process.cwd(), 'apples/and/bananas')
      );
    });

    it(`with root path of process.cwd()`, function() {
      const rootPath: string = getRootPath(process.cwd())!;
      assert.strictEqual(getAutomationWorkingFolderPath(rootPath), rootPath);
    });

    it(`with getRootFolderPath()`, function() {
      const rootFolderPath: string = getRootFolderPath();
      assert.strictEqual(getAutomationWorkingFolderPath(rootFolderPath), rootFolderPath);
    });
  });

  describe('getGenerationWorkingFolderPath()', function() {
    it('with no arguments', async function() {
      const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath();
      try {
        assert.strictEqual(generationWorkingFolderPath, joinPath(process.cwd(), '1'));
        assert.strictEqual(await folderExists(generationWorkingFolderPath), true);
      } finally {
        assert.strictEqual(await deleteFolder(generationWorkingFolderPath), true);
      }
    });

    it('with undefined', async function() {
      const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(undefined);
      try {
        assert.strictEqual(generationWorkingFolderPath, joinPath(process.cwd(), '1'));
        assert.strictEqual(await folderExists(generationWorkingFolderPath), true);
      } finally {
        assert.strictEqual(await deleteFolder(generationWorkingFolderPath), true);
      }
    });

    it('with null', async function() {
      // tslint:disable-next-line:no-null-keyword
      const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(null as any);
      try {
        assert.strictEqual(generationWorkingFolderPath, joinPath(process.cwd(), '1'));
        assert.strictEqual(await folderExists(generationWorkingFolderPath), true);
      } finally {
        assert.strictEqual(await deleteFolder(generationWorkingFolderPath), true);
      }
    });

    it(`with ""`, async function() {
      const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath('');
      try {
        assert.strictEqual(generationWorkingFolderPath, joinPath(process.cwd(), '1'));
        assert.strictEqual(await folderExists(generationWorkingFolderPath), true);
      } finally {
        assert.strictEqual(await deleteFolder(generationWorkingFolderPath), true);
      }
    });

    it(`with "oranges"`, async function() {
      const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath('oranges');
      try {
        assert.strictEqual(generationWorkingFolderPath, joinPath(process.cwd(), 'oranges/1'));
        assert.strictEqual(await folderExists(generationWorkingFolderPath), true);
      } finally {
        assert.strictEqual(await deleteFolder('oranges'), true);
      }
    });

    it(`with getRootFolderPath()`, async function() {
      const rootFolderPath: string = getRootFolderPath();
      const generationWorkingFolderPath: string = await getGenerationWorkingFolderPath(rootFolderPath);
      try {
        const rootFolderPathOption: string = joinPath(rootFolderPath, '1');
        const cwdOption: string = joinPath(process.cwd(), '1');
        assert(
          generationWorkingFolderPath === rootFolderPathOption || generationWorkingFolderPath === cwdOption,
          `generationWorkingFolderPath (${generationWorkingFolderPath}) must be either "${rootFolderPathOption}" or "${cwdOption}.`
        );
        assert.strictEqual(await folderExists(generationWorkingFolderPath), true);
      } finally {
        assert.strictEqual(await deleteFolder(generationWorkingFolderPath), true);
      }
    });
  });

  describe('trimNewLine()', function() {
    it('with empty string', function() {
      assert.strictEqual(trimNewLine(''), '');
    });

    it('with non-empty string with no newline', function() {
      assert.strictEqual(trimNewLine('abcd'), 'abcd');
    });

    it('with only \\n', function() {
      assert.strictEqual(trimNewLine('\n'), '');
    });

    it('with only \\r\\n', function() {
      assert.strictEqual(trimNewLine('\r\n'), '');
    });

    it("with 'a\\r\\n'", function() {
      assert.strictEqual(trimNewLine('a\r\n'), 'a');
    });
  });
});
