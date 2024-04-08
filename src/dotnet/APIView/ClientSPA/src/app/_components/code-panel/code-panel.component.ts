import { Component, ElementRef, Input, ViewChild } from '@angular/core';
import { BehaviorSubject, Subject, takeUntil } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { CodeHuskNode, CreateLinesOfTokensMessage } from 'src/app/_models/revision';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent {
  @Input() apiTreeNodeData: BehaviorSubject<CodeHuskNode | null> = new BehaviorSubject<CodeHuskNode | null>(null);
  @Input() tokenLineData: BehaviorSubject<CreateLinesOfTokensMessage | null> = new BehaviorSubject<CreateLinesOfTokensMessage | null>(null);
  @ViewChild('codeLinesContainer', { static: true }) codeLinesContainer: ElementRef | undefined;
  @ViewChild('lineNumbersContainer', { static: true }) lineNumbersContainer: ElementRef | undefined;

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
        //div.textContent = id;
        div.id = id;
        div.style.paddingLeft = `${nodeData.indent * 20}px`;
        this.codeLinesContainer!.nativeElement.appendChild(div);
        this.isLoading = false;
      }
    });

    this.tokenLineData.pipe(takeUntil(this.destroyTokenLineData$)).subscribe(lineData => {
      if (lineData != null) {
        const node = document.getElementById(`${lineData.nodeId}-${lineData.position}`);
        const line = document.createElement('div');
        const lineActions = document.createElement('div');
        const lineNumber = document.createElement('span');
        const commentButton = document.createElement('span');
        const commentIcon = document.createElement('i');

        if (lineData.nodeId && lineData.tokenLine) {
          for (let token of lineData.tokenLine) {
            const span = document.createElement('span');
            token.renderClasses.forEach((c: string) => span.classList.add(c));
            span.textContent = token.properties["Value"];
            if (token.properties["Id"]){
              span.setAttribute('data-token-id', token.properties["Id"]);
            }
            line.appendChild(span);
          }
          
          if (lineData.tokenLine.length > 0)
          {
            this.lineNumberCount++;
            line.setAttribute('data-line-key', this.lineNumberCount.toString());
            line.setAttribute('data-line-id', lineData.lineId);
            lineActions.setAttribute('data-line-actions-key', this.lineNumberCount.toString());
            lineNumber.textContent = `${this.lineNumberCount}`;
            lineActions.classList.add('line-actions');
            
            commentIcon.classList.add('bi', 'bi-plus-square-fill');
            commentButton.appendChild(commentIcon);
            commentButton.classList.add('comment-button');
            if (lineData.position === "top") {
              commentButton.classList.add('commentable');
            }

            lineActions.appendChild(lineNumber);
            lineActions.appendChild(commentButton);
            node!.appendChild(line);
            this.lineNumbersContainer!.nativeElement.appendChild(lineActions);
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
