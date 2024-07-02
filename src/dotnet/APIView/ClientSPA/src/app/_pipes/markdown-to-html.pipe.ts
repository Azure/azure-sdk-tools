import { Pipe, PipeTransform } from '@angular/core';
import { unified } from 'unified';
import remarkParse from 'remark-parse';
import remarkRehype from 'remark-rehype';
import rehypeRaw from 'rehype-raw';
import rehypeStringify from 'rehype-stringify';

@Pipe({
  name: 'markdownToHtml'
})
export class MarkdownToHtmlPipe implements PipeTransform {
  transform(markdown: string): Promise<string> {
    return new Promise((resolve, reject) => {
      unified()
      .use(remarkParse)
      .use(remarkRehype, { allowDangerousHtml: true })
      .use(rehypeRaw)
      .use(rehypeStringify) 
      .process(markdown)
      .then((file) => {
        resolve(String(file));
      })
      .catch((err) => {
        reject(err);
      });
    });
  }
}
