import { describe, it, expect } from 'vitest';
import type { MetadataSnapshot, LanguagePackageMetadata, SpecMetadata } from './metadata.js';

describe('MetadataSnapshot structure', () => {
  it('should have required top-level fields', () => {
    const snapshot: MetadataSnapshot = {
      emitterVersion: '0.1.0',
      generatedAt: new Date().toISOString(),
      spec: {
        namespaces: [],
      },
      languages: {},
    };

    expect(snapshot).toHaveProperty('emitterVersion');
    expect(snapshot).toHaveProperty('generatedAt');
    expect(snapshot).toHaveProperty('spec');
    expect(snapshot).toHaveProperty('languages');
  });

  it('should format generatedAt as ISO timestamp', () => {
    const snapshot: MetadataSnapshot = {
      emitterVersion: '0.1.0',
      generatedAt: new Date().toISOString(),
      spec: {
        namespaces: [],
      },
      languages: {},
    };

    expect(snapshot.generatedAt).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/);
  });

  it('should have sourceConfigPath when available', () => {
    const snapshot: MetadataSnapshot = {
      emitterVersion: '0.1.0',
      generatedAt: new Date().toISOString(),
      spec: {
        namespaces: [],
      },
      languages: {},
      sourceConfigPath: 'C:/path/to/tspconfig.yaml',
    };

    expect(snapshot.sourceConfigPath).toBe('C:/path/to/tspconfig.yaml');
  });
});

describe('SpecMetadata structure', () => {
  it('should contain namespaces array', () => {
    const spec: SpecMetadata = {
      namespaces: [
        {
          name: 'MyService',
        },
      ],
    };

    expect(spec.namespaces).toHaveLength(1);
    expect(spec.namespaces[0].name).toBe('MyService');
  });

  it('should support optional summary and documentation', () => {
    const spec: SpecMetadata = {
      namespaces: [
        {
          name: 'MyService',
          summary: 'My service summary',
          documentation: 'My service documentation',
        },
      ],
      summary: 'Overall summary',
    };

    expect(spec.namespaces[0].summary).toBe('My service summary');
    expect(spec.namespaces[0].documentation).toBe('My service documentation');
    expect(spec.summary).toBe('Overall summary');
  });
});

describe('LanguagePackageMetadata structure', () => {
  it('should use language name as dictionary key', () => {
    const languages: Record<string, LanguagePackageMetadata> = {
      python: {
        emitterName: '@azure-tools/typespec-python',
        packageName: 'azure-keyvault-secrets',
        namespace: 'azure.keyvault.secrets',
        outputDir: '{output-dir}/sdk/keyvault/azure-keyvault-secrets',
        flavor: 'azure',
        serviceDir: 'sdk/keyvault',
      },
    };

    expect(languages).toHaveProperty('python');
    expect(languages.python.packageName).toBe('azure-keyvault-secrets');
  });

  it('should support multiple languages', () => {
    const languages: Record<string, LanguagePackageMetadata> = {
      python: {
        emitterName: '@azure-tools/typespec-python',
        packageName: 'azure-keyvault-secrets',
        namespace: 'azure.keyvault.secrets',
        outputDir: '{output-dir}/sdk/keyvault/azure-keyvault-secrets',
        flavor: 'azure',
        serviceDir: 'sdk/keyvault',
      },
      java: {
        emitterName: '@azure-tools/typespec-java',
        packageName: 'azure-security-keyvault-secrets',
        namespace: 'com.azure.security.keyvault.secrets',
        outputDir: '{output-dir}/sdk/keyvault/azure-security-keyvault-secrets',
        flavor: 'azure',
        serviceDir: 'sdk/keyvault',
      },
    };

    expect(Object.keys(languages)).toHaveLength(2);
    expect(languages).toHaveProperty('python');
    expect(languages).toHaveProperty('java');
  });

  it('should use {output-dir} placeholder in outputDir', () => {
    const lang: LanguagePackageMetadata = {
      emitterName: '@azure-tools/typespec-python',
      packageName: 'azure-keyvault-secrets',
      outputDir: '{output-dir}/sdk/keyvault/azure-keyvault-secrets',
    };

    expect(lang.outputDir).toContain('{output-dir}');
    expect(lang.outputDir).not.toContain('c:/');
    expect(lang.outputDir).not.toContain('C:/');
  });

  it('should support optional fields', () => {
    const minimal: LanguagePackageMetadata = {
      emitterName: '@azure-tools/typespec-python',
    };

    expect(minimal.emitterName).toBe('@azure-tools/typespec-python');
    expect(minimal.packageName).toBeUndefined();
    expect(minimal.namespace).toBeUndefined();
    expect(minimal.outputDir).toBeUndefined();
    expect(minimal.flavor).toBeUndefined();
    expect(minimal.serviceDir).toBeUndefined();
  });

  it('should support language-specific service-dir', () => {
    const languages: Record<string, LanguagePackageMetadata> = {
      go: {
        emitterName: '@azure-tools/typespec-go',
        packageName: 'sdk/security/keyvault/azsecrets',
        namespace: 'sdk/security/keyvault/azsecrets',
        outputDir: '{output-dir}/sdk/security/keyvault/azsecrets',
        serviceDir: 'sdk/security/keyvault', // Different from other languages
      },
      python: {
        emitterName: '@azure-tools/typespec-python',
        packageName: 'azure-keyvault-secrets',
        namespace: 'azure.keyvault.secrets',
        outputDir: '{output-dir}/sdk/keyvault/azure-keyvault-secrets',
        serviceDir: 'sdk/keyvault', // Default service-dir
      },
    };

    expect(languages.go.serviceDir).toBe('sdk/security/keyvault');
    expect(languages.python.serviceDir).toBe('sdk/keyvault');
  });
});

describe('Complete snapshot example', () => {
  it('should create a valid complete snapshot', () => {
    const snapshot: MetadataSnapshot = {
      emitterVersion: '0.1.0',
      generatedAt: '2026-01-07T18:00:00.000Z',
      spec: {
        namespaces: [
          {
            name: 'KeyVault',
            documentation: 'The key vault client performs cryptographic key operations and vault operations against the Key Vault service.',
          },
        ],
      },
      languages: {
        python: {
          emitterName: '@azure-tools/typespec-python',
          packageName: 'azure-keyvault-secrets',
          namespace: 'azure.keyvault.secrets',
          outputDir: '{output-dir}/sdk/keyvault/azure-keyvault-secrets',
          flavor: 'azure',
          serviceDir: 'sdk/keyvault',
        },
        java: {
          emitterName: '@azure-tools/typespec-java',
          packageName: 'azure-security-keyvault-secrets',
          namespace: 'com.azure.security.keyvault.secrets',
          outputDir: '{output-dir}/sdk/keyvault/azure-security-keyvault-secrets',
          flavor: 'azure',
          serviceDir: 'sdk/keyvault',
        },
        typescript: {
          emitterName: '@azure-tools/typespec-ts',
          packageName: '@azure/keyvault-secrets',
          namespace: '@azure/keyvault-secrets',
          outputDir: '{output-dir}/sdk/keyvault/keyvault-secrets',
          flavor: 'azure',
          serviceDir: 'sdk/keyvault',
        },
      },
      sourceConfigPath: 'C:/repos/azure-rest-api-specs/specification/keyvault/Security.KeyVault.Secrets/tspconfig.yaml',
    };

    // Validate structure
    expect(snapshot.emitterVersion).toBeTruthy();
    expect(snapshot.generatedAt).toBeTruthy();
    expect(snapshot.spec.namespaces).toHaveLength(1);
    expect(Object.keys(snapshot.languages)).toHaveLength(3);
    expect(snapshot.sourceConfigPath).toBeTruthy();

    // Validate no absolute paths in output directories
    Object.values(snapshot.languages).forEach(lang => {
      if (lang.outputDir) {
        expect(lang.outputDir).toContain('{output-dir}');
      }
    });
  });
});
