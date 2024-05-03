import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { DropdownFilterOptions } from 'primeng/dropdown';
import { APIRevision } from 'src/app/_models/revision';

@Component({
  selector: 'app-api-revision-options',
  templateUrl: './api-revision-options.component.html',
  styleUrls: ['./api-revision-options.component.scss']
})
export class ApiRevisionOptionsComponent implements OnChanges{
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeRevisionId: string | null = '';
  activeApiRevisionsMenu: any[] = [];
  diffApiRevisionsMenu: any[] = [];
  selectedActiveAPIRevision: any;
  selectedDiffAPIRevision: any = null;

  activeApiRevisionsFilterValue: string | undefined = '';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['apiRevisions'] && changes['apiRevisions'].currentValue.length > 0) {
      const selectedActiveAPIRevisionindex = this.apiRevisions.findIndex((apiRevision: APIRevision) => apiRevision.id === this.activeRevisionId);
      this.activeApiRevisionsMenu = this.apiRevisions
        .map((apiRevision: APIRevision) => {
        let typeClass = '';
        switch (apiRevision.apiRevisionType) {
          case 'Manual':
            typeClass = "fa-solid fa-arrow-up-from-bracket";
            break;
          case 'PullRequest':
            typeClass = "fa-solid fa-code-pull-request";
            break;
          case 'Automatic':
            typeClass = "fa-solid fa-robot";
            break;
        }
        return {
          resolvedLabel: apiRevision.resolvedLabel,
          label: (apiRevision.label) ? apiRevision.label : "no label",
          typeClass: typeClass,
          version: apiRevision.packageVersion,
          prNo: apiRevision.pullRequestNo,
          createdOn: apiRevision.createdOn,
          creatorBy: apiRevision.createdBy,
          lastUpdatedOn: apiRevision.lastUpdatedOn,
          isApproved: apiRevision.isApproved,
          command: () => {
            console.log('Selected API Revision:', apiRevision.label);
          }
        };
      });
      this.diffApiRevisionsMenu = this.activeApiRevisionsMenu.filter((apiRevision: any) => apiRevision.id !== this.activeRevisionId);
      this.selectedActiveAPIRevision = this.activeApiRevisionsMenu[selectedActiveAPIRevisionindex];
    }
  }
}
