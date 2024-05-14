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

  manualIcon = "fa-solid fa-arrow-up-from-bracket";
  prIcon = "fa-solid fa-code-pull-request";
  automaticIcon = "fa-solid fa-robot";

  activeApiRevisionsSearchValue: string | undefined = '';
  diffApiRevisionsSearchValue: string | undefined = '';
  activeApiRevisionsFilterValue: string | undefined = '';
  diffApiRevisionsFilterValue: string | undefined = '';
  filterOptions: any[] = [
    { label: 'All', value: 'all' },
    { label: 'PR', value: 'Pull Request' },
    { label: 'Manual', value: 'Manual' },
    { label: 'Automatic', value: 'Automatic' },
    { label: 'Released', value: 'Released' }];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['apiRevisions'] && changes['apiRevisions'].currentValue.length > 0) {
      const selectedActiveAPIRevisionindex = this.apiRevisions.findIndex((apiRevision: APIRevision) => apiRevision.id === this.activeRevisionId);
      this.activeApiRevisionsMenu = this.apiRevisions
        .map((apiRevision: APIRevision) => {
        let typeClass = '';
        switch (apiRevision.apiRevisionType) {
          case 'Manual':
            typeClass = this.manualIcon;
            break;
          case 'PullRequest':
            typeClass = this.prIcon;
            break;
          case 'Automatic':
            typeClass = this.automaticIcon;
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

  apiRevisionSearchFunction(event: KeyboardEvent, options: any) {
    options.filter(event);
  }

  apiRevisionFilterFunction(event: any) {
    console.log('Filter value changed:', event.value);
  }

  resetApiRevisionFilterFunction(options: any) {
    options.reset();
  }
}
