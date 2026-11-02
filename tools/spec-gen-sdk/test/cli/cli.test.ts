import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { sdkAutoMain } from '../../src/automation/entrypoint';

const cli = vi.hoisted(() => ({ args: [] as string[] }));
vi.mock('yargs/helpers', () => ({ hideBin: () => cli.args }));
vi.mock('../../src/automation/entrypoint', () => ({
  sdkAutoMain: vi.fn().mockResolvedValue('succeeded'),
}));

describe('typespec-emitter CLI option', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    vi.spyOn(process, 'chdir').mockImplementation(() => {});
    vi.spyOn(process, 'exit').mockImplementation(vi.fn());
    vi.spyOn(console, 'log').mockImplementation(() => {});
    cli.args = [
      '--local-spec-repo-path', 'spec',
      '--local-sdk-repo-path', 'sdk',
      '--sdk-repo-name', 'azure-sdk-for-js',
      '--spec-commit-sha', 'sha',
      '--tsp-config-relative-path', 'project/tspconfig.yaml',
    ];
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it.each(['local', 'spec-pull-request', 'release', 'batch'])('preserves existing OpenAPI CLI calls in %s mode', async (runMode) => {
    cli.args.splice(-2);
    cli.args.push('--readme-relative-path', 'project/readme.md', '--run-mode', runMode);

    await import('../../src/cli/cli');

    await vi.waitFor(() => expect(process.exit).toHaveBeenCalledWith(0));
    expect(sdkAutoMain).toHaveBeenCalledWith(expect.objectContaining({
      typespecEmitter: undefined,
      tspConfigPath: undefined,
      readmePath: 'project/readme.md',
      runMode,
      runEnv: runMode === 'local' ? 'local' : 'azureDevOps',
    }));
  });

  it.each([
    '@azure-tools/typespec-ts-provisioning',
    '@azure-tools/typespec-ts',
    undefined,
  ])('forwards optional emitter %s into workflow options', async (typespecEmitter) => {
    if (typespecEmitter !== undefined) {
      cli.args.push('--typespec-emitter', typespecEmitter);
    }

    await import('../../src/cli/cli');

    await vi.waitFor(() => expect(process.exit).toHaveBeenCalledWith(0));
    expect(sdkAutoMain).toHaveBeenCalledWith(expect.objectContaining({
      typespecEmitter,
      tspConfigPath: 'project/tspconfig.yaml',
      sdkName: 'azure-sdk-for-js',
    }));
  });
});
