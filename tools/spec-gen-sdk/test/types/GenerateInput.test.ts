import { describe, it, expect } from 'vitest';
import { GenerateInput, generateInputSchema } from '../../src/types/GenerateInput';
import { getTypeTransformer } from '../../src/types/validator';

describe('GenerateInput contract', () => {
  const validate = getTypeTransformer<GenerateInput>(generateInputSchema, 'GenerateInput');
  const input: GenerateInput = {
    specFolder: 'spec',
    headSha: 'sha',
    repoHttpsUrl: 'https://github.com/Azure/azure-rest-api-specs',
    changedFiles: [],
    runMode: 'local',
    installInstructionInput: {
      isPublic: true,
      downloadUrlPrefix: '',
      downloadCommandTemplate: 'downloadCommand',
    },
  };

  it('accepts existing inputs without typespecEmitter', () => {
    expect(validate({ ...input })).toEqual(input);
  });

  it('accepts an optional emitter package name', () => {
    const withEmitter = { ...input, typespecEmitter: '@azure-tools/typespec-ts-provisioning' };
    expect(validate(withEmitter)).toEqual(withEmitter);
  });

  it('rejects a non-string emitter', () => {
    expect(() => validate({ ...input, typespecEmitter: {} })).toThrow('typespecEmitter');
  });
});
