import { Component, OnInit } from '@angular/core';
import { Review } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { Pagination } from 'src/app/_models/pagination';
import { TableLazyLoadEvent } from 'primeng/table';

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

  badgeClass : Map<string, string> = new Map<string, string>();


  constructor(private reviewsService: ReviewsService) { }

  ngOnInit(): void {
    this.loadReviews(0, this.pageSize * 2); // Load row 1 - 40 for starts
    this.createFilters();

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
        value: 'All',
        items: [
          { label: "Open", value: "open" },
          { label: "Closed", value: "closed" }
        ]
      },
      {
        label: 'Status',
        value: 'All',
        items: [
          { label: "Approved", value: "true" },
          { label: "Pending", value: "false" },
        ]
      },
      {
        label: 'Type',
        value: 'All',
        items: [
          { label: "Automatic", value: "0" },
          { label: "Manual", value: "1" },
          { label: "Pull Request", value: "2" }
        ]
      }
    ];
  }


  /**
   * Callback to invoke on scroll /lazy load.
   * @param event the lazyload event
   */
  onLazyLoad(event: TableLazyLoadEvent) {
    if (event.last! > (this.insertIndex - this.pageSize))
    {
      if (this.pagination)
      {
        this.loadReviews(this.pagination!.noOfItemsRead, this.pageSize);
      }
    }
    event.forceUpdate!();
  }
}
