import { ChangeDetectorRef, Component, ElementRef, Input, OnChanges, OnInit, SimpleChanges, ViewChild, ViewContainerRef } from '@angular/core';
import { BehaviorSubject, fromEvent, of, Subject, takeUntil } from 'rxjs';
import { debounceTime, finalize, scan, take } from 'rxjs/operators';
import { CommentItemModel } from 'src/app/_models/review';
import { CodePanelRowDatatype } from 'src/app/_models/revision';
import { CodePanelRowData, CodePanelToggleableData, InsertCodePanelRowDataMessage, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';
import { AfterViewInit } from '@angular/core';
import { OnDestroy } from '@angular/core';
import { Datasource, IDatasource, SizeStrategy } from 'ngx-ui-scroll';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent implements OnChanges, OnDestroy{
  @Input() codeLinesData: CodePanelRowData[] = [];
  @Input() otherCodePanelData: Map<string, CodePanelToggleableData> = new Map<string, CodePanelToggleableData>();
  @Input() reviewComments : CommentItemModel[] | undefined = [];
  @Input() isDiffView: boolean = false;

  @ViewChild('commentThreadRef', { read: ViewContainerRef }) commentThreadRef!: ViewContainerRef;

  lineNumberCount : number = 0;
  isLoading: boolean = true;
  lastHuskNodeId :  string | undefined = undefined;
  codeWindowHeight: string | undefined = undefined;

  codePanelRowSource: IDatasource<CodePanelRowData> | undefined;
  visibleCodePanelRowData: CodePanelRowData[] = [];
  CodePanelRowDatatype = CodePanelRowDatatype;
  
  private destroyApiTreeNode$ = new Subject<void>();
  private destroyTokenLineData$ = new Subject<void>();

  constructor(private changeDeterctorRef: ChangeDetectorRef) { }

  ngOnInit() {
    this.codeWindowHeight = `${window.innerHeight - 80}`;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codeLinesData']) {
      if (changes['codeLinesData'].currentValue.length > 0) {
        this.setMaxLineNumberWidth();
        this.initializeDataSource().then(() => {
          this.codePanelRowSource?.adapter?.init$.pipe(take(1)).subscribe(() => {
            this.isLoading = false;
          });
        }).catch((error) => {
          console.error(error);
        });
      } else {
        this.isLoading = true;
        this.codePanelRowSource = undefined;
      }
    }
  }

  onCodePanelItemClick(event: Event) {
    const target = event.target as Element;
    if (target.classList.contains('toggle-documentation-btn') && !target.classList.contains('hide')) {
      this.toggleNodeDocumentation(target);
    }

    if (target.classList.contains('toggle-user-comments-btn')) {
      console.log('toggle-user-comments-btn clicked');
    }
  }

  getClassObject(renderClasses: Set<string>) {
    let classObject: { [key: string]: boolean } = {};
    for (let className of Array.from(renderClasses)) {
      classObject[className] = true;
    }
    return classObject;
  }

  async toggleNodeDocumentation(target: Element) {
    const nodeId = target.closest(".code-line")!.getAttribute('data-node-id');
    const lineNumber = target.closest('.line-actions')!.querySelector('.line-number')!.textContent;

    if (target.classList.contains('bi-arrow-up-square')) {
      const documentationData = this.otherCodePanelData.get(nodeId!)?.documentation;
      await this.codePanelRowSource?.adapter?.relax();
      await this.insertItemIntoScroller(documentationData!, lineNumber!, "toggleDocumentationClasses", "bi-arrow-up-square", "bi-arrow-down-square");
      target.classList.remove('bi-arrow-up-square')
      target.classList.add('bi-arrow-down-square');
    } else if (target.classList.contains('bi-arrow-down-square')) {
      const documentationData = this.otherCodePanelData.get(nodeId!)?.documentation;
      const lineNumbersOfLinesToRemove = new Set(documentationData!.map(d => d.lineNumber));
      await this.codePanelRowSource?.adapter?.relax();
      await this.removeItemFromScroller(lineNumbersOfLinesToRemove, lineNumber!, "toggleDocumentationClasses", "bi-arrow-down-square", "bi-arrow-up-square", "documentation");
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

  async insertItemIntoScroller(itemsToInsert: CodePanelRowData[], lineNumber: string, 
      propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string) {
    let preData = [];
    let nodeIndex = 0;
    for (let i = 0; i < this.codeLinesData.length; i++) {
      if (this.codeLinesData[i].lineNumber === parseInt(lineNumber)) {
        nodeIndex = i;
        break;
      }
      preData.push(this.codeLinesData[i]);
    }
    let postData = this.codeLinesData.slice(nodeIndex);

    if (propertyToChange && iconClassToremove && iconClassToAdd) {
      postData[0] = this.toggleLineActionIcon(iconClassToremove, iconClassToAdd, postData[0], propertyToChange);
    }

    this.codeLinesData = [
      ...preData,
      ...itemsToInsert!,
      ...postData
    ];

    await this.codePanelRowSource?.adapter?.insert({
      beforeIndex: nodeIndex,
      items: itemsToInsert!
    });
  }

  async removeItemFromScroller(lineNumbersToRemove: Set<number | undefined>, actionLineNumber: string,
    propertyToChange?: string, iconClassToremove?: string, iconClassToAdd?: string, lineClasstoRemove?: string) {
    const indexesToRemove : number[] = [];
    const filteredCodeLinesData : CodePanelRowData[] = [];

    for (let i = 0; i < this.codeLinesData.length; i++) {
      let lineNo = this.codeLinesData[i].lineNumber!;
      let rowClasses = this.codeLinesData[i].rowClasses;
      if (lineNo === parseInt(actionLineNumber) && propertyToChange && iconClassToremove && iconClassToAdd) {
        this.codeLinesData[i] = this.toggleLineActionIcon(iconClassToremove, iconClassToAdd, this.codeLinesData[i], propertyToChange);
      }

      if (lineNumbersToRemove.size > 0 && lineNumbersToRemove.has(lineNo) && rowClasses.has(lineClasstoRemove!)) {
        indexesToRemove.push(i);
      } else {
        filteredCodeLinesData.push(this.codeLinesData[i]);
      }
    }

    this.codeLinesData = filteredCodeLinesData;
    await this.codePanelRowSource?.adapter?.remove({
      indexes: indexesToRemove
    });
  }

  initializeDataSource() : Promise<void> {
    return new Promise((resolve, reject) => {
      this.codePanelRowSource = new Datasource<CodePanelRowData>({
        get: (index, count, success) => {
          let data : any = [];
          if (this.codeLinesData.length > 0) {
            data = this.codeLinesData.slice(index, index + count);
          }
          success(data);
        },
        settings: {
          bufferSize: 50,
          padding: 1,
          itemSize: 21,
          startIndex : 0,
          minIndex: 0,
          maxIndex: this.codeLinesData.length - 1,
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

  scrollToIndex(scrollPosition: number) {
    this.codePanelRowSource?.adapter?.fix({ scrollPosition: scrollPosition });
  }

  setMaxLineNumberWidth() {
    document.documentElement.style.setProperty('--max-line-number-width', `${this.codeLinesData[this.codeLinesData.length - 1].lineNumber!.toString().length}ch`)
  }

  ngOnDestroy() {
    this.destroyApiTreeNode$.next();
    this.destroyApiTreeNode$.complete();
    this.destroyTokenLineData$.next();
    this.destroyTokenLineData$.complete();
  }
}
