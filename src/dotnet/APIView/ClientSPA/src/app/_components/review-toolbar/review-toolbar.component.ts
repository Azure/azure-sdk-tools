import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { PopoverModule } from 'primeng/popover';
import { TooltipModule } from 'primeng/tooltip';
import { ButtonModule } from 'primeng/button';
import { ButtonGroupModule } from 'primeng/buttongroup';
import { DialogModule } from 'primeng/dialog';
import { TimeagoModule } from 'ngx-timeago';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { APIRevision } from 'src/app/_models/revision';
import { CodeLineSearchInfo } from 'src/app/_models/codeLineSearchInfo';
import { UserProfile } from 'src/app/_models/userProfile';
import { CodeLineRowNavigationDirection, FULL_DIFF_STYLE, getTypeClass, TREE_DIFF_STYLE, AUTOMATIC_ICON, MANUAL_ICON, PR_ICON } from 'src/app/_helpers/common-helpers';
import { DIFF_API_REVISION_ID_QUERY_PARAM, DIFF_STYLE_QUERY_PARAM, getQueryParams } from 'src/app/_helpers/router-helpers';
import { AzureEngSemanticVersion } from 'src/app/_models/azureEngSemanticVersion';
import { LastUpdatedOnPipe } from 'src/app/_pipes/last-updated-on.pipe';

@Component({
  selector: 'app-review-toolbar',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SelectModule,
    SelectButtonModule,
    ToggleSwitchModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    PopoverModule,
    TooltipModule,
    ButtonModule,
    ButtonGroupModule,
    DialogModule,
    TimeagoModule,
    LastUpdatedOnPipe
  ],
  templateUrl: './review-toolbar.component.html',
  styleUrl: './review-toolbar.component.scss',
})
export class ReviewToolbarComponent implements OnInit, OnChanges {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';
  @Input() isDiffView: boolean = false;
  @Input() contentHasDiff: boolean | undefined = false;
  @Input() diffStyleInput: string | undefined;
  @Input() userProfile: UserProfile | undefined;
  @Input() codeLineSearchInfo: CodeLineSearchInfo | undefined = undefined;
  @Input() hasHiddenAPIs: boolean = false;

  @Output() diffStyleEmitter: EventEmitter<string> = new EventEmitter<string>();
  @Output() diffNavigationEmitter: EventEmitter<number> = new EventEmitter<number>();
  @Output() showCommentsEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showSystemCommentsEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showDocumentationEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() disableCodeLinesLazyLoadingEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLineNumbersEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showHiddenAPIEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() codeLineSearchTextEmitter: EventEmitter<string> = new EventEmitter<string>();
  @Output() codeLineSearchInfoEmitter: EventEmitter<CodeLineSearchInfo> = new EventEmitter<CodeLineSearchInfo>();
  @Output() copyReviewTextEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() commentNavigationEmitter: EventEmitter<number> = new EventEmitter<number>();

  private destroy$ = new Subject<void>();

  // Revision dropdown properties
  mappedApiRevisions: any[] = [];
  diffApiRevisionsMenu: any[] = [];
  selectedDiffAPIRevision: any = null;
  diffApiRevisionsSearchValue: string = '';
  diffApiRevisionsFilterValue: string | undefined = '';

  DIFF_API_REVISION_SELECT: string = 'diff-api';

  manualIcon = MANUAL_ICON;
  prIcon = PR_ICON;
  automaticIcon = AUTOMATIC_ICON;

  filterOptions: any[] = [
    { label: 'Approved', value: 'approved' },
    { label: 'Released', value: 'released' },
    { label: 'Auto', icon: this.automaticIcon, value: 'automatic' },
    { label: 'PR', icon: this.prIcon, value: 'pullRequest' },
    { label: 'Manual', icon: this.manualIcon, value: 'manual' }
  ];

  // Diff style properties
  diffStyleOptions: any[] = [
    { label: 'Full diff', value: FULL_DIFF_STYLE },
    { label: 'Changes only', value: TREE_DIFF_STYLE }
  ];
  selectedDiffStyle: string = TREE_DIFF_STYLE;

  // Page settings properties
  showCommentsSwitch: boolean = true;
  showSystemCommentsSwitch: boolean = true;
  showDocumentationSwitch: boolean = true;
  showHiddenAPISwitch: boolean = false;
  disableCodeLinesLazyLoading: boolean = false;
  showDisableCodeLinesLazyLoadingModal: boolean = false;
  showLineNumbersSwitch: boolean = true;

  // Search properties
  codeLineSearchText: FormControl = new FormControl('');
  CodeLineRowNavigationDirection = CodeLineRowNavigationDirection;

  // Safe display properties
  currentMatchIndexValue: number = 0;
  totalMatchCountValue: number = 0;
  showSearchControls: boolean = false;

  constructor(private route: ActivatedRoute, private router: Router) {}

  ngOnInit() {
    // Initialize settings from user profile
    if (this.userProfile?.preferences) {
      this.showCommentsSwitch = this.userProfile.preferences.showComments ?? true;
      this.showSystemCommentsSwitch = this.userProfile.preferences.showSystemComments ?? true;
      this.showDocumentationSwitch = this.userProfile.preferences.showDocumentation ?? true;
      this.showHiddenAPISwitch = this.userProfile.preferences.showHiddenApis ?? false;
      this.showLineNumbersSwitch = !this.userProfile.preferences.hideLineNumbers;
      this.disableCodeLinesLazyLoading = this.userProfile.preferences.disableCodeLinesLazyLoading ?? false;
    }

    // Initialize diff style
    if (this.diffStyleInput) {
      this.selectedDiffStyle = this.diffStyleInput;
    }

    // Setup search text listener
    this.codeLineSearchText.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe((value: string) => {
      this.codeLineSearchTextEmitter.emit(value);
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codeLineSearchInfo']) {
      if (this.codeLineSearchInfo && this.codeLineSearchInfo.currentMatch) {
         try {
           const match = this.codeLineSearchInfo.currentMatch as any;
           this.currentMatchIndexValue = (typeof match.index === 'number') ? match.index + 1 : 1;
           this.totalMatchCountValue = this.codeLineSearchInfo.totalMatchCount || 0;
           this.showSearchControls = true;
         } catch(e) {
           this.currentMatchIndexValue = 1;
           this.totalMatchCountValue = 0;
           this.showSearchControls = false;
         }
      } else {
        this.currentMatchIndexValue = 0;
        this.totalMatchCountValue = 0;
        this.showSearchControls = false;
      }
    }

    if (changes['apiRevisions'] || changes['activeApiRevisionId'] || changes['diffApiRevisionId']) {
      if (this.apiRevisions.length > 0) {
        this.mappedApiRevisions = this.mapRevisionToMenu(this.apiRevisions);
        this.tagSpecialRevisions(this.mappedApiRevisions);

        this.diffApiRevisionsMenu = this.mappedApiRevisions.filter((apiRevision: any) => apiRevision.id !== this.activeApiRevisionId);
        const selectedDiffAPIRevisionindex = this.diffApiRevisionsMenu.findIndex((apiRevision: APIRevision) => apiRevision.id === this.diffApiRevisionId);
        if (selectedDiffAPIRevisionindex >= 0) {
          this.selectedDiffAPIRevision = this.diffApiRevisionsMenu[selectedDiffAPIRevisionindex];
        } else {
          this.selectedDiffAPIRevision = null;
        }
      }
    }

    if (changes['diffStyleInput'] && this.diffStyleInput) {
      this.selectedDiffStyle = this.diffStyleInput;
    }

    if (changes['userProfile'] && this.userProfile?.preferences) {
      this.showCommentsSwitch = this.userProfile.preferences.showComments ?? true;
      this.showSystemCommentsSwitch = this.userProfile.preferences.showSystemComments ?? true;
      this.showDocumentationSwitch = this.userProfile.preferences.showDocumentation ?? true;
      this.showHiddenAPISwitch = this.userProfile.preferences.showHiddenApis ?? false;
      this.showLineNumbersSwitch = !this.userProfile.preferences.hideLineNumbers;
      this.disableCodeLinesLazyLoading = this.userProfile.preferences.disableCodeLinesLazyLoading ?? false;
    }
  }

  diffApiRevisionChange(event: any) {
    let newQueryParams = getQueryParams(this.route);
    newQueryParams[DIFF_API_REVISION_ID_QUERY_PARAM] = event.value?.id;
    newQueryParams[DIFF_STYLE_QUERY_PARAM] = TREE_DIFF_STYLE;
    this.router.navigate([], { queryParams: newQueryParams });
  }

  diffApiRevisionClear(event: any) {
    let newQueryParams = getQueryParams(this.route);
    newQueryParams[DIFF_API_REVISION_ID_QUERY_PARAM] = null;
    newQueryParams[DIFF_STYLE_QUERY_PARAM] = null;
    this.router.navigate([], { queryParams: newQueryParams });
  }

  diffApiRevisionSearchFunction(event: KeyboardEvent) {
    this.diffApiRevisionsSearchValue = (event.target as HTMLInputElement).value;
    this.searchAndFilterDropdown(this.diffApiRevisionsSearchValue, this.diffApiRevisionsFilterValue);
  }

  diffApiRevisionFilterFunction(event: any) {
    this.diffApiRevisionsFilterValue = event.value;
    this.searchAndFilterDropdown(this.diffApiRevisionsSearchValue, this.diffApiRevisionsFilterValue);
  }

  searchAndFilterDropdown(searchValue: string, filterValue: string | undefined) {
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

    this.diffApiRevisionsMenu = filtered.filter((apiRevision: APIRevision) => apiRevision.id !== this.activeApiRevisionId);
    if (this.selectedDiffAPIRevision && !this.diffApiRevisionsMenu.includes(this.selectedDiffAPIRevision)) {
      this.diffApiRevisionsMenu.unshift(this.selectedDiffAPIRevision);
    }
  }

  resetDropDownFilter() {
    this.diffApiRevisionsSearchValue = '';
    this.diffApiRevisionsFilterValue = '';
    this.searchAndFilterDropdown(this.diffApiRevisionsSearchValue, this.diffApiRevisionsFilterValue);
  }

  onDiffStyleChange(event: any) {
    if (event.value) {
      this.diffStyleEmitter.emit(event.value);
    }
  }

  onCommentsSwitchChange(event: any) {
    this.showCommentsEmitter.emit(event.checked);
  }


  onShowSystemCommentsSwitchChange(event: any) {
    this.showSystemCommentsEmitter.emit(event.checked);
  }

  onShowDocumentationSwitchChange(event: any) {
    this.showDocumentationEmitter.emit(event.checked);
  }

  onShowHiddenAPISwitchChange(event: any) {
    this.showHiddenAPIEmitter.emit(event.checked);
  }

  onShowLineNumbersSwitchChange(event: any) {
    this.showLineNumbersEmitter.emit(event.checked);
  }

  onDisableLazyLoadingSwitchChange(event: any) {
    if (event.checked) {
      this.showDisableCodeLinesLazyLoadingModal = true;
    } else {
      this.disableCodeLinesLazyLoadingEmitter.emit(event.checked);
    }
  }

  onDisableLazyLoadingModalHide() {
    this.showDisableCodeLinesLazyLoadingModal = false;
  }

  onDisableLazyLoadingConfirm() {
    this.disableCodeLinesLazyLoadingEmitter.emit(true);
    this.showDisableCodeLinesLazyLoadingModal = false;
  }

  onDisableLazyLoadingCancel() {
    this.showDisableCodeLinesLazyLoadingModal = false;
    // Revert the toggle if they cancel
    this.disableCodeLinesLazyLoading = false;
  }

  navigateSearch(direction: number) {
    const searchInfo = this.codeLineSearchInfo;
    if (searchInfo && searchInfo.currentMatch && searchInfo.totalMatchCount !== undefined && direction !== 0) {
      let newMatch = searchInfo.currentMatch;

      if (direction > 0) {
        if (newMatch.next) {
          newMatch = newMatch.next;
        } else {
          while (newMatch.prev) {
            newMatch = newMatch.prev;
          }
        }
      } else {
        if (newMatch.prev) {
          newMatch = newMatch.prev;
        } else {
          while (newMatch.next) {
            newMatch = newMatch.next;
          }
        }
      }

      this.codeLineSearchInfoEmitter.emit(new CodeLineSearchInfo(newMatch, searchInfo.totalMatchCount));
    }
  }

  clearReviewSearch() {
    this.codeLineSearchText.setValue('');
    this.codeLineSearchTextEmitter.emit('');
  }

  copyReviewText() {
    this.copyReviewTextEmitter.emit(this.isDiffView);
  }

  mapRevisionToMenu(apiRevisions: APIRevision[]) {
    return apiRevisions.map((apiRevision: APIRevision) => {
      const mapped = {
        id: apiRevision.id,
        resolvedLabel: apiRevision.resolvedLabel || apiRevision.label || apiRevision.packageVersion || `PR ${apiRevision.pullRequestNo}` || 'Revision',
        language: apiRevision.language,
        label: apiRevision.label || apiRevision.resolvedLabel || apiRevision.packageVersion || `PR ${apiRevision.pullRequestNo}` || 'Revision',
        files: apiRevision.files,
        packageName: apiRevision.packageName,
        typeClass: getTypeClass(apiRevision.apiRevisionType),
        apiRevisionType: apiRevision.apiRevisionType,
        packageVersion: apiRevision.packageVersion,
        prNo: apiRevision.pullRequestNo,
        createdOn: apiRevision.createdOn,
        createdBy: apiRevision.createdBy,
        lastUpdatedOn: apiRevision.lastUpdatedOn,
        isApproved: apiRevision.isApproved,
        isReleased: apiRevision.isReleased,
        releasedOn: apiRevision.releasedOn,
        changeHistory: apiRevision.changeHistory,
        isLatestGA: false,
        isLatestApproved: false,
        isLatestMain: false,
        isLatestReleased: false
      };
      return mapped;
    });
  }

  tagSpecialRevisions(mappedApiRevisions: any[]) {
    this.tagLatestGARevision(mappedApiRevisions);
    this.tagLatestApprovedRevision(mappedApiRevisions);
    this.tagCurrentMainRevision(mappedApiRevisions);
    this.tagLatestReleasedRevision(mappedApiRevisions);
  }

  tagLatestGARevision(apiRevisions: any[]) {
    const gaRevisions: any[] = [];
    for (let apiRevision of apiRevisions) {
      if (apiRevision.isReleased && apiRevision.packageVersion) {
        const semVar = new AzureEngSemanticVersion(apiRevision.packageVersion, apiRevision.language);
        if (semVar.versionType == "GA") {
          apiRevision.semanticVersion = semVar;
          gaRevisions.push(apiRevision);
        }
      }
    }
    if (gaRevisions.length > 0) {
      gaRevisions.sort((a: any, b: any) => b.semanticVersion.compareTo(a.semanticVersion));
      gaRevisions[0].isLatestGA = true;
      this.updateTaggedAPIRevisions(apiRevisions, gaRevisions[0]);
    }
  }

  tagLatestApprovedRevision(apiRevisions: any[]) {
    const approvedRevisions: any[] = [];
    for (let apiRevision of apiRevisions) {
      if (apiRevision.isApproved && apiRevision.changeHistory.length > 0) {
        var approval = apiRevision.changeHistory.find((change: any) => change.changeAction === 'approved');
        if (approval) {
          apiRevision.approvedOn = approval.changedOn;
          approvedRevisions.push(apiRevision);
        }
      }
    }
    if (approvedRevisions.length > 0) {
      approvedRevisions.sort((a: any, b: any) => (new Date(b.approvedOn) as any) - (new Date(a.approvedOn) as any));
      approvedRevisions[0].isLatestApproved = true;
      this.updateTaggedAPIRevisions(apiRevisions, approvedRevisions[0]);
    }
  }

  tagCurrentMainRevision(apiRevisions: any[]) {
    const automaticRevisions: any[] = [];
    for (let apiRevision of apiRevisions) {
      if (apiRevision.apiRevisionType === 'automatic') {
        automaticRevisions.push(apiRevision);
      }
    }
    if (automaticRevisions.length > 0) {
      automaticRevisions.sort((a: any, b: any) => (new Date(b.lastUpdatedOn) as any) - (new Date(a.lastUpdatedOn) as any));
      automaticRevisions[0].isLatestMain = true;
      this.updateTaggedAPIRevisions(apiRevisions, automaticRevisions[0]);
    }
  }

  tagLatestReleasedRevision(apiRevisions: any[]) {
    const releasedRevisions: any[] = [];
    for (let apiRevision of apiRevisions) {
      if (apiRevision.isReleased) {
        releasedRevisions.push(apiRevision);
      }
    }
    if (releasedRevisions.length > 0) {
      releasedRevisions.sort((a: any, b: any) => (new Date(b.releasedOn) as any) - (new Date(a.releasedOn) as any));
      releasedRevisions[0].isLatestReleased = true;
      this.updateTaggedAPIRevisions(apiRevisions, releasedRevisions[0]);
    }
  }

  private updateTaggedAPIRevisions(apiRevisions: any[], taggedApiRevision: any) {
    apiRevisions.splice(apiRevisions.indexOf(taggedApiRevision), 1);
    apiRevisions.unshift(taggedApiRevision);
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
