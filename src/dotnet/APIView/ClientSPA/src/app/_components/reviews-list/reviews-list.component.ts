import { Component, OnInit } from '@angular/core';
import { Review } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { Pagination } from 'src/app/_models/pagination';
import { TableFilterEvent, TableLazyLoadEvent } from 'primeng/table';
import { MenuItem, SortEvent } from 'primeng/api';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-reviews-list',
  templateUrl: './reviews-list.component.html',
  styleUrls: ['./reviews-list.component.scss']
})

export class ReviewsListComponent implements OnInit {
  reviewPageWebAppUrl : string = environment.webAppUrl + "Assemblies/review/";
  profilePageWebAppUrl : string = environment.webAppUrl + "Assemblies/profile/";
  reviews : Review[] = [];
  totalNumberOfReviews = 0;
  pagination: Pagination | undefined;
  insertIndex = 0;

  pageSize = 20;
  first: number = 0;
  last: number  = 0;

  sidebarVisible : boolean = false;

  // Filters
  languages: any[] = [];
  selectedLanguages: any[] = [];
  details: any[] = [];
  selectedDetails: any[] = [];

  // Context Menu
  contextMenuItems! : MenuItem[];
  selectedReview!: Review;
  selectedReviews!: Review[];
  showSelectionAction : boolean = false;

  // Create Review Selections
  crLanguages: any[] = [];
  selectedCRLanguages: any[] = [];

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
  loadReviews(noOfItemsRead : number, pageSize: number, filters: any = null) {
    let name : string = "";
    let author : string = "";
    let languages : string [] = [];
    let details : string [] = [];
    if (filters)
    {
      name = filters.name.value ?? name;
      author = filters.author.value ?? author;
      languages = (filters.languages.value != null)? filters.languages.value.map((item: any) => item.data) : languages;
      details = (filters.details.value != null) ? filters.details.value.map((item: any) => item.data): details;
    }

    this.reviewsService.getReviews(noOfItemsRead, pageSize, name, author, languages, details).subscribe({
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
    this.languages = this.crLanguages = [
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
        this.loadReviews(this.pagination!.noOfItemsRead, this.pageSize, event.filters);
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
