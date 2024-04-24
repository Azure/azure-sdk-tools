import { ChangeDetectorRef, Component, ElementRef, Input, OnChanges, OnInit, SimpleChanges, ViewChild, ViewContainerRef } from '@angular/core';
import { BehaviorSubject, fromEvent, of, Subject, takeUntil } from 'rxjs';
import { debounceTime, finalize, scan } from 'rxjs/operators';
import { CommentItemModel } from 'src/app/_models/review';
import { CodeLineData, InsertCodeLineDataMessage, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';
import { AfterViewInit } from '@angular/core';
import { OnDestroy } from '@angular/core';
import { Datasource, IDatasource, IAdapter } from 'ngx-ui-scroll';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent implements OnChanges, OnDestroy{
  @Input() codeLineData: CodeLineData[] = [];
  @Input() reviewComments : CommentItemModel[] | undefined = [];

  @ViewChild('commentThreadRef', { read: ViewContainerRef }) commentThreadRef!: ViewContainerRef;

  lineNumberCount : number = 0;
  isLoading: boolean = true;
  lastHuskNodeId :  string | undefined = undefined;
  codeWindowHeight: string | undefined = undefined;

  codeLinesDataSource: IDatasource | undefined;

  private destroyApiTreeNode$ = new Subject<void>();
  private destroyTokenLineData$ = new Subject<void>();

  constructor(private changeDeterctorRef: ChangeDetectorRef) { }

  ngOnInit() {
    this.codeWindowHeight = `${window.innerHeight - 80}`;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codeLineData'] && changes['codeLineData'].currentValue.length > 0) {
      this.isLoading = false;
      this.codeLinesDataSource = new Datasource({
        get: (index, count, success) => {
          let data : any = [];
          if (this.codeLineData.length > 0) {
            data = this.codeLineData.slice(index - 1, index - 1 + count);
          }
          success(data);
        },
        settings: {
          bufferSize: 50,
          itemSize: 21,
          minIndex: 1,
          maxIndex: this.codeLineData.length
        }
      });
      document.documentElement.style.setProperty('--max-line-number-width', `${this.codeLineData[this.codeLineData.length - 1].lineNumber.toString().length}ch`)
    }
  }

  scrollToIndex(index: number) {
    console.log(this.codeLinesDataSource?.adapter);
    this.codeLinesDataSource?.adapter?.reload();
    this.codeLinesDataSource?.adapter?.fix({ scrollPosition: index * 21 });
    //this.codeLinesDataSource?.adapter?.fix({
    //  scrollPosition: index
    //});   
  }

  //ngAfterViewInit() { 
    //this.codeLineData.pipe(
      //  takeUntil(this.destroyTokenLineData$), finalize(() => {
    //})).subscribe(lineData => {
      //if (lineData != null) {
        //this.codeLineDataItems.push(lineData);
        //this.changeDeterctorRef.detectChanges();
        //if(this.codeLineDataItems.length == 15000){
        //  this.isLoading = false;
        //}
        //const nodeId = `${lineData.nodeId}-${lineData.position}`;
        //if (lineData.directive === ReviewPageWorkerMessageDirective.CreateLineOfTokens) {
        //  const apiTreeNode = document.getElementById(nodeId);
//
        //  const line = document.createElement('div');
        //  const lineActions = document.createElement('div');
        //  const lineContent = document.createElement('div');
        //  const lineNumber = document.createElement('span');
        //  const commentButton = document.createElement('span');
        //  const commentIcon = document.createElement('i');
//
        //  let commentThreadData : CommentItemModel[] = [];
        //  let isDocumentationLine = false;
//
        //  if (lineData.nodeId && lineData.tokenLine) {
        //    for (let token of lineData.tokenLine) {
        //      const tokenSpan = document.createElement('span');
        //      token.renderClasses.forEach((c: string) => tokenSpan.classList.add(c));
        //      tokenSpan.textContent = token.value;
        //      if (token.id){
        //        const tokenId = token.id;
        //        tokenSpan.setAttribute('data-token-id', tokenId);
        //        if (this.reviewComments && this.reviewComments.length > 0)
        //        {
        //          commentThreadData = this.reviewComments.filter(c => c.elementId === tokenId);
        //        }
        //        commentButton.classList.add('commentable');
        //      }
//
        //      if ("GroupId" in token.properties) {
        //        line.classList.add(`${token.properties["GroupId"]}`);
        //        isDocumentationLine = true;
        //      }
//
        //      if (token.diffKind === "Added" || token.diffKind === "Removed") {
        //        tokenSpan.classList.add(`token-${token.diffKind.toLowerCase()}`);
        //      }
//
        //      lineContent.appendChild(tokenSpan);
        //    }
        //    
        //    if (lineData.tokenLine.length > 0)
        //    {
        //      this.lineNumberCount++;
//
        //      // Construct DocumentFragment so that we can append all the lines in this node at once
        //      let apiTokens : DocumentFragment;
        //      let documentationTokens : DocumentFragment;
        //      if (this.codeLineFragments.has(nodeId)) {
        //        const codeLineFragment = this.codeLineFragments.get(nodeId);
        //        apiTokens = codeLineFragment.apiTokens;
        //        documentationTokens = codeLineFragment.documentationTokens;
        //      }
        //      else {
        //        apiTokens = document.createDocumentFragment();
        //        documentationTokens = document.createDocumentFragment();
        //        const nodeFragmentData = { 
        //          apiTokens: apiTokens,
        //          documentationTokens: documentationTokens,
        //        };
        //        this.codeLineFragments.set(nodeId, nodeFragmentData);
        //      }
        //      
        //      lineNumber.textContent = `${this.lineNumberCount}`;
        //      document.documentElement.style.setProperty('--max-line-number-width', `${this.lineNumberCount.toString().length}ch`);
        //      lineNumber.classList.add('line-number');
        //      line.classList.add('code-line');
        //      if (lineData.diffKind === "Added" || lineData.diffKind === "Removed") {
        //        line.classList.add(`code-line-${lineData.diffKind.toLowerCase()}`);
        //      }
        //      lineContent.classList.add('code-line-content')
        //      lineContent.style.paddingLeft = `${apiTreeNode!.dataset["indent"] as unknown as number * 20}px`;
        //      lineActions.classList.add('line-actions');
        //      commentIcon.classList.add('bi', 'bi-plus-square-fill');
        //      commentButton.appendChild(commentIcon);
        //      commentButton.classList.add('comment-button');
//
        //      lineActions.appendChild(lineNumber);
        //      lineActions.appendChild(commentButton);
        //      line.appendChild(lineActions);
        //      line.appendChild(lineContent);
//
        //      if (isDocumentationLine) {
        //        documentationTokens.appendChild(line);
        //      }
        //      else {
        //        apiTokens.appendChild(line);
        //        if (commentThreadData.length > 0)
        //        {
        //          const commentThreadNode = this.commentThreadRef.createComponent(CommentThreadComponent);
        //          commentThreadNode.instance.comments = commentThreadData;
        //          apiTokens!.appendChild(commentThreadNode.location.nativeElement);
        //        }
        //      }
        //    }
        //  }
        //}
//
        //if (lineData.directive === ReviewPageWorkerMessageDirective.AppendTokenLinesToNode) {
        //  if (this.codeLineFragments.has(nodeId)) {
        //    const apiTreeNode = document.getElementById(nodeId);
        //    const codeLineFragment = this.codeLineFragments.get(nodeId);
        //    setTimeout(() => {
        //      apiTreeNode!.appendChild(codeLineFragment.apiTokens);
        //      codeLineFragment.apiTokens = null;
        //    }, 0);
        //  }
        //}
     // }
   // });
 // }

  getClassObject(renderClasses: Set<string>) {
    let classObject: { [key: string]: boolean } = {};
    for (let className of Array.from(renderClasses)) {
      classObject[className] = true;
    }
    return classObject;
  }

  ngOnDestroy() {
    this.destroyApiTreeNode$.next();
    this.destroyApiTreeNode$.complete();
    this.destroyTokenLineData$.next();
    this.destroyTokenLineData$.complete();
  }
}
