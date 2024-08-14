import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { take } from 'rxjs/operators';
import { Datasource, IDatasource, SizeStrategy } from 'ngx-ui-scroll';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { ActivatedRoute, Router } from '@angular/router';
import { SCROLL_TO_NODE_QUERY_PARAM } from 'src/app/_helpers/common-helpers';
import { CodePanelData, CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { UserProfile } from 'src/app/_models/userProfile';
import { Message } from 'primeng/api/message';

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

  constructor(private changeDetectorRef: ChangeDetectorRef, private commentsService: CommentsService, 
    private route: ActivatedRoute, private router: Router) { }

  ngOnInit() {
    this.codeWindowHeight = `${window.innerHeight - 80}`;
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

    if (target.classList.contains('toggle-user-comments-btn')) {
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
    const nodeIdHashed = target.closest('.code-line')!.getAttribute('data-node-id');
    const rowPositionInGroup = parseInt(target.closest('.code-line')!.getAttribute('data-row-position-in-group')!, 10);
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
      this.insertItemsIntoScroller([commentThreadRow], nodeIdHashed!, rowPositionInGroup);
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
    const nodeIdHashed = target.closest(".code-line")!.getAttribute('data-node-id');

    if (target.classList.contains('bi-arrow-up-square')) {
      const documentationData = this.codePanelData?.nodeMetaData[nodeIdHashed!]?.documentation;
      await this.insertItemsIntoScroller(documentationData!, nodeIdHashed!, -1, "toggleDocumentationClasses", "bi-arrow-up-square", "bi-arrow-down-square");
      target.classList.remove('bi-arrow-up-square')
      target.classList.add('bi-arrow-down-square');
    } else if (target.classList.contains('bi-arrow-down-square')) {
      await this.removeItemsFromScroller(nodeIdHashed!, CodePanelRowDatatype.Documentation, "toggleDocumentationClasses", "bi-arrow-down-square", "bi-arrow-up-square");
      target.classList.remove('bi-arrow-down-square')
      target.classList.add('bi-arrow-up-square');
    }
  }

  toggleLineActionIcon(iconClassToremove: string, iconClassToAdd: string, 
    codeLineData: CodePanelRowData, codeLineDataProperty: string) : CodePanelRowData {
    if (codeLineDataProperty === "toggleDocumentationClasses") {
      codeLineData.toggleDocumentationClasses = codeLineData.toggleDocumentationClasses?.replace(iconClassToremove, iconClassToAdd);
    }
    return codeLineData;
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

  async insertItemsIntoScroller(itemsToInsert: CodePanelRowData[], nodeIdhashed: string, insertPosition : number, 
      propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string) {
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

        if (insertPosition == this.codePanelRowData[nodeIndex].rowPositionInGroup) {
          insertPositionFound = true;
        }
      }
      preData.push(this.codePanelRowData[nodeIndex]);
      nodeIndex++;
    }
    let postData = this.codePanelRowData.slice(nodeIndex);

    if (propertyToChange && iconClassToremove && iconClassToAdd) {
      postData[0] = this.toggleLineActionIcon(iconClassToremove, iconClassToAdd, postData[0], propertyToChange);
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
        if (propertyToChange && iconClassToremove && iconClassToAdd) {
          this.codePanelRowData[i] = this.toggleLineActionIcon(iconClassToremove, iconClassToAdd, this.codePanelRowData[i], propertyToChange);
        }
        indexesToRemove.push(i);
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
          bufferSize: 50,
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
      if (this.codePanelRowData[index].nodeIdHashed === nodeIdHashed || this.codePanelRowData[index].nodeId === nodeId) {
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
      this.codePanelRowSource?.adapter?.reload(scrollIndex);
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

  handleCancelCommentActionEmitter(data: any) {
    const commentsInNode = this.codePanelData?.nodeMetaData[data.nodeIdHashed]?.commentThread
    if (commentsInNode && commentsInNode.hasOwnProperty(data.associatedRowPositionInGroup)) {
      const commentThread = commentsInNode[data.associatedRowPositionInGroup];
      if (!commentThread.comments || commentThread.comments.length === 0) {
        this.removeItemsFromScroller(data.nodeIdHashed, CodePanelRowDatatype.CommentThread, undefined, undefined, undefined, data.associatedRowPositionInGroup);
        this.codePanelData!.nodeMetaData[data.nodeIdHashed].commentThread = [];
      }
      else {
        for (let i = 0; i < this.codePanelRowData.length; i++) {
          if (this.codePanelRowData[i].nodeIdHashed === data.nodeIdHashed && this.codePanelRowData[i].type === CodePanelRowDatatype.CommentThread
            && this.codePanelRowData[i].associatedRowPositionInGroup === data.associatedRowPositionInGroup
          ) {
            this.codePanelRowData[i].showReplyTextBox = false;
            break;
          }
        }
      }
    }
  }

  handleSaveCommentActionEmitter(data: any) {
    if (data.commentId) {
      this.commentsService.updateComment(this.reviewId!, data.commentId, data.commentText).pipe(take(1)).subscribe({
        next: () => {
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].comments.filter(c => c.id === data.commentId)[0].commentText = data.commentText;
          this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup]);
          this.updateHasActiveConversations();
        }
      });
    }
    else {
      this.commentsService.createComment(this.reviewId!, this.activeApiRevisionId!, data.nodeId, data.commentText, CommentType.APIRevision, data.allowAnyOneToResolve)
        .pipe(take(1)).subscribe({
            next: (response: CommentItemModel) => {
              const comments = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].comments;
              comments.push(response);
              this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].comments = [...comments]
              this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup]);
              this.updateHasActiveConversations();
            }
          }
        );
    }
  }

  handleDeleteCommentActionEmitter(data: any) {
    this.commentsService.deleteComment(this.reviewId!, data.commentId).pipe(take(1)).subscribe({
      next: () => {
        const comments = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].comments;
        this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].comments = comments.filter(c => c.id !== data.commentId);

        if (this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].comments.length === 0) {
          this.removeItemsFromScroller(data.nodeIdHashed!, CodePanelRowDatatype.CommentThread, undefined, undefined, undefined, data.associatedRowPositionInGroup);
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread = {};
        } else {
          this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup]);
        }
        this.updateHasActiveConversations();
      }
    });
  }

  handleCommentResolutionActionEmitter(data: any) {
    if (data.action === "Resolve") {
      this.commentsService.resolveComments(this.reviewId!, data.elementId).pipe(take(1)).subscribe({
        next: () => {
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].isResolvedCommentThread = true;
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].commentThreadIsResolvedBy = this.userProfile?.userName!;
          this.updateItemInScroller({ ...this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup]});
          this.updateHasActiveConversations();
        }
      });
    }
    if (data.action === "Unresolve") {
      this.commentsService.unresolveComments(this.reviewId!, data.elementId).pipe(take(1)).subscribe({
        next: () => {
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].isResolvedCommentThread = false;
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].commentThreadIsResolvedBy = '';
          this.updateItemInScroller({ ...this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup]});
          this.updateHasActiveConversations();
        }
      });
    }
  }

  handleCommentUpvoteActionEmitter(data: any){
    this.commentsService.toggleCommentUpVote(this.reviewId!, data.commentId).pipe(take(1)).subscribe({
      next: () => {
        const comment = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup].comments.find(c => c.id === data.commentId);
        if (comment) {
          if (comment.upvotes.includes(this.userProfile?.userName!)) {
            comment.upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
          } else {
            comment.upvotes.push(this.userProfile?.userName!);
          }
          this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[data.associatedRowPositionInGroup]);
        }
      }
    });
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
}
