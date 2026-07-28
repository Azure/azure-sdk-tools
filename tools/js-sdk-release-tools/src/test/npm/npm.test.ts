import { describe, expect, test, vi } from 'vitest';
import { updatePackageVersion } from '../../mlc/clientGenerator/utils/typeSpecUtils.js';
import { join } from 'path';
import { load } from '@npmcli/package-json';
import { tryGetNpmView } from '../../common/npmUtils.js';

vi.mock('npm-registry-fetch', () => ({
  json: vi.fn((url: string) => {
    if (url === '/connect') {
      return Promise.resolve({ name: 'connect', version: '3.7.0' });
    }
    if (url === '/non-exist') {
      return Promise.reject(new Error('404 Not Found'));
    }
    return Promise.reject(new Error('Not found'));
  }),
}));

describe('Npm package json', () => {
  test('Replace package version', async () => {
    const packageDirectory = join(__dirname, 'testCases');
    await updatePackageVersion(packageDirectory, '2.0.0');
    const packageJson = await load(packageDirectory);
    expect(packageJson.content.version).toBe('2.0.0');
  });
});

describe('Npm view', () => {
  test('View package version', async () => {
    const nonExistResult = await tryGetNpmView('non-exist');
    expect(nonExistResult).toBeUndefined();

    const normalResult = await tryGetNpmView('connect');
    expect(normalResult!['name']).toBe('connect');
  });
});
