import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { getCodePanelRowDataClass, getStructuredTokenClass } from 'src/app/_helpers/common-helpers';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { UserProfile } from 'src/app/_models/userProfile';
import { environment } from 'src/environments/environment';

@Component({
    selector: 'app-cross-lang-view',
    templateUrl: './cross-lang-view.component.html',
    styleUrls: ['./cross-lang-view.component.scss'],
    host: {
        'class': 'cross-language-view-content'
    },
    standalone: true,
    imports: [CommonModule, LanguageNamesPipe]
})
export class CrossLangViewComponent {
  @Input() codePanelRowData: CodePanelRowData | undefined = undefined;
  @Input() userProfile: UserProfile | undefined;

  assetsPath : string = environment.assetsPath;
  CodePanelRowDatatype = CodePanelRowDatatype;
  activeTabIndex: number = 0;

  @Output() closeCrossLanguageViewEmitter : EventEmitter<[string, string]> = new EventEmitter<[string, string]>();

  getAvailableLanguages(): string[] {
    return Array.from(this.codePanelRowData?.crossLanguageLines?.keys() || []);
  }

  getTabId(language: string): string {
    return `${this.codePanelRowData?.nodeIdHashed!}-${language}-tab`;
  }

  getTabPaneId(language: string, addHash: boolean = false): string {
    const result = `${this.codePanelRowData?.nodeIdHashed!}-${language}-tab-pane`;
    if (addHash) {
      return `#${result}`;
    }
    return result;
  }

  getCodeLines(language: string): CodePanelRowData[] {
    return this.codePanelRowData?.crossLanguageLines?.get(language)?.codeLines || [];
  }

  getRowClassObject(row: CodePanelRowData) {
    return getCodePanelRowDataClass(row);
  }

  getTokenClassObject(token: StructuredToken) {
    return getStructuredTokenClass(token);
  }

  getCodeLineUrl(language: string,) {
    var crossLanguageViewInfo = this.codePanelRowData?.crossLanguageLines?.get(language);
    var lineId = crossLanguageViewInfo?.codeLines[0]?.nodeId;
    return `review/${crossLanguageViewInfo?.reviewId}?activeApiRevisionId=${crossLanguageViewInfo?.apiRevisionId}&nId=${lineId}`;
  }

  getCodeLineUrlText(language: string,) {
    var crossLanguageViewInfo = this.codePanelRowData?.crossLanguageLines?.get(language);
    return ` Open in ${language}, ${crossLanguageViewInfo?.packageName} : ${crossLanguageViewInfo?.packageVersion}`;
  }

  setActiveTab(index: number): void {
    this.activeTabIndex = index;
  }
}
