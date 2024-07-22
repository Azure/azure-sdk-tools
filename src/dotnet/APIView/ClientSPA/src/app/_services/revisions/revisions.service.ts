import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';


import { PaginatedResult } from 'src/app/_models/pagination';
import { APIRevision, ParserStyle } from 'src/app/_models/revision';
import { ConfigService } from '../config/config.service';


@Injectable({
  providedIn: 'root'
})
export class RevisionsService {
  baseUrl : string = this.configService.apiUrl + "APIRevisions";
  paginatedResult: PaginatedResult<APIRevision[]> = new PaginatedResult<APIRevision[]>
  
  constructor(private http: HttpClient, private configService: ConfigService) { }

  getAPIRevisions(noOfItemsRead: number, pageSize: number,
    reviewId : string, label: string | undefined = undefined, author: string | undefined = undefined, 
    details: string [] = [], sortField: string = "lastUpdatedOn", sortOrder: number = 1, isDeleted: boolean = false,
    isAssignedToMe: boolean = false, withTreeStyleTokens: boolean = false
    ): Observable<PaginatedResult<APIRevision[]>> {
    let params = new HttpParams();
    params = params.append('noOfItemsRead', noOfItemsRead);
    params = params.append('pageSize', pageSize);

    const data = {
      isDeleted: isDeleted,
      assignedToMe: isAssignedToMe,
      withTreeStyleTokens: withTreeStyleTokens,
      label: label,
      author: author,
      reviewId: reviewId,
      details: details,
      sortField: sortField,
      sortOrder: sortOrder
    };

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })
       
    return this.http.post<APIRevision[]>(this.baseUrl, data,
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

  deleteAPIRevisions(reviewId: string, revisionIds: string[]): Observable<any> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    const data = {
      reviewId: reviewId,
      apiRevisionIds: revisionIds
    };
   
    return this.http.put<any>(this.baseUrl + '/delete', data,
    { 
      headers: headers, 
      withCredentials: true,
      observe: 'response'
    });
  }

  restoreAPIRevisions(reviewId: string, revisionIds: string[]): Observable<any> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    const data = {
      reviewId: reviewId,
      apiRevisionIds: revisionIds
    };
   
    return this.http.put<any>(this.baseUrl + '/restore', data,
    { 
      headers: headers, 
      withCredentials: true,
      observe: 'response'
    });
  }

  toggleAPIRevisionViewedByForUser(apiRevisionId: string, state: boolean) : Observable<APIRevision> {
    let params = new HttpParams();
    params = params.append('state', state.toString());

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });
   
    return this.http.post<APIRevision>(this.baseUrl + `/${apiRevisionId}/toggleViewedBy`, {},
    { 
      headers: headers,
      params: params,
      withCredentials: true
    });
  }

  toggleAPIRevisionApproval(reviewId: string, apiRevisionId: string) : Observable<APIRevision> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });
    return this.http.post<APIRevision>(this.baseUrl + `/${reviewId}/${apiRevisionId}`, {},
    { 
      headers: headers,
      withCredentials: true,
    });
  }

  openDiffOfAPIRevisions(activeAPIRevision: APIRevision, diffAPIRevision: APIRevision, currentRoute: string) {
    const target = (currentRoute.includes("review")) ? '_self' : '_blank';

    if (activeAPIRevision.files[0].parserStyle === "tree") {
      window.open(`/review/${activeAPIRevision.reviewId}?activeApiRevisionId=${activeAPIRevision.id}&diffApiRevisionId=${diffAPIRevision.id}`, target);
    } else {
      window.open(this.configService.webAppUrl + `Assemblies/Review/${activeAPIRevision.reviewId}?revisionId=${activeAPIRevision.id}&diffOnly=False&doc=False&diffRevisionId=${diffAPIRevision.id}`, target);
    }
  }

  openAPIRevisionPage(apiRevision: APIRevision, currentRoute: string) {
    const target = (currentRoute.includes("review")) ? '_self' : '_blank';

    if (apiRevision.files[0].parserStyle === "tree") {
      window.open(`/review/${apiRevision.reviewId}?activeApiRevisionId=${apiRevision.id}`, target);
    } else {
      window.open(this.configService.webAppUrl + `Assemblies/Review/${apiRevision.reviewId}?revisionId=${apiRevision.id}`, target);
    }
  }

  updateSelectedReviewers(reviewId: string, apiRevisionId: string, reviewers: Set<string>): Observable<APIRevision> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    const reviewersArray = Array.from(reviewers);

    return this.http.post<APIRevision>(`${this.baseUrl}/${reviewId}/${apiRevisionId}/reviewers`, reviewersArray, {
      headers: headers,
      withCredentials: true,
    });
  }
}
