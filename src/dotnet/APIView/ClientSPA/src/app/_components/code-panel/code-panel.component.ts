import { ChangeDetectorRef, Component, ElementRef, Input, ViewChild, ViewContainerRef } from '@angular/core';
import { BehaviorSubject, fromEvent, Subject, takeUntil } from 'rxjs';
import { debounceTime, finalize } from 'rxjs/operators';
import { CommentItemModel } from 'src/app/_models/review';
import { CodeHuskNode, CreateCodeLineHuskMessage, CreateLinesOfTokensMessage, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';
import { AfterViewInit } from '@angular/core';
import { OnDestroy } from '@angular/core';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent implements AfterViewInit, OnDestroy{
  @Input() apiTreeNodeData: BehaviorSubject<CreateCodeLineHuskMessage | null> = new BehaviorSubject<CreateCodeLineHuskMessage | null>(null);
  @Input() tokenLineData: BehaviorSubject<CreateLinesOfTokensMessage | null> = new BehaviorSubject<CreateLinesOfTokensMessage | null>(null);
  @Input() reviewComments : CommentItemModel[] | undefined = [];

  @ViewChild('codeLinesContainer', { static: true }) codeLinesContainer: ElementRef | undefined;
  @ViewChild('commentThreadRef', { read: ViewContainerRef }) commentThreadRef!: ViewContainerRef;

  lineNumberCount : number = 0;
  isLoading: boolean = true;
  codeLineFragments : Map<string, any> = new Map<string, any>();
  lastHuskNodeId :  string | undefined = undefined;

  private destroyApiTreeNode$ = new Subject<void>();
  private destroyTokenLineData$ = new Subject<void>();

  constructor(private changeDeterctorRef: ChangeDetectorRef) { }

  ngAfterViewInit() {   
    this.apiTreeNodeData.pipe(
      takeUntil(this.destroyApiTreeNode$),
      finalize(() => {
        
      })
    ).subscribe(data => {
      if (data?.nodeData != null)
      {
        if (data.isLastHuskNode) {
          this.lastHuskNodeId = `${data.nodeData.id}-${data.nodeData.position}`;
        }
        else {
          const div = document.createElement('div');
          const id = `${data.nodeData.id}-${data.nodeData.position}`;
          div.id = id;
          div.classList.add('api-node');
          div.dataset["indent"] = data.nodeData.indent.toString();
          this.codeLinesContainer!.nativeElement.appendChild(div);
          this.isLoading = false;
        }
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
          let isDocumentationLine = false;

          if (lineData.nodeId && lineData.tokenLine) {
            for (let token of lineData.tokenLine) {
              const tokenSpan = document.createElement('span');
              token.renderClasses.forEach((c: string) => tokenSpan.classList.add(c));
              tokenSpan.textContent = token.value;
              if (token.id){
                const tokenId = token.id;
                tokenSpan.setAttribute('data-token-id', tokenId);
                if (this.reviewComments && this.reviewComments.length > 0)
                {
                  commentThreadData = this.reviewComments.filter(c => c.elementId === tokenId);
                }
                commentButton.classList.add('commentable');
              }

              if ("GroupId" in token.properties) {
                line.classList.add(`${token.properties["GroupId"]}`);
                isDocumentationLine = true;
              }

              if (token.diffKind === "Added" || token.diffKind === "Removed") {
                tokenSpan.classList.add(`token-${token.diffKind.toLowerCase()}`);
              }

              lineContent.appendChild(tokenSpan);
            }
            
            if (lineData.tokenLine.length > 0)
            {
              this.lineNumberCount++;

              // Construct DocumentFragment so that we can append all the lines in this node at once
              let apiTokens : DocumentFragment;
              let documentationTokens : DocumentFragment;
              if (this.codeLineFragments.has(nodeId)) {
                const codeLineFragment = this.codeLineFragments.get(nodeId);
                apiTokens = codeLineFragment.apiTokens;
                documentationTokens = codeLineFragment.documentationTokens;
              }
              else {
                apiTokens = document.createDocumentFragment();
                documentationTokens = document.createDocumentFragment();
                const nodeFragmentData = { 
                  apiTokens: apiTokens,
                  documentationTokens: documentationTokens,
                };
                this.codeLineFragments.set(nodeId, nodeFragmentData);
              }
              
              lineNumber.textContent = `${this.lineNumberCount}`;
              document.documentElement.style.setProperty('--max-line-number-width', `${this.lineNumberCount.toString().length}ch`);
              lineNumber.classList.add('line-number');
              line.classList.add('code-line');
              if (lineData.diffKind === "Added" || lineData.diffKind === "Removed") {
                line.classList.add(`code-line-${lineData.diffKind.toLowerCase()}`);
              }
              lineContent.classList.add('code-line-content')
              lineContent.style.paddingLeft = `${apiTreeNode!.dataset["indent"] as unknown as number * 20}px`;
              lineActions.classList.add('line-actions');
              commentIcon.classList.add('bi', 'bi-plus-square-fill');
              commentButton.appendChild(commentIcon);
              commentButton.classList.add('comment-button');

              lineActions.appendChild(lineNumber);
              lineActions.appendChild(commentButton);
              line.appendChild(lineActions);
              line.appendChild(lineContent);

              if (isDocumentationLine) {
                documentationTokens.appendChild(line);
              }
              else {
                apiTokens.appendChild(line);
                if (commentThreadData.length > 0)
                {
                  const commentThreadNode = this.commentThreadRef.createComponent(CommentThreadComponent);
                  commentThreadNode.instance.comments = commentThreadData;
                  apiTokens!.appendChild(commentThreadNode.location.nativeElement);
                }
              }
            }
          }
        }

        if (lineData.directive === ReviewPageWorkerMessageDirective.AppendTokenLinesToNode) {
          if (this.codeLineFragments.has(nodeId)) {
            const apiTreeNode = document.getElementById(nodeId);
            const codeLineFragment = this.codeLineFragments.get(nodeId);
            setTimeout(() => {
              apiTreeNode!.appendChild(codeLineFragment.apiTokens);
              codeLineFragment.apiTokens = null;
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
