import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SimplemdeModule, SimplemdeOptions } from 'ngx-simplemde';

@Component({
    selector: 'app-editor',
    templateUrl: './editor.component.html',
    styleUrls: ['./editor.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        SimplemdeModule
    ]
})
export class EditorComponent {
  @Input() content: string = '';
  @Output() contentChange = new EventEmitter<string>();
  @Input() editorId: string = '';

  private currentContent: string = '';

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

  constructor(private elementRef: ElementRef) {}

  onContentChange(newContent: string) {
    this.currentContent = newContent;
    this.contentChange.emit(newContent);
  }

  ngAfterViewInit() {
    setTimeout(() => {
      this.elementRef.nativeElement.querySelectorAll('.editor-toolbar a').forEach((button: any) => {
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

      (this.elementRef.nativeElement.querySelectorAll('.CodeMirror textarea')[0] as HTMLElement).focus();
    }, 0);
  }

  getEditorContent() : string {
    // Return the most recently updated content from the editor
    return this.currentContent || this.content;
  }
}
