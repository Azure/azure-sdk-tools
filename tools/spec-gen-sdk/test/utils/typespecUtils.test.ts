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

});
