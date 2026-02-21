import { ChangeDetectorRef, Component, ElementRef, EventEmitter, Input, OnChanges, Output, QueryList, SimpleChanges, ViewChildren } from '@angular/core';
import { filter, take, takeUntil } from 'rxjs/operators';
import { Datasource, IDatasource, SizeStrategy } from 'ngx-ui-scroll';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { ActivatedRoute, Router } from '@angular/router';
import { CodeLineRowNavigationDirection, convertRowOfTokensToString, isDiffRow, DIFF_ADDED, DIFF_REMOVED, getCodePanelRowDataClass, getStructuredTokenClass } from 'src/app/_helpers/common-helpers';
import { SCROLL_TO_NODE_QUERY_PARAM } from 'src/app/_helpers/router-helpers';
import { CodePanelData, CodePanelRowData, CodePanelRowDatatype, CrossLanguageContentDto, CrossLanguageRowDto } from 'src/app/_models/codePanelModels';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { CommentItemModel, CommentSeverity, CommentSource, CommentType } from 'src/app/_models/commentItemModel';
import { UserProfile } from 'src/app/_models/userProfile';
import { MenuItem, MenuItemCommandEvent, MessageService, ToastMessageOptions } from 'primeng/api';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { fromEvent, Observable, Subject } from 'rxjs';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { Menu } from 'primeng/menu';
import { CodeLineSearchInfo, CodeLineSearchMatch } from 'src/app/_models/codeLineSearchInfo';
import { DoublyLinkedList } from 'src/app/_helpers/doubly-linkedlist';

@Component({
    selector: 'app-code-panel',
    templateUrl: './code-panel.component.html',
    styleUrls: ['./code-panel.component.scss'],
    standalone: false
})
export class CodePanelComponent implements OnChanges {
  @Input() codePanelRowData: CodePanelRowData[] = [];
  @Input() crossLanguageRowData: CrossLanguageContentDto[] = [];
  @Input() codePanelData: CodePanelData | null = null;
  @Input() isDiffView: boolean = false;
  @Input() language: string | undefined;
  @Input() languageSafeName: string | undefined;
  @Input() scrollToNodeIdHashed: Observable<string> | undefined;
  @Input() scrollToNodeId: string | undefined;
  @Input() reviewId: string | undefined;
  @Input() activeApiRevisionId: string | undefined;
  @Input() userProfile: UserProfile | undefined;
  @Input() showLineNumbers: boolean = true;
  @Input() showDocumentation: boolean = true;
  @Input() loadFailed: boolean = false;
  @Input() loadFailedMessage: string | undefined;
  @Input() loadingMessage: string | undefined;
  @Input() codeLineSearchText: string | undefined;
  @Input() codeLineSearchInfo: CodeLineSearchInfo | undefined = undefined;
  @Input() allComments: CommentItemModel[] = [];

  @Output() hasActiveConversationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() codeLineSearchInfoEmitter : EventEmitter<CodeLineSearchInfo> = new EventEmitter<CodeLineSearchInfo>();

  @ViewChildren(Menu) menus!: QueryList<Menu>;

  noDiffInContentMessage : ToastMessageOptions[] = [{ severity: 'info', icon:'bi bi-info-circle', detail: 'There is no difference between the two API revisions.' }];

  isLoading: boolean = true;
  codeWindowHeight: string | undefined = undefined;
  codePanelRowDataIndicesMap = new Map<string, number>();

  codePanelRowSource: IDatasource<CodePanelRowData> | undefined;
  CodePanelRowDatatype = CodePanelRowDatatype;

  searchMatchedRowInfo: Map<string, RegExpMatchArray[]> = new Map<string, RegExpMatchArray[]>();
  codeLineSearchMatchInfo: DoublyLinkedList<CodeLineSearchMatch> | undefined = undefined;

  destroy$ = new Subject<void>();

  commentThreadNavigationPointer: number | undefined = undefined;
  diffNodeNavigationPointer: number | undefined = undefined;

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

    this.commentsService.severityChanged$.pipe(takeUntil(this.destroy$)).subscribe(({ commentId, newSeverity }) => {
      this.updateCommentSeverity(commentId, newSeverity);
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
    const nodeIdHashed = codeLine.getAttribute('data-node-id')!;
    const rowPositionInGroup = parseInt(codeLine.getAttribute('data-row-position-in-group')!, 10);
    const rowType = codeLine.getAttribute('data-row-type')!;
    const existingCommentThread = this.codePanelData?.nodeMetaData[nodeIdHashed]?.commentThread;
    const existingCodeLine = this.codePanelData?.nodeMetaData[nodeIdHashed!]?.codeLines[rowPositionInGroup];

    if (!existingCommentThread || !existingCommentThread[rowPositionInGroup]) {
      const threadId = this.generateThreadId(nodeIdHashed, rowPositionInGroup);
      const commentThreadRow = this.createCommentThreadRow(nodeIdHashed, existingCodeLine?.nodeId, rowPositionInGroup, threadId);

      if (!this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread) {
        this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread = {};
      }
      this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread[rowPositionInGroup] = [commentThreadRow];
      this.insertItemsIntoScroller([commentThreadRow], nodeIdHashed, rowType, rowPositionInGroup, "toggleCommentsClasses", "can-show", "show");
    } else {
      const row = this.codePanelRowData.find(r =>
        r.nodeIdHashed === nodeIdHashed &&
        r.type === CodePanelRowDatatype.CommentThread &&
        r.rowPositionInGroup === rowPositionInGroup
      );
      if (row) row.showReplyTextBox = true;
    }
  }

  canAddComment(item: CodePanelRowData): boolean {
    const hasNonWhitespaceContent = item.rowOfTokens &&
                                     item.rowOfTokens.some(token => token.value && token.value.trim().length > 0);

    // Handle rowClasses being either a Set or an Array (can happen after JSON deserialization)
    let isRemoved = false;
    if (item.rowClasses) {
      if (item.rowClasses instanceof Set) {
        isRemoved = item.rowClasses.has('removed');
      } else if (Array.isArray(item.rowClasses)) {
        isRemoved = (item.rowClasses as unknown as string[]).includes('removed');
      }
    }

    return item.type === CodePanelRowDatatype.CodeLine &&
           !isRemoved &&
           this.userProfile !== undefined &&
           hasNonWhitespaceContent;
  }

  addNewCommentThread(event: Event) {
    event.stopPropagation();
    const codeLine = (event.target as Element).closest('.code-line')!;
    const nodeIdHashed = codeLine.getAttribute('data-node-id')!;
    const rowPositionInGroup = parseInt(codeLine.getAttribute('data-row-position-in-group')!, 10);
    const rowType = codeLine.getAttribute('data-row-type')!;
    const existingCodeLine = this.codePanelData?.nodeMetaData[nodeIdHashed]?.codeLines[rowPositionInGroup];

    const existingThreads = this.codePanelData?.nodeMetaData[nodeIdHashed]?.commentThread?.[rowPositionInGroup];
    if (existingThreads?.some(t => t.showReplyTextBox && (!t.comments || t.comments.length === 0))) {
      return;
    }

    const threadId = this.generateThreadId(nodeIdHashed, rowPositionInGroup);
    const commentThreadRow = this.createCommentThreadRow(nodeIdHashed, existingCodeLine?.nodeId, rowPositionInGroup, threadId);

    if (!this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread) {
      this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread = {};
    }
    if (!this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread[rowPositionInGroup]) {
      this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread[rowPositionInGroup] = [];
    }

    this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread[rowPositionInGroup].push(commentThreadRow);
    this.insertItemsIntoScroller([commentThreadRow], nodeIdHashed, rowType, rowPositionInGroup, "toggleCommentsClasses", "can-show", "show");
  }  private generateThreadId(nodeIdHashed: string, rowPosition: number): string {
    return `${nodeIdHashed}-${rowPosition}-${Date.now()}`;
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

  async insertRowTypeIntoScroller(codePanelRowDatatype: CodePanelRowDatatype) {
    await this.codePanelRowSource?.adapter?.relax();

    const updatedCodeLinesData: CodePanelRowData[] = [];

    for (let i = 0; i < this.codePanelRowData.length; i++) {
      if (this.codePanelRowData[i].type === CodePanelRowDatatype.CodeLine && this.codePanelRowData[i].nodeIdHashed! in this.codePanelData?.nodeMetaData!) {
        const nodeData = this.codePanelData?.nodeMetaData[this.codePanelRowData[i].nodeIdHashed!];

        switch (codePanelRowDatatype) {
          case CodePanelRowDatatype.CommentThread:
            updatedCodeLinesData.push(this.codePanelRowData[i]);
            if (nodeData?.commentThread && nodeData?.commentThread.hasOwnProperty(this.codePanelRowData[i].rowPositionInGroup)) {
              const threadData = nodeData.commentThread[this.codePanelRowData[i].rowPositionInGroup] as any;
              if (Array.isArray(threadData)) {
                updatedCodeLinesData.push(...threadData);
              } else if (threadData && typeof threadData === 'object') {
                if (threadData.type || threadData.comments) {
                  updatedCodeLinesData.push(threadData);
                } else {
                  updatedCodeLinesData.push(...Object.values(threadData) as CodePanelRowData[]);
                }
              }
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
    insertPosition: number, propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string) {
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

  async removeRowTypeFromScroller(codePanelRowDatatype: CodePanelRowDatatype) {
    await this.codePanelRowSource?.adapter?.relax();

    const indexesToRemove: number[] = [];
    let filteredCodeLinesData: CodePanelRowData[] = [];
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

  /**
   * Removes diagnostic comment threads from the scroller in real-time.
   */
  async removeDiagnosticCommentThreads() {
    await this.codePanelRowSource?.adapter?.relax();

    const indexesToRemove: number[] = [];
    const filteredData: CodePanelRowData[] = [];

    for (let i = 0; i < this.codePanelRowData.length; i++) {
      if (this.isDiagnosticCommentThread(this.codePanelRowData[i])) {
        indexesToRemove.push(i);
      } else {
        filteredData.push(this.codePanelRowData[i]);
      }
    }

    this.codePanelRowData = filteredData;
    await this.codePanelRowSource?.adapter?.remove({ indexes: indexesToRemove });
  }

  /**
   * Inserts diagnostic comment threads back into the scroller from codePanelData.
   */
  async insertDiagnosticCommentThreads() {
    if (!this.codePanelData?.nodeMetaData) return;
    await this.codePanelRowSource?.adapter?.relax();

    for (const [nodeIdHashed, nodeMetaData] of Object.entries(this.codePanelData.nodeMetaData)) {
      if (!nodeMetaData.commentThread) continue;

      for (const [rowPosition, threads] of Object.entries(nodeMetaData.commentThread)) {
        if (!Array.isArray(threads)) continue;

        for (const thread of threads) {
          if (!this.isDiagnosticCommentThread(thread)) continue;

          thread.rowClasses = new Set<string>(thread.rowClasses as any);
          const insertIndex = this.findInsertIndexForThread(nodeIdHashed, parseInt(rowPosition));

          if (insertIndex >= 0) {
            this.codePanelRowData.splice(insertIndex, 0, thread);
            await this.codePanelRowSource?.adapter?.insert({ beforeIndex: insertIndex, items: [thread] });
          }
        }
      }
    }
  }

  private isDiagnosticCommentThread(row: CodePanelRowData): boolean {
    return row.type === CodePanelRowDatatype.CommentThread &&
      row.comments?.some(c => c.commentSource === CommentSource.Diagnostic) === true;
  }

  private findInsertIndexForThread(nodeIdHashed: string, rowPosition: number): number {
    for (let i = 0; i < this.codePanelRowData.length; i++) {
      const row = this.codePanelRowData[i];
      if (row.nodeIdHashed === nodeIdHashed && row.rowPositionInGroup === rowPosition) {
        let insertIndex = i + 1;
        // Skip existing comment threads at this position
        while (insertIndex < this.codePanelRowData.length &&
               this.codePanelRowData[insertIndex].type === CodePanelRowDatatype.CommentThread &&
               this.codePanelRowData[insertIndex].nodeIdHashed === nodeIdHashed) {
          insertIndex++;
        }
        return insertIndex;
      }
    }
    return -1;
  }

  async removeItemsFromScroller(nodeIdHashed: string, codePanelRowDatatype:  CodePanelRowDatatype,
    propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string, associatedRowPositionInGroup?: number, threadId?: string) {
    await this.codePanelRowSource?.adapter?.relax();

    const indexesToRemove: number[] = [];
    const filteredCodeLinesData: CodePanelRowData[] = [];

    for (let i = 0; i < this.codePanelRowData.length; i++) {
      const shouldRemove = this.codePanelRowData[i].nodeIdHashed === nodeIdHashed &&
        this.codePanelRowData[i].type === codePanelRowDatatype &&
        (!associatedRowPositionInGroup || this.codePanelRowData[i].associatedRowPositionInGroup === associatedRowPositionInGroup) &&
        (!threadId || this.codePanelRowData[i].threadId === threadId);

      if (!shouldRemove) {
        filteredCodeLinesData.push(this.codePanelRowData[i]);
      } else {
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

  private matchesThreadForUpdate(row: CodePanelRowData, updateData: CodePanelRowData, isLegacy: boolean): boolean {
    if (row.nodeIdHashed !== updateData.nodeIdHashed || row.type !== updateData.type) {
      return false;
    }
    if (row.associatedRowPositionInGroup !== updateData.associatedRowPositionInGroup) {
      return false;
    }
    if (updateData.type !== CodePanelRowDatatype.CommentThread) {
      return true;
    }

    return isLegacy
      ? this.isLegacyThreadId(row.threadId)
      : row.threadId === updateData.threadId;
  }

  async updateItemInScroller(updateData: CodePanelRowData) {
    const isLegacyThread = this.isLegacyThreadId(updateData.threadId);

    const targetIndex = this.codePanelRowData.findIndex(row =>
      this.matchesThreadForUpdate(row, updateData, isLegacyThread)
    );

    if (targetIndex === -1) {
      return;
    }

    this.codePanelRowData[targetIndex] = updateData;

    await this.codePanelRowSource?.adapter?.relax();
    await this.codePanelRowSource?.adapter?.fix({
      updater: ({ data }) => {
        if (this.matchesThreadForUpdate(data, updateData, isLegacyThread)) {
          Object.assign(data, updateData);
        }
        return true;
      }
    });
  }

  initializeDataSource(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.codePanelRowSource = new Datasource<CodePanelRowData>({
        get: (index, count, success) => {
          const data: CodePanelRowData[] = [];
          const maxValidIndex = this.codePanelRowData.length - 1;
          const startIndex = Math.max(0, index);
          const endIndex = Math.min(index + count - 1, maxValidIndex);

          for (let i = startIndex; i <= endIndex; i++) {
            if (this.codePanelRowData[i]) {
              data.push(this.codePanelRowData[i]);
            }
          }
          success(data);
        },
        settings: {
          bufferSize: (this.userProfile?.preferences.disableCodeLinesLazyLoading) ? this.codePanelRowData.length : 50,
          padding: 1,
          itemSize: 21,
          startIndex: 0,
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
    let scrollIndex: number | undefined = undefined;
    let indexesHighlighted: number[] = [];
    while (index < this.codePanelRowData.length) {
      if (scrollIndex && this.codePanelRowData[index].nodeIdHashed !== nodeIdHashed) {
        break;
      }
      if ((nodeIdHashed && this.codePanelRowData[index].nodeIdHashed === nodeIdHashed) || (nodeId && this.codePanelRowData[index].nodeId === nodeId)) {
        nodeIdHashed = this.codePanelRowData[index].nodeIdHashed;
        const rowClasses = this.ensureRowClassesSet(this.codePanelRowData[index]);

        if (highlightRow) {
          rowClasses.add('active');
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
            this.ensureRowClassesSet(this.codePanelRowData[index]).delete('active');
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

  /**
   * Ensures rowClasses is a proper Set. This is needed because when data is
   * deserialized from JSON, Sets become plain arrays.
   */
  private ensureRowClassesSet(row: CodePanelRowData): Set<string> {
    if (!row.rowClasses) {
      row.rowClasses = new Set<string>();
    } else if (!(row.rowClasses instanceof Set)) {
      // Convert array to Set if it was deserialized from JSON
      row.rowClasses = new Set<string>(row.rowClasses as unknown as string[]);
    }
    return row.rowClasses;
  }

  private isLegacyThreadId(threadId: string | undefined | null): boolean {
    return !threadId || threadId === '';
  }

  private findCommentThread(threads: CodePanelRowData[] | undefined, threadId: string | undefined | null): CodePanelRowData | undefined {
    if (!threads?.length) return undefined;
    if (this.isLegacyThreadId(threadId)) {
      return threads.find(t => this.isLegacyThreadId(t.threadId)) || threads[0];
    }
    return threads.find(t => t.threadId === threadId);
  }

  private findCommentThreadIndex(commentThreads: CodePanelRowData[] | undefined, threadId: string | undefined | null): number {
    if (!commentThreads || commentThreads.length === 0) return -1;
    if (this.isLegacyThreadId(threadId)) {
      const index = commentThreads.findIndex(t => this.isLegacyThreadId(t.threadId));
      return index !== -1 ? index : 0;
    }
    return commentThreads.findIndex(t => t.threadId === threadId);
  }


  private createCommentThreadRow(nodeIdHashed: string, nodeId: string | undefined, position: number, threadId: string, comments: CommentItemModel[] = []): CodePanelRowData {
    const row = new CodePanelRowData();
    row.type = CodePanelRowDatatype.CommentThread;
    row.nodeId = nodeId!;
    row.nodeIdHashed = nodeIdHashed;
    row.threadId = threadId;
    row.rowClasses = new Set<string>(['user-comment-thread']);
    row.associatedRowPositionInGroup = position;
    row.comments = comments;
    row.showReplyTextBox = comments.length === 0;
    return row;
  }

  async handleCancelCommentActionEmitter(commentUpdates: any) {
    const commentsInNode = this.codePanelData?.nodeMetaData[commentUpdates.nodeIdHashed]?.commentThread
    if (commentsInNode && commentsInNode.hasOwnProperty(commentUpdates.associatedRowPositionInGroup)) {
      const commentThreads = commentsInNode[commentUpdates.associatedRowPositionInGroup];
      const commentThread = this.findCommentThread(commentThreads, commentUpdates.threadId);
      if (commentThread && (!commentThread.comments || commentThread.comments.length === 0)) {
        await this.removeItemsFromScroller(commentUpdates.nodeIdHashed, CodePanelRowDatatype.CommentThread, "toggleCommentsClasses", "show", "can-show", commentUpdates.associatedRowPositionInGroup, commentUpdates.threadId);
        const index = commentThreads.indexOf(commentThread);
        if (index > -1) {
          commentThreads.splice(index, 1);
        }
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
      const isNewThread = commentUpdates.isReply === false;
      const resolutionLocked = commentUpdates.allowAnyOneToResolve !== undefined ? !commentUpdates.allowAnyOneToResolve : false;
      this.commentsService.createComment(this.reviewId!, this.activeApiRevisionId!, commentUpdates.nodeId!, commentUpdates.commentText!, CommentType.APIRevision, resolutionLocked, commentUpdates.severity, commentUpdates.threadId)
        .pipe(take(1)).subscribe({
            next: (response: CommentItemModel) => {
              if (!commentUpdates.threadId && response.threadId) {
                commentUpdates.threadId = response.threadId;
              }
              this.addCommentToCommentThread(commentUpdates, response);
              commentUpdates.comment = response;
              // Only refresh quality score for new threads, not replies
              if (isNewThread) {
                this.commentsService.notifyQualityScoreRefresh();
              }
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
        this.commentsService.notifyQualityScoreRefresh();
      }
    });
  }

  handleCommentResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!;
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
      this.commentsService.resolveComments(this.reviewId!, commentUpdates.elementId!, commentUpdates.threadId).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.commentsService.notifyQualityScoreRefresh();
        }
      });
    }
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
      this.commentsService.unresolveComments(this.reviewId!, commentUpdates.elementId!, commentUpdates.threadId).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.commentsService.notifyQualityScoreRefresh();
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
        this.applyCommentResolutionUpdate(commentUpdates);
        break;
    }
  }

  handleCommentUpvoteActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!;
    if (!commentUpdates.threadId && commentUpdates.commentId) {
      const codePanelRowData = this.findRowForCommentUpdates(commentUpdates.commentId, commentUpdates.elementId!);
      commentUpdates.threadId = codePanelRowData?.threadId;
    }

    this.commentsService.toggleCommentUpVote(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe();
  }

  handleCommentDownvoteActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.reviewId!;

    if (!commentUpdates.threadId && commentUpdates.commentId) {
      const codePanelRowData = this.findRowForCommentUpdates(commentUpdates.commentId, commentUpdates.elementId!);
      commentUpdates.threadId = codePanelRowData?.threadId;
    }

    this.commentsService.toggleCommentDownVote(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe();
  }

  handleRealTimeCommentUpdates() {
    this.signalRService.onCommentUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (commentUpdates: CommentUpdatesDto) => {
        if ((commentUpdates.reviewId && commentUpdates.reviewId == this.reviewId) ||
          (commentUpdates.comment && commentUpdates.comment.reviewId == this.reviewId)) {

          // Handle bulk auto-generated comments deletion before the per-comment guard checks
          if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.AutoGeneratedCommentsDeleted) {
            this.removeAllAutoGeneratedComments();
            return;
          }

          if (!commentUpdates.nodeIdHashed || commentUpdates.associatedRowPositionInGroup == undefined) {
            const codePanelRowData = this.findRowForCommentUpdates(commentUpdates.commentId!, commentUpdates.elementId!);
            commentUpdates.nodeIdHashed = codePanelRowData?.nodeIdHashed;
            commentUpdates.associatedRowPositionInGroup = codePanelRowData?.associatedRowPositionInGroup;
            if (!commentUpdates.threadId) {
              commentUpdates.threadId = codePanelRowData?.threadId;
            }
          }

          if (!commentUpdates.threadId && commentUpdates.comment) {
            const codePanelRowData = this.findRowForCommentUpdates(commentUpdates.comment.id, commentUpdates.comment.elementId);
            commentUpdates.threadId = codePanelRowData?.threadId;
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

  handleCommentThreadNavigationEmitter(event: any) {
    this.commentThreadNavigationPointer = Number(event.commentThreadNavigationPointer);
    this.navigateToCommentThread(event.direction);
  }

  handleCloseCrossLanguageViewEmitter(event: any) {
    this.removeItemsFromScroller(event[0], event[1] as CodePanelRowDatatype);
  }

  navigateToCommentThread(direction: CodeLineRowNavigationDirection) {
    const firstVisible = this.codePanelRowSource?.adapter?.firstVisible!.$index!;
    const lastVisible = this.codePanelRowSource?.adapter?.lastVisible!.$index!;
    let foundIndex: number | undefined = undefined;

    if (direction == CodeLineRowNavigationDirection.next) {
      const startIndex = (this.commentThreadNavigationPointer !== undefined)
        ? this.commentThreadNavigationPointer + 1
        : firstVisible;
      foundIndex = this.findNextCommentThreadIndex(startIndex);

      if (foundIndex === undefined && this.commentThreadNavigationPointer !== undefined) {
        foundIndex = this.findNextCommentThreadIndex(0);
      }
    }
    else {
      const startIndex = (this.commentThreadNavigationPointer !== undefined)
        ? this.commentThreadNavigationPointer - 1
        : lastVisible;
      foundIndex = this.findPrevCommentThreadIndex(startIndex);

      if (foundIndex === undefined && this.commentThreadNavigationPointer !== undefined) {
        foundIndex = this.findPrevCommentThreadIndex(this.codePanelRowData.length - 1);
      }
    }

    if (foundIndex !== undefined) {
      this.commentThreadNavigationPointer = foundIndex;
      this.scrollToCommentThread(foundIndex);
    }
    else {
      this.messageService.add({ severity: 'info', icon: 'bi bi-info-circle', summary: 'Comment Navigation', detail: 'No active comment threads to navigate to.', key: 'bc', life: 3000 });
    }
  }

  navigateToDiffNode(direction: CodeLineRowNavigationDirection) {
    const firstVisible = this.codePanelRowSource?.adapter?.firstVisible!.$index!;
    const lastVisible = this.codePanelRowSource?.adapter?.lastVisible!.$index!;
    let navigateToRow: CodePanelRowData | undefined = undefined;
    if (direction == CodeLineRowNavigationDirection.next) {
      const startIndex = (this.diffNodeNavigationPointer && this.diffNodeNavigationPointer >= firstVisible && this.diffNodeNavigationPointer <= lastVisible) ?
        this.diffNodeNavigationPointer : firstVisible;
      navigateToRow = this.findNextDiffNode(startIndex);
    }
    else {
      const startIndex = (this.diffNodeNavigationPointer && this.diffNodeNavigationPointer >= firstVisible && this.diffNodeNavigationPointer <= lastVisible) ?
        this.diffNodeNavigationPointer: lastVisible;
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
    const reviewText: string[] = [];
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
            default:
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
   * Highlights a comment when the URL contains a comment ID in the fragment (hash)
   */
  private highlightCommentFromFragment() {
    const fragment = window.location.hash;
    if (!fragment || fragment.length <= 1) {
      return;
    }

    const commentId = fragment.substring(1); // Remove the '#' prefix
    if (!commentId) {
      return;
    }

    setTimeout(() => {
      const commentPanel = this.elementRef.nativeElement.querySelector(`[data-comment-id="${commentId}"]`);
      if (commentPanel) {
        commentPanel.classList.add('active');
        commentPanel.scrollIntoView({ behavior: 'smooth', block: 'center' });
        setTimeout(() => {
          commentPanel.classList.remove('active');
        }, 1550);
      }
    }, 600);
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

  private findNextCommentThreadIndex(index: number): number | undefined {
    while (index < this.codePanelRowData.length) {
      if (this.codePanelRowData[index].type === CodePanelRowDatatype.CommentThread && !this.codePanelRowData![index].isResolvedCommentThread) {
        return index;
      }
      index++;
    }
    return undefined;
  }

  private findPrevCommentThreadIndex(index: number): number | undefined {
    while (index < this.codePanelRowData.length && index >= 0) {
      if (this.codePanelRowData[index].type === CodePanelRowDatatype.CommentThread && !this.codePanelRowData![index].isResolvedCommentThread) {
        return index;
      }
      index--;
    }
    return undefined;
  }


  private async scrollToCommentThread(targetIndex: number): Promise<void> {
    this.clearNavigationHighlight();

    const row = this.codePanelRowData[targetIndex];
    if (!row) return;

    const rowClasses = this.ensureRowClassesSet(row);
    rowClasses.add('active');

    const scrollIndex = Math.max(targetIndex - 2, 0);

    if (scrollIndex < this.codePanelRowSource?.adapter?.bufferInfo.firstIndex! ||
        scrollIndex > this.codePanelRowSource?.adapter?.bufferInfo.lastIndex!) {
      await this.codePanelRowSource?.adapter?.reload(scrollIndex);
    } else {
      await this.codePanelRowSource?.adapter?.fix({
        scrollToItem: (item) => item.data === row,
        scrollToItemOpt: { behavior: 'smooth', block: 'center' }
      });
    }

    let newQueryParams = getQueryParams(this.route);
    let nodeIdForUrl = row.nodeId;
    if (!nodeIdForUrl && row.type === CodePanelRowDatatype.CommentThread) {
      const nodeIdHashedForLookup = row.nodeIdHashed;
      const rowPosition = row.associatedRowPositionInGroup;
      const codeLines = this.codePanelData?.nodeMetaData[nodeIdHashedForLookup]?.codeLines;
      if (codeLines && codeLines[rowPosition]) {
        nodeIdForUrl = codeLines[rowPosition].nodeId;
      }
    }
    newQueryParams[SCROLL_TO_NODE_QUERY_PARAM] = nodeIdForUrl;
    this.router.navigate([], { queryParams: newQueryParams, state: { skipStateUpdate: true } });

    setTimeout(() => {
      rowClasses.delete('active');
    }, 1500);
  }

  private clearNavigationHighlight(): void {
    for (const row of this.codePanelRowData) {
      if (row.rowClasses) {
        const rowClasses = this.ensureRowClassesSet(row);
        if (rowClasses.has('active')) {
          rowClasses.delete('active');
        }
      }
    }
  }

  private findNextDiffNode(index: number): CodePanelRowData | undefined {
    let checkForDiffNode = (isDiffRow(this.codePanelRowData[index])) ? false : true;
    while (index < this.codePanelRowData.length) {
      if (!checkForDiffNode && !isDiffRow(this.codePanelRowData[index])) {
        checkForDiffNode = true;
      }
      if (checkForDiffNode && isDiffRow(this.codePanelRowData[index])) {
        this.diffNodeNavigationPointer = index;
        return this.codePanelRowData[index];
      }
      index++;
    }
    return undefined;
  }

  private findPrevDiffNode(index: number): CodePanelRowData | undefined {
    let checkForDiffNode = (isDiffRow(this.codePanelRowData[index])) ? false : true;
    while (index < this.codePanelRowData.length && index >= 0) {
      if (!checkForDiffNode && !isDiffRow(this.codePanelRowData[index])) {
        checkForDiffNode = true;
      }
      if (checkForDiffNode && isDiffRow(this.codePanelRowData[index])) {
        this.diffNodeNavigationPointer = index;
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
        setTimeout(async () => {
          await this.scrollToNode(undefined, this.scrollToNodeId);
          this.highlightCommentFromFragment();
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
    const nodeMetaData = this.codePanelData?.nodeMetaData[data.nodeIdHashed!];
    if (!nodeMetaData?.commentThread?.[data.associatedRowPositionInGroup!]) {
      return;
    }
    const commentThreads = nodeMetaData.commentThread[data.associatedRowPositionInGroup!];
    const commentThread = this.findCommentThread(commentThreads, data.threadId);
    if (commentThread) {
      const comment = commentThread.comments.find((c: CommentItemModel) => c.id === data.commentId);
      if (comment) {
        if (data.commentText !== undefined) {
          comment.commentText = data.commentText;
        }
        if (data.severity !== undefined) {
          comment.severity = data.severity;
        }
        this.updateItemInScroller(commentThread);
      }
    }
    this.updateHasActiveConversations();
  }

  private updateCommentSeverity(commentId: string, newSeverity: CommentSeverity) {
    for (const row of this.codePanelRowData) {
      if (row.type === CodePanelRowDatatype.CommentThread && row.comments) {
        const comment = row.comments.find((c: CommentItemModel) => c.id === commentId);
        if (comment) {
          comment.severity = newSeverity;
          this.updateItemInScroller(row);
          break;
        }
      }
    }
  }

  private addCommentToCommentThread(commentUpdates: CommentUpdatesDto, newComment: CommentItemModel) {
    const { nodeIdHashed, associatedRowPositionInGroup: position, threadId } = commentUpdates;
    if (this.codePanelRowData.some(row => row.type === CodePanelRowDatatype.CommentThread && row.comments?.some(c => c.id === newComment.id))) {
      return;
    }

    const nodeMetaData = this.codePanelData?.nodeMetaData?.[nodeIdHashed!];
    if (!nodeMetaData) {
      return;
    }
    const insertNewThread = () => {
      const row = this.createCommentThreadRow(nodeIdHashed!, undefined, position!, threadId!, [newComment]);
      row.showReplyTextBox = false;
      return row;
    };

    //No comment threads exist for this node yet
    if (!nodeMetaData.commentThread) {
      nodeMetaData.commentThread = {};
      const newRow = insertNewThread();
      nodeMetaData.commentThread[position!] = [newRow];
      this.insertItemsIntoScroller([newRow], nodeIdHashed!, CodePanelRowDatatype.CodeLine, position!, "toggleCommentsClasses", "can-show", "show");
      this.updateHasActiveConversations();
      return;
    }

    // Position exists - look for matching thread
    const threadsByPosition = nodeMetaData.commentThread;
    if (threadsByPosition.hasOwnProperty(position!)) {
      const threads = threadsByPosition[position!];
      const existingThread = this.findCommentThread(threads, threadId);

      if (existingThread) {
        if (!existingThread.comments) existingThread.comments = [];
        if (existingThread.comments.some(c => c.id === newComment.id)) {
          return;
        }
        const updatedThread = Object.assign(new CodePanelRowData(), existingThread);
        updatedThread.comments = [...existingThread.comments, newComment];
        updatedThread.showReplyTextBox = false;

        const idx = this.findCommentThreadIndex(threads, threadId);
        if (idx !== -1) threads[idx] = updatedThread;

        this.updateItemInScroller(updatedThread);
        this.updateHasActiveConversations();
      } else {
        const newRow = insertNewThread();
        threads.push(newRow);
        this.insertItemsIntoScroller([newRow], nodeIdHashed!, CodePanelRowDatatype.CodeLine, position!, "toggleCommentsClasses", "can-show", "show");
        this.updateHasActiveConversations();
      }
    } else {
      threadsByPosition[position!] = [];
      const newRow = insertNewThread();
      threadsByPosition[position!].push(newRow);
      this.insertItemsIntoScroller([newRow], nodeIdHashed!, CodePanelRowDatatype.CodeLine, position!, "toggleCommentsClasses", "can-show", "show");
      this.updateHasActiveConversations();
    }
  }

  private deleteCommentFromCommentThread(commentUpdates: CommentUpdatesDto) {
    const { nodeIdHashed, associatedRowPositionInGroup: position, threadId, commentId } = commentUpdates;

    const nodeMetaData = this.codePanelData?.nodeMetaData?.[nodeIdHashed!];
    if (!nodeMetaData?.commentThread?.[position!]) {
      this.updateHasActiveConversations();
      return;
    }
    const threads = nodeMetaData.commentThread[position!];
    const thread = this.findCommentThread(threads, threadId);

    if (!thread) {
      this.updateHasActiveConversations();
      return;
    }

    const remaining = thread.comments.filter((c: CommentItemModel) => c.id !== commentId);

    if (remaining.length === 0) {
      this.removeItemsFromScroller(nodeIdHashed!, CodePanelRowDatatype.CommentThread, "toggleCommentsClasses", "show", "can-show", position, threadId);
      const idx = threads.indexOf(thread);
      if (idx > -1) threads.splice(idx, 1);
    } else {
      const updated = Object.assign(new CodePanelRowData(), thread);
      updated.comments = remaining;

      const idx = this.findCommentThreadIndex(threads, threadId);
      if (idx !== -1) threads[idx] = updated;

      this.updateItemInScroller(updated);
    }
    this.updateHasActiveConversations();
  }

  /**
   * Removes all auto-generated (azure-sdk) comments from the code panel in a single bulk operation.
   * Handles both the virtual scroller rows and the underlying nodeMetaData.
   */
  private async removeAllAutoGeneratedComments() {
    await this.codePanelRowSource?.adapter?.relax();

    const indexesToRemove: number[] = [];
    const filteredRows: CodePanelRowData[] = [];

    for (let i = 0; i < this.codePanelRowData.length; i++) {
      const row = this.codePanelRowData[i];
      if (row.type === CodePanelRowDatatype.CommentThread && row.comments) {
        const remaining = row.comments.filter((c: CommentItemModel) => c.createdBy !== 'azure-sdk');
        if (remaining.length === 0) {
          // Entire thread was auto-generated — remove the row
          indexesToRemove.push(i);
          continue;
        } else if (remaining.length < row.comments.length) {
          // Mixed thread — keep only human comments
          row.comments = remaining;
        }
      }
      filteredRows.push(row);
    }

    // Update toggle-comment icons on code-line rows that lost their comment threads
    for (const row of filteredRows) {
      if (row.toggleCommentsClasses?.includes('show')) {
        const hasRemainingThreads = filteredRows.some(
          r => r.type === CodePanelRowDatatype.CommentThread &&
               r.nodeIdHashed === row.nodeIdHashed &&
               r.associatedRowPositionInGroup === row.associatedRowPositionInGroup
        );
        if (!hasRemainingThreads) {
          row.toggleCommentsClasses = row.toggleCommentsClasses.replace('show', 'can-show');
        }
      }
    }

    this.codePanelRowData = filteredRows;
    if (indexesToRemove.length > 0) {
      await this.codePanelRowSource?.adapter?.remove({ indexes: indexesToRemove });
    }

    // Clean up nodeMetaData
    if (this.codePanelData?.nodeMetaData) {
      for (const nodeId of Object.keys(this.codePanelData.nodeMetaData)) {
        const node = this.codePanelData.nodeMetaData[nodeId];
        if (node.commentThread) {
          for (const pos of Object.keys(node.commentThread)) {
            const threads = node.commentThread[Number(pos)];
            for (let t = threads.length - 1; t >= 0; t--) {
              if (threads[t].comments) {
                threads[t].comments = threads[t].comments.filter((c: CommentItemModel) => c.createdBy !== 'azure-sdk');
                if (threads[t].comments.length === 0) {
                  threads.splice(t, 1);
                }
              }
            }
          }
        }
      }
    }

    this.updateHasActiveConversations();
  }

  private applyCommentResolutionUpdate(commentUpdates: CommentUpdatesDto) {
    const nodeMetaData = this.codePanelData?.nodeMetaData?.[commentUpdates.nodeIdHashed!];
    if (!nodeMetaData?.commentThread?.[commentUpdates.associatedRowPositionInGroup!]) {
      this.updateHasActiveConversations();
      return;
    }
    const commentThreads = nodeMetaData.commentThread[commentUpdates.associatedRowPositionInGroup!];
    const commentThread = this.findCommentThread(commentThreads, commentUpdates.threadId);

    if (commentThread) {
      commentThread.isResolvedCommentThread = (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved);
      commentThread.commentThreadIsResolvedBy = commentUpdates.resolvedBy!;

      commentThread.comments.forEach(comment => {
        const globalComment = this.allComments?.find(c => c.id === comment.id);
        if (globalComment) {
          globalComment.isResolved = commentThread.isResolvedCommentThread;
        }
      });

      this.updateItemInScroller({ ...commentThread });
    }
    this.updateHasActiveConversations();
  }

  private toggleCommentUpVote(data: CommentUpdatesDto) {
    const nodeMetaData = this.codePanelData?.nodeMetaData?.[data.nodeIdHashed!];
    if (!nodeMetaData?.commentThread?.[data.associatedRowPositionInGroup!]) {
      return;
    }
    const commentThreads = nodeMetaData.commentThread[data.associatedRowPositionInGroup!];
    const commentThread = this.findCommentThread(commentThreads, data.threadId);

    if (commentThread) {
      const comment = commentThread.comments.find((c: CommentItemModel) => c.id === data.commentId);

      if (comment) {
        this.toggleVoteUp(comment);

        const globalComment = this.allComments?.find(c => c.id === data.commentId);
        if (globalComment) {
          this.toggleVoteUp(globalComment);
        }

        this.updateItemInScroller(commentThread);
      }
    }
  }

  private toggleCommentDownVote(data: CommentUpdatesDto) {
    const nodeMetaData = this.codePanelData?.nodeMetaData?.[data.nodeIdHashed!];
    if (!nodeMetaData?.commentThread?.[data.associatedRowPositionInGroup!]) {
      return;
    }
    const commentThreads = nodeMetaData.commentThread[data.associatedRowPositionInGroup!];
    const commentThread = this.findCommentThread(commentThreads, data.threadId);

    if (commentThread) {
      const comment = commentThread.comments.find((c: CommentItemModel) => c.id === data.commentId);
      if (comment) {
        this.toggleVoteDown(comment);

        const globalComment = this.allComments?.find(c => c.id === data.commentId);
        if (globalComment) {
          this.toggleVoteDown(globalComment);
        }

        this.updateItemInScroller(commentThread);
      }
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

  private findRowForCommentUpdates(commentId: string, elementId: string): CodePanelRowData | undefined {
    for (const key in this.codePanelData?.nodeMetaData) {
      const commentThreadsInNode = this.codePanelData?.nodeMetaData[key].commentThread;
      if (commentThreadsInNode && Object.keys(commentThreadsInNode).length > 0) {
        for (const commentThreadKey in commentThreadsInNode) {
          const commentThreads = commentThreadsInNode[commentThreadKey];
          for (let thread of commentThreads) {
            for (let comment of thread.comments) {
              if (comment.id === commentId || comment.elementId === elementId) {
                return thread;
              }
            }
          }
        }
      }
    }
    return undefined;
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

  onKeyDown(event: KeyboardEvent) {
    if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown'].includes(event.key)) {
      const viewport = event.currentTarget as HTMLElement;
      if (viewport) {
        const scrollLineAmount = 40;
        const scrollPageAmount = viewport.clientHeight;

        let scrollDelta = 0;

        switch (event.key) {
          case 'ArrowUp':
            scrollDelta = event.metaKey ? -scrollPageAmount : -scrollLineAmount;
            break;
          case 'ArrowDown':
            scrollDelta = event.metaKey ? scrollPageAmount : scrollLineAmount;
            break;
          case 'PageUp':
            scrollDelta = -scrollPageAmount;
            break;
          case 'PageDown':
            scrollDelta = scrollPageAmount;
            break;
        }

        if (scrollDelta !== 0) {
          viewport.scrollTop += scrollDelta;
          event.preventDefault();
        }
      }
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
