import { describe, it, expect, beforeEach, afterAll } from 'vitest';
import { ConfigurationLoader } from '../src/services/ConfigurationLoader';
import * as fs from 'fs';
import * as path from 'path';

const originalPath = ConfigurationLoader.getConfigPath();

const tempDirs: string[] = [];

function writeTempConfig(json: any): string {
  const tempDir = fs.mkdtempSync(path.join(process.cwd(), 'cfg-'));
  tempDirs.push(tempDir);
  const file = path.join(tempDir, 'knowledge-config.json');
  fs.writeFileSync(file, JSON.stringify(json, null, 2));
  return file;
}

describe('ConfigurationLoader', () => {
  beforeEach(() => {
    // reset internal cached config between tests
    ConfigurationLoader.setConfigPath(originalPath); // ensures next load uses real path unless overridden
  });

  it('loads config and exposes sources', () => {
    const testConfig = {
      version: '1.0.0',
      sources: [
        {
          repository: { url: 'https://github.com/org/repo.git', branch: 'main', authType: 'public' },
          paths: [
            { description: 'Docs A', path: 'docs', folder: 'folder_a' },
            { description: 'Docs B', folder: 'folder_b', relativeByRepoPath: true }
          ]
        }
      ]
    };
    const temp = writeTempConfig(testConfig);
    ConfigurationLoader.setConfigPath(temp);

    const docs = ConfigurationLoader.getDocumentationSources();
    // relativeByRepoPath true or path undefined uses docs/<repoName>
    expect(docs).toHaveLength(2);
    expect(docs[0].folder).toBe('folder_a');
    expect(docs[0].path).toContain('docs/repo');
    expect(docs[1].folder).toBe('folder_b');
  });

  it('transforms repository configs including sparse checkout', () => {
    const testConfig = {
      version: '1.0.0',
      sources: [
        {
          repository: { url: 'https://github.com/org/another.git', branch: 'dev', authType: 'public' },
          paths: [
            { description: 'Part1', path: 'docs/part1', folder: 'f1' },
            { description: 'Part2', path: 'docs/part2', folder: 'f2' }
          ]
        }
      ]
    };
    const temp = writeTempConfig(testConfig);
    ConfigurationLoader.setConfigPath(temp);

    const repos = ConfigurationLoader.getRepositoryConfigs();
    expect(repos).toHaveLength(1);
    expect(repos[0].name).toBe('another');
    expect(repos[0].sparseCheckout).toEqual(['docs/part1', 'docs/part2']);
  });

  it('throws when file missing', () => {
    const missing = path.join(process.cwd(), 'definitely-missing-config.json');
    ConfigurationLoader.setConfigPath(missing);
    expect(() => ConfigurationLoader.getDocumentationSources()).toThrow();
  });
  afterAll(() => {
    for (const dir of tempDirs) {
      try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* ignore */ }
    }
  });
});
