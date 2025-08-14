import { describe, it, expect } from 'vitest';
import { findMarkdownCodeBlocks, findSwaggerToSDKConfiguration } from '../../src/utils/readme';
import path from 'path';
import fs from 'fs';

describe('readme utils', () => {
  describe('find SDK Swagger Config from readme.md', () => {
    const rootPath = process.cwd();
    const readmeMdPath = path.join(rootPath, './test/fixtures/test.readme.md');
    const readmeContent = fs.readFileSync(readmeMdPath).toString();
    it('test findMarkdownCodeBlocks from readme.md', () => {
      const blockContent = findMarkdownCodeBlocks(readmeContent);
      expect(blockContent.length).toEqual(6);
    });

    it('test findSwaggerToSDKConfiguration from readme.md', () => {
      const blockContent = findSwaggerToSDKConfiguration(readmeContent);
      expect(blockContent).toEqual({ repositories: [{ repo: 'azure-cli-extensions' }, { repo: 'azure-resource-manager-schemas' }, { repo: 'azure-powershell' }] });
    });
  });

  describe('findMarkdownCodeBlocks', () => {
    it('should return empty array for empty markdown', () => {
      const result = findMarkdownCodeBlocks('');
      expect(result).toEqual([]);
    });

    it('should extract single code block', () => {
      const markdown = '```yaml\ncontent\n```';
      const result = findMarkdownCodeBlocks(markdown);
      expect(result).toEqual([{ info: 'yaml', content: 'content' }]);
    });

    it('should extract multiple code blocks', () => {
      const markdown = '```yaml\nblock1\n```\nsome text\n```js\nblock2\n```';
      const result = findMarkdownCodeBlocks(markdown);
      expect(result).toEqual([
        { info: 'yaml', content: 'block1' },
        { info: 'js', content: 'block2' },
      ]);
    });

    it('should handle code blocks with empty info string', () => {
      const markdown = '```\ncontent\n```';
      const result = findMarkdownCodeBlocks(markdown);
      expect(result).toEqual([{ info: '', content: 'content' }]);
    });
  });

  describe('findSwaggerToSDKConfiguration', () => {
    it('should return undefined for undefined input', () => {
      const result = findSwaggerToSDKConfiguration(undefined);
      expect(result).toBeUndefined();
    });

    it('should return undefined for empty input', () => {
      const result = findSwaggerToSDKConfiguration('');
      expect(result).toEqual(undefined);
    });

    it('should parse valid swagger-to-sdk configuration', () => {
      const markdown = '```$(swagger-to-sdk)\nswagger-to-sdk:\n  - repo: azure/azure-sdk-for-js\n    after_scripts:\n      - npm install\n```';
      const result = findSwaggerToSDKConfiguration(markdown);
      expect(result).toEqual({
        repositories: [
          {
            repo: 'azure/azure-sdk-for-js',
            after_scripts: ['npm install'],
          },
        ],
      });
    });

    it('should handle multiple swagger-to-sdk blocks', () => {
      const markdown = [
        '```$(swagger-to-sdk)',
        'swagger-to-sdk:',
        '  - repo: azure/azure-sdk-for-js',
        '    after_scripts: [script1]',
        '```',
        '```$(swagger-to-sdk)',
        'swagger-to-sdk:',
        '  - repo: azure/azure-sdk-for-python',
        '    after_scripts: [script2]',
        '```',
      ].join('\n');

      const result = findSwaggerToSDKConfiguration(markdown);
      expect(result).toEqual({
        repositories: [
          { repo: 'azure/azure-sdk-for-js', after_scripts: ['script1'] },
          { repo: 'azure/azure-sdk-for-python', after_scripts: ['script2'] },
        ],
      });
    });

    it('should handle missing swagger-to-sdk section', () => {
      const markdown = '```$(swagger-to-sdk)\nother-section: value\n```';
      const result = findSwaggerToSDKConfiguration(markdown);
      expect(result).toEqual({ repositories: [] });
    });
  });
});
