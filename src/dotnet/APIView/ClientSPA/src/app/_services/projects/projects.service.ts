import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ConfigService } from '../config/config.service';
import { Project, RelatedReviewsResponse } from 'src/app/_models/projectModel';
import { ProjectNamespaceInfo, NamespaceDecisionStatus } from 'src/app/_models/namespaceModel';

@Injectable({
  providedIn: 'root'
})
export class ProjectsService {
  baseUrl: string = this.configService.apiUrl + 'projects';

  constructor(private http: HttpClient, private configService: ConfigService) {
  }

  getProject(projectId: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/${projectId}`, { withCredentials: true });
  }

  getRelatedReviews(reviewId: string): Observable<RelatedReviewsResponse> {
    const url = `${this.baseUrl}/reviews/${reviewId}/related`;
    return this.http.get<RelatedReviewsResponse>(this.baseUrl + `/reviews/${reviewId}/related`, { withCredentials: true });
    
  }

  getProjectNamespaces(projectId: string): Observable<ProjectNamespaceInfo> {
    const url = `${this.baseUrl}/${projectId}/namespaces`;
    return this.http.get<ProjectNamespaceInfo>(url, { withCredentials: true });
  }

  updateNamespaceStatus(projectId: string, language: string, status: NamespaceDecisionStatus, notes?: string): Observable<Project> {
    return this.http.patch<Project>(
      `${this.baseUrl}/${projectId}/namespaces/${encodeURIComponent(language)}`,
      { status, notes },
      { withCredentials: true }
    );
  }
}
