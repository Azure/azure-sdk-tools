import { getInMemoryLogger, InMemoryLogger } from '@azure/logger-js';
import {
  assertEx,
  HttpClient,
  NodeHttpClient,
  ReadmeMdSwaggerToSDKConfiguration,
  RepositoryConfiguration
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { SpecificationReadmeMdFile } from '../lib/specificationReadmeMdFile';

describe('specificationReadmeMdFile.ts', function() {
  this.timeout(10000);

  it('constructor()', function() {
    const httpClient: HttpClient = new NodeHttpClient();
    const logger: InMemoryLogger = getInMemoryLogger();
    const repository = 'Azure/my-repo';
    const versionRef = 'my/branch/name';
    const relativeFilePath = 'path/to/my/readme.md';
    const specificationReadmeMdFile = new SpecificationReadmeMdFile(
      httpClient,
      logger,
      repository,
      versionRef,
      relativeFilePath
    );
    assert.strictEqual(specificationReadmeMdFile.relativeFilePath, relativeFilePath);
    assert.strictEqual(
      specificationReadmeMdFile.contentsUrl,
      `https://raw.githubusercontent.com/${repository}/${versionRef}/${relativeFilePath}`
    );
    assert.deepEqual(logger.allLogs, []);
  });

  describe('getContents()', function() {
    it("when file doesn't exist", async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/my-repo';
      const versionRef = 'my/branch/name';
      const relativeFilePath = 'path/to/my/readme.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      assert.strictEqual(await specificationReadmeMdFile.getContents(), undefined);
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/my-repo/my/branch/name/path/to/my/readme.md"...`,
        `Merged readme.md response status code is 404.`
      ]);
    });

    it('when file exists', async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/azure-rest-api-specs';
      const versionRef = 'master';
      const relativeFilePath = 'specification/advisor/resource-manager/readme.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      const contents: string = (await specificationReadmeMdFile.getContents())!;
      assertEx.definedAndNotEmpty(contents, 'contents');
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specification/advisor/resource-manager/readme.md"...`,
        `Merged readme.md response status code is 200.`
      ]);
    });
  });

  describe('getSwaggerToSDKConfiguration()', function() {
    it("when file doesn't exist", async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/my-repo';
      const versionRef = 'my/branch/name';
      const relativeFilePath = 'path/to/my/readme.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      assert.strictEqual(await specificationReadmeMdFile.getSwaggerToSDKConfiguration(), undefined);
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/my-repo/my/branch/name/path/to/my/readme.md"...`,
        `Merged readme.md response status code is 404.`,
        `Merged readme.md response body is empty.`
      ]);
    });

    it("when file exists but doesn't have a swagger-to-sdk configuration section", async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/azure-rest-api-specs';
      const versionRef = 'master';
      const relativeFilePath = 'README.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      const swaggerToSDKConfiguration: ReadmeMdSwaggerToSDKConfiguration = (await specificationReadmeMdFile.getSwaggerToSDKConfiguration())!;
      assertEx.defined(swaggerToSDKConfiguration, 'swaggerToSDKConfiguration');
      assert.deepEqual(swaggerToSDKConfiguration.repositories, []);
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/README.md"...`,
        `Merged readme.md response status code is 200.`
      ]);
    });

    it('when file exists and has swagger-to-sdk configuration section', async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/azure-rest-api-specs';
      const versionRef = 'master';
      const relativeFilePath = 'specification/advisor/resource-manager/readme.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      const swaggerToSDKConfiguration: ReadmeMdSwaggerToSDKConfiguration = (await specificationReadmeMdFile.getSwaggerToSDKConfiguration())!;
      assertEx.defined(swaggerToSDKConfiguration, 'swaggerToSDKConfiguration');
      assertEx.defined(swaggerToSDKConfiguration.repositories, 'swaggerToSDKConfiguration.repositories');
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specification/advisor/resource-manager/readme.md"...`,
        `Merged readme.md response status code is 200.`
      ]);
    });
  });

  describe('getSwaggerToSDKRepositoryConfigurations()', function() {
    it("when file doesn't exist", async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/my-repo';
      const versionRef = 'my/branch/name';
      const relativeFilePath = 'path/to/my/readme.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      assert.strictEqual(await specificationReadmeMdFile.getSwaggerToSDKRepositoryConfigurations(), undefined);
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/my-repo/my/branch/name/path/to/my/readme.md"...`,
        `Merged readme.md response status code is 404.`,
        `Merged readme.md response body is empty.`
      ]);
    });

    it("when file exists but doesn't have a swagger-to-sdk configuration section", async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/azure-rest-api-specs';
      const versionRef = 'master';
      const relativeFilePath = 'README.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      const repositoryConfigurations: RepositoryConfiguration[] = (await specificationReadmeMdFile.getSwaggerToSDKRepositoryConfigurations())!;
      assert.deepEqual(repositoryConfigurations, []);
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/README.md"...`,
        `Merged readme.md response status code is 200.`,
        `Found 0 requested SDK repositories:`
      ]);
    });

    it('when file exists and has swagger-to-sdk configuration section', async function() {
      const httpClient: HttpClient = new NodeHttpClient();
      const logger: InMemoryLogger = getInMemoryLogger();
      const repository = 'Azure/azure-rest-api-specs';
      const versionRef = 'master';
      const relativeFilePath = 'specification/advisor/resource-manager/readme.md';
      const specificationReadmeMdFile = new SpecificationReadmeMdFile(
        httpClient,
        logger,
        repository,
        versionRef,
        relativeFilePath
      );
      const repositoryConfigurations: RepositoryConfiguration[] = (await specificationReadmeMdFile.getSwaggerToSDKRepositoryConfigurations())!;
      assertEx.defined(repositoryConfigurations, 'repositoryConfigurations');
      assert.notDeepEqual(repositoryConfigurations, []);
      assert.deepEqual(logger.allLogs, [
        `Getting file contents for "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specification/advisor/resource-manager/readme.md"...`,
        `Merged readme.md response status code is 200.`,
        `Found 6 requested SDK repositories:`,
        `  azure-sdk-for-net`,
        `  azure-sdk-for-python`,
        `  azure-sdk-for-java`,
        `  azure-sdk-for-go`,
        `  azure-sdk-for-js`,
        `  azure-sdk-for-node`
      ]);
    });
  });
});
