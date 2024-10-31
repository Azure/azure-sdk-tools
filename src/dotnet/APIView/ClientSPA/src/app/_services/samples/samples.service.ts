import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ConfigService } from '../config/config.service';
import { SamplesRevision } from 'src/app/_models/samples';

import { PaginatedResult } from 'src/app/_models/pagination';
import { map, Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SamplesRevisionService {
  baseUrl : string = this.configService.apiUrl + "SamplesRevisions";
  paginatedResult: PaginatedResult<SamplesRevision[]> = new PaginatedResult<SamplesRevision[]>

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getLatestSampleRevision(reviewId: string): Observable<SamplesRevision> {
    return this.http.get<SamplesRevision>(this.baseUrl + `/${reviewId}/latest`, { withCredentials: true });
  }

  getSamplesRevisions(noOfItemsRead: number, pageSize: number,
    reviewId : string, title: string | undefined = undefined, sortField: string = "createdOn", sortOrder: number = 1,
    isDeleted: boolean = false): Observable<PaginatedResult<SamplesRevision[]>> {
    let params = new HttpParams();
    params = params.append('noOfItemsRead', noOfItemsRead);
    params = params.append('pageSize', pageSize);

    const data = {
      isDeleted: isDeleted,
      title: title,
      reviewId: reviewId,
      sortField: sortField,
      sortOrder: sortOrder
    };

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })
       
    return this.http.post<SamplesRevision[]>(this.baseUrl, data,
      { 
        headers:headers,
        params: params,
        observe: 'response', 
        withCredentials: true 
      }).pipe(
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

  getSamplesContent(reviewId: string, activeSamplesRevisionId: string | null = null) : Observable<string>{
    let params = new HttpParams();
    if (activeSamplesRevisionId) {
      params = params.append('activeSamplesRevisionId', activeSamplesRevisionId);
    }
    return this.http.get<string>(this.baseUrl + `/${reviewId}/content`, { params: params,  observe: 'body', withCredentials: true });
  }

  createUsageSample(reviewId: string, formData: any) : Observable<SamplesRevision> {
    const headers = new HttpHeaders({
      'Content-Type': 'undefined',
    });

    return this.http.post<SamplesRevision>(this.baseUrl  + `/${reviewId}/create`, formData,
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

  updateUsageSample(reviewId: string, sampleRevisionId: string, formData: any) : Observable<SamplesRevision> {
    const headers = new HttpHeaders({
      'Content-Type': 'undefined',
    });

    return this.http.patch<SamplesRevision>(this.baseUrl + `/${reviewId}/update?sampleRevisionId=${sampleRevisionId}`, formData,
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

  deleteSampleRevisions(reviewId: string, revisionIds: string[]): Observable<any> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    const data = {
      reviewId: reviewId,
      samplesRevisionIds: revisionIds
    };
   
    return this.http.put<any>(this.baseUrl + '/delete', data,
    { 
      headers: headers, 
      withCredentials: true,
      observe: 'response'
    });
  }

  restoreSampleRevisions(reviewId: string, revisionIds: string[]): Observable<any> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    const data = {
      reviewId: reviewId,
      samplesRevisionIds: revisionIds
    };
   
    return this.http.put<any>(this.baseUrl + '/restore', data,
    { 
      headers: headers, 
      withCredentials: true,
      observe: 'response'
    });
  }  
}
