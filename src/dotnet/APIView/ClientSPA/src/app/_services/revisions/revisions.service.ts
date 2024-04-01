import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';


import { PaginatedResult } from 'src/app/_models/pagination';
import { APIRevision } from 'src/app/_models/revision';
import { environment } from 'src/environments/environment';
import { ConfigService } from '../config/config.service';


@Injectable({
  providedIn: 'root'
})
export class RevisionsService {
  baseUrl : string = this.configService.apiUrl + "APIRevisions";
  paginatedResult: PaginatedResult<APIRevision[]> = new PaginatedResult<APIRevision[]>
  
  constructor(private http: HttpClient, private configService: ConfigService) { }

  getAPIRevisions(noOfItemsRead: number, pageSize: number,
    reviewId : string, label: string, author: string, 
    details: string [], sortField: string, sortOrder: number, isDeleted: boolean = false, isAssignedToMe: boolean = false
    ): Observable<PaginatedResult<APIRevision[]>> {
    let params = new HttpParams();
    params = params.append('noOfItemsRead', noOfItemsRead);
    params = params.append('pageSize', pageSize);

    const data = {
      isDeleted: isDeleted,
      assignedToMe: isAssignedToMe,
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

  openDiffOfAPIRevisions(reviewId: string, activeAPIRevisionId: string, diffAPIRevisionsId: string) {
    window.open(this.configService.webAppUrl + `Assemblies/Review/${reviewId}?revisionId=${activeAPIRevisionId}&diffOnly=False&doc=False&diffRevisionId=${diffAPIRevisionsId}`, '_blank');
  }

  openAPIRevisionPage(reviewId: string, activeAPIRevisionId: string) {
    window.open(this.configService.webAppUrl + `Assemblies/Review/${reviewId}?revisionId=${activeAPIRevisionId}`, '_blank');
  }
}
