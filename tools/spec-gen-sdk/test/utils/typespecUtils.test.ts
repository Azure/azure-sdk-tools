import { describe, it, expect } from 'vitest';
import { findSDKToGenerateFromTypeSpecProject } from '../../src/utils/typespecUtils';
import { SpecConfig } from '../../src/types/SpecConfig';
import path from 'path';
import fs from 'fs';

describe('findSDKToGenerateFromTypeSpecProject', () => {
  const rootPath = process.cwd();
  const specificationRepositoryConfiguration = path.join(rootPath, './test/fixtures/specificationRepositoryConfiguration.json');
  const specificationRepositoryConfigurationContent: SpecConfig = JSON.parse(fs.readFileSync(specificationRepositoryConfiguration).toString());

  it('should return empty array when no content provided', () => {
    const result = findSDKToGenerateFromTypeSpecProject(undefined, specificationRepositoryConfigurationContent);
    expect(result).toEqual([]);
  });

  it('should return empty array when no typespec mapping provided', () => {
    const result = findSDKToGenerateFromTypeSpecProject('content', {} as SpecConfig);
    expect(result).toEqual([]);
  });

  it('should throw error on invalid YAML content', () => {
    const invalidYaml = '{\ninvalid: yaml:\n}';
    expect(() => findSDKToGenerateFromTypeSpecProject(invalidYaml, specificationRepositoryConfigurationContent)).toThrow(/The parsing of the file was unsuccessful/);
  });

  it('should handle empty YAML content', () => {
    const result = findSDKToGenerateFromTypeSpecProject('{}', specificationRepositoryConfigurationContent);
    expect(result).toEqual([]);
  });

  it('should parse emitters under options field', () => {
    const yaml = `
            options:
                "@azure-tools/typespec-csharp":
                    package-dir: "Azure.Compute.Batch"
                    clear-output-folder: true
                    model-namespace: false
                    head-as-boolean: false
                    namespace: Azure.Compute.Batch
                    flavor: azure
        `;
    const result = findSDKToGenerateFromTypeSpecProject(yaml, specificationRepositoryConfigurationContent);
    expect(result).toEqual(['azure-sdk-for-net']);
  });

  it('should parse emitters under emitters field', () => {
    const yaml = `
            emitters:
                "@azure-tools/typespec-ts":
                    package-dir: "batch-rest"
                    package-details:
                      name: "@azure-rest/batch"
                      description: "Batch Service Rest Level Client"
                      version: "1.0.0-beta.1"
                    flavor: azure
        `;
    const result = findSDKToGenerateFromTypeSpecProject(yaml, specificationRepositoryConfigurationContent);
    expect(result).toEqual(['azure-sdk-for-js']);
  });

  it('should filter unmapped emitters', () => {
    const yaml = `
            options:
                '@azure-tools/unmapped-emitter': true
        `;
    const result = findSDKToGenerateFromTypeSpecProject(yaml, specificationRepositoryConfigurationContent);
    expect(result).toEqual([]);
  });

  describe.each([
    ['@azure-tools/typespec-ts', '@azure-tools/typespec-ts-provisioning', 'azure-sdk-for-js'],
    ['@azure-tools/typespec-csharp', '@azure-tools/typespec-csharp-provisioning', 'azure-sdk-for-net'],
  ])('selected emitter %s', (regularEmitter, selectedEmitter, repository) => {
    const specConfig: SpecConfig = {
      ...specificationRepositoryConfigurationContent,
      typespecEmitterToSdkRepositoryMapping: {
        ...specificationRepositoryConfigurationContent.typespecEmitterToSdkRepositoryMapping,
        [selectedEmitter]: repository,
      },
    };

    it.each(['options', 'emitters'])('requires the selected emitter under %s even if the repository is enabled', (field) => {
      const yaml = `${field}:\n  "${regularEmitter}": {}`;
      expect(findSDKToGenerateFromTypeSpecProject(yaml, specConfig, selectedEmitter)).toEqual([]);
    });

    it.each(['options', 'emitters'])('accepts a configured selected emitter under %s', (field) => {
      const yaml = `${field}:\n  "${regularEmitter}": {}\n  "${selectedEmitter}": {}`;
      expect(findSDKToGenerateFromTypeSpecProject(yaml, specConfig, selectedEmitter)).toEqual([repository]);
    });

    it.each(['false', 'null'])('rejects a disabled selected emitter (%s)', (value) => {
      const yaml = `options:\n  "${regularEmitter}": {}\n  "${selectedEmitter}": ${value}`;
      expect(findSDKToGenerateFromTypeSpecProject(yaml, specConfig, selectedEmitter)).toEqual([]);
    });

    it.each([undefined, ''])('preserves repository-level detection for selection %s', (selection) => {
      const yaml = `options:\n  "${regularEmitter}": {}`;
      expect(findSDKToGenerateFromTypeSpecProject(yaml, specConfig, selection)).toEqual([repository]);
    });

    it('does not fall back to legacy emitters when options is present', () => {
      const yaml = `options: {}\nemitters:\n  "${selectedEmitter}": {}`;
      expect(findSDKToGenerateFromTypeSpecProject(yaml, specConfig, selectedEmitter)).toEqual([]);
    });
  });

  it('rejects a configured selected emitter without a repository mapping', () => {
    const emitter = '@azure-tools/unmapped-emitter';
    expect(findSDKToGenerateFromTypeSpecProject(`options:\n  "${emitter}": {}`, specificationRepositoryConfigurationContent, emitter)).toEqual([]);
  });

  it('preserves YAML parsing errors with an explicit emitter', () => {
    expect(() => findSDKToGenerateFromTypeSpecProject('options: [', specificationRepositoryConfigurationContent, '@azure-tools/typespec-ts'))
      .toThrow(/The parsing of the file was unsuccessful/);
  });

});
