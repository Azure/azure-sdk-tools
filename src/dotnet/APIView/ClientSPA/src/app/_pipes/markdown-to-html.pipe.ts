import { Pipe, PipeTransform } from '@angular/core';
import { remark }  from 'remark';
import html from 'remark-html';

@Pipe({
  name: 'markdownToHtml'
})
export class MarkdownToHtmlPipe implements PipeTransform {
  transform(markdown: string): Promise<string> {
    return new Promise((resolve, reject) => {
      remark()
        .use(html)
        .process(markdown, (err, file) => {
          if (err) {
            reject(err);
          } else {
            resolve(String(file));
          }
        });
    });
  }

}
