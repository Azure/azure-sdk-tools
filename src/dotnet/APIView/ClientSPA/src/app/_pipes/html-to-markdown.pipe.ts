import { Pipe, PipeTransform } from '@angular/core';
import TurndownService from 'turndown';

@Pipe({
  name: 'htmlToMarkdown',
})
export class HtmlToMarkdownPipe implements PipeTransform {
  private turndownService: TurndownService;

  constructor() {
    this.turndownService = new TurndownService();
    this.turndownService.keep(['u', 's', 'mark', 'span', 'div']);
  }

  transform(html: string, escapeSpacialCharacters: boolean = true): string {
    if (!escapeSpacialCharacters) {
      this.turndownService.escape = (text: string) => text;
    }

    try {
      return this.turndownService.turndown(html);
    } catch (error) {
      console.error('Error converting HTML to Markdown:', error);
      return '';
    }
  }
}
