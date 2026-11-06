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

  it('keeps repository-relative citation roots but selects only the configured path', () => {
    const temp = writeTempConfig({
      version: '1.0.0',
      sources: [{
        repository: { url: 'https://github.com/org/repo.git', branch: 'main', authType: 'public' },
        paths: [
          { description: 'Docs', path: '/doc', folder: 'docs', relativeByRepoPath: true },
          { description: 'Readme', path: '/eng/tool/README.md', folder: 'docs', relativeByRepoPath: true },
          { description: 'Entire repo', folder: 'whole_repo', relativeByRepoPath: true }
        ]
      }]
    });
    ConfigurationLoader.setConfigPath(temp);

    const docs = ConfigurationLoader.getDocumentationSources();
    expect(docs.map(source => source.path)).toEqual(['docs/repo', 'docs/repo', 'docs/repo']);
    expect(docs.map(source => source.includePath)).toEqual(['doc', 'eng/tool/README.md', undefined]);
    expect(ConfigurationLoader.getRepositoryConfigs()[0].sparseCheckout).toEqual([
      '/doc', '/eng/tool/README.md'
    ]);
  });

  it('throws when file missing', () => {
    const missing = path.join(process.cwd(), 'definitely-missing-config.json');
    ConfigurationLoader.setConfigPath(missing);
    expect(() => ConfigurationLoader.getDocumentationSources()).toThrow();
  });

  it('syncs Azure MCP contribution and evaluator guides with repository-relative citation paths', () => {
    const expectedPaths = [
      '/CONTRIBUTING.md',
      '/.github/skills/add-azure-mcp-tools/SKILL.md',
      '/servers/Azure.Mcp.Server/docs/new-command.md',
      '/eng/tools/ToolDescriptionEvaluator/Quickstart.md',
      '/eng/tools/ToolDescriptionEvaluator/README.md'
    ];
    const config = ConfigurationLoader.loadConfig();
    const mcpSource = config.sources.find(
      source => source.repository.url === 'https://github.com/microsoft/mcp.git'
    );

    expect(mcpSource?.paths).toEqual(expect.arrayContaining(expectedPaths.map(path =>
      expect.objectContaining({
        path,
        folder: 'azure_mcp_server_docs',
        relativeByRepoPath: true
      })
    )));
    const repository = ConfigurationLoader.getRepositoryConfigs().find(
      repo => repo.url === 'https://github.com/microsoft/mcp.git'
    );
    expect(repository?.sparseCheckout).toEqual(expect.arrayContaining(expectedPaths));
    const sources = ConfigurationLoader.getDocumentationSources().filter(
      source => source.folder === 'azure_mcp_server_docs'
    );
    expect(sources.length).toBeGreaterThan(0);
    expect(sources.every(source => source.path === 'docs/mcp')).toBe(true);
  });

  it('uses repository documentation and contributing guidance instead of the JS wiki', () => {
    const config = ConfigurationLoader.loadConfig();
    const jsSource = config.sources.find(
      source => source.repository.url === 'https://github.com/Azure/azure-sdk-for-js.git'
    );

    expect(jsSource).toBeDefined();
    expect(jsSource?.paths).toEqual(expect.arrayContaining([
      expect.objectContaining({
        path: '/documentation',
        folder: 'azure_sdk_for_js_docs',
        relativeByRepoPath: true
      }),
      expect.objectContaining({
        path: '/CONTRIBUTING.md',
        folder: 'azure_sdk_for_js_docs',
        relativeByRepoPath: true,
        metadata: { scope: 'branded' }
      }),
      expect.objectContaining({
        path: '/eng/tools/js-sdk-release-tools/docs/automation-pipeline.md',
        folder: 'azure_sdk_for_js_docs',
        relativeByRepoPath: true
      })
    ]));
    expect(config.sources.some(
      source => source.repository.url === 'https://github.com/Azure/azure-sdk-for-js.wiki.git'
    )).toBe(false);
    expect(config.sources.flatMap(source => source.paths).some(
      docPath => docPath.folder === 'azure_sdk_for_js_wiki'
    )).toBe(false);
  });

  afterAll(() => {
    ConfigurationLoader.setConfigPath(originalPath);
    for (const dir of tempDirs) {
      try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* ignore */ }
    }
  });
});
