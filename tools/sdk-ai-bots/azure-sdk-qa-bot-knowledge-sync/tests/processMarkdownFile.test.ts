import { describe, it, expect, afterAll, vi } from 'vitest';
import { processMarkdownFile, processSourceDirectory } from '../src/DailySyncKnowledge';
import { ConfigurationLoader } from '../src/services/ConfigurationLoader';
import { BlobService } from '../src/services/StorageService';
import * as fs from 'fs';
import * as path from 'path';

const tmpDirs: string[] = [];

vi.mock('../src/services/StorageService', () => ({
  BlobService: class {
    hasContentChanged() { return true; }
    hasMetadataChanged() { return false; }
  }
}));

describe('processMarkdownFile', () => {
  it('generates filename from path when frontmatter missing', () => {
  const tempDir = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
  tmpDirs.push(tempDir);
    const filePath = path.join(tempDir, 'nested', 'file.md');
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, 'Body');

    const source = { path: '', folder: 'folder' } as any;
    const processed = processMarkdownFile(filePath, source, tempDir);
    expect(processed.filename).toBe('nested#file.md');
    expect(processed.blobPath).toBe('folder/nested#file.md');
    expect(processed.content).toContain('Body');
  });

  it('lowercases filename when fileNameLowerCase flag set', () => {
  const tempDir = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
  tmpDirs.push(tempDir);
    const filePath = path.join(tempDir, 'Name.md');
    fs.writeFileSync(filePath, '---\ntitle: Title\npermalink: MyFile\n---\n');
    const source = { path: '', folder: 'folder', fileNameLowerCase: true } as any;
    const processed = processMarkdownFile(filePath, source, tempDir);
    expect(processed.blobPath).toBe('folder/myfile');
  });

  it('processes only configured specs paths and preserves repository-relative citations', async () => {
    const root = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(root);
    const included = [
      'documentation/contributing/typespec-development-process.md',
      'documentation/specification-guidelines/azure-service-versioning-guide.md',
      'documentation/validation/sdk-breaking-changes.md',
      'documentation/validation/sdk-validation.md',
      'documentation/ci-fix.md',
      'documentation/sdkautomation/README.md',
      'documentation/openapi-authoring-automated-guidelines.md',
      'documentation/Semantic-and-Model-Violations-Reference.md',
      'documentation/validation/swagger-breaking-change.md',
      'documentation/x-ms-examples.md',
      'eng/tools/typespec-suppressions/README.md',
      '.github/arm-leases/README.md'
    ];
    const excluded = [
      'documentation/Getting started with OpenAPI specifications.md',
      'documentation/samplefiles/readme.csharp.md',
      'documentation/samplefiles-dp/samplefiles-dp-for-single-client/README.md',
      'documentation/api-scenario/how-to/QuickStart.md',
      'documentation/restler/QuickStart.md',
      'documentation/backlog-triage.md',
      'documentation/api-reviewer-agent.md',
      '.github/arm-leases/maintenance.md',
      'eng/tools/typespec-suppressions/notes.md',
      'README.md'
    ];
    for (const relativePath of [...included, ...excluded]) {
      const file = path.join(root, relativePath);
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.writeFileSync(file, '# Repository guidance\n\nContent.');
    }
    const target = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(target);
    const sources = ConfigurationLoader.getDocumentationSources()
      .filter(source => source.folder === 'azure_rest_api_specs_docs');
    expect(sources).toHaveLength(3);
    const filenames: string[] = [];
    for (const source of sources) {
      const sourceDir = path.join(root, path.relative('docs/azure-rest-api-specs', source.path));
      const result = await processSourceDirectory(
        sourceDir, source, target, new Map(), new BlobService(), root
      );
      const selectedPath = path.relative(root, sourceDir).replace(/\\/g, '/');
      const expected = included.filter(file => file === selectedPath || file.startsWith(`${selectedPath}/`));
      expect(result.changedFiles.map(file => file.filename).sort()).toEqual(
        expected.map(file => file.replace(/\//g, '#')).sort()
      );
      expect(result.totalProcessed).toBe(expected.length);
      filenames.push(...result.changedFiles.map(file => file.filename));
    }
    expect(filenames.sort()).toEqual(included.map(file => file.replace(/\//g, '#')).sort());
    expect(new Set(filenames).size).toBe(included.length);
  });

  it('preserves directory-relative and single-file filenames without a citation root', async () => {
    const root = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(root);
    const docs = path.join(root, 'documentation');
    fs.mkdirSync(docs);
    const file = path.join(docs, 'guide.md');
    fs.writeFileSync(file, '# Guide\n\nContent.');
    const target = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(target);
    const source = { path: docs, folder: 'folder' };
    for (const sourceDir of [docs, file]) {
      const result = await processSourceDirectory(sourceDir, source, target, new Map(), new BlobService());
      expect(result.changedFiles.map(file => file.filename)).toEqual(['guide.md']);
    }
  });

  it('uses the citation root for metadata overrides while restricting the scan', async () => {
    const root = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(root);
    const docs = path.join(root, 'documentation');
    fs.mkdirSync(docs);
    fs.writeFileSync(path.join(docs, 'guide.md'), '# Guide\n\nContent.');
    fs.writeFileSync(path.join(root, 'unselected.md'), '# Other\n\nContent.');
    const target = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(target);
    const result = await processSourceDirectory(
      docs, {
        path: docs,
        folder: 'folder',
        metadata: { scope: 'branded' },
        overrides: [{
          pattern: 'documentation/*.md',
          metadata: { scope: 'branded', service_type: 'management-plane' }
        }]
      },
      target, new Map(), new BlobService(), root
    );
    expect(result.changedFiles).toHaveLength(1);
    expect(result.changedFiles[0].blobPath).toBe('folder/documentation#guide.md');
    expect(result.changedFiles[0].metadata).toEqual({
      scope: 'branded', service_type: 'management-plane'
    });
  });

  it('matches ignored paths with either separator without affecting generated sources', async () => {
    const root = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(root);
    fs.mkdirSync(path.join(root, 'nested'));
    fs.writeFileSync(path.join(root, 'nested', 'guide.md'), '# Excluded\n\nContent.');
    const target = fs.mkdtempSync(path.join(process.cwd(), 'tmp-'));
    tmpDirs.push(target);
    for (const ignoredPath of ['nested/', 'nested\\']) {
      const result = await processSourceDirectory(
        root, { path: root, folder: 'folder', ignoredPaths: [ignoredPath] },
        target, new Map(), new BlobService()
      );
      expect(result.totalProcessed).toBe(0);
    }
    fs.writeFileSync(path.join(root, 'nested#guide.md'), '# Generated\n\nContent.');
    const generated = await processSourceDirectory(
      root, { path: root, folder: 'folder', ignoredPaths: ['nested/'], isGenerated: true },
      target, new Map(), new BlobService()
    );
    expect(generated.changedFiles.map(file => file.filename)).toEqual(['nested#guide.md']);
  });

  afterAll(() => {
    for (const dir of tmpDirs) {
      try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* ignore */ }
    }
  });
});
