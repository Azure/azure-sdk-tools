import { describe, it, expect } from 'vitest';
import { extractDateFromFilename } from '../src/DailySyncKnowledge';

describe('extractDateFromFilename', () => {
  it('parses valid release date from filename', () => {
    const d = extractDateFromFilename('release-2024-12-25.md');
    expect(d.getFullYear()).toBe(2024);
    expect(d.getMonth()).toBe(11);
    expect(d.getDate()).toBe(25);
  });

  it('returns epoch for invalid filename', () => {
    const d = extractDateFromFilename('not-a-release-file.md');
    expect(d.getTime()).toBe(0);
  });
});
