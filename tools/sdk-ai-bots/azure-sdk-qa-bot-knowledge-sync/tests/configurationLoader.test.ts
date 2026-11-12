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
    expect(docs).toHaveLength(2);
    expect(docs[0].folder).toBe('folder_a');
    expect(docs[0].path).toBe('docs/repo/docs');
    expect(docs[0].citationRoot).toBeUndefined();
    expect(docs[1].folder).toBe('folder_b');
    expect(docs[1].path).toBe('docs/repo');
    expect(docs[1].citationRoot).toBe('docs/repo');
  });

  it('separates configured file and directory paths from their citation root', () => {
    const temp = writeTempConfig({
      sources: [{
        repository: { url: 'https://github.com/org/repo.git', path: 'checkout', branch: 'main' },
        paths: [
          { path: '/documentation', folder: 'docs', relativeByRepoPath: true },
          { path: '/.github/arm-leases/README.md', folder: 'docs', relativeByRepoPath: true },
          { path: '/guides', folder: 'guides' }
        ]
      }]
    });
    ConfigurationLoader.setConfigPath(temp);
    const docs = ConfigurationLoader.getDocumentationSources();
    expect(docs.map(source => source.path)).toEqual([
      'docs/checkout/documentation',
      'docs/checkout/.github/arm-leases/README.md',
      'docs/checkout/guides'
    ]);
    expect(docs.map(source => source.citationRoot)).toEqual([
      'docs/checkout', 'docs/checkout', undefined
    ]);
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
    expect(sources.map(source => source.path)).toEqual(
      mcpSource?.paths.map(entry => `docs/mcp${entry.path}`)
    );
    expect(sources.every(source => source.citationRoot === 'docs/mcp')).toBe(true);
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

  it('syncs migrated REST API specs guides from the repository instead of the wiki', () => {
    const config = ConfigurationLoader.loadConfig();
    const specsSources = config.sources.filter(
      source => source.repository.url === 'https://github.com/Azure/azure-rest-api-specs.git'
    );

    expect(specsSources).toHaveLength(1);
    expect(specsSources[0].repository).toMatchObject({
      branch: 'main',
      authType: 'public',
      issueTracker: { provider: 'github', repository: 'Azure/azure-sdk-pr' }
    });
    expect(specsSources[0].paths).toEqual(expect.arrayContaining([
      expect.objectContaining({
        path: '/documentation',
        folder: 'azure_rest_api_specs_docs',
        relativeByRepoPath: true,
        metadata: { scope: 'branded' }
      }),
      expect.objectContaining({ path: '/eng/tools/typespec-suppressions/README.md' }),
      expect.objectContaining({ path: '/.github/arm-leases/README.md' })
    ]));
    expect(specsSources[0].paths).toHaveLength(3);
    expect(config.sources.some(
      source => source.repository.url === 'https://github.com/Azure/azure-rest-api-specs.wiki.git'
    )).toBe(false);
    expect(config.sources.flatMap(source => source.paths).some(
      docPath => docPath.folder === 'azure_rest_api_specs_wiki'
    )).toBe(false);

    const repository = ConfigurationLoader.getRepositoryConfigs().find(
      repo => repo.url === specsSources[0].repository.url
    );
    expect(repository?.sparseCheckout).toContain('/documentation');
    const sources = ConfigurationLoader.getDocumentationSources().filter(
      source => source.folder === 'azure_rest_api_specs_docs'
    );
    expect(sources).toHaveLength(3);
    expect(sources.map(source => source.path)).toEqual([
      'docs/azure-rest-api-specs/documentation',
      'docs/azure-rest-api-specs/eng/tools/typespec-suppressions/README.md',
      'docs/azure-rest-api-specs/.github/arm-leases/README.md'
    ]);
    expect(sources.every(source => source.citationRoot === 'docs/azure-rest-api-specs')).toBe(true);
    expect(sources.map(source => source.ignoredPaths)).toEqual([
      [
        'Getting started with OpenAPI specifications.md',
        'samplefiles/',
        'samplefiles-dp/',
        'api-scenario/',
        'restler/',
        'backlog-triage.md',
        'api-reviewer-agent.md'
      ],
      undefined,
      undefined
    ]);
  });

  afterAll(() => {
    for (const dir of tempDirs) {
      try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* ignore */ }
    }
  });
});
