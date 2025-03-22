import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';

@Component({
  selector: 'app-cross-lang-view',
  templateUrl: './cross-lang-view.component.html',
  styleUrl: './cross-lang-view.component.scss',
  host: {
    'class': 'cross-language-view-content'
  },
})
export class CrossLangViewComponent {
  @Input() codePanelRowData: CodePanelRowData | undefined = undefined;

  @Output() closeCrossLanguageViewEmitter : EventEmitter<[string, string]> = new EventEmitter<[string, string]>();
}
