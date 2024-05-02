import { Component, ElementRef, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { MenuItem, SortEvent } from 'primeng/api';
import { Table, TableFilterEvent, TableLazyLoadEvent } from 'primeng/table';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { Pagination } from 'src/app/_models/pagination';
import { FirstReleaseApproval, Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { AuthService } from 'src/app/_services/auth/auth.service';
import { ConfigService } from 'src/app/_services/config/config.service';
import { RevisionsService } from 'src/app/_services/revisions/revisions.service';

@Component({
  selector: 'app-revisions-list',
  templateUrl: './revisions-list.component.html',
  styleUrls: ['./revisions-list.component.scss']
})
export class RevisionsListComponent implements OnInit, OnChanges {
  @Input() review : Review | null = null;

  userProfile : UserProfile | undefined;
  reviewPageWebAppUrl : string = this.configService.webAppUrl + "Assemblies/Review/";
  profilePageWebAppUrl : string = this.configService.webAppUrl + "Assemblies/Profile/";
  revisions : APIRevision[] = [];
  totalNumberOfRevisions = 0;
  pagination: Pagination | undefined;
  insertIndex : number = 0;
  rowHeight: number = 48;
  noOfRows: number = Math.floor((window.innerHeight * 0.75) / this.rowHeight); // Dynamically Computing the number of rows to show at once
  pageSize = 20; // No of items to load from server at a time
  sortField : string = "lastUpdatedOn";
  sortOrder : number = 1;
  filters: any = null;

  sidebarVisible : boolean = false;

  // Filters
  details: any[] = [];
  selectedDetails: any[] = [];
  private _showDeletedAPIRevisions : boolean = false;
  private _showAPIRevisionsAssignedToMe : boolean = false;

  // Context Menu
  contextMenuItems! : MenuItem[];
  selectedRevision!: APIRevision;
  selectedRevisions!: APIRevision[];
  showSelectionActions : boolean = false;
  showDiffButton : boolean = false;
  showDeleteButton : boolean = false;

  // Messages
  apiRevisionsListDetail: string = "APIRevision(s) from"

  badgeClass : Map<string, string> = new Map<string, string>();

  constructor(private revisionsService: RevisionsService, private authService: AuthService, private configService: ConfigService) { }

  ngOnInit(): void {
    this.createFilters();
    this.createContextMenuItems();
    this.setDetailsIcons();
    this.loadAPIRevisions(0, this.pageSize * 2, true);
    this.authService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['review'] && changes['review'].previousValue != changes['review'].currentValue){
      if (this.showAPIRevisionsAssignedToMe)
      {
        this.toggleShowAPIRevisionsAssignedToMe();
      }
      else {
        this.loadAPIRevisions(0, this.pageSize * 2, true);
      }
      this.showSelectionActions = false;
      this.showDiffButton = false;
    }
  }

  /**
   * Load revision from API
   *  * @param append wheather to add to or replace existing list
   */
  loadAPIRevisions(noOfItemsRead : number, pageSize: number, resetReviews = false, filters: any = null, sortField: string ="lastUpdatedOn",  sortOrder: number = 1) {
    let label : string = "";
    let author : string = "";
    let reviewId: string = this.review?.id ?? "";
    let details : string [] = [];
    if (filters)
    {
      label = filters.label.value ?? label;
      author = filters.author.value ?? author;
      details = (filters.details.value != null) ? filters.details.value.map((item: any) => item.data): details;
    }

    this.revisionsService.getAPIRevisions(noOfItemsRead, pageSize, reviewId, label, author, details, sortField, sortOrder, 
      this.showDeletedAPIRevisions, this.showAPIRevisionsAssignedToMe).subscribe({
      next: (response: any) => {
        if (response.result && response.pagination) {
          if (resetReviews)
          {
            const arraySize = Math.ceil(response.pagination!.totalCount + Math.min(20, (0.05 * response.pagination!.totalCount))) // Add 5% extra rows to avoid flikering
            this.revisions = Array.from({ length: arraySize });
            this.insertIndex = 0;
            this.showSelectionActions = false;
            this.showDiffButton = false;
          }

          if (response.result.length > 0)
          {
            this.revisions.splice(this.insertIndex, this.insertIndex + response.result.length, ...response.result);
            this.insertIndex = this.insertIndex + response.result.length;
            this.pagination = response.pagination;
            this.totalNumberOfRevisions = this.pagination?.totalCount!;
          }
        }
      }
    });
  }

  createContextMenuItems() {
    if (this.showDeletedAPIRevisions)
    {
      this.contextMenuItems = [
        { label: 'Restore', icon: 'pi pi-folder-open', command: () => this.viewRevision(this.selectedRevision) }
      ];
    }
    else 
    {
      this.contextMenuItems = [
        { label: 'View', icon: 'pi pi-folder-open', command: () => this.viewRevision(this.selectedRevision) },
        { label: 'Delete', icon: 'pi pi-fw pi-times', command: () => this.deleteRevision(this.selectedRevision) }
      ];
    }
  }

  createFilters() {     
    this.details = [
      {
        label: 'Status',
        data: 'All',
        items: [
          { label: "Approved", data: "Approved" },
          { label: "Pending", data: "Pending" },
        ]
      },
      {
        label: 'Type',
        data: 'All',
        items: [
          { label: "Automatic", data: "Automatic" },
          { label: "Manual", data: "Manual" },
          { label: "Pull Request", data: "PullRequest" }
        ]
      }
    ];
  }

  setDetailsIcons(){
    // Set Badge Class for details Icons
    this.badgeClass.set("false", "fa-solid fa-circle-minus text-warning");
    this.badgeClass.set("true", "fas fa-check-circle text-success");
    this.badgeClass.set("Manual", "fa-solid fa-arrow-up-from-bracket");
    this.badgeClass.set("PullRequest", "fa-solid fa-code-pull-request");
    this.badgeClass.set("Automatic", "fa-solid fa-robot");
  }

  viewDiffOfSelectedAPIRevisions() {
    if (this.selectedRevisions.length == 2)
    {
      this.revisionsService.openDiffOfAPIRevisions(this.review!.id, this.selectedRevisions[0].id, this.selectedRevisions[1].id)
    }
  }
  
  viewRevision(revision: APIRevision) {
    if (!this.showDeletedAPIRevisions)
    {
      this.revisionsService.openAPIRevisionPage(this.review!.id, revision.id);
    }
  }

  deleteRevisions(revisions: APIRevision []) {
    this.revisionsService.deleteAPIRevisions(this.review!.id, revisions.map(r => r.id)).subscribe({
      next: (response: any) => {
        if (response) {
          this.loadAPIRevisions(0, this.pageSize * 2, true);
          this.clearActionButtons();
        }
      }
    });
  }

  restoreRevisions(revisions: APIRevision []) {
    this.revisionsService.restoreAPIRevisions(this.review!.id, revisions.map(r => r.id)).subscribe({
      next: (response: any) => {
        if (response) {
          this.loadAPIRevisions(0, this.pageSize * 2, true);
          this.clearActionButtons();
        }
      }
    });
  }

  deleteRevision(revision: APIRevision) {
    this.revisionsService.deleteAPIRevisions(revision.reviewId, [revision.id]).subscribe({
      next: (response: any) => {
        if (response) {
          this.loadAPIRevisions(0, this.pageSize * 2, true);
          this.clearActionButtons();
        }
      }
    });
  }

  clearActionButtons() {
    this.selectedRevisions = [];
    this.showSelectionActions = false;
    this.showDiffButton = false;
    this.showDeleteButton = false;
  }

  /**
  * Return true if table has filters applied.
  */
  tableHasFilters() : boolean {
    return (
      this.sortField != "lastUpdatedOn" || this.sortOrder != 1 || 
      (this.filters && (this.filters.label.value != null || this.filters.author.value != null || this.filters.details.value != null)) ||
      this.showDeletedAPIRevisions || this.showAPIRevisionsAssignedToMe);
  }

  /**
  * Clear all filters in Table
  */
  clear(table: Table | undefined = undefined) {
    if (table) {
      table.clear();
    }
    this.showAPIRevisionsAssignedToMe = false;
    this.showDeletedAPIRevisions = false;
    this.loadAPIRevisions(0, this.pageSize * 2, true);
  }

  /**
  * Clear selected items on the page
  */
  clearSelection() {
    this.selectedRevisions = []
    this.showSelectionActions = false;
    this.showDeleteButton = false;
  }

  /**
  * Toggle Show deleted APIRevisions
  */
  toggleShowDeletedAPIRevisions() {
    this.showDeletedAPIRevisions = !this.showDeletedAPIRevisions;
    this.showAPIRevisionsAssignedToMe = false;
    this.loadAPIRevisions(0, this.pageSize * 2, true);
  }

  /**
  * Toggle Show APIRevisions Assigned to Me
  */
  toggleShowAPIRevisionsAssignedToMe() {
    this.showAPIRevisionsAssignedToMe = !this.showAPIRevisionsAssignedToMe;
    this.showDeletedAPIRevisions = false;
    if (this.showAPIRevisionsAssignedToMe) {
      this.review = null;
    }
    this.loadAPIRevisions(0, this.pageSize * 2, true);
  }

  updateAPIRevisoinsListDetails() {
    let msg = "APIRevision(s)";
    if (this.showDeletedAPIRevisions)
    {
      msg = "Deleted " + msg;
    }
    if (this.showAPIRevisionsAssignedToMe)
    {
      msg = msg + " Assigned to Me";
    }
    msg = msg + " from";
    this.apiRevisionsListDetail = msg;
  }

  // Getters and Setters
  get showDeletedAPIRevisions(): boolean {
    return this._showDeletedAPIRevisions;
  }
  set showDeletedAPIRevisions(value: boolean) {
    this._showDeletedAPIRevisions = value;
    this.updateAPIRevisoinsListDetails();
    this.createContextMenuItems();
  }

  get showAPIRevisionsAssignedToMe(): boolean {
    return this._showAPIRevisionsAssignedToMe;
  }
  set showAPIRevisionsAssignedToMe(value: boolean) {
    this._showAPIRevisionsAssignedToMe = value;
    this.updateAPIRevisoinsListDetails();
  }

  /**
   * Callback to invoke on scroll /lazy load.
   * @param event the lazyload event
   */
  onLazyLoad(event: TableLazyLoadEvent) {
      const last = Math.min(event.last!, this.totalNumberOfRevisions);
      this.sortField = event.sortField as string ?? "lastUpdatedOn";
      this.sortOrder = event.sortOrder as number ?? 1;
      this.filters = event.filters;
      if (last! > (this.insertIndex - this.pageSize))
      {
        if (this.pagination && this.pagination?.noOfItemsRead! < this.pagination?.totalCount!)
        {
          this.loadAPIRevisions(this.pagination!.noOfItemsRead, this.pageSize, false, event.filters, this.sortField, this.sortOrder);
        }
      }
      event.forceUpdate!();
    }

  /**
   * Callback to invoke on table filter.
   * @param event the Filter event
   */
  onFilter(event: TableFilterEvent) {
    this.loadAPIRevisions(0, this.pageSize, true, event.filters);
  }

  /**
   * Callback to invoke on table selection.
   * @param event the Filter event
   */
  onSelectionChange(value : APIRevision[] = []) {
    this.selectedRevisions = value;
    this.showSelectionActions = (value.length > 0) ? true : false;
    this.showDiffButton = (value.length == 2) ? true : false;
    let canDelete = (value.length > 0)? true : false;
    for (const revision of value) {
      if (revision.createdBy != this.userProfile?.userName || revision.apiRevisionType != "Manual")
      {
        canDelete = false;
        break;
      }
    }
    this.showDeleteButton = canDelete;
  }

  /**
   * Callback to invoke on column sort.
   * @param event the Filter event
   */
  onSort(event: SortEvent) {
      this.loadAPIRevisions(0, this.pageSize, true, null, event.field, event.order);
    }
}
