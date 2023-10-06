import {Component, Input } from '@angular/core';
import { ReviewLine, DiffLineKind} from 'src/app/_models/review';

declare var monaco: any;

@Component({
  selector: 'app-code-panel',
  templateUrl: './code-panel.component.html',
  styleUrls: ['./code-panel.component.scss'],
})

export class CodePanelComponent { 
  @Input() reviewLines: ReviewLine[] = [];
  public diffKind = DiffLineKind;
}
