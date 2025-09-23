import { describe, it, expect, afterAll } from 'vitest';
import { processMarkdownFile } from '../src/DailySyncKnowledge';
import * as fs from 'fs';
import * as path from 'path';

const tmpDirs: string[] = [];

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
  afterAll(() => {
    for (const dir of tmpDirs) {
      try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* ignore */ }
    }
  });
});
