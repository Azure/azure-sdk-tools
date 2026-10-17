import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { Observable, map } from 'rxjs';
import { PaginatedResult } from 'src/app/_models/pagination';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { ConfigService } from '../config/config.service';
import { CrossLanguageContentDto } from 'src/app/_models/codePanelModels';

@Injectable({
  providedIn: 'root'
})
export class ReviewsService {
  baseUrl : string = this.configService.apiUrl + "reviews";
  paginatedResult: PaginatedResult<Review[]> = new PaginatedResult<Review[]>();

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

  getEnableNamespaceReview() : Observable<boolean> {
    return this.http.get<boolean>(this.baseUrl + `/enableNamespaceReview`, { withCredentials: true });
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

  toggleReviewApproval(reviewId: string, apiRevisionId: string, approve: boolean) : Observable<Review> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    return this.http.post<Review>(this.baseUrl + `/${reviewId}/${apiRevisionId}`, { approve: approve },
    {
      headers: headers,
      withCredentials: true,
    });
  }

  requestNamespaceReview(reviewId: string, activeApiRevisionId: string, notes: string = '') : Observable<Review> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });
    return this.http.post<Review>(this.baseUrl + `/${reviewId}/requestNamespaceReview/${activeApiRevisionId}`, { notes },
    {
      headers: headers,
      withCredentials: true,
    });
  }

  toggleReviewSubscriptionByUser(reviewId: string, state: boolean) {
    let params = new HttpParams();
    params = params.append('state', state.toString());

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    return this.http.post<APIRevision>(this.baseUrl + `/${reviewId}/toggleSubscribe`, {},
    {
      headers: headers,
      params: params,
      withCredentials: true
    });
  }

  getReviewContent(reviewId: string, activeApiRevisionId: string | null = null, diffApiRevisionId: string | null = null) : Observable<HttpResponse<ArrayBuffer>>{
    let params = new HttpParams();
    if (activeApiRevisionId) {
      params = params.append('activeApiRevisionId', activeApiRevisionId);
    }
    if (diffApiRevisionId) {
      params = params.append('diffApiRevisionId', diffApiRevisionId);
    }
    return this.http.get(this.baseUrl + `/${reviewId}/content`,
    {
      params: params, observe: 'response',
      responseType: 'arraybuffer', withCredentials: true
    });
  }

  getCrossLanguageContent(apiRevisionId: string, apiCodeFileId: string) : Observable<CrossLanguageContentDto> {
    let params = new HttpParams();
    params = params.append('apiRevisionId', apiRevisionId);
    params = params.append('apiCodeFileId', apiCodeFileId);
    return this.http.get<CrossLanguageContentDto>(this.baseUrl + `/crossLanguageContent`,
    {
      params: params, withCredentials: true
    });
  }

  getIsReviewByCopilotRequired(language?: string): Observable<boolean> {
    const url = `${this.baseUrl}/isReviewByCopilotRequired`;
    const params = language ? `?language=${encodeURIComponent(language)}` : '';
    return this.http.get<boolean>(`${url}${params}`, { withCredentials: true });
  }

  getIsReviewVersionReviewedByCopilot(reviewId: string, packageVersion?: string): Observable<boolean> {
    let url = `${this.baseUrl}/${reviewId}/isReviewVersionReviewedByCopilot`;
    if (packageVersion) {
      url += `?packageVersion=${encodeURIComponent(packageVersion)}`;
    }
    return this.http.get<boolean>(url, { withCredentials: true });
  }

  getReviewRevisionCount(reviewId: string): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/${reviewId}/revisionCount`, { withCredentials: true });
  }

  deleteReview(reviewId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${reviewId}`, { withCredentials: true });
  }
}
