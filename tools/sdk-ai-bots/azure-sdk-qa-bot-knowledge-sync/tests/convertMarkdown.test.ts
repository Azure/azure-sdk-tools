import { describe, it, expect } from 'vitest';
import { convertMarkdown } from '../src/DailySyncKnowledge';

describe('convertMarkdown', () => {
  it('uses frontmatter title and permalink for filename', () => {
    const md = `---\ntitle: Sample Title\npermalink: custom-name\n---\nHello world.`;
    const result = convertMarkdown(md);
    expect(result.filename).toBe('custom-name');
    expect(result.content.startsWith('# Sample Title')).toBe(true);
    expect(result.content).toContain('Hello world.');
  });

  it('supports file without permalink (empty filename)', () => {
    const md = `---\ntitle: Sample Title\n---\nHello again.`;
    const result = convertMarkdown(md);
    expect(result.filename).toBe('');
    expect(result.content.startsWith('# Sample Title')).toBe(true);
  });
});
