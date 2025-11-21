import { ChangeDetectorRef, Component, ElementRef, EventEmitter, Input, OnChanges, Output, QueryList, SimpleChanges, ViewChildren } from '@angular/core';
import { filter, take, takeUntil } from 'rxjs/operators';
import { Datasource, IDatasource, SizeStrategy } from 'ngx-ui-scroll';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { ActivatedRoute, Router } from '@angular/router';
import { CodeLineRowNavigationDirection, convertRowOfTokensToString, isDiffRow, DIFF_ADDED, DIFF_REMOVED, getCodePanelRowDataClass, getStructuredTokenClass } from 'src/app/_helpers/common-helpers';
import { SCROLL_TO_NODE_QUERY_PARAM } from 'src/app/_helpers/router-helpers';
import { CodePanelData, CodePanelRowData, CodePanelRowDatatype, CrossLanguageContentDto, CrossLanguageRowDto} from 'src/app/_models/codePanelModels';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { UserProfile } from 'src/app/_models/userProfile';
import { Message } from 'primeng/api/message';
import { MenuItem, MenuItemCommandEvent, MessageService } from 'primeng/api';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { fromEvent, Observable, Subject } from 'rxjs';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { Menu } from 'primeng/menu';
import { CodeLineSearchInfo, CodeLineSearchMatch } from 'src/app/_models/codeLineSearchInfo';
import { DoublyLinkedList } from 'src/app/_helpers/doubly-linkedlist';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent implements OnChanges{
  @Input() codePanelRowData: CodePanelRowData[] = [];
  @Input() crossLanguageRowData: CrossLanguageContentDto[] = [];
  @Input() codePanelData: CodePanelData | null = null;
  @Input() isDiffView: boolean = false;
  @Input() language: string | undefined;
  @Input() languageSafeName: string | undefined;
  @Input() scrollToNodeIdHashed: Observable<string> | undefined;
  @Input() scrollToNodeId : string | undefined;
  @Input() reviewId: string | undefined;
  @Input() activeApiRevisionId: string | undefined;
  @Input() userProfile : UserProfile | undefined;
  @Input() showLineNumbers: boolean = true;
  @Input() loadFailed : boolean = false;
  @Input() loadFailedMessage : string | undefined;
  @Input() codeLineSearchText: string | undefined;
  @Input() codeLineSearchInfo: CodeLineSearchInfo | undefined = undefined;
  @Input() preferredApprovers : string[] = [];
  @Input() allComments: CommentItemModel[] = [];

  @Output() hasActiveConversationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() codeLineSearchInfoEmitter : EventEmitter<CodeLineSearchInfo> = new EventEmitter<CodeLineSearchInfo>();
  
  @ViewChildren(Menu) menus!: QueryList<Menu>;
  
  noDiffInContentMessage : Message[] = [{ severity: 'info', icon:'bi bi-info-circle', detail: 'There is no difference between the two API revisions.' }];

  isLoading: boolean = true;
  codeWindowHeight: string | undefined = undefined;
  codePanelRowDataIndicesMap = new Map<string, number>();

  codePanelRowSource: IDatasource<CodePanelRowData> | undefined;
  CodePanelRowDatatype = CodePanelRowDatatype;

  searchMatchedRowInfo: Map<string, RegExpMatchArray[]> = new Map<string, RegExpMatchArray[]>();
  codeLineSearchMatchInfo : DoublyLinkedList<CodeLineSearchMatch> | undefined = undefined;

  destroy$ = new Subject<void>();

  commentThreadNavaigationPointer: number | undefined = undefined;
  diffNodeNavaigationPointer: number | undefined = undefined;

  menuItemsLineActions: MenuItem[] = [];

  constructor(private changeDetectorRef: ChangeDetectorRef, private commentsService: CommentsService, 
    private signalRService: SignalRService, private route: ActivatedRoute, private router: Router,
    private messageService: MessageService, private elementRef: ElementRef<HTMLElement>) { }

  ngOnInit() {
    this.codeWindowHeight = `${window.innerHeight - 80}`;
    this.handleRealTimeCommentUpdates();

    this.menuItemsLineActions = [
      { label: 'Copy line', icon: 'bi bi-clipboard', command: (event) => this.copyCodeLineToClipBoard(event) },
      { label: 'Copy permalink', icon: 'bi bi-clipboard', command: (event) => this.copyCodeLinePermaLinkToClipBoard(event) }
    ];

    fromEvent<KeyboardEvent>(document, 'keydown')
      .pipe(
        filter(event => event.ctrlKey && event.key === 'Enter'),
        takeUntil(this.destroy$)
      ).subscribe(event => this.handleKeyboardEvents(event));

    this.scrollToNodeIdHashed?.pipe(takeUntil(this.destroy$)).subscribe((nodeIdHashed) => {
      this.scrollToNode(nodeIdHashed);
    });
  }

  async ngOnChanges(changes: SimpleChanges) {
    if (changes['codePanelRowData']) {
      if (changes['codePanelRowData'].currentValue.length > 0) {
        this.loadCodePanelViewPort();
        this.updateHasActiveConversations();
      } else {
        this.isLoading = true;
        this.codePanelRowSource = undefined;
      }
    }

    if (changes['loadFailed'] && changes['loadFailed'].currentValue) {
      this.isLoading = false;
    }

    if (changes['codeLineSearchText']) {
      await this.searchCodePanelRowData(this.codeLineSearchText!);
    }

    if (changes['codeLineSearchInfo'] && changes['codeLineSearchInfo'].currentValue != changes['codeLineSearchInfo'].previousValue) {
      this.navigateToCodeLineWithSearchMatch();
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
    return getCodePanelRowDataClass(row);
  }

  getLineNumberClassObject(row: CodePanelRowData) {
    let classObject: { [key: string]: boolean } = {};
    classObject['line-number'] = true;
    if (row.crossLanguageId && this.crossLanguageRowData.some(item => row.crossLanguageId.toLowerCase() in item.content)) {
      classObject['has-cross-language'] = true;
    }
    return classObject;
  }

  getTokenClassObject(token: StructuredToken) {
    return getStructuredTokenClass(token);
  }

  getLineMenu(row: CodePanelRowData) {
    if (row.crossLanguageId && this.crossLanguageRowData.some(item => row.crossLanguageId.toLowerCase() in item.content)) {
      const menu = [...this.menuItemsLineActions];
      menu.push({ label: 'Cross language', icon: 'bi bi-arrow-left-right', command: (event) => this.showCrossLanguageView(event, row.crossLanguageId) });
      return menu;
    } else {
      return this.menuItemsLineActions;
    }
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

  toggleLineActionMenu(event: any, id: string) {
    const menu: Menu | undefined = this.menus.find(menu => menu.el.nativeElement.getAttribute('data-line-action-menu-id') == id);
    if (menu) {
      menu.toggle(event);
    }
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
    // Find the actual index in codePanelRowData
    let targetIndex = this.codePanelRowData.findIndex(row => {
      if (row.nodeIdHashed === updateData.nodeIdHashed && row.type === updateData.type) {
        if (updateData.type === CodePanelRowDatatype.CommentThread) {
          return row.associatedRowPositionInGroup === updateData.associatedRowPositionInGroup;
        }
        return true;
      }
      return false;
    });

    // Update the actual array reference
    if (targetIndex !== -1) {
      this.codePanelRowData[targetIndex] = updateData;
    }
      
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

  async scrollToNode( 
    nodeIdHashed: string | undefined = undefined, nodeId: string | undefined = undefined,
    highlightRow: boolean = true, updateQueryParams: boolean = true): Promise<void> {
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

        if (highlightRow) {
          this.codePanelRowData[index].rowClasses.add('active');
        }

        indexesHighlighted.push(index);
        if (!scrollIndex) {
          scrollIndex = index;
        }
      }
      index++;
    }
    if (scrollIndex) {
      scrollIndex = Math.max(scrollIndex - 4, 0);

      if (scrollIndex < this.codePanelRowSource?.adapter?.bufferInfo.firstIndex! ||
        scrollIndex > this.codePanelRowSource?.adapter?.bufferInfo.lastIndex!
      ) {
        await this.codePanelRowSource?.adapter?.reload(scrollIndex);
      } else {
        await this.codePanelRowSource?.adapter?.fix({
          scrollToItem: (item) => item.data.nodeIdHashed === nodeIdHashed,
          scrollToItemOpt: { behavior: 'smooth', block: 'center' }
        });
      }

      let newQueryParams = getQueryParams(this.route);
      if (updateQueryParams) {
        newQueryParams[SCROLL_TO_NODE_QUERY_PARAM] = this.codePanelRowData[scrollIndex].nodeId;
      } else {
        newQueryParams[SCROLL_TO_NODE_QUERY_PARAM] = null;
      }
      this.router.navigate([], { queryParams: newQueryParams, state: { skipStateUpdate: true } });

      if (highlightRow) {
        setTimeout(() => {
          indexesHighlighted.forEach((index) => {
            this.codePanelRowData[index].rowClasses?.delete('active');
          });
        }, 1550);
      }
    }
  }

  setMaxLineNumberWidth() {
    if (this.codePanelRowData[this.codePanelRowData.length - 1].lineNumber) {
      document.documentElement.style.setProperty('--max-line-number-width', `${this.codePanelRowData[this.codePanelRowData.length - 1].lineNumber!.toString().length + 1}ch`);
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
          }
        });    
      }
    else {
      this.commentsService.createComment(this.reviewId!, this.activeApiRevisionId!, commentUpdates.nodeId!, commentUpdates.commentText!, CommentType.APIRevision, commentUpdates.allowAnyOneToResolve, commentUpdates.severity)
        .pipe(take(1)).subscribe({
            next: (response: CommentItemModel) => {
              this.addCommentToCommentThread(commentUpdates, response);
              commentUpdates.comment = response;
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
      }
    });
  }

  handleCommentResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!;
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
      this.commentsService.resolveComments(this.reviewId!, commentUpdates.elementId!).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
        }
      });    
    }
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
      this.commentsService.unresolveComments(this.reviewId!, commentUpdates.elementId!).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
        }
      });    
    }
  }

  handleBatchResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!;
    
    switch (commentUpdates.commentThreadUpdateAction) {
      case CommentThreadUpdateAction.CommentCreated:
        if (commentUpdates.comment) {
          this.addCommentToCommentThread(commentUpdates, commentUpdates.comment);
        }
        break;
      case CommentThreadUpdateAction.CommentTextUpdate:
        this.updateCommentTextInCommentThread(commentUpdates);
        break;
      case CommentThreadUpdateAction.CommentResolved:
        this.commentsService.resolveComments(this.reviewId!, commentUpdates.elementId!).pipe(take(1)).subscribe();
        break;
    }
  }

  handleCommentUpvoteActionEmitter(commentUpdates: CommentUpdatesDto){
    commentUpdates.reviewId = this.reviewId!;
    this.commentsService.toggleCommentUpVote(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe();
  }

  handleCommentDownvoteActionEmitter(commentUpdates: CommentUpdatesDto){
    commentUpdates.reviewId = this.reviewId!;
    this.commentsService.toggleCommentDownVote(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe();
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
            case CommentThreadUpdateAction.CommentDownVoteToggled:
              this.toggleCommentDownVote(commentUpdates);
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

  handleCloseCrossLanguageViewEmitter(event: any) {
    this.removeItemsFromScroller(event[0], event[1] as CodePanelRowDatatype);
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
      this.messageService.add({ severity: 'info', icon: 'bi bi-info-circle', summary: 'Comment Navigation', detail: 'No more active comments threads to navigate to.', key: 'bc', life: 3000 });
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
      this.messageService.add({ severity: 'info', icon: 'bi bi-info-circle', summary: 'Diff Navigation', detail: 'No more diffs to navigate to.', key: 'bc', life: 3000 });
    }
  }

  copyReviewTextToClipBoard(isDiffView: boolean) {
    const reviewText : string [] = [];
    this.codePanelRowData.forEach((row) => {
      if (row.rowOfTokens && row.rowOfTokens.length > 0) {
        let codeLineText = convertRowOfTokensToString(row.rowOfTokens);
        if (isDiffView) {
          switch (row.diffKind) {
            case DIFF_ADDED:
              codeLineText = `+ ${codeLineText}`;
              break; 
            case DIFF_REMOVED:
              codeLineText = `- ${codeLineText}`;
              break;
            default :
              codeLineText = `  ${codeLineText}`;
              break;
          }
        }

        if (row.indent && row.indent > 0) {
          codeLineText = '\t'.repeat(row.indent - 1) + codeLineText;
        }
        reviewText.push(codeLineText);
      }
    });
    navigator.clipboard.writeText(reviewText.join('\n'));
  }

  showNoDiffInContentMessage() {
    return this.codePanelData && !this.isLoading && this.isDiffView && !this.codePanelData?.hasDiff
  }

  async searchCodePanelRowData(searchText: string) {
    this.searchMatchedRowInfo.clear();
    if (!searchText || searchText.length === 0) {
      this.clearSearchMatchHighlights();
      this.codeLineSearchMatchInfo = undefined;
      this.codeLineSearchInfo = undefined;
      this.codeLineSearchInfoEmitter.emit(this.codeLineSearchInfo);
      return;
    }

    let hasMatch = false;
    this.codeLineSearchMatchInfo = new DoublyLinkedList<CodeLineSearchMatch>();
    
    this.codePanelRowData.forEach((row, idx) => {
      let codeLineAsString = undefined;
      if (row.type == CodePanelRowDatatype.CodeLine || row.type == CodePanelRowDatatype.Documentation) {
        codeLineAsString = convertRowOfTokensToString(row.rowOfTokens);
      } else if (row.type == CodePanelRowDatatype.Diagnostics) {
        codeLineAsString = row.diagnostics.text;
      }

      if (codeLineAsString) {
        codeLineAsString = this.escapeHtml(codeLineAsString);
        const regex = new RegExp(searchText, "gi");
        const matches = [...codeLineAsString.matchAll(regex)];
        if (matches.length > 0) {
          hasMatch = true;
          const matchKey = `${row.nodeIdHashed}-${row.type}-${row.rowPositionInGroup}`;
          this.searchMatchedRowInfo.set(matchKey, matches);
          matches.forEach((match, index) => {
            const searchMatch = new CodeLineSearchMatch(idx, row.type, row.rowPositionInGroup, row.nodeIdHashed!, index);
            this.codeLineSearchMatchInfo!.append(searchMatch);
          });
        }
      }
    });
    
    if (hasMatch) {
      this.codeLineSearchInfo = new CodeLineSearchInfo(this.codeLineSearchMatchInfo.head, this.codeLineSearchMatchInfo.length);

      if (this.codeLineSearchInfo.currentMatch?.value.rowIndex! < this.codePanelRowSource?.adapter?.firstVisible.$index! ||
        this.codeLineSearchInfo.currentMatch?.value.rowIndex! > this.codePanelRowSource?.adapter?.lastVisible.$index!) {
          // Scroll first match into view
        await this.scrollToNode(this.codeLineSearchInfo.currentMatch!.value.nodeIdHashed, undefined, false, false);
        await this.codePanelRowSource?.adapter?.relax();
      }

      this.highlightSearchMatches();
      this.highlightActiveSearchMatch();
    } else {
      this.clearSearchMatchHighlights();
      this.codeLineSearchMatchInfo = undefined;
      this.codeLineSearchInfo = undefined;
    }
    this.codeLineSearchInfoEmitter.emit(this.codeLineSearchInfo);
  }

  async highlightSearchMatches() {
    this.clearSearchMatchHighlights();
    const codeLines = this.elementRef.nativeElement.querySelectorAll('.code-line');
    
    codeLines.forEach((codeLine) => {
      const nodeIdhashed = codeLine.getAttribute('data-node-id');
      const rowType = codeLine.getAttribute('data-row-type');
      const rowPositionInGroup = codeLine.getAttribute('data-row-position-in-group');
      const matchKey = `${nodeIdhashed}-${rowType}-${rowPositionInGroup}`;
      if (this.searchMatchedRowInfo.has(matchKey)) {
        const tokens = codeLine.querySelectorAll('.code-line-content > span');
        const matches = this.searchMatchedRowInfo.get(matchKey)!;

        let currentOffset = 0;
        let matchIndex = 0;

        tokens.forEach((token) => {
          const tokenContent = token.innerHTML || '';
          const tokenLength = tokenContent.length;

          let newInnerHTML = '';
          let lastIndex = 0;

          for (let i = matchIndex; i < matches.length; i++) {          
            let match = matches[i];
            const matchStartIndex = match.index!;
            const matchEndIndex = matchStartIndex + match[0].length;
  
            const tokenStart = currentOffset;
            const tokenEnd = currentOffset + tokenLength;
  
            if (matchStartIndex < tokenEnd && matchEndIndex > tokenStart) {
              const highlightStart = Math.max(0, matchStartIndex - tokenStart);
              const highlightEnd = Math.min(tokenLength, matchEndIndex - tokenStart);
        
              const beforeMatch = tokenContent.slice(lastIndex, highlightStart);
              const matchText = tokenContent.slice(highlightStart, highlightEnd);
              lastIndex = highlightEnd;
        
              newInnerHTML += `${beforeMatch}<mark class="codeline-search-match-highlight search-match-${i}">${matchText}</mark>`;
              matchIndex++;
            }
          }

          newInnerHTML += tokenContent.slice(lastIndex);
          token.innerHTML = newInnerHTML;
          currentOffset += tokenLength;
        });
      }
    });
  }

  async clearSearchMatchHighlights() {
    this.elementRef.nativeElement.querySelectorAll('.codeline-search-match-highlight').forEach((element) => {
      const parent = element.parentNode as HTMLElement;
      if (parent) {
        parent.innerHTML = this.escapeHtml(parent.textContent!) || '';
      }
    });
  }

  private escapeHtml(text: string) {
    const element = document.createElement('div');
    element.textContent = text;
    return element.innerHTML;
  }

  private getCodeLineIndex(event: MenuItemCommandEvent) {
    const target = (event.originalEvent?.target as Element).closest("span") as Element;
    return target.getAttribute('data-item-id');
  }

  private copyCodeLinePermaLinkToClipBoard(event: MenuItemCommandEvent) {
    const codeLineIndex = this.getCodeLineIndex(event);
    const codeLine = this.codePanelRowData[parseInt(codeLineIndex!, 10)];
    const queryParams = { ...this.route.snapshot.queryParams };
    queryParams[SCROLL_TO_NODE_QUERY_PARAM] = codeLine.nodeId;
    const updatedUrl = this.router.createUrlTree([], {
      relativeTo: this.route,
      queryParams: queryParams,
      queryParamsHandling: 'merge'
    }).toString();
    const fullExternalUrl = window.location.origin + updatedUrl;
    navigator.clipboard.writeText(fullExternalUrl);
  }

  private copyCodeLineToClipBoard(event: MenuItemCommandEvent) {
    const codeLineIndex = this.getCodeLineIndex(event);
    const codeLine = this.codePanelRowData[parseInt(codeLineIndex!, 10)];
    const codeLineText = convertRowOfTokensToString(codeLine.rowOfTokens);
    navigator.clipboard.writeText(codeLineText);
  }

  private showCrossLanguageView(event: MenuItemCommandEvent, crossLanguageId: string) {
    const codeLineIndex = this.getCodeLineIndex(event);
    const codeLine = this.codePanelRowData[parseInt(codeLineIndex!, 10)];

    const crossLanguageRow = new CodePanelRowData();
    crossLanguageRow.type = CodePanelRowDatatype.CrossLanguage;

    for (const entry of this.crossLanguageRowData) {
      const crossLangLines = entry.content[crossLanguageId.toLowerCase()];
      const crossLangaugeLines = {
        codeLines: crossLangLines,
        apiRevisionId: entry.apiRevisionId,
        reviewId: entry.reviewId,
        language: entry.language,
        packageName: entry.packageName,
        packageVersion: entry.packageVersion,
      } as CrossLanguageRowDto;
      crossLanguageRow.crossLanguageLines.set(entry.language, crossLangaugeLines);
    }

    crossLanguageRow.nodeIdHashed = codeLine.nodeIdHashed;
    crossLanguageRow.nodeId = codeLine.nodeId;
    crossLanguageRow.rowClasses = new Set<string>(['cross-language-view']);
    crossLanguageRow.associatedRowPositionInGroup = codeLine.rowPositionInGroup;
    this.insertItemsIntoScroller([crossLanguageRow], crossLanguageRow.nodeIdHashed, codeLine.type, crossLanguageRow.associatedRowPositionInGroup);
  }

  private highlightActiveSearchMatch(scrollIntoView: boolean = true) {
    if (this.codeLineSearchInfo?.currentMatch) {
      const nodeIdHashed = this.codeLineSearchInfo?.currentMatch.value.nodeIdHashed;
      const rowPositionInGroup = this.codeLineSearchInfo?.currentMatch.value.rowPositionInGroup;
      const rowType = this.codeLineSearchInfo?.currentMatch.value.rowType;
      const matchId = this.codeLineSearchInfo?.currentMatch.value.matchId;

      const activeMatch = this.elementRef.nativeElement.querySelector('.codeline-search-match-highlight.active');
      if (activeMatch) {
        activeMatch.classList.remove('active');
      }
      const codeLine = this.elementRef.nativeElement.querySelector(
        `.code-line[data-node-id="${nodeIdHashed}"][data-row-position-in-group="${rowPositionInGroup}"][data-row-type="${rowType}"]`    
      );
      if (codeLine) {
        const match = codeLine.querySelector(`.search-match-${matchId}`) as HTMLElement;
        if (match) {
          setTimeout(() => {
            match.classList.add('active');
            if (scrollIntoView) {
              match.scrollIntoView({ behavior: 'smooth', inline: 'center' });
            }
          }, 0);
        }
      }
    }
  }

  /**
   * Navigates to the next or previous code line that contains a search match but is outside the viewport
   */
  private navigateToCodeLineWithSearchMatch() {
    if (this.codeLineSearchInfo?.currentMatch) {
      const firstVisibleIndex = this.codePanelRowSource?.adapter?.firstVisible.$index!;
      const lastVisibleIndex = this.codePanelRowSource?.adapter?.lastVisible.$index!;

      if (this.codeLineSearchInfo?.currentMatch && (this.codeLineSearchInfo?.currentMatch.value.rowIndex < firstVisibleIndex || this.codeLineSearchInfo?.currentMatch.value.rowIndex > lastVisibleIndex)) {
        this.scrollToNode(this.codeLineSearchInfo?.currentMatch.value.nodeIdHashed, undefined, false, false);
        this.codePanelRowSource?.adapter?.relax();
      }
      this.highlightActiveSearchMatch();
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
          const viewport = this.elementRef.nativeElement.ownerDocument.getElementById('viewport');
          if (viewport) {
            viewport.addEventListener('scroll', (event) => {
              if (this.codeLineSearchInfo?.currentMatch) {
                this.highlightSearchMatches();
                this.highlightActiveSearchMatch(false);
              }
            });
          }
        }, 500);
      });
    }).catch((error) => {
      console.error(error);
    });
  }

  private updateCommentTextInCommentThread(data: CommentUpdatesDto) {
    const commentThread = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!];
    const comment = commentThread.comments.find(c => c.id === data.commentId);
    
    if (!comment) {
      return;
    }
    
    if (data.commentText) {
      comment.commentText = data.commentText;
    }
    if (data.severity !== undefined && data.severity !== null) {
      comment.severity = data.severity;
    }
    this.updateItemInScroller(commentThread);
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
    const commentThread = this.codePanelData!.nodeMetaData[commentUpdates.nodeIdHashed!].commentThread[commentUpdates.associatedRowPositionInGroup!];
    const isResolved = (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved);
    
    commentThread.isResolvedCommentThread = isResolved;
    commentThread.commentThreadIsResolvedBy = commentUpdates.resolvedBy!;
  
    this.updateItemInScroller(commentThread);
    this.updateHasActiveConversations();
  }

  private toggleCommentUpVote(data: CommentUpdatesDto) {
    const comment = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!].comments.find(c => c.id === data.commentId);
    if (comment) {
      this.toggleVoteUp(comment);
      this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!]);
    }
  }

  private toggleCommentDownVote(data: CommentUpdatesDto) {
    const comment = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!].comments.find(c => c.id === data.commentId);
    if (comment) {
      this.toggleVoteDown(comment);
      this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup!]);
    }
  }

  private toggleVoteUp(comment: CommentItemModel) {
    if (comment) {
      if (comment.upvotes.includes(this.userProfile?.userName!)) {
        comment.upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
      } else {
        comment.upvotes.push(this.userProfile?.userName!);
        if (comment.downvotes.includes(this.userProfile?.userName!)) {
          comment.downvotes.splice(comment.downvotes.indexOf(this.userProfile?.userName!), 1);
        }
      }
    }
  }

   private toggleVoteDown(comment: CommentItemModel) {
    if (comment) {
      if (comment.downvotes.includes(this.userProfile?.userName!)) {
        comment.downvotes.splice(comment.downvotes.indexOf(this.userProfile?.userName!), 1);
      } else {
        comment.downvotes.push(this.userProfile?.userName!);
        if (comment.upvotes.includes(this.userProfile?.userName!)) {
          comment.upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
        }
      }
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

  private handleKeyboardEvents(event: KeyboardEvent): void {
    const activeElement = document.activeElement as HTMLElement;
    if (activeElement?.tagName.toLowerCase() === "textarea") {
      const editorContainer = activeElement.closest('.edit-editor-container, .reply-editor-container');
      if (editorContainer) {
        const submitButton = editorContainer.querySelector('.editor-action-btn.submit') as HTMLButtonElement;
        if (submitButton && !submitButton.disabled) {
          submitButton.click();
        }
      }
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}