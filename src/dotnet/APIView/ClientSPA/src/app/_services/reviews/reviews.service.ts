import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { Observable, map } from 'rxjs';
import { PaginatedResult } from 'src/app/_models/pagination';
import { Review, ReviewList } from 'src/app/_models/review';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ReviewsService {
  baseUrl : string = environment.apiUrl + "reviews";
  paginatedResult: PaginatedResult<Review[]> = new PaginatedResult<Review[]>

  constructor(private http: HttpClient) { }

  getReviews(page: number, itemsPerPage: number): Observable<PaginatedResult<Review[]>> {
    let params = new HttpParams();
    if (page && itemsPerPage) {
      params = params.append('pageNumber', page);
      params = params.append('pageSize', itemsPerPage);
    }

    return this.http.get<ReviewList>(this.baseUrl,
      { 
        params: params,
        observe: 'response', 
        withCredentials: true 
      } ).pipe(
          map((response : any) => {
            if (response.body) {
              this.paginatedResult.result = response.body;
            }
            const pagination = response.headers.get('Pagination');
            if (pagination){
              this.paginatedResult.pagination = JSON.parse(pagination);
            }
            return this.paginatedResult;
          }
        )
      );
  }
}
