import { Component } from '@angular/core';
import { MenuItem, SortEvent } from 'primeng/api';
import { TableFilterEvent, TableLazyLoadEvent } from 'primeng/table';
import { Pagination } from 'src/app/_models/pagination';
import { Revision } from 'src/app/_models/revision';
import { RevisionsService } from 'src/app/_services/revisions/revisions.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-revisions-list',
  templateUrl: './revisions-list.component.html',
  styleUrls: ['./revisions-list.component.scss']
})
export class RevisionsListComponent {
  reviewPageWebAppUrl : string = environment.webAppUrl + "Assemblies/review/";
  profilePageWebAppUrl : string = environment.webAppUrl + "Assemblies/profile/";
  revisions : Revision[] = [];
  totalNumberOfRevisions = 0;
  pagination: Pagination | undefined;
  insertIndex : number = 0;

  pageSize = 20;
  first: number = 0;
  last: number  = 0;

  // Filters
  details: any[] = [];
  selectedDetails: any[] = [];

  // Context Menu
  contextMenuItems! : MenuItem[];
  selectedRevision!: Revision;
  selectedRevisions!: Revision[];
  showSelectionAction : boolean = false;

  badgeClass : Map<string, string> = new Map<string, string>();

  constructor(private revisionsService: RevisionsService) { }

  ngOnInit(): void {
    this.loadRevisions(0, this.pageSize * 2, true); // Load row 1 - 40 for starts
    this.createFilters();
    this.createContextMenuItems();
    this.setDetailsIcons();
  }


  /**
   * Load revision from API
   *  * @param append wheather to add to or replace existing list
   */
  loadRevisions(noOfItemsRead : number, pageSize: number, resetReviews = false, filters: any = null, sortField: string ="lastUpdated",  sortOrder: number = 1) {
    let label : string = "";
    let languages : string [] = [];
    let details : string [] = [];
    if (filters)
    {
      label = filters.label.value ?? label;
      details = (filters.details.value != null) ? filters.details.value.map((item: any) => item.data): details;
    }

    this.revisionsService.getRevisions(noOfItemsRead, pageSize, label, languages, details, sortField, sortOrder).subscribe({
      next: response => {
        if (response.result && response.pagination) {
          if (resetReviews)
          {
            this.revisions = Array.from({ length: response.pagination!.totalCount });
            this.insertIndex = 0;
          }
          this.revisions.splice(this.insertIndex, this.insertIndex + response.result.length, ...response.result);
          this.insertIndex = this.insertIndex + response.result.length;
          this.pagination = response.pagination;
          this.totalNumberOfRevisions = this.pagination.totalCount;
        }
      }
    });
  }

  createContextMenuItems() {
    this.contextMenuItems = [
      { label: 'View', icon: 'pi pi-fw pi-search', command: () => this.viewRevision(this.selectedRevision) },
      { label: 'Delete', icon: 'pi pi-fw pi-times', command: () => this.deleteRevision(this.selectedRevision) }
    ];
  }

  createFilters() {     
    this.details = [
      {
        label: 'Status',
        data: 'All',
        items: [
          { label: "Approved", data: "approved" },
          { label: "Pending", data: "pending" },
        ]
      },
      {
        label: 'Type',
        data: 'All',
        items: [
          { label: "Automatic", data: "automatic" },
          { label: "Manual", data: "manual" },
          { label: "Pull Request", data: "pullrequest" }
        ]
      }
    ];
  }

  setDetailsIcons(){
    // Set Badge Class for details Icons
    this.badgeClass.set("Pending", "");
    this.badgeClass.set("Approved", "fa-solid fa-check-double");
    this.badgeClass.set("Manual", "fa-solid fa-arrow-up-from-bracket");
    this.badgeClass.set("PullRequest", "fa-solid fa-code-pull-request");
    this.badgeClass.set("Automatic", "fa-solid fa-robot");
  }
  
  viewRevision(revision: Revision) {
      
  }

  deleteRevision(revision: Revision) {
      
  }

  /**
   * Callback to invoke on scroll /lazy load.
   * @param event the lazyload event
   */
  onLazyLoad(event: TableLazyLoadEvent) {
      console.log("On Lazy Event Emitted %o", event);
      this.first = event.first!;
      this.last = event.last!;
      if (event.last! > (this.insertIndex - this.pageSize))
      {
        if (this.pagination)
        {
          const sortField : string = event.sortField as string ?? "lastUpdated";
          const sortOrder : number = event.sortOrder as number ?? 1;
          this.loadRevisions(this.pagination!.noOfItemsRead, this.pageSize, false, event.filters, sortField, sortOrder);
        }
      }
      event.forceUpdate!();
    }

  /**
   * Callback to invoke on table filter.
   * @param event the Filter event
   */
  onFilter(event: TableFilterEvent) {
    console.log("On Filter Event Emitted %o", event);
    this.loadRevisions(0, this.pageSize, true, event.filters);
  }

  /**
   * Callback to invoke on table selection.
   * @param event the Filter event
   */
  onSelectionChange(value = []) {
    console.log("On Selection Event Emitted %o", value);
    this.showSelectionAction = (value.length > 0) ? true : false;
  }

  /**
   * Callback to invoke on column sort.
   * @param event the Filter event
   */
  onSort(event: SortEvent) {
      console.log("Sort Event Emitted %o", event);
      this.loadRevisions(0, this.pageSize, true, null, event.field, event.order);
    }
}
