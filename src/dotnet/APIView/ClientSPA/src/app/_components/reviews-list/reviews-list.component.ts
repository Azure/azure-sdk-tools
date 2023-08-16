import { Component, Inject, OnInit } from '@angular/core';
import { Review } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { Pagination } from 'src/app/_models/pagination';
import { TableFilterEvent, TableLazyLoadEvent, TablePageEvent } from 'primeng/table';
import { ScrollerOptions, TreeNode } from 'primeng/api';

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
  state: any[] = [];
  selectedState: any[] = [];
  type: any[] = [];
  selectedType: any[] = [];
  status: any[] = [];
  selectedStatus: any[] = [];

  constructor(private reviewsService: ReviewsService) { }

  ngOnInit(): void {
    this.loadReviews(0, this.pageSize * 2); // Load row 1 - 40 for starts
    this.createFilters();
  }

  /**
   * Load reviews from API
   *  * @param append wheather to add to or replace existing list
   */
  loadReviews(noOfItemsRead : number, pageSize: number) {
    console.log(`NoOfItemsRead: ${noOfItemsRead} , pageSize: ${pageSize}`)
    this.reviewsService.getReviews(noOfItemsRead, pageSize).subscribe({
      next: response => {
        if (response.result && response.pagination) {
          if (this.reviews.length == 0)
          {
            this.reviews = Array.from({ length: response.pagination!.totalCount });
            console.log(`Array Size Set to:  ${this.reviews.length}`)
          }
          console.log(`Array of length:  ${response.result.length} loaded`)
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
    this.state = [
      { label: "Open", data: "open" },
      { label: "Closed", data: "closed" }
    ];
    this.status = [
      { label: "Approved", data: "true" },
      { label: "Pending", data: "false" },
    ];
    this.type = [
      { label: "Automatic", data: "0" },
      { label: "Manual", data: "1" },
      { label: "Pull Request", data: "2" }
    ];
  }


  /**
   * Callback to invoke on pagination values change
   * @param event the page event
  // */
  //onPage(event: TablePageEvent) {
  //  if ((event.first > (this.reviews.length / 2) - 1) && this.pagination?.currentPage! < this.pagination?.totalPages!) {
  //    this.loadReviews(this.pagination?.currentPage! + 1, this.pagination?.itemsPerPage!);
  //  }
  //}

  onLazyLoad(event: TableLazyLoadEvent) {
    console.log("Lazy load event %o", event);
    if (event.last! > (this.insertIndex - this.pageSize))
    {
      this.loadReviews(this.pagination!.noOfItemsRead, this.pageSize);
    }
    event.forceUpdate!();
  }
}
