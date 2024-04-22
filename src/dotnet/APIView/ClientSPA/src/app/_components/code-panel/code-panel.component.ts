import { ChangeDetectorRef, Component, ElementRef, Input, ViewChild, ViewContainerRef } from '@angular/core';
import { BehaviorSubject, fromEvent, Subject, takeUntil } from 'rxjs';
import { debounceTime, finalize } from 'rxjs/operators';
import { CommentItemModel } from 'src/app/_models/review';
import { CodeHuskNode, CreateLinesOfTokensMessage, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';
import { OnInit } from '@angular/core';
import { AfterViewInit } from '@angular/core';
import { OnDestroy } from '@angular/core';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent implements AfterViewInit, OnDestroy{
  @Input() apiTreeNodeData: BehaviorSubject<CodeHuskNode | null> = new BehaviorSubject<CodeHuskNode | null>(null);
  @Input() tokenLineData: BehaviorSubject<CreateLinesOfTokensMessage | null> = new BehaviorSubject<CreateLinesOfTokensMessage | null>(null);
  @Input() reviewComments : CommentItemModel[] | undefined = [];

  @ViewChild('codeLinesContainer', { static: true }) codeLinesContainer: ElementRef | undefined;
  @ViewChild('commentThreadRef', { read: ViewContainerRef }) commentThreadRef!: ViewContainerRef;

  lineNumberCount : number = 0;
  isLoading: boolean = true;
  isAppendingTokens: boolean = false;
  codeLineFragments : Map<string, any> = new Map<string, any>();

  private destroyApiTreeNode$ = new Subject<void>();
  private destroyTokenLineData$ = new Subject<void>();

  constructor(private changeDeterctorRef: ChangeDetectorRef) { }

  ngAfterViewInit() {
    fromEvent(window, 'scroll').pipe(
      debounceTime(this.isAppendingTokens ? 1000 : 0)
    ).subscribe(() => {
    });
    
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
        const nodeId = `${lineData.nodeId}-${lineData.position}`;
        if (lineData.directive === ReviewPageWorkerMessageDirective.CreateLineOfTokens) {
          const apiTreeNode = document.getElementById(nodeId);

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
              span.textContent = token.value;
              if (token.id){
                const tokenId = token.id;
                span.setAttribute('data-token-id', tokenId);

                if (this.reviewComments && this.reviewComments.length > 0)
                {
                  commentThreadData = this.reviewComments.filter(c => c.elementId === tokenId);
                }
              }

              if (token.diffKind === "Added" || token.diffKind === "Removed") {
                span.classList.add(`token-${token.diffKind.toLowerCase()}`);
              }

              lineContent.appendChild(span);
            }
            
            if (lineData.tokenLine.length > 0)
            {
              this.lineNumberCount++;
              let codeLineFragment : DocumentFragment;
              if (this.codeLineFragments.has(nodeId)) {
                codeLineFragment = this.codeLineFragments.get(nodeId)?.documentFragment!;
              }
              else {
                codeLineFragment = document.createDocumentFragment();
                const nodeFragmentData = { 
                  documentFragment: codeLineFragment,
                  lastLineNumber: this.lineNumberCount
                };
                this.codeLineFragments.set(nodeId, nodeFragmentData);
              }
              
              lineNumber.textContent = `${this.lineNumberCount}`;
              lineNumber.classList.add('line-number');
              line.classList.add('code-line');
              if (lineData.diffKind === "Added" || lineData.diffKind === "Removed") {
                line.classList.add(`code-line-${lineData.diffKind.toLowerCase()}`);
              }
              lineContent.classList.add('code-line-content');
              lineContent.style.paddingLeft = `${apiTreeNode!.dataset["indent"] as unknown as number * 20}px`;
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
              codeLineFragment!.appendChild(line);

              if (commentThreadData.length > 0)
              {
                const commentThreadNode = this.commentThreadRef.createComponent(CommentThreadComponent);
                commentThreadNode.instance.comments = commentThreadData;
                codeLineFragment!.appendChild(commentThreadNode.location.nativeElement);
                // this.changeDeterctorRef.detectChanges();
              }
            }
          }
        }

        if (lineData.directive === ReviewPageWorkerMessageDirective.AppendTokenLinesToNode) {
          if (this.codeLineFragments.has(nodeId)) {
            const apiTreeNode = document.getElementById(nodeId);
            const nodeFragmentData = this.codeLineFragments.get(nodeId);
            this.codeLineFragments.delete(nodeId);
            if (nodeFragmentData.lastLineNumber > 3000)
            {
              apiTreeNode!.style.setProperty('content-visibility', 'auto');
            }
            
            this.isAppendingTokens = true;
            setTimeout(() => {
              apiTreeNode!.appendChild(nodeFragmentData.documentFragment!);
              this.isAppendingTokens = false;
            }, 0);
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
