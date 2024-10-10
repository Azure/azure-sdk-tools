import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { take, takeUntil } from 'rxjs/operators';
import { Datasource, IDatasource, SizeStrategy } from 'ngx-ui-scroll';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { ActivatedRoute, Router } from '@angular/router';
import { CodeLineRowNavigationDirection, isDiffRow } from 'src/app/_helpers/common-helpers';
import { SCROLL_TO_NODE_QUERY_PARAM } from 'src/app/_helpers/router-helpers';
import { CodePanelData, CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { UserProfile } from 'src/app/_models/userProfile';
import { Message } from 'primeng/api/message';
import { MessageService } from 'primeng/api';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { Subject } from 'rxjs';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent implements OnChanges{
  @Input() codePanelRowData: CodePanelRowData[] = [];
  @Input() codePanelData: CodePanelData | null = null;
  @Input() isDiffView: boolean = false;
  @Input() language: string | undefined;
  @Input() languageSafeName: string | undefined;
  @Input() scrollToNodeIdHashed: string | undefined;
  @Input() scrollToNodeId : string | undefined;
  @Input() reviewId: string | undefined;
  @Input() activeApiRevisionId: string | undefined;
  @Input() userProfile : UserProfile | undefined;
  @Input() showLineNumbers: boolean = true;
  @Input() loadFailed : boolean = false;

  @Output() hasActiveConversationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();

  
  noDiffInContentMessage : Message[] = [{ severity: 'info', icon:'bi bi-info-circle', detail: 'There is no difference between the two API revisions.' }];

  isLoading: boolean = true;
  codeWindowHeight: string | undefined = undefined;
  codePanelRowDataIndicesMap = new Map<string, number>();

  codePanelRowSource: IDatasource<CodePanelRowData> | undefined;
  CodePanelRowDatatype = CodePanelRowDatatype;

  destroy$ = new Subject<void>();

  commentThreadNavaigationPointer: number | undefined = undefined;
  diffNodeNavaigationPointer: number | undefined = undefined;

  constructor(private changeDetectorRef: ChangeDetectorRef, private commentsService: CommentsService, 
    private signalRService: SignalRService, private route: ActivatedRoute, private router: Router, private messageService: MessageService) { }

  ngOnInit() {
    this.codeWindowHeight = `${window.innerHeight - 80}`;
    this.handleRealTimeCommentUpdates();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codePanelRowData']) {
      if (changes['codePanelRowData'].currentValue.length > 0) {
        this.loadCodePanelViewPort();
        this.updateHasActiveConversations();
      } else {
        this.isLoading = true;
        this.codePanelRowSource = undefined;
      }
    }

    if (changes['scrollToNodeIdHashed'] && changes['scrollToNodeIdHashed'].currentValue) {
      this.scrollToNode(this.scrollToNodeIdHashed!);
    }

    if (changes['loadFailed'] && changes['loadFailed'].currentValue) {
      this.isLoading = false;
    }
  }

  onCodePanelItemClick(event: Event) {
    const target = event.target as Element;
    if (target.classList.contains('nav-token')) {
      const navigationId = target.getAttribute('data-navigate-to-id');
      this.scrollToNode(undefined, navigationId!);
    }

    if (target.classList.contains('url-token')) {
      const url = target.getAttribute('data-navigate-to-url');
      window.open(url!, '_blank');
    }

    if (target.classList.contains('toggle-documentation-btn') && !target.classList.contains('hide')) {
      this.toggleNodeDocumentation(target);
    }

    if (target.classList.contains('toggle-user-comments-btn') && !target.classList.contains('hide')) {
      this.toggleNodeComments(target);
    }
  }

  getAssociatedCodeLine(item: CodePanelRowData): CodePanelRowData | undefined {
    if (this.codePanelData?.nodeMetaData && this.codePanelData.nodeMetaData[item.nodeIdHashed]) {
      return this.codePanelData.nodeMetaData[item.nodeIdHashed].codeLines[item.associatedRowPositionInGroup] || undefined;
    }
    return undefined;
  }

  getRowClassObject(row: CodePanelRowData) {
    let classObject: { [key: string]: boolean } = {};
    if (row.rowClasses) {
      for (let className of Array.from(row.rowClasses)) {
        classObject[className] = true;
      }
    }

    if (row.isHiddenAPI) {
      classObject['hidden-api'] = true;
    }
    return classObject;
  }

  getTokenClassObject(token: StructuredToken) {
    let classObject: { [key: string]: boolean } = {};
    if (token.renderClasses) {
      for (let className of Array.from(token.renderClasses)) {
        classObject[className] = true;
      }
    }

    if (token.properties && 'NavigateToUrl' in token.properties) {
      classObject['url-token'] = true;
    }

    if (token.properties && 'NavigateToId' in token.properties) {
      classObject['nav-token'] = true;
    }

    if (token.tags && new Set(token.tags).has('Deprecated')) {
      classObject['deprecated-token'] = true;
    }
    return classObject;
  }

  getNavigationId(token: StructuredToken) {
    if (token.properties && 'NavigateToId' in token.properties) {
      return token.properties['NavigateToId'];
    }
    return "";
  }

  getNavigationUrl(token: StructuredToken) {
    if (token.properties && 'NavigateToUrl' in token.properties) {
      return token.properties['NavigateToUrl'];
    }
    return "";
  }

  toggleNodeComments(target: Element) {
    const codeLine = target.closest('.code-line')!;
    const nodeIdHashed = codeLine.getAttribute('data-node-id');
    const rowPositionInGroup = parseInt(codeLine.getAttribute('data-row-position-in-group')!, 10);
    const rowType = codeLine.getAttribute('data-row-type')!;
    const existingCommentThread = this.codePanelData?.nodeMetaData[nodeIdHashed!]?.commentThread;
    const exisitngCodeLine = this.codePanelData?.nodeMetaData[nodeIdHashed!]?.codeLines[rowPositionInGroup];
    
    if (!existingCommentThread || !existingCommentThread[rowPositionInGroup]) {
      const commentThreadRow = new CodePanelRowData();
      commentThreadRow.type = CodePanelRowDatatype.CommentThread;
      commentThreadRow.nodeId = exisitngCodeLine?.nodeId!;
      commentThreadRow.nodeIdHashed = exisitngCodeLine?.nodeIdHashed!;
      commentThreadRow.rowClasses = new Set<string>(['user-comment-thread']);
      commentThreadRow.showReplyTextBox = true;
      commentThreadRow.associatedRowPositionInGroup = rowPositionInGroup;
      this.codePanelData!.nodeMetaData[nodeIdHashed!].commentThread = {};
      this.codePanelData!.nodeMetaData[nodeIdHashed!].commentThread[rowPositionInGroup] = commentThreadRow;
      this.insertItemsIntoScroller([commentThreadRow], nodeIdHashed!, rowType, rowPositionInGroup, "toggleCommentsClasses", "can-show", "show");
    }
    else {
      for (let i = 0; i < this.codePanelRowData.length; i++) {
        if (this.codePanelRowData[i].nodeIdHashed === nodeIdHashed && this.codePanelRowData[i].type === CodePanelRowDatatype.CommentThread &&
            this.codePanelRowData[i].rowPositionInGroup === rowPositionInGroup) {
          this.codePanelRowData[i].showReplyTextBox = true;
          break;
        }
      }
    }
  }

  async toggleNodeDocumentation(target: Element) {
    const codeLine = target.closest('.code-line')!;
    const nodeIdHashed = codeLine.getAttribute('data-node-id');
    const rowType = codeLine.getAttribute('data-row-type')!;

    if (target.classList.contains('bi-arrow-up-square')) {
      const documentationData = this.codePanelData?.nodeMetaData[nodeIdHashed!]?.documentation;
      await this.insertItemsIntoScroller(documentationData!, nodeIdHashed!, rowType, -1, "toggleDocumentationClasses", "bi-arrow-up-square", "bi-arrow-down-square");
    } else if (target.classList.contains('bi-arrow-down-square')) {
      await this.removeItemsFromScroller(nodeIdHashed!, CodePanelRowDatatype.Documentation, "toggleDocumentationClasses", "bi-arrow-down-square", "bi-arrow-up-square");
    }
  }

  async insertRowTypeIntoScroller(codePanelRowDatatype:  CodePanelRowDatatype) {
    await this.codePanelRowSource?.adapter?.relax();

    const updatedCodeLinesData : CodePanelRowData[] = [];

    for (let i = 0; i < this.codePanelRowData.length; i++) {
      if (this.codePanelRowData[i].type === CodePanelRowDatatype.CodeLine &&  this.codePanelRowData[i].nodeIdHashed! in this.codePanelData?.nodeMetaData!) {
        const nodeData = this.codePanelData?.nodeMetaData[this.codePanelRowData[i].nodeIdHashed!];

        switch (codePanelRowDatatype) {
          case CodePanelRowDatatype.CommentThread:
            updatedCodeLinesData.push(this.codePanelRowData[i]);
            if (nodeData?.commentThread && nodeData?.commentThread.hasOwnProperty(this.codePanelRowData[i].rowPositionInGroup)) {
              updatedCodeLinesData.push(nodeData?.commentThread[this.codePanelRowData[i].rowPositionInGroup]);
            }
            break;
          case CodePanelRowDatatype.Diagnostics:
            updatedCodeLinesData.push(this.codePanelRowData[i]);
            if (nodeData?.diagnostics) {
              updatedCodeLinesData.push(...nodeData?.diagnostics);
            }
            break;
          case CodePanelRowDatatype.Documentation:
            if (this.codePanelRowData[i].toggleDocumentationClasses?.includes('bi-arrow-up-square')) {
              if (nodeData?.documentation) {
                updatedCodeLinesData.push(...nodeData?.documentation);
              }
              this.codePanelRowData[i].toggleDocumentationClasses = this.codePanelRowData[i].toggleDocumentationClasses?.replace('bi-arrow-up-square', 'bi-arrow-down-square');
            }
            updatedCodeLinesData.push(this.codePanelRowData[i]);
            break;         
        }
      }
      else {
        updatedCodeLinesData.push(this.codePanelRowData[i]);
      }
    }
    this.isLoading = true;
    this.codePanelRowSource = undefined
    this.codePanelRowData = updatedCodeLinesData;
    this.changeDetectorRef.detectChanges();
    this.loadCodePanelViewPort();
  }

  async insertItemsIntoScroller(itemsToInsert: CodePanelRowData[], nodeIdhashed: string, targetRowType: string,
      insertPosition : number, propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string) {
    await this.codePanelRowSource?.adapter?.relax();

    let preData = [];
    let nodeIndex = 0;
    let insertPositionFound = false;    

    while (nodeIndex < this.codePanelRowData.length) {
      if (insertPositionFound) {
        break;
      }
      
      if (this.codePanelRowData[nodeIndex].nodeIdHashed === nodeIdhashed) {
        if (insertPosition === -1) {
          break;
        }

        if (insertPosition == this.codePanelRowData[nodeIndex].rowPositionInGroup && this.codePanelRowData[nodeIndex].type === targetRowType) {
          insertPositionFound = true;
        }
      }
      preData.push(this.codePanelRowData[nodeIndex]);
      nodeIndex++;
    }
    let postData = this.codePanelRowData.slice(nodeIndex);

    if (propertyToChange && iconClassToremove && iconClassToAdd) {
      if (propertyToChange === "toggleDocumentationClasses") {
        postData[0].toggleDocumentationClasses = postData[0].toggleDocumentationClasses?.replace(iconClassToremove, iconClassToAdd);
      }
  
      if (propertyToChange === "toggleCommentsClasses") {
        preData[preData.length - 1].toggleCommentsClasses = preData[preData.length - 1].toggleCommentsClasses?.replace(iconClassToremove, iconClassToAdd);
      }
    }

    this.codePanelRowData = [
      ...preData,
      ...itemsToInsert!,
      ...postData
    ];

    await this.codePanelRowSource?.adapter?.insert({
      beforeIndex: nodeIndex,
      items: itemsToInsert!
    });
  }

  async removeRowTypeFromScroller(codePanelRowDatatype:  CodePanelRowDatatype) {
    await this.codePanelRowSource?.adapter?.relax();

    const indexesToRemove : number[] = [];
    let filteredCodeLinesData : CodePanelRowData[] = [];
    for (let i = 0; i < this.codePanelRowData.length; i++) {
      if (this.codePanelRowData[i].type === codePanelRowDatatype) {
        indexesToRemove.push(i);
      }
      else {
        if (codePanelRowDatatype === CodePanelRowDatatype.Documentation) {
          this.codePanelRowData[i].toggleDocumentationClasses = this.codePanelRowData[i].toggleDocumentationClasses?.replace('bi-arrow-down-square', 'bi-arrow-up-square');
        }
        filteredCodeLinesData.push(this.codePanelRowData[i]);
      }
    }

    this.codePanelRowData = filteredCodeLinesData;
    await this.codePanelRowSource?.adapter?.remove({
      indexes: indexesToRemove
    });
  }

  async removeItemsFromScroller(nodeIdHashed: string, codePanelRowDatatype:  CodePanelRowDatatype,
    propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string, associatedRowPositionInGroup?: number) {
    await this.codePanelRowSource?.adapter?.relax();

    const indexesToRemove : number[] = [];
    const filteredCodeLinesData : CodePanelRowData[] = [];

    for (let i = 0; i < this.codePanelRowData.length; i++) {
      if (this.codePanelRowData[i].nodeIdHashed != nodeIdHashed || this.codePanelRowData[i].type != codePanelRowDatatype
        || (associatedRowPositionInGroup && this.codePanelRowData[i].associatedRowPositionInGroup !== associatedRowPositionInGroup)) {
          filteredCodeLinesData.push(this.codePanelRowData[i]);
      }
      else {
        indexesToRemove.push(i);
      }
    }

    if (propertyToChange && iconClassToremove && iconClassToAdd) {
      if (propertyToChange === "toggleDocumentationClasses") {
        const index = indexesToRemove[0];
        filteredCodeLinesData[index].toggleDocumentationClasses = filteredCodeLinesData[index].toggleDocumentationClasses?.replace(iconClassToremove, iconClassToAdd);
      }
  
      if (propertyToChange === "toggleCommentsClasses") {
        const index = indexesToRemove[0] - 1;
        filteredCodeLinesData[index].toggleCommentsClasses = filteredCodeLinesData[index].toggleCommentsClasses?.replace(iconClassToremove, iconClassToAdd);
      }
    }

    this.codePanelRowData = filteredCodeLinesData;
    await this.codePanelRowSource?.adapter?.remove({
      indexes: indexesToRemove
    });
  }

  async updateItemInScroller(updateData: CodePanelRowData) {
    let filterdData = this.codePanelRowData.filter(row => row.nodeIdHashed === updateData.nodeIdHashed &&
      row.type === updateData.type);

    if (updateData.type === CodePanelRowDatatype.CommentThread) {
      filterdData = filterdData.filter(row => row.associatedRowPositionInGroup === updateData.associatedRowPositionInGroup);
    }

    filterdData[0] = updateData;
      
    await this.codePanelRowSource?.adapter?.relax();
    await this.codePanelRowSource?.adapter?.update({
      predicate: ({ $index, data, element}) => {
        if (data.nodeIdHashed === updateData.nodeIdHashed && data.type === updateData.type) {
          return [updateData];
        }
        return true;
      }
    });
  }

  initializeDataSource() : Promise<void> {
    return new Promise((resolve, reject) => {
      this.codePanelRowSource = new Datasource<CodePanelRowData>({
        get: (index, count, success) => {
          let data : any = [];
          if (this.codePanelRowData.length > 0) {
            data = this.codePanelRowData.slice(index, index + count);
          }
          success(data);
        },
        settings: {
          bufferSize: (this.userProfile?.preferences.disableCodeLinesLazyLoading) ? this.codePanelRowData.length : 50,
          padding: 1,
          itemSize: 21,
          startIndex : 0,
          minIndex: 0,
          maxIndex: this.codePanelRowData.length - 1,
          sizeStrategy: SizeStrategy.Average
        }
      });

      if (this.codePanelRowSource) {
        resolve();
      } else {
        reject('Failed to Initialize Datasource');
      }
    });
  }

  scrollToNode(nodeIdHashed: string | undefined = undefined, nodeId: string | undefined = undefined) {
    let index = 0;
    let scrollIndex : number | undefined = undefined;
    let indexesHighlighted : number[] = [];
    while (index < this.codePanelRowData.length) {
      if (scrollIndex && this.codePanelRowData[index].nodeIdHashed !== nodeIdHashed) {
        break;
      }
      if ((nodeIdHashed && this.codePanelRowData[index].nodeIdHashed === nodeIdHashed) || (nodeId && this.codePanelRowData[index].nodeId === nodeId)) {
        nodeIdHashed = this.codePanelRowData[index].nodeIdHashed;
        this.codePanelRowData[index].rowClasses = this.codePanelRowData[index].rowClasses || new Set<string>();
        this.codePanelRowData[index].rowClasses.add('active');
        indexesHighlighted.push(index);
        if (!scrollIndex) {
          scrollIndex = index;
        }
      }
      index++;
    }
    if (scrollIndex) {
      let scrollPadding = 0;
      scrollPadding = (this.showNoDiffInContentMessage()) ? scrollPadding + 2 : scrollPadding;

      this.codePanelRowSource?.adapter?.reload(scrollIndex - scrollPadding);
      let newQueryParams = getQueryParams(this.route);
      newQueryParams[SCROLL_TO_NODE_QUERY_PARAM] = this.codePanelRowData[scrollIndex].nodeId;
      this.router.navigate([], { queryParams: newQueryParams, state: { skipStateUpdate: true } });
      setTimeout(() => {
        indexesHighlighted.forEach((index) => {
          this.codePanelRowData[index].rowClasses?.delete('active');
        });
      }, 1550);
    }
  }

  setMaxLineNumberWidth() {
    if (this.codePanelRowData[this.codePanelRowData.length - 1].lineNumber) {
      document.documentElement.style.setProperty('--max-line-number-width', `${this.codePanelRowData[this.codePanelRowData.length - 1].lineNumber!.toString().length}ch`);
    }
  }

  handleCancelCommentActionEmitter(commentUpdates: any) {
    const commentsInNode = this.codePanelData?.nodeMetaData[commentUpdates.nodeIdHashed]?.commentThread
    if (commentsInNode && commentsInNode.hasOwnProperty(commentUpdates.associatedRowPositionInGroup)) {
      const commentThread = commentsInNode[commentUpdates.associatedRowPositionInGroup];
      if (!commentThread.comments || commentThread.comments.length === 0) {
        this.removeItemsFromScroller(commentUpdates.nodeIdHashed, CodePanelRowDatatype.CommentThread, "toggleCommentsClasses", "show", "can-show", commentUpdates.associatedRowPositionInGroup);
        this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed].commentThread = [];
      }
      else {
        for (let i = 0; i < this.codePanelRowData.length; i++) {
          if (this.codePanelRowData[i].nodeIdHashed === commentUpdates.nodeIdHashed && this.codePanelRowData[i].type === CodePanelRowDatatype.CommentThread
            && this.codePanelRowData[i].associatedRowPositionInGroup === commentUpdates.associatedRowPositionInGroup
          ) {
            this.codePanelRowData[i].showReplyTextBox = false;
            break;
          }
        }
      }
    }
  }

  handleSaveCommentActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!; // Need review if to push updates to conversation page
    if (commentUpdates.commentId) {
      this.commentsService.updateComment(this.reviewId!, commentUpdates.commentId, commentUpdates.commentText!)
        .pipe(take(1)).subscribe({
          next: () => {
            this.updateCommentTextInCommentThread(commentUpdates);
            this.signalRService.pushCommentUpdates(commentUpdates);
          }
        });
    }
    else {
      this.commentsService.createComment(this.reviewId!, this.activeApiRevisionId!, commentUpdates.nodeId!, commentUpdates.commentText!, CommentType.APIRevision, commentUpdates.allowAnyOneToResolve)
        .pipe(take(1)).subscribe({
            next: (response: CommentItemModel) => {
              this.addCommentToCommentThread(commentUpdates, response);
              commentUpdates.comment = response;
              this.signalRService.pushCommentUpdates(commentUpdates);
            }
          }
        );
    }
  }

  handleDeleteCommentActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!;
    this.commentsService.deleteComment(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe({
      next: () => {
        this.deleteCommentFromCommentThread(commentUpdates);
        this.signalRService.pushCommentUpdates(commentUpdates);
      }
    });
  }

  handleCommentResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!;
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
      this.commentsService.resolveComments(this.reviewId!, commentUpdates.elementId!).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.signalRService.pushCommentUpdates(commentUpdates);
        }
      });
    }
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
      this.commentsService.unresolveComments(this.reviewId!, commentUpdates.elementId!).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.signalRService.pushCommentUpdates(commentUpdates);
        }
      });
    }
  }

  handleCommentUpvoteActionEmitter(commentUpdates: CommentUpdatesDto){
    commentUpdates.reviewId = this.reviewId!;
    this.commentsService.toggleCommentUpVote(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe({
      next: () => {
        this.signalRService.pushCommentUpdates(commentUpdates);
      }
    });
  }

  handleRealTimeCommentUpdates() {
    this.signalRService.onCommentUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (commentUpdates: CommentUpdatesDto) => {
        if ((commentUpdates.reviewId && commentUpdates.reviewId == this.reviewId) ||
          (commentUpdates.comment && commentUpdates.comment.reviewId == this.reviewId)) {
          if (!commentUpdates.nodeIdHashed || commentUpdates.associatedRowPositionInGroup == undefined) {
            const codePanelRowData = this.findRowForCommentUpdates(commentUpdates.commentId!, commentUpdates.elementId!);
            commentUpdates.nodeIdHashed = codePanelRowData?.nodeIdHashed;
            commentUpdates.associatedRowPositionInGroup = codePanelRowData?.associatedRowPositionInGroup;
          }

          if (!commentUpdates.nodeIdHashed || commentUpdates.associatedRowPositionInGroup == undefined) {
            return;
          }

          switch (commentUpdates.commentThreadUpdateAction) {
            case CommentThreadUpdateAction.CommentCreated:
              this.addCommentToCommentThread(commentUpdates, commentUpdates.comment!);
              break;
            case CommentThreadUpdateAction.CommentTextUpdate:
              this.updateCommentTextInCommentThread(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentResolved:
              this.applyCommentResolutionUpdate(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentUnResolved:
              this.applyCommentResolutionUpdate(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentUpVoteToggled:
              this.toggleCommentUpVote(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentDeleted:
              this.deleteCommentFromCommentThread(commentUpdates);
              break;
          }
        }
      }
    });
  }

  handleCommentThreadNavaigationEmitter(event: any) {
    this.commentThreadNavaigationPointer = Number(event.commentThreadNavaigationPointer);
    this.navigateToCommentThread(event.direction);
  }

  navigateToCommentThread(direction: CodeLineRowNavigationDirection) {
    const firstVisible = this.codePanelRowSource?.adapter?.firstVisible!.$index!;
    const lastVisible = this.codePanelRowSource?.adapter?.lastVisible!.$index!;
    let navigateToRow : CodePanelRowData | undefined = undefined;
    if (direction == CodeLineRowNavigationDirection.next) {
      const startIndex = (this.commentThreadNavaigationPointer && this.commentThreadNavaigationPointer >= firstVisible && this.commentThreadNavaigationPointer <= lastVisible) ? 
        this.commentThreadNavaigationPointer + 1 : firstVisible;
      navigateToRow = this.findNextCommentThread(startIndex);
    }
    else {
      const startIndex = (this.commentThreadNavaigationPointer && this.commentThreadNavaigationPointer >= firstVisible && this.commentThreadNavaigationPointer <= lastVisible) ? 
        this.commentThreadNavaigationPointer - 1 : lastVisible;
      navigateToRow = this.findPrevCommentthread(startIndex);
    }

    if (navigateToRow) {
      this.scrollToNode(navigateToRow.nodeIdHashed);
    }
    else {
      this.messageService.add({ severity: 'info', icon: 'bi bi-info-circle', detail: 'No more active comments threads to navigate to.', key: 'bl', life: 3000 });
    }
  }

  navigateToDiffNode(direction: CodeLineRowNavigationDirection) {
    const firstVisible = this.codePanelRowSource?.adapter?.firstVisible!.$index!;
    const lastVisible = this.codePanelRowSource?.adapter?.lastVisible!.$index!;
    let navigateToRow : CodePanelRowData | undefined = undefined;
    if (direction == CodeLineRowNavigationDirection.next) {
      const startIndex = (this.diffNodeNavaigationPointer && this.diffNodeNavaigationPointer >= firstVisible && this.diffNodeNavaigationPointer <= lastVisible) ? 
        this.diffNodeNavaigationPointer : firstVisible;
      navigateToRow = this.findNextDiffNode(startIndex);
    }
    else {
      const startIndex = (this.diffNodeNavaigationPointer && this.diffNodeNavaigationPointer >= firstVisible && this.diffNodeNavaigationPointer <= lastVisible) ? 
        this.diffNodeNavaigationPointer: lastVisible;
      navigateToRow = this.findPrevDiffNode(startIndex);
    }

    if (navigateToRow) {
      this.scrollToNode(navigateToRow.nodeIdHashed);
    }
    else {
      this.messageService.add({ severity: 'info', icon: 'bi bi-info-circle', detail: 'No more diffs to navigate to.', key: 'bl', life: 3000 });
    }
  }

  private findNextCommentThread (index: number) : CodePanelRowData | undefined {
    while (index < this.codePanelRowData.length) {
      if (this.codePanelRowData[index].type === CodePanelRowDatatype.CommentThread && !this.codePanelRowData![index].isResolvedCommentThread) {
        this.commentThreadNavaigationPointer = index;
        return this.codePanelRowData[index];
      }
      index++;
    }
    return undefined;
  }

  private findPrevCommentthread (index: number) : CodePanelRowData | undefined {
    while (index < this.codePanelRowData.length && index >= 0) {
      if (this.codePanelRowData[index].type === CodePanelRowDatatype.CommentThread && !this.codePanelRowData![index].isResolvedCommentThread) {
        this.commentThreadNavaigationPointer = index;
        return this.codePanelRowData[index];
      }
      index--;
    }
    return undefined;
  }

  private findNextDiffNode (index: number) : CodePanelRowData | undefined {
    let checkForDiffNode = (isDiffRow(this.codePanelRowData[index])) ? false : true;
    while (index < this.codePanelRowData.length) {
      if (!checkForDiffNode && !isDiffRow(this.codePanelRowData[index])) {
        checkForDiffNode = true;
      }
      if (checkForDiffNode && isDiffRow(this.codePanelRowData[index])) {
        this.diffNodeNavaigationPointer = index;
        return this.codePanelRowData[index];
      }
      index++;
    }
    return undefined;
  }

  private findPrevDiffNode (index: number) : CodePanelRowData | undefined {
    let checkForDiffNode = (isDiffRow(this.codePanelRowData[index])) ? false : true;
    while (index < this.codePanelRowData.length && index >= 0) {
      if (!checkForDiffNode && !isDiffRow(this.codePanelRowData[index])) {
        checkForDiffNode = true;
      }
      if (checkForDiffNode && isDiffRow(this.codePanelRowData[index])) {
        this.diffNodeNavaigationPointer = index;
        return this.codePanelRowData[index];
      }
      index--;
    }
    return undefined;
  }
  
  showNoDiffInContentMessage() {
    return this.codePanelData && !this.isLoading && this.isDiffView && !this.codePanelData?.hasDiff
  }

  private updateHasActiveConversations() {
    let hasActiveConversation = false;
    for (let row of this.codePanelRowData) {
      if (row.type === CodePanelRowDatatype.CommentThread) {
        if (row.comments && row.comments.length > 0 && row.isResolvedCommentThread === false) {
          hasActiveConversation = true;
          break;
        }
      }
    }
    this.hasActiveConversationEmitter.emit(hasActiveConversation);
  }

  private loadCodePanelViewPort() {
    this.setMaxLineNumberWidth();
    this.initializeDataSource().then(() => {
      this.codePanelRowSource?.adapter?.init$.pipe(take(1)).subscribe(() => {
        this.isLoading = false;
        setTimeout(() => {
          this.scrollToNode(undefined, this.scrollToNodeId);
        }, 500);
        
      });
    }).catch((error) => {
      console.error(error);
    });
  }

  private updateCommentTextInCommentThread(data: CommentUpdatesDto) {
    this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!].comments.filter(c => c.id === data.commentId)[0].commentText = data.commentText!;
    this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!]);
    this.updateHasActiveConversations();
  }

  private addCommentToCommentThread(commentUpdates: CommentUpdatesDto, newComment: CommentItemModel) {
    const commentThreadsForNode = this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread;
    if (!commentThreadsForNode) {
      this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread = {};
      const commentThreadRow = new CodePanelRowData();
      commentThreadRow.type = CodePanelRowDatatype.CommentThread;
      commentThreadRow.rowClasses = new Set<string>(['user-comment-thread']);
      commentThreadRow.nodeIdHashed = commentUpdates.nodeIdHashed!;
      commentThreadRow.associatedRowPositionInGroup = commentUpdates.associatedRowPositionInGroup!;
      commentThreadRow.comments = [newComment];
      this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!] = commentThreadRow;
      this.insertItemsIntoScroller([commentThreadRow], commentThreadRow.nodeIdHashed!, CodePanelRowDatatype.CodeLine, commentThreadRow.associatedRowPositionInGroup, "toggleCommentsClasses", "can-show", "show");
      this.updateHasActiveConversations();
    }
    else if (commentThreadsForNode.hasOwnProperty(commentUpdates.associatedRowPositionInGroup!)) {
      let comments = commentThreadsForNode[commentUpdates.associatedRowPositionInGroup!].comments;
      if (!comments.some(c => c.id === newComment.id)) {
        comments.push(newComment);
        this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!].comments = [...comments]
        this.updateItemInScroller(this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!]);
        this.updateHasActiveConversations();
      }
    }
  }

  private deleteCommentFromCommentThread(commentUpdates: CommentUpdatesDto) {
    const comments = this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!].comments;
    this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!].comments = comments.filter(c => c.id !== commentUpdates.commentId);

    if (this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!].comments.length === 0) {
      this.removeItemsFromScroller(commentUpdates.nodeIdHashed!, CodePanelRowDatatype.CommentThread, "toggleCommentsClasses", "show", "can-show", commentUpdates.associatedRowPositionInGroup);
      this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread = {};
    } else {
      this.updateItemInScroller(this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!]);
    }
    this.updateHasActiveConversations();
  }

  private applyCommentResolutionUpdate(commentUpdates: CommentUpdatesDto) {
    this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!].isResolvedCommentThread = (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved)? true : false;
    this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!].commentThreadIsResolvedBy = commentUpdates.resolvedBy!;
    this.updateItemInScroller({ ...this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!]});
    this.updateHasActiveConversations();
  }

  private toggleCommentUpVote(data: CommentUpdatesDto) {
    const comment = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!].comments.find(c => c.id === data.commentId);
    if (comment) {
      if (comment.upvotes.includes(this.userProfile?.userName!)) {
        comment.upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
      } else {
        comment.upvotes.push(this.userProfile?.userName!);
      }
      this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!]);
    }
  }

  private findRowForCommentUpdates(commentId: string, elementId: string) : CodePanelRowData | undefined {
    for (const key in this.codePanelData?.nodeMetaData) {
      const commentThreadsInNode = this.codePanelData?.nodeMetaData[key].commentThread;
      if (commentThreadsInNode && Object.keys(commentThreadsInNode).length > 0) {
        for (const commentThreadKey in commentThreadsInNode) {
          for (let comment of commentThreadsInNode[commentThreadKey].comments) {
            if (comment.id === commentId || comment.elementId === elementId) {
              return commentThreadsInNode[commentThreadKey];
            }
          }
        }
      }
    }
    return undefined
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
