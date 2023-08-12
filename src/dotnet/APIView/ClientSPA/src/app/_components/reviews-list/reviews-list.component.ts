import { Component, Inject, OnInit } from '@angular/core';
import { DOCUMENT } from '@angular/common'
import { FilterMatchMode, SelectItemGroup, TableState } from 'primeng/api';
import { BehaviorSubject, Observable, debounceTime, distinct, filter, fromEvent, map, merge, mergeMap, tap } from 'rxjs';
import { Review, ReviewList } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { Pagination } from 'src/app/_models/pagination';
import { TablePageEvent } from 'primeng/table';


interface SelectedFilters {
  name: string,
  value: string
}

@Component({
  selector: 'app-reviews-list',
  templateUrl: './reviews-list.component.html',
  styleUrls: ['./reviews-list.component.scss']
})

export class ReviewsListComponent implements OnInit {
  reviews : Review[] = [];
  totalNumberOfReviews = 0;
  pagination: Pagination | undefined;
  pageNumber = 1;
  pageSize = 100;
  reviewFilters!: SelectItemGroup[];
  selectedFilters!: SelectedFilters[];


  constructor(private reviewsService: ReviewsService) { }

  ngOnInit(): void {
    this.loadReviews(this.pageNumber, this.pageSize * 2);
  }

  /**
   * Load reviews from API
   *  * @param append wheather to addto or replace existing list
   */
  loadReviews(pageNumber : number, pageSize: number, append : boolean=false) {
    this.reviewsService.getReviews(pageNumber, pageSize).subscribe({
      next: response => {
        if (response.result && response.pagination) {
          if (append){
            console.log("Appending %o", response.result.length);
            this.reviews = this.reviews.concat(response.result);
          }
          else {
            this.reviews = response.result;
          }
          this.pagination = response.pagination;
          this.totalNumberOfReviews = this.pagination.totalItems;
        }
      }
    });
  }

  createFilters() {
    this.reviewFilters = [
        {
          label: 'Languages',
          value: 'language',
          items: [
            {label: 'C', value: 'c'},
            {label: 'C#', value: 'C#'},
          ]
        }
    ]
  }

  /**
   * Callback to invoke on pagination values change
   * @param event the page event
   */
  onPage(event: TablePageEvent) {
    if ((event.first > (this.reviews.length / 2) - 1) && this.pagination?.currentPage! < this.pagination?.totalPages!) {
      this.loadReviews(this.pagination?.currentPage! + 1, this.pagination?.itemsPerPage!, true);
    }
  }
}
