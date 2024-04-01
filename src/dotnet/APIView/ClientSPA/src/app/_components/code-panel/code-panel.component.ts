import { Component, ElementRef, Input, ViewChild } from '@angular/core';
import { BehaviorSubject, Subject, takeUntil } from 'rxjs';

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent {
  @Input() apiTreeNodeData: BehaviorSubject<any> = new BehaviorSubject<any>({});
  @Input() tokenLineData: BehaviorSubject<any> = new BehaviorSubject<any>({});
  @ViewChild('codeLinesContainer', { static: true }) container: ElementRef | undefined;
  private destroyApiTreeNode$ = new Subject<void>();
  private destroyTokenLineData$ = new Subject<void>();

  ngAfterViewInit() {
    this.apiTreeNodeData.pipe(takeUntil(this.destroyApiTreeNode$)).subscribe(node => {
      const div = document.createElement('div');
      div.textContent = node.id;
      div.id = node.id;
      div.style.paddingLeft = `${node.indent * 20}px`;
      this.container!.nativeElement.appendChild(div);
    });

    this.tokenLineData.pipe(takeUntil(this.destroyTokenLineData$)).subscribe(lineData => {
      const node = document.getElementById(lineData.nodeId);
      const line = document.createElement('div');

      if (lineData.nodeId && lineData.tokenLine) {
        for (let token of lineData.tokenLine) {
          const span = document.createElement('span');
          span.style.color = `red`;
          span.textContent = token.properties["Value"];
          line.appendChild(span);
        }

        if (lineData.appendTo === 'top')
        node!.appendChild(line);
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
