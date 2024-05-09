import { AfterViewInit, Component, Input } from '@angular/core';

@Component({
  selector: 'app-editor',
  templateUrl: './editor.component.html',
  styleUrls: ['./editor.component.scss']
})
export class EditorComponent implements AfterViewInit{
  @Input() content: string = '';
  allowAnyOneToResolve : boolean = true;
  originalContent: string = '';

  ngAfterViewInit() {
    this.originalContent = this.content;
  }

  cancelCommentAction(event: Event) {
    const target = event.target as Element;
    const replyEditorContainer = target.closest(".reply-editor-container") as Element;
    if (replyEditorContainer) {
      const replyButtonContainer = (replyEditorContainer.nextElementSibling as Element).firstChild as Element;
      replyButtonContainer.classList.remove("d-none");
      replyEditorContainer.classList.add("d-none");
    } else {
      const editEditor = target.closest(".edit-editor-container") as Element;
      const renderedCommentContent = editEditor.previousSibling as Element;
      editEditor.classList.add("d-none");
      renderedCommentContent.classList.remove("d-none");
    }
    this.content = this.originalContent;
  }
}
