import { describe, it, expect } from 'vitest';
import { extractReleaseInfo } from '../src/DailySyncKnowledge';

describe('extractReleaseInfo', () => {
  it('extracts title, date, and version from frontmatter', () => {
    const md = `---\nTITLE: Should be ignored\ntitle: "My Title"\nreleaseDate: 2025-01-10\nversion: 1.2.3\n---\nBody`; 
    const info = extractReleaseInfo(md);
    expect(info.title).toBe('My Title');
    expect(info.releaseDate).toBe('2025-01-10');
    expect(info.version).toBe('1.2.3');
  });

  it('returns empty fields when frontmatter missing', () => {
    const info = extractReleaseInfo('No frontmatter here');
    expect(info).toEqual({ title: '', releaseDate: '', version: '' });
  });
});
