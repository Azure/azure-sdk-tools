import {Component, Input } from '@angular/core';
import { CodeLine } from 'src/app/_models/review';

declare var monaco: any;

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss']
})
export class CodePanelComponent { 
  public _editor : any;
  @Input() codeLines: CodeLine [] = [];


}
