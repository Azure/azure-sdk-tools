import { ApplicationRef, Component, ElementRef, Injector, Input, ViewChild, ViewContainerRef, } from '@angular/core';
import { BehaviorSubject, Subject, takeUntil } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { CommentItemModel } from 'src/app/_models/review';
import { CodeHuskNode, CreateLinesOfTokensMessage } from 'src/app/_models/revision';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent {
  @Input() apiTreeNodeData: BehaviorSubject<CodeHuskNode | null> = new BehaviorSubject<CodeHuskNode | null>(null);
  @Input() tokenLineData: BehaviorSubject<CreateLinesOfTokensMessage | null> = new BehaviorSubject<CreateLinesOfTokensMessage | null>(null);
  @Input() reviewComments : CommentItemModel[] | undefined = [];

  @ViewChild('codeLinesContainer', { static: true }) codeLinesContainer: ElementRef | undefined;
  @ViewChild('commentThreadRef', { read: ViewContainerRef }) commentThreadRef!: ViewContainerRef;

  lineNumberCount : number = 0;
  isLoading: boolean = true;

  private destroyApiTreeNode$ = new Subject<void>();
  private destroyTokenLineData$ = new Subject<void>();

  ngAfterViewInit() {
    this.apiTreeNodeData.pipe(
      takeUntil(this.destroyApiTreeNode$),
      finalize(() => {
        
      })
    ).subscribe(nodeData => {
      if (nodeData != null)
      {
        const div = document.createElement('div');
        const id = `${nodeData.id}-${nodeData.position}`;
        div.id = id;
        div.classList.add('api-node');
        div.dataset["indent"] = nodeData.indent.toString();
        this.codeLinesContainer!.nativeElement.appendChild(div);
        this.isLoading = false;
      }
    });

    this.tokenLineData.pipe(takeUntil(this.destroyTokenLineData$), finalize(() => {
    })).subscribe(lineData => {
      if (lineData != null) {
        const node = document.getElementById(`${lineData.nodeId}-${lineData.position}`);
        const line = document.createElement('div');
        const lineActions = document.createElement('div');
        const lineContent = document.createElement('div');
        const lineNumber = document.createElement('span');
        const commentButton = document.createElement('span');
        const commentIcon = document.createElement('i');
        let commentThreadData : CommentItemModel[] = [];

        if (lineData.nodeId && lineData.tokenLine) {
          for (let token of lineData.tokenLine) {
            const span = document.createElement('span');
            token.renderClasses.forEach((c: string) => span.classList.add(c));
            span.textContent = token.properties["Value"];
            if (token.properties["Id"]){
              const tokenId = token.properties["Id"];
              span.setAttribute('data-token-id', tokenId);

              if (this.reviewComments && this.reviewComments.length > 0)
              {
                commentThreadData = this.reviewComments.filter(c => c.elementId === tokenId);
              }
            }

            lineContent.appendChild(span);
          }
          
          if (lineData.tokenLine.length > 0)
          {
            this.lineNumberCount++;
            lineNumber.textContent = `${this.lineNumberCount}`;
            lineNumber.classList.add('line-number');
            line.classList.add('code-line');
            lineContent.classList.add('code-line-content');
            lineContent.style.paddingLeft = `${node!.dataset["indent"] as unknown as number * 20}px`;
            lineActions.classList.add('line-actions');
            commentIcon.classList.add('bi', 'bi-plus-square-fill');
            commentButton.appendChild(commentIcon);
            commentButton.classList.add('comment-button');
            if (lineData.position === "top") {
              commentButton.classList.add('commentable');
            }

            lineActions.appendChild(lineNumber);
            lineActions.appendChild(commentButton);
            line.appendChild(lineActions);
            line.appendChild(lineContent);
            node!.appendChild(line);

            if (commentThreadData.length > 0)
            {
              const commentThreadNode = this.commentThreadRef.createComponent(CommentThreadComponent);
              commentThreadNode.instance.comments = commentThreadData;
              node!.appendChild(commentThreadNode.location.nativeElement);
            }
          }
        }
      }
    });
  }

  ngOnDestroy() {
    this.destroyApiTreeNode$.next();
    this.destroyApiTreeNode$.complete();
    this.destroyTokenLineData$.next();
    this.destroyTokenLineData$.complete();
  }
}
