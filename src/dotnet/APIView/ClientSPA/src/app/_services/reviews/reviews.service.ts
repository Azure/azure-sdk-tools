import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { Observable, map } from 'rxjs';
import { PaginatedResult } from 'src/app/_models/pagination';
import { Review } from 'src/app/_models/review';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ReviewsService {
  baseUrl : string = environment.apiUrl + "reviews";
  paginatedResult: PaginatedResult<Review[]> = new PaginatedResult<Review[]>

  constructor(private http: HttpClient) { }

  getReviews(noOfItemsRead: number, pageSize: number,
    name: string, author: string, languages: string [], details: string []
    ): Observable<PaginatedResult<Review[]>> {
    let params = new HttpParams();
    params = params.append('noOfItemsRead', noOfItemsRead);
    params = params.append('pageSize', pageSize);

    const data = {
      name: name,
      author: author,
      languages: languages,
      details: details
    };

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })
   

    console.log("getReviews: " + JSON.stringify(data));
    
    return this.http.post<Review[]>(this.baseUrl, data,
      { 
        headers:headers,
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
