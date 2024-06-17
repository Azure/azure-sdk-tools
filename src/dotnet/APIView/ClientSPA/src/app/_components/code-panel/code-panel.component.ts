import { ChangeDetectorRef, Component, ElementRef, Input, OnChanges, OnInit, SimpleChanges, ViewChild, ViewContainerRef } from '@angular/core';
import { take } from 'rxjs/operators';
import { CommentItemModel, CommentType } from 'src/app/_models/review';
import { CodePanelData, CodePanelRowDatatype } from 'src/app/_models/revision';
import { CodePanelRowData } from 'src/app/_models/revision';
import { Datasource, IDatasource, SizeStrategy } from 'ngx-ui-scroll';
import { CommentsService } from 'src/app/_services/comments/comments.service';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent implements OnChanges{
  @Input() codePanelRowData: CodePanelRowData[] = [];
  @Input() codePanelData: CodePanelData | null = null;
  @Input() reviewComments : CommentItemModel[] | undefined = [];
  @Input() isDiffView: boolean = false;
  @Input() language: string | undefined;
  @Input() languageSafeName: string | undefined;
  @Input() navTreeNodIdHashed: string | undefined;
  @Input() reviewId: string | undefined;
  @Input() activeApiRevisionId: string | undefined;

  lineNumberCount : number = 0;
  isLoading: boolean = true;
  lastHuskNodeId :  string | undefined = undefined;
  codeWindowHeight: string | undefined = undefined;

  codePanelRowSource: IDatasource<CodePanelRowData> | undefined;
  CodePanelRowDatatype = CodePanelRowDatatype;

  constructor(private changeDetectorRef: ChangeDetectorRef, private commentsService: CommentsService) { }

  ngOnInit() {
    this.codeWindowHeight = `${window.innerHeight - 80}`;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codePanelRowData']) {
      if (changes['codePanelRowData'].currentValue.length > 0) {
        this.loadCodePanelViewPort();
      } else {
        this.isLoading = true;
        this.codePanelRowSource = undefined;
      }
    }

    if (changes['navTreeNodIdHashed']) {
      this.scrollToNode(this.navTreeNodIdHashed!);
    }
  }

  onCodePanelItemClick(event: Event) {
    const target = event.target as Element;
    if (target.classList.contains('toggle-documentation-btn') && !target.classList.contains('hide')) {
      this.toggleNodeDocumentation(target);
    }

    if (target.classList.contains('toggle-user-comments-btn')) {
      this.toggleNodeComments(target);
    }
  }

  getClassObject(renderClasses: Set<string>) {
    let classObject: { [key: string]: boolean } = {};
    if (renderClasses) {
      for (let className of Array.from(renderClasses)) {
        classObject[className] = true;
      }
    }
    return classObject;
  }

  toggleNodeComments(target: Element) {
    const nodeIdHashed = target.closest('.code-line')!.getAttribute('data-node-id');
    const existingCommentThread = this.codePanelData?.nodeMetaData[nodeIdHashed!]?.commentThread;
    const exisitngCodeLine = this.codePanelData?.nodeMetaData[nodeIdHashed!]?.codeLines[0];
    
    if (!existingCommentThread || existingCommentThread.length === 0) {
      const commentThreadRow = new CodePanelRowData();
      commentThreadRow.type = CodePanelRowDatatype.CommentThread;
      commentThreadRow.nodeId = exisitngCodeLine?.nodeId!;
      commentThreadRow.nodeIdHashed = exisitngCodeLine?.nodeIdHashed!;
      commentThreadRow.rowClasses = new Set<string>(['user-comment-thread']);
      commentThreadRow.showReplyTextBox = true;
      this.codePanelData!.nodeMetaData[nodeIdHashed!].commentThread = [commentThreadRow];
      this.insertItemsIntoScroller([commentThreadRow], nodeIdHashed!, true);
    }
    else {
      for (let i = 0; i < this.codePanelRowData.length; i++) {
        if (this.codePanelRowData[i].nodeIdHashed === nodeIdHashed && this.codePanelRowData[i].type === CodePanelRowDatatype.CommentThread) {
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
      await this.insertItemsIntoScroller(documentationData!, nodeIdHashed!, false, "toggleDocumentationClasses", "bi-arrow-up-square", "bi-arrow-down-square");
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
            if (nodeData?.commentThread) {
              updatedCodeLinesData.push(...nodeData?.commentThread);
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

  async insertItemsIntoScroller(itemsToInsert: CodePanelRowData[], nodeIdhashed: string, insertAfterNodeIdhashed : boolean = false, 
      propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string) {
    await this.codePanelRowSource?.adapter?.relax();

    let preData = [];
    let nodeIndex = 0;
    let targetNodeIdHashed = null;

    while (nodeIndex < this.codePanelRowData.length) {
      if (this.codePanelRowData[nodeIndex].nodeIdHashed === nodeIdhashed) {
        targetNodeIdHashed = nodeIdhashed;
        if (!insertAfterNodeIdhashed) {
          break;
        }
      }
      if (targetNodeIdHashed && insertAfterNodeIdhashed && this.codePanelRowData[nodeIndex].nodeIdHashed != targetNodeIdHashed) {
        break;
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
    propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string) {
    await this.codePanelRowSource?.adapter?.relax();

    const indexesToRemove : number[] = [];
    const filteredCodeLinesData : CodePanelRowData[] = [];

    for (let i = 0; i < this.codePanelRowData.length; i++) {
      if (this.codePanelRowData[i].nodeIdHashed === nodeIdHashed && this.codePanelRowData[i].type === codePanelRowDatatype) {
        if (propertyToChange && iconClassToremove && iconClassToAdd) {
          this.codePanelRowData[i] = this.toggleLineActionIcon(iconClassToremove, iconClassToAdd, this.codePanelRowData[i], propertyToChange);
        }
        indexesToRemove.push(i);
      }
      else {
        filteredCodeLinesData.push(this.codePanelRowData[i]);
      }
    }

    this.codePanelRowData = filteredCodeLinesData;
    await this.codePanelRowSource?.adapter?.remove({
      indexes: indexesToRemove
    });
  }

  async updateItemInScroller(updateData: CodePanelRowData) {
    this.codePanelRowData.filter(row => row.nodeIdHashed === updateData.nodeIdHashed &&
      row.type === updateData.type)[0] = updateData;

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

  scrollToNode(nodeIdHashed: string) {
    const nodeIndex = this.codePanelRowData.findIndex((row) => row.nodeIdHashed === nodeIdHashed);
    if (nodeIndex > -1) {
      this.codePanelRowSource?.adapter?.reload(nodeIndex);
    }
  }

  setMaxLineNumberWidth() {
    if (this.codePanelRowData[this.codePanelRowData.length - 1].lineNumber) {
      document.documentElement.style.setProperty('--max-line-number-width', `${this.codePanelRowData[this.codePanelRowData.length - 1].lineNumber!.toString().length}ch`);
    }
  }

  handleCancelCommentActionEmitter(nodeIdHashed: string) {
    const commentThread = this.codePanelData?.nodeMetaData[nodeIdHashed]?.commentThread
    if (commentThread && commentThread.length > 0) {
      if (!commentThread[0].comments || commentThread[0].comments.length === 0) {
        this.removeItemsFromScroller(nodeIdHashed, CodePanelRowDatatype.CommentThread);
        this.codePanelData!.nodeMetaData[nodeIdHashed].commentThread = [];
      }
      else {
        for (let i = 0; i < this.codePanelRowData.length; i++) {
          if (this.codePanelRowData[i].nodeIdHashed === nodeIdHashed && this.codePanelRowData[i].type === CodePanelRowDatatype.CommentThread) {
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
        next: (response: any) => {
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0].comments.filter(c => c.id === data.commentId)[0].commentText = data.commentText;
          this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0]);
        },
        error: (error: any) => {
          
        }
      });
    }
    else {
      this.commentsService.createComment(this.reviewId!, this.activeApiRevisionId!, data.nodeId, data.commentText, CommentType.APIRevision, data.allowAnyOneToResolve)
        .pipe(take(1)).subscribe({
            next: (response: CommentItemModel) => {
              const comments = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0].comments;
              comments.push(response);
              this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0].comments = [...comments]
              this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0]);
            },
            error: (error: any) => {
              
            }
          }
        );
    }
  }

  handleDeleteCommentActionEmitter(data: any) {
    this.commentsService.deleteComment(this.reviewId!, data.commentId).pipe(take(1)).subscribe({
      next: (response: any) => {
        const comments = this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0].comments;
        this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0].comments = comments.filter(c => c.id !== data.commentId);

        if (this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0].comments.length === 0) {
          this.removeItemsFromScroller(data.nodeIdHashed!, CodePanelRowDatatype.CommentThread);
          this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread = [];
        } else {
          this.updateItemInScroller(this.codePanelData!.nodeMetaData[data.nodeIdHashed!].commentThread[0]);
        }
      },
      error: (error: any) => {
        
      }
    });
  }

  private loadCodePanelViewPort() {
    this.setMaxLineNumberWidth();
    this.initializeDataSource().then(() => {
      this.codePanelRowSource?.adapter?.init$.pipe(take(1)).subscribe(() => {
        this.isLoading = false;
      });
    }).catch((error) => {
      console.error(error);
    });
  }
}
