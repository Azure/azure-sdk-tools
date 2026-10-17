import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService } from '../config/config.service';
import { PullRequestModel } from 'src/app/_models/pullRequestModel';

@Injectable({
  providedIn: 'root'
})
export class PullRequestsService {
  baseUrl : string = this.configService.apiUrl + "pullrequests";

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getAssociatedPullRequests(reviewId: string, apiRevisionId : string) : Observable<PullRequestModel[]> {
    return this.http.get<PullRequestModel[]>(this.baseUrl + `/${reviewId}/${apiRevisionId}`, { withCredentials: true });
  }

  getPullRequestsOfAssociatedAPIRevisions(reviewId: string, apiRevisionId : string) : Observable<PullRequestModel[]> {
    return this.http.get<PullRequestModel[]>(this.baseUrl + `/${reviewId}/${apiRevisionId}/prsofassociatedapirevisions`, { withCredentials: true });
  }
}
