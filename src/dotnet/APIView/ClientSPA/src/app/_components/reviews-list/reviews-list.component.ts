import { Component, OnInit } from '@angular/core';
import { Review } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { Pagination } from 'src/app/_models/pagination';
import { TableFilterEvent, TableLazyLoadEvent } from 'primeng/table';
import { MenuItem, SortEvent } from 'primeng/api';

@Component({
  selector: 'app-reviews-list',
  templateUrl: './reviews-list.component.html',
  styleUrls: ['./reviews-list.component.scss']
})

export class ReviewsListComponent implements OnInit {
  reviews : Review[] = [];
  totalNumberOfReviews = 0;
  pagination: Pagination | undefined;
  insertIndex = 0;

  pageSize = 20;
  first: number = 0;
  last: number  = 0;

  // Filters
  languages: any[] = [];
  selectedLanguages: any[] = [];
  details: any[] = [];
  selectedDetails: any[] = [];

  contextMenuItems! : MenuItem[];
  selectedReview!: Review;
  selectedReviews!: Review[];
  showSelectionAction : boolean = false;

  badgeClass : Map<string, string> = new Map<string, string>();


  constructor(private reviewsService: ReviewsService) { }

  ngOnInit(): void {
    this.loadReviews(0, this.pageSize * 2); // Load row 1 - 40 for starts
    this.createFilters();
    this.createContextMenuItems();
    this.setDetailsIcons();
  }

  /**
   * Load reviews from API
   *  * @param append wheather to add to or replace existing list
   */
  loadReviews(noOfItemsRead : number, pageSize: number) {
    this.reviewsService.getReviews(noOfItemsRead, pageSize).subscribe({
      next: response => {
        if (response.result && response.pagination) {
          if (this.reviews.length == 0)
          {
            this.reviews = Array.from({ length: response.pagination!.totalCount });
          }
          this.reviews.splice(this.insertIndex, this.insertIndex + response.result.length, ...response.result);
          this.insertIndex = this.insertIndex + response.result.length;
          this.pagination = response.pagination;
          this.totalNumberOfReviews = this.pagination.totalCount;
        }
      }
    });
  }

  createContextMenuItems() {
    this.contextMenuItems = [
      { label: 'View', icon: 'pi pi-fw pi-search', command: () => this.viewReview(this.selectedReview) },
      { label: 'Delete', icon: 'pi pi-fw pi-times', command: () => this.deleteReview(this.selectedReview) }
    ];
  }

  createFilters() {
    this.languages = [
        { label: "C", data: "C" },
        { label: "C#", data: "C#" },
        { label: "C++", data: "C++" },
        { label: "Go", data: "Go" },
        { label: "Java", data: "Java" },
        { label: "JavaScript", data: "JavaScript" },
        { label: "Json", data: "Json" },
        { label: "Kotlin", data: "Kotlin" },
        { label: "Python", data: "Python" },
        { label: "Swagger", data: "Swagger" },
        { label: "Swift", data: "Swift" },
        { label: "TypeSpec", data: "TypeSpec" },
        { label: "Xml", data: "Xml" }
    ];
    this.details = [
      {
        label: 'State',
        data: 'All',
        items: [
          { label: "Open", data: "open" },
          { label: "Closed", data: "closed" }
        ]
      },
      {
        label: 'Status',
        data: 'All',
        items: [
          { label: "Approved", data: "true" },
          { label: "Pending", data: "false" },
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
    this.badgeClass.set("Pending", "");
    this.badgeClass.set("Approved", "fa-solid fa-check-double");
    this.badgeClass.set("1stRelease", "fa-solid fa-check");
    this.badgeClass.set("Closed", "fa-regular fa-circle-xmark");
    this.badgeClass.set("Open", "");
    this.badgeClass.set("Manual", "fa-solid fa-arrow-up-from-bracket");
    this.badgeClass.set("PullRequest", "fa-solid fa-code-pull-request");
    this.badgeClass.set("Automatic", "fa-solid fa-robot");
  }

  viewReview(product: Review) {
      
  }

  deleteReview(product: Review) {
      
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
        this.loadReviews(this.pagination!.noOfItemsRead, this.pageSize);
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
    }
}
