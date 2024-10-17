import { Component, EventEmitter, Input, Output } from '@angular/core';
import { EditorTextChangeEvent } from 'primeng/editor';

@Component({
  selector: 'app-editor',
  templateUrl: './editor.component.html',
  styleUrls: ['./editor.component.scss']
})
export class EditorComponent {
  @Input() content: string = '';
  @Input() editorId: string = '';

  @Output() contentEmitter : EventEmitter<string> = new EventEmitter<string>();

  getEditorContent() : string {
    return this.content;
  }

  onTextChange(event: EditorTextChangeEvent) {
    this.contentEmitter.emit(event.textValue);
    console.log(event.textValue);
  }
}