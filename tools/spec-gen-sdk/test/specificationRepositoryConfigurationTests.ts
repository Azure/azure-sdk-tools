import { getInMemoryLogger, InMemoryLogger } from '@azure/logger-js';
import { FakeHttpClient } from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import {
  getSpecificationRepositoryConfiguration,
  SpecificationRepositoryConfiguration
} from '../lib/specificationRepositoryConfiguration';
import { createTestHttpClient } from './test';

describe('specificationRepositoryConfiguration.ts', () => {
  describe('getSpecificationRepositoryConfiguration()', () => {
    it("when specificationRepositoryConfiguration.json file doesn't exists", async () => {
      const logger: InMemoryLogger = getInMemoryLogger();
      const httpClient: FakeHttpClient = createTestHttpClient(false) as FakeHttpClient;
      httpClient.add(
        'GET',
        'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
        404
      );

      const specificationRepositoryConfiguration:
        | SpecificationRepositoryConfiguration
        | undefined = await getSpecificationRepositoryConfiguration(
        httpClient,
        'Azure/azure-rest-api-specs',
        'master',
        logger
      );
      assert.strictEqual(specificationRepositoryConfiguration, undefined);
      assert.deepEqual(logger.allLogs, [
        `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
        `Specification repository configuration response status code: 404`
      ]);
    });

    it('when specificationRepositoryConfiguration.json file exists but is empty', async () => {
      const logger: InMemoryLogger = getInMemoryLogger();
      const httpClient: FakeHttpClient = createTestHttpClient(false) as FakeHttpClient;
      httpClient.add(
        'GET',
        'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
        200,
        undefined,
        ''
      );
      const specificationRepositoryConfiguration:
        | SpecificationRepositoryConfiguration
        | undefined = await getSpecificationRepositoryConfiguration(
        httpClient,
        'Azure/azure-rest-api-specs',
        'master',
        logger
      );

      assert.strictEqual(specificationRepositoryConfiguration, undefined);
      assert.deepEqual(logger.allLogs, [
        `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
        `Specification repository configuration response status code: 200`,
        `Specification repository configuration file exists, but it has no content.`
      ]);
    });

    it('when specificationRepositoryConfiguration.json file exists but has non-JSON content', async () => {
      const logger: InMemoryLogger = getInMemoryLogger();
      const httpClient: FakeHttpClient = createTestHttpClient(false) as FakeHttpClient;
      httpClient.add(
        'GET',
        'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
        200,
        undefined,
        'hello!'
      );
      const specificationRepositoryConfiguration:
        | SpecificationRepositoryConfiguration
        | undefined = await getSpecificationRepositoryConfiguration(
        httpClient,
        'Azure/azure-rest-api-specs',
        'master',
        logger
      );

      assert.strictEqual(specificationRepositoryConfiguration, undefined);
      assert.deepEqual(logger.allLogs, [
        `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
        `Specification repository configuration response status code: 200`,
        `Failed to parse the specification repository configuration file's contents.`
      ]);
    });

    it(`when specificationRepositoryConfiguration.json file exists with {}`, async () => {
      const logger: InMemoryLogger = getInMemoryLogger();
      const httpClient: FakeHttpClient = createTestHttpClient(false) as FakeHttpClient;
      httpClient.add(
        'GET',
        'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
        200,
        undefined,
        JSON.stringify({})
      );
      const specificationRepositoryConfiguration:
        | SpecificationRepositoryConfiguration
        | undefined = await getSpecificationRepositoryConfiguration(
        httpClient,
        'Azure/azure-rest-api-specs',
        'master',
        logger
      );

      assert.deepEqual(specificationRepositoryConfiguration, {});
      assert.deepEqual(logger.allLogs, [
        `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
        `Specification repository configuration response status code: 200`
      ]);
    });

    it(`when specificationRepositoryConfiguration.json file exists with { sdkRepositoryMappings: {} }`, async () => {
      const logger: InMemoryLogger = getInMemoryLogger();
      const httpClient: FakeHttpClient = createTestHttpClient(false) as FakeHttpClient;
      httpClient.add(
        'GET',
        'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
        200,
        undefined,
        JSON.stringify({
          sdkRepositoryMappings: {}
        })
      );
      const specificationRepositoryConfiguration:
        | SpecificationRepositoryConfiguration
        | undefined = await getSpecificationRepositoryConfiguration(
        httpClient,
        'Azure/azure-rest-api-specs',
        'master',
        logger
      );

      assert.deepEqual(specificationRepositoryConfiguration, {
        sdkRepositoryMappings: {}
      });
      assert.deepEqual(logger.allLogs, [
        `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
        `Specification repository configuration response status code: 200`
      ]);
    });

    it(`when specificationRepositoryConfiguration.json file exists with { sdkRepositoryMappings: { "azure-sdk-for-js": "Azure/azure-sdk-for-js" } }`, async () => {
      const logger: InMemoryLogger = getInMemoryLogger();
      const httpClient: FakeHttpClient = createTestHttpClient(false) as FakeHttpClient;
      httpClient.add(
        'GET',
        'https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json',
        200,
        undefined,
        JSON.stringify({
          sdkRepositoryMappings: {
            'azure-sdk-for-js': 'Azure/azure-sdk-for-js'
          }
        })
      );
      const specificationRepositoryConfiguration:
        | SpecificationRepositoryConfiguration
        | undefined = await getSpecificationRepositoryConfiguration(
        httpClient,
        'Azure/azure-rest-api-specs',
        'master',
        logger
      );

      assert.deepEqual(specificationRepositoryConfiguration, {
        sdkRepositoryMappings: {
          'azure-sdk-for-js': 'Azure/azure-sdk-for-js'
        }
      });
      assert.deepEqual(logger.allLogs, [
        `Getting specification repository configuration from "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/master/specificationRepositoryConfiguration.json"...`,
        `Specification repository configuration response status code: 200`
      ]);
    });
  });
});
