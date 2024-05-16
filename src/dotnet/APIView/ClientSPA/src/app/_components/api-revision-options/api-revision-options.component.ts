import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { APIRevision } from 'src/app/_models/revision';

@Component({
  selector: 'app-api-revision-options',
  templateUrl: './api-revision-options.component.html',
  styleUrls: ['./api-revision-options.component.scss']
})
export class ApiRevisionOptionsComponent implements OnChanges {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';

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

  filterOptions: any[] = [
    { label: 'PR', value: 'PullRequest' },
    { label: 'Manual', value: 'Manual' },
    { label: 'Auto', value: 'Automatic' },
    { label: 'Released', value: 'Released' },
    { label: 'Approved', value: 'Approved' },
  ];

  constructor(private route: ActivatedRoute, private router: Router) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['apiRevisions'] || changes['activeApiRevisionId'] || changes['diffApiRevisionId']) {
      if (this.apiRevisions.length > 0) {
        const mappedApiRevisions = this.mapRevisionToMenu(this.apiRevisions);

        this.activeApiRevisionsMenu = mappedApiRevisions.filter((apiRevision: any) => apiRevision.id !== this.diffApiRevisionId);
        const selectedActiveAPIRevisionindex = this.activeApiRevisionsMenu.findIndex((apiRevision: APIRevision) => apiRevision.id === this.activeApiRevisionId);
        this.selectedActiveAPIRevision = this.activeApiRevisionsMenu[selectedActiveAPIRevisionindex];

        this.diffApiRevisionsMenu = mappedApiRevisions.filter((apiRevision: any) => apiRevision.id !== this.activeApiRevisionId);
        const selectedDiffAPIRevisionindex = this.diffApiRevisionsMenu.findIndex((apiRevision: APIRevision) => apiRevision.id === this.diffApiRevisionId);
        if (selectedDiffAPIRevisionindex >= 0) {
          this.selectedDiffAPIRevision = this.diffApiRevisionsMenu[selectedDiffAPIRevisionindex];
        }
        else {
          this.selectedDiffAPIRevision = null;
        }
      }
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
    let newQueryParams = this.getQueryParams()
    newQueryParams['activeApiRevisionId'] = event.value.id;
    this.router.navigate([], { queryParams: newQueryParams });
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
    let newQueryParams = this.getQueryParams()
    newQueryParams['diffApiRevisionId'] = event.value.id;
    this.router.navigate([], { queryParams: newQueryParams });
  }

  diffApiRevisionClear(event: any) {
    let newQueryParams = this.getQueryParams()
    newQueryParams['diffApiRevisionId'] = null;
    this.router.navigate([], { queryParams: newQueryParams });
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
        this.activeApiRevisionsMenu.unshift(this.selectedActiveAPIRevision);
      }
    }

    if (dropDownMenu === "diff") {
      filtered = filtered.filter((apiRevision: APIRevision) => apiRevision.id !== this.activeApiRevisionId);
      this.diffApiRevisionsMenu = this.mapRevisionToMenu(filtered);
      if (this.selectedDiffAPIRevision && !this.diffApiRevisionsMenu.includes(this.selectedDiffAPIRevision)) {
        this.diffApiRevisionsMenu.unshift(this.selectedDiffAPIRevision);
      }
    }
  }

  resetDropDownFilter(dropDownMenu: string) {
    if (dropDownMenu === "active") {
      this.activeApiRevisionsSearchValue = '';
      this.activeApiRevisionsFilterValue = '';
      this.searchAndFilterDropdown(this.activeApiRevisionsSearchValue, this.activeApiRevisionsFilterValue, "active");
    }

    if (dropDownMenu === "diff") {
      this.diffApiRevisionsSearchValue = '';
      this.diffApiRevisionsFilterValue = '';
      this.searchAndFilterDropdown(this.diffApiRevisionsSearchValue, this.diffApiRevisionsFilterValue, "diff");
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
        id : apiRevision.id,
        resolvedLabel: apiRevision.resolvedLabel,
        label: apiRevision.label,
        typeClass: typeClass,
        apiRevisionType: apiRevision.apiRevisionType,
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

  getQueryParams() {
    return this.route.snapshot.queryParamMap.keys.reduce((params: { [key: string]: any; }, key) => {
      params[key] = this.route.snapshot.queryParamMap.get(key);
      return params;
    }, {});
  }
}

