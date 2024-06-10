import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
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

  mappedApiRevisions: any[] = [];
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
    { label: 'Approved', value: 'approved' },
    { label: 'Released', value: 'released' },
    { label: 'Auto', icon: this.automaticIcon, value: 'automatic' },
    { label: 'PR', icon: this.prIcon, value: 'pullRequest' },
    { label: 'Manual', icon: this.manualIcon, value: 'manual' }
  ];

  constructor(private route: ActivatedRoute, private router: Router) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['apiRevisions'] || changes['activeApiRevisionId'] || changes['diffApiRevisionId']) {
      if (this.apiRevisions.length > 0) {
        let mappedApiRevisions = this.mapRevisionToMenu(this.apiRevisions);
        this.mappedApiRevisions = this.identifyAndProcessSpecialAPIRevisions(mappedApiRevisions);

        this.activeApiRevisionsMenu = this.mappedApiRevisions.filter((apiRevision: any) => apiRevision.id !== this.diffApiRevisionId);
        const selectedActiveAPIRevisionindex = this.activeApiRevisionsMenu.findIndex((apiRevision: APIRevision) => apiRevision.id === this.activeApiRevisionId);
        this.selectedActiveAPIRevision = this.activeApiRevisionsMenu[selectedActiveAPIRevisionindex];

        this.diffApiRevisionsMenu = this.mappedApiRevisions.filter((apiRevision: any) => apiRevision.id !== this.activeApiRevisionId);
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
    let newQueryParams = getQueryParams(this.route);
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
    let newQueryParams = getQueryParams(this.route);
    newQueryParams['diffApiRevisionId'] = event.value?.id;
    this.router.navigate([], { queryParams: newQueryParams });
  }

  diffApiRevisionClear(event: any) {
    let newQueryParams = getQueryParams(this.route);
    newQueryParams['diffApiRevisionId'] = null;
    newQueryParams['diffStyle'] = null;
    this.router.navigate([], { queryParams: newQueryParams });
  }

  searchAndFilterDropdown(searchValue : string, filterValue  : string | undefined, dropDownMenu : string) {
    let filtered = this.mappedApiRevisions.filter((apiRevision: APIRevision) => {
      switch (filterValue) {
        case 'pullRequest':
          return apiRevision.apiRevisionType === 'pullRequest';
        case 'manual':
          return apiRevision.apiRevisionType === 'manual';
        case 'automatic':
          return apiRevision.apiRevisionType === 'automatic';
        case 'released':
          return apiRevision.isReleased;
        case 'approved':
          return apiRevision.isApproved;
        default:
          return true;
      }
    });
    
    filtered = filtered.filter((apiRevision: APIRevision) => {
      return apiRevision.resolvedLabel.toLowerCase().includes(searchValue.toLowerCase());
    });

    if (dropDownMenu === "active") {
      this.activeApiRevisionsMenu = filtered;
      if (this.selectedActiveAPIRevision && !this.activeApiRevisionsMenu.includes(this.selectedActiveAPIRevision)) {
        this.activeApiRevisionsMenu.unshift(this.selectedActiveAPIRevision);
      }
    }

    if (dropDownMenu === "diff") {
      filtered = filtered.filter((apiRevision: APIRevision) => apiRevision.id !== this.activeApiRevisionId);
      this.diffApiRevisionsMenu = filtered
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
        case 'manual':
          typeClass = this.manualIcon;
          break;
        case 'pullRequest':
          typeClass = this.prIcon;
          break;
        case 'automatic':
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
        isLatestGA : false,
        isLatestApproved : false,
        isLatestMain : false,
        isLatestReleased : false,
        command: () => {
        }
      };
    });
  }

  identifyAndProcessSpecialAPIRevisions(mappedApiRevisions: any []) {
    const result = [];
    const semVarRegex = /(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?/;

    let latestGAApiRevision : any = null;
    let latestApprovedApiRevision : any = null;
    let currentMainApiRevision : any = null;
    let latestReleasedApiRevision : any = null;

    while (mappedApiRevisions.length > 0) {
      let apiRevision = mappedApiRevisions.shift();

      if (latestGAApiRevision === null) {
        let versionParts = apiRevision.version.match(semVarRegex);
        if (versionParts.groups?.prelabel === undefined && versionParts.groups?.prenumber === undefined &&
          versionParts.groups?.prenumsep === undefined && versionParts.groups?.presep === undefined) {
            apiRevision.isLatestGA = true;
            latestGAApiRevision = apiRevision;
            continue;
        }
      }

      if (latestApprovedApiRevision === null) {
        if (apiRevision.isApproved) {
          apiRevision.isLatestApproved = true;
          latestApprovedApiRevision = apiRevision;
          continue;
        }
      }

      if (currentMainApiRevision === null) {
        if (apiRevision.apiRevisionType === 'Automatic') {
          apiRevision.isLatestMain = true;
          currentMainApiRevision = apiRevision;
          continue;
        }
      }

      if (latestReleasedApiRevision === null) {
        if (apiRevision.isReleased) {
          apiRevision.isLatestReleased = true;
          latestReleasedApiRevision = apiRevision;
          continue;
        }
      }
      result.push(apiRevision);
    }

    if (latestGAApiRevision) {
      result.unshift(latestGAApiRevision);
    }

    if (latestApprovedApiRevision) {
      result.unshift(latestApprovedApiRevision);
    }

    if (currentMainApiRevision) {
      result.unshift(currentMainApiRevision);
    }

    if (latestReleasedApiRevision) {
      result.unshift(latestReleasedApiRevision);
    }

    return result;
  }
}

