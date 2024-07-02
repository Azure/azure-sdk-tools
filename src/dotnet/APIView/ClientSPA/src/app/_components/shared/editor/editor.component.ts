import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-editor',
  templateUrl: './editor.component.html',
  styleUrls: ['./editor.component.scss']
})
export class EditorComponent {
  @Input() content: string = '';
  @Input() editorId: string = '';

  getEditorContent() : string {
    return this.content;
  }  
}