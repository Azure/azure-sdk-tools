import { beforeEach, describe, expect, test, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  runCommand: vi.fn(),
  info: vi.fn(),
  warn: vi.fn(),
}));

vi.mock('../../common/utils.js', () => ({
  runCommand: mocks.runCommand,
  runCommandOptions: { shell: true },
}));

vi.mock('../../utils/logger.js', () => ({
  logger: {
    info: mocks.info,
    warn: mocks.warn,
  },
}));

import { customizePackage } from '../../common/devToolUtils.js';

describe('package customization', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  test('runs the package customize script from the package directory', async () => {
    mocks.runCommand.mockResolvedValue({ stdout: 'skipped', stderr: '', code: 0 });

    await expect(customizePackage('/sdk/service/package')).resolves.toBeUndefined();

    expect(mocks.runCommand).toHaveBeenCalledOnce();
    expect(mocks.runCommand).toHaveBeenCalledWith(
      'npm',
      ['run', 'customize'],
      { shell: true, cwd: '/sdk/service/package' },
      true,
      600,
      true
    );
    expect(mocks.info).toHaveBeenCalledWith('Package customization completed successfully.');
  });

  test('reports a customization failure and continues generation', async () => {
    const error = new Error('custom merge conflict');
    mocks.runCommand.mockRejectedValue(error);

    await expect(customizePackage('/sdk/service/package')).resolves.toBeUndefined();

    expect(mocks.warn).toHaveBeenCalledOnce();
    expect(mocks.warn).toHaveBeenCalledWith(
      expect.stringContaining("Package customization failed in '/sdk/service/package'")
    );
    expect(mocks.warn).toHaveBeenCalledWith(expect.stringContaining('Generation will continue'));
    expect(mocks.warn).toHaveBeenCalledWith(expect.stringContaining('custom merge conflict'));
  });
});
