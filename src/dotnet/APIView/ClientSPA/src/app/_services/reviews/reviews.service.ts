import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { Observable } from 'rxjs';
import { ReviewList } from 'src/app/_models/review';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ReviewsService {
  baseUrl : string = environment.apiUrl + "reviews";

  constructor(private http: HttpClient) { }

  getReviews(offset: number, limit: number): Observable<ReviewList> {
    let url : string = this.baseUrl + `?offset=${offset}&limit=${limit}`;
    return this.http.get<ReviewList>(url, { withCredentials: true } );
  }
}
