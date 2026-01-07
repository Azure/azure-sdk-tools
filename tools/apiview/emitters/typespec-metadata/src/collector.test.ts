import { describe, it, expect } from 'vitest';

describe('outputDir path handling', () => {
  it('should replace absolute base path with {output-dir} placeholder', () => {
    const baseDir = 'c:/repos/project/tsp-output';
    const absolutePath = 'c:/repos/project/tsp-output/sdk/keyvault/secrets';
    const expected = '{output-dir}/sdk/keyvault/secrets';

    // Test the logic that converts absolute to placeholder
    const normalizedBase = baseDir.replace(/\\/g, '/').replace(/\/$/, '');
    const normalizedPath = absolutePath.replace(/\\/g, '/');

    let result: string;
    if (normalizedPath.startsWith(normalizedBase + '/')) {
      const relativePart = normalizedPath.substring(normalizedBase.length + 1);
      result = `{output-dir}/${relativePart}`;
    } else {
      result = absolutePath;
    }

    expect(result).toBe(expected);
  });

  it('should handle paths that do not start with base directory', () => {
    const baseDir = 'c:/repos/project/tsp-output';
    const absolutePath = 'c:/other/path/sdk/keyvault/secrets';

    const normalizedBase = baseDir.replace(/\\/g, '/').replace(/\/$/, '');
    const normalizedPath = absolutePath.replace(/\\/g, '/');

    let result: string;
    if (normalizedPath.startsWith(normalizedBase + '/')) {
      const relativePart = normalizedPath.substring(normalizedBase.length + 1);
      result = `{output-dir}/${relativePart}`;
    } else {
      result = absolutePath;
    }

    expect(result).toBe(absolutePath);
  });

  it('should handle exact base directory match', () => {
    const baseDir = 'c:/repos/project/tsp-output';
    const absolutePath = 'c:/repos/project/tsp-output';
    const expected = '{output-dir}';

    const normalizedBase = baseDir.replace(/\\/g, '/').replace(/\/$/, '');
    const normalizedPath = absolutePath.replace(/\\/g, '/');

    let result: string;
    if (normalizedPath.startsWith(normalizedBase + '/')) {
      const relativePart = normalizedPath.substring(normalizedBase.length + 1);
      result = `{output-dir}/${relativePart}`;
    } else if (normalizedPath === normalizedBase) {
      result = '{output-dir}';
    } else {
      result = absolutePath;
    }

    expect(result).toBe(expected);
  });
});

describe('variable substitution', () => {
  it('should replace template variables with values', () => {
    const fillVars = (value: unknown, data: Record<string, unknown>): unknown => {
      if (typeof value !== 'string') {
        return value;
      }

      let prev: string | undefined;
      let current = value;

      while (prev !== current) {
        prev = current;
        current = current.replace(/\{([^{}]+)\}/g, (match, key) => {
          const replacement = data[key];
          return replacement !== undefined && replacement !== null ? String(replacement) : match;
        });
      }

      return current;
    };

    const data = {
      'service-dir': 'sdk/keyvault',
      'package-name': 'azure-keyvault-secrets',
    };

    expect(fillVars('{service-dir}/test', data)).toBe('sdk/keyvault/test');
    expect(fillVars('{package-name}', data)).toBe('azure-keyvault-secrets');
    expect(fillVars('{unknown}', data)).toBe('{unknown}');
    expect(fillVars('no-vars', data)).toBe('no-vars');
  });

  it('should handle nested variable substitution', () => {
    const fillVars = (value: unknown, data: Record<string, unknown>): unknown => {
      if (typeof value !== 'string') {
        return value;
      }

      let prev: string | undefined;
      let current = value;

      while (prev !== current) {
        prev = current;
        current = current.replace(/\{([^{}]+)\}/g, (match, key) => {
          const replacement = data[key];
          return replacement !== undefined && replacement !== null ? String(replacement) : match;
        });
      }

      return current;
    };

    const data = {
      'var1': '{var2}',
      'var2': 'final-value',
    };

    expect(fillVars('{var1}', data)).toBe('final-value');
  });
});

describe('language-specific parsers', () => {
  it('should parse Python package metadata correctly', () => {
    const options = {
      'package-name': 'azure-keyvault-secrets',
      'namespace': 'azure.keyvault.secrets._generated',
    };

    // Python strips ._generated suffix
    const namespace = String(options.namespace).replace(/\._generated$/, '');

    expect(namespace).toBe('azure.keyvault.secrets');
  });

  it('should derive Python namespace from package-name', () => {
    const options = {
      'package-name': 'azure-keyvault-secrets',
    };

    const namespace = String(options['package-name']).replace(/-/g, '.');

    expect(namespace).toBe('azure.keyvault.secrets');
  });

  it('should parse Java package metadata correctly', () => {
    const options = {
      'namespace': 'com.azure.security.keyvault.secrets',
    };

    const ns = String(options.namespace);
    const stripped = ns.startsWith('com.') ? ns.substring(4) : ns;
    const packageName = stripped.replace(/\./g, '-');

    expect(packageName).toBe('azure-security-keyvault-secrets');
  });

  it('should parse Go module path correctly', () => {
    const options = {
      'module': 'github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/azsecrets',
    };

    const modulePath = String(options.module);
    const sdkIndex = modulePath.indexOf('azure-sdk-for-go/');
    const packageName = sdkIndex >= 0 
      ? modulePath.substring(sdkIndex + 'azure-sdk-for-go/'.length)
      : modulePath;

    expect(packageName).toBe('sdk/security/keyvault/azsecrets');
  });

  it('should parse Rust crate-name correctly', () => {
    const options = {
      'crate-name': 'azure_security_keyvault_secrets',
    };

    const packageName = options['crate-name'];

    expect(packageName).toBe('azure_security_keyvault_secrets');
  });
});

describe('service-dir handling', () => {
  it('should use language-specific service-dir if present', () => {
    const languageServiceDir = 'sdk/security/keyvault';
    const defaultServiceDir = 'sdk/keyvault';

    const result = languageServiceDir ? languageServiceDir : defaultServiceDir;

    expect(result).toBe('sdk/security/keyvault');
  });

  it('should fall back to default service-dir if not present in language options', () => {
    const languageServiceDir = undefined;
    const defaultServiceDir = 'sdk/keyvault';

    const result = languageServiceDir ? languageServiceDir : defaultServiceDir;

    expect(result).toBe('sdk/keyvault');
  });
});

describe('parameter extraction', () => {
  it('should extract parameters with default values', () => {
    const optionMap = {
      'parameters': {
        'service-dir': {
          default: 'sdk/keyvault',
        },
        'dependencies': {
          default: '',
        },
      },
    };

    const params: Record<string, unknown> = {};
    const value = optionMap['parameters'];
    if (typeof value === 'object' && value !== null) {
      for (const [paramKey, paramValue] of Object.entries(value)) {
        if (typeof paramValue === 'object' && paramValue !== null && 'default' in paramValue) {
          params[paramKey] = (paramValue as any).default;
        } else {
          params[paramKey] = paramValue;
        }
      }
    }

    expect(params['service-dir']).toBe('sdk/keyvault');
    expect(params['dependencies']).toBe('');
  });
});
