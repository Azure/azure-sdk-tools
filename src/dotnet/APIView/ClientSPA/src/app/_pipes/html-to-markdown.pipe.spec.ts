import { HtmlToMarkdownPipe } from './html-to-markdown.pipe';

describe('HtmlToMarkdownPipe', () => {
  it('create an instance', () => {
    const pipe = new HtmlToMarkdownPipe();
    expect(pipe).toBeTruthy();
  });
});
