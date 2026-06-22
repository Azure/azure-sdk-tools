import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService } from '../config/config.service';

export interface ReportIssueRequest {
  description: string;
  reviewLink?: string | null;
  language?: string | null;
  commentId?: string | null;
}

export interface ReportIssueResponse {
  issueUrl: string;
  issueNumber: number;
}

@Injectable({
  providedIn: 'root'
})
export class AgentService {
  baseUrl: string = this.configService.apiUrl + 'agent';

  constructor(private http: HttpClient, private configService: ConfigService) { }

  reportIssue(request: ReportIssueRequest): Observable<ReportIssueResponse> {
    return this.http.post<ReportIssueResponse>(
      `${this.baseUrl}/report-issue`,
      request,
      { withCredentials: true }
    );
  }
}
