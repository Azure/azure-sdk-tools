import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { Observable, map } from 'rxjs';
import { PaginatedResult } from 'src/app/_models/pagination';
import { Review } from 'src/app/_models/review';
import { APIRevision, CodePanelData } from 'src/app/_models/revision';
import { ConfigService } from '../config/config.service';

@Injectable({
  providedIn: 'root'
})
export class ReviewsService {
  baseUrl : string = this.configService.apiUrl + "reviews";
  paginatedResult: PaginatedResult<Review[]> = new PaginatedResult<Review[]>

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getReviews(noOfItemsRead: number, pageSize: number,
    name: string, languages: string [], approval: string,
    sortField: string, sortOrder: number,
    ): Observable<PaginatedResult<Review[]>> {
    let params = new HttpParams();
    params = params.append('noOfItemsRead', noOfItemsRead);
    params = params.append('pageSize', pageSize);
    if (name) {
      params = params.append('name', name);
    }
    if (languages && languages.length > 0) {
      languages.forEach(language => {
        params = params.append('languages', language);
      });
    }

    if (approval == "Approved" || approval == "Pending") {
      params = (approval == "Approved") ? params.append('isApproved', true) : params.append('isApproved', false);
    }
      
    params = params.append('sortField', sortField);
    params = params.append('sortOrder', sortOrder);

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })
      
    return this.http.get<Review[]>(this.baseUrl,
      { 
        headers: headers,
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

  getReview(reviewId: string) : Observable<Review> {
    return this.http.get<Review>(this.baseUrl + `/${reviewId}`, { withCredentials: true });
  }

  openReviewPage(reviewId: string) {
    window.open(this.configService.webAppUrl + `Assemblies/Review/${reviewId}`, '_blank');
  }

  createReview(formData: any) : Observable<APIRevision> {
    const headers = new HttpHeaders({
      'Content-Type': 'undefined',
    })

    return this.http.post<Review>(this.baseUrl, formData,
      { 
        observe: 'response', 
        withCredentials: true 
      }).pipe(
        map((response : any) => {
          if (response.body) {
            return response.body;
          }
        }
      )
    );
  }

  getReviewContent(reviewId: string, activeApiRevisionId: string | null = null, diffApiRevisionId: string | null = null) : Observable<ArrayBuffer>{
    let params = new HttpParams();
    if (activeApiRevisionId) {
      params = params.append('activeApiRevisionId', activeApiRevisionId);
    }
    if (diffApiRevisionId) {
      params = params.append('diffApiRevisionId', diffApiRevisionId);
    }
    return this.http.get(this.baseUrl + `/${reviewId}/content`, 
    { params: params, responseType: 'arraybuffer', withCredentials: true });
  }}
