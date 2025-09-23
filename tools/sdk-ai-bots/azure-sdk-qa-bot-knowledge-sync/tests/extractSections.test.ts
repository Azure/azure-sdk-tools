import { describe, it, expect } from 'vitest';
import { extractSections } from '../src/DailySyncKnowledge';

describe('extractSections', () => {
  it('removes frontmatter and downgrades headers', () => {
    const md = `---\ntitle: Example\n---\n# Heading1\n## Heading2\nText`;
    const sections = extractSections(md);
    expect(sections).not.toContain('title:');
    expect(sections).toContain('## Heading1'); // downgraded
    expect(sections).toContain('### Heading2'); // downgraded
  });

  it('removes caution blocks', () => {
    const md = `---\n---\n:::caution\nBe careful!\n:::\n# Header`;
    const sections = extractSections(md);
    expect(sections).not.toContain('Be careful');
    expect(sections).toContain('## Header');
  });
});
