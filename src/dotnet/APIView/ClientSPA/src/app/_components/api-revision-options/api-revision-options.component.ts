import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { APIRevision } from 'src/app/_models/revision';

@Component({
  selector: 'app-api-revision-options',
  templateUrl: './api-revision-options.component.html',
  styleUrls: ['./api-revision-options.component.scss']
})
export class ApiRevisionOptionsComponent implements OnChanges {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  activeApiRevisionsMenu: any[] = [];
  diffApiRevisionsMenu: any[] = [];
  selectedActiveAPIRevision: any;
  selectedDiffAPIRevision: any = null;

  manualIcon = "fa-solid fa-arrow-up-from-bracket";
  prIcon = "fa-solid fa-code-pull-request";
  automaticIcon = "fa-solid fa-robot";

  activeApiRevisionsSearchValue: string = '';
  diffApiRevisionsSearchValue: string = '';

  activeApiRevisionsFilterValue: string | undefined = '';
  diffApiRevisionsFilterValue: string | undefined = '';

  activeApiRevisionfilterOptions: any[] = [
    { label: 'PR', value: 'PullRequest' },
    { label: 'Manual', value: 'Manual' },
    { label: 'Auto', value: 'Automatic' },
    { label: 'Released', value: 'Released' },
    { label: 'Approved', value: 'Approved' },
  ];

  diffApiRevisionfilterOptions: any[] = [
    { label: 'LatestRelease', value: 'LatestRelease' },
    { label: 'LatestApproved', value: 'LatestApproved' },
    { label: 'PR', value: 'PullRequest' },
    { label: 'Manual', value: 'Manual' },
    { label: 'Auto', value: 'Automatic' },
    { label: 'Released', value: 'Released' },
    { label: 'Approved', value: 'Approved' },
  ];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['apiRevisions'] && changes['apiRevisions'].currentValue.length > 0) {
      const selectedActiveAPIRevisionindex = this.apiRevisions.findIndex((apiRevision: APIRevision) => apiRevision.id === this.activeApiRevisionId);
      this.activeApiRevisionsMenu = this.mapRevisionToMenu(this.apiRevisions);
      this.diffApiRevisionsMenu = this.activeApiRevisionsMenu.filter((apiRevision: any) => apiRevision.id !== this.activeApiRevisionId);
      this.selectedActiveAPIRevision = this.activeApiRevisionsMenu[selectedActiveAPIRevisionindex];
    }
  }

  activeApiRevisionSearchFunction(event: KeyboardEvent) {
    this.activeApiRevisionsSearchValue = (event.target as HTMLInputElement).value;
    this.searchAndFilterDropdown(this.activeApiRevisionsSearchValue, this.activeApiRevisionsFilterValue, "active");
  }

  activeApiRevisionFilterFunction(event: any) {
    this.activeApiRevisionsFilterValue = event.value;
    this.searchAndFilterDropdown(this.activeApiRevisionsSearchValue, this.activeApiRevisionsFilterValue, "active");
  }

  activeApiRevisionChange(event: any) {
    console.log('Selected API Revision:', event);
  }

  diffApiRevisionSearchFunction(event: KeyboardEvent) {
    this.diffApiRevisionsSearchValue = (event.target as HTMLInputElement).value;
    this.searchAndFilterDropdown(this.diffApiRevisionsSearchValue, this.diffApiRevisionsFilterValue, "diff");
  }

  diffApiRevisionFilterFunction(event: any) {
    this.diffApiRevisionsFilterValue = event.value;
    this.searchAndFilterDropdown(this.diffApiRevisionsSearchValue, this.diffApiRevisionsFilterValue, "diff");
  }

  diffApiRevisionChange(event: any) {
    console.log('Selected API Revision:', event);
  }

  searchAndFilterDropdown(searchValue : string, filterValue  : string | undefined, dropDownMenu : string) {
    let filtered = this.apiRevisions.filter((apiRevision: APIRevision) => {
      switch (filterValue) {
        case 'PullRequest':
          return apiRevision.apiRevisionType === 'PullRequest';
        case 'Manual':
          return apiRevision.apiRevisionType === 'Manual';
        case 'Automatic':
          return apiRevision.apiRevisionType === 'Automatic';
        case 'Released':
          return apiRevision.isReleased;
        case 'Approved':
          return apiRevision.isApproved;
        default:
          return true;
      }
    });
    
    filtered = filtered.filter((apiRevision: APIRevision) => {
      return apiRevision.resolvedLabel.toLowerCase().includes(searchValue.toLowerCase());
    });

    if (dropDownMenu === "active") {
      this.activeApiRevisionsMenu = this.mapRevisionToMenu(filtered);
      if (this.selectedActiveAPIRevision && !this.activeApiRevisionsMenu.includes(this.selectedActiveAPIRevision)) {
        this.activeApiRevisionsMenu.push(this.selectedActiveAPIRevision);
      }
    }
  }

  resetDropDownFilter(dropDownMenu: string) {
    if (dropDownMenu === "active") {
      this.activeApiRevisionsSearchValue = '';
      this.activeApiRevisionsFilterValue = '';
      this.searchAndFilterDropdown(this.activeApiRevisionsSearchValue, this.activeApiRevisionsFilterValue, "active");
    }
  }

  mapRevisionToMenu(apiRevisions: APIRevision[]) {
    return apiRevisions
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
        isReleased: apiRevision.isReleased,
        releasedOn: apiRevision.releasedOn,
        command: () => {
          console.log('Selected API Blah:', apiRevision.label);
        }
      };
    });
  }
}

