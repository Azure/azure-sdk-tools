import { Component, EventEmitter, Input, Output } from '@angular/core';
import { SimplemdeOptions } from 'ngx-simplemde';

@Component({
  selector: 'app-editor',
  templateUrl: './editor.component.html',
  styleUrls: ['./editor.component.scss']
})
export class EditorComponent {
  @Input() content: string = '';
  @Input() editorId: string = '';

  editorOptions : SimplemdeOptions = {
    autosave: { enabled: false },
    status: false,
    renderingConfig: {
      codeSyntaxHighlighting: true
    },
    toolbar: [
      'bold', 'italic', 'strikethrough', 'heading', '|',
      'code', 'quote', 'link', 'table', 'horizontal-rule', '|',
      'unordered-list', 'ordered-list'
    ]
  };

  ngAfterViewInit() {
    setTimeout(() => {
      document.querySelectorAll('.editor-toolbar a').forEach((button: any) => {
        if (button.classList.contains('smdi-bold')) {
          button.setAttribute('title', 'Bold (Ctrl-B)');
        } else if (button.classList.contains('smdi-italic')) {
          button.setAttribute('title', 'Italic (Ctrl-I)');
        } else if (button.classList.contains('smdi-strikethrough')) {
          button.setAttribute('title', 'Strikethrough');
        } else if (button.classList.contains('smdi-header')) {
          button.setAttribute('title', 'Heading (Ctrl-H)');
        } else if (button.classList.contains('smdi-code')) {
          button.setAttribute('title', 'Code (Ctrl-Alt-C)');
        } else if (button.classList.contains('smdi-quote-left')) {
          button.setAttribute('title', 'Quote (Ctrl-\')');
        } else if (button.classList.contains('smdi-link')) {
          button.setAttribute('title', 'Link (Ctrl-K)');
        } else if (button.classList.contains('smdi-table')) {
          button.setAttribute('title', 'Table');
        } else if (button.classList.contains('smdi-line')) {
          button.setAttribute('title', 'Horizontal Rule');
        } else if (button.classList.contains('smdi-list-ul')) {
          button.setAttribute('title', 'Unordered List (Ctrl-L)');
        } else if (button.classList.contains('smdi-list-ol')) {
          button.setAttribute('title', 'Ordered List (Ctrl-Alt-L)');
        }
      });
    }, 0);
  }

  getEditorContent() : string {
    return this.content;
  }
}