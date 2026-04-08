import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService } from '../config/config.service';
import { CrossLanguageProposalModel } from 'src/app/_models/crossLanguageProposalModel';

@Injectable({
  providedIn: 'root'
})
export class ProposalsService {
  baseUrl: string = this.configService.apiUrl + 'proposals';

  constructor(private http: HttpClient, private configService: ConfigService) {}

  getProposalsByReview(reviewId: string): Observable<CrossLanguageProposalModel[]> {
    return this.http.get<CrossLanguageProposalModel[]>(`${this.baseUrl}/byReview/${reviewId}`, { withCredentials: true });
  }

  getProposalsByCrossLanguageId(crossLanguageId: string): Observable<CrossLanguageProposalModel[]> {
    return this.http.get<CrossLanguageProposalModel[]>(`${this.baseUrl}/byCrossLanguageId?crossLanguageId=${encodeURIComponent(crossLanguageId)}`, { withCredentials: true });
  }

  createProposal(reviewId: string, elementId: string, crossLanguageId: string, proposalText: string, threadId?: string, description?: string): Observable<CrossLanguageProposalModel> {
    const formData = new FormData();
    formData.append('reviewId', reviewId);
    formData.append('elementId', elementId);
    formData.append('crossLanguageId', crossLanguageId);
    formData.append('proposalText', proposalText);
    if (threadId) {
      formData.append('threadId', threadId);
    }
    if (description) {
      formData.append('description', description);
    }
    return this.http.post<CrossLanguageProposalModel>(this.baseUrl, formData, { withCredentials: true });
  }

  voteOnProposal(reviewId: string, proposalId: string, language: string, decision: string, modificationText?: string): Observable<CrossLanguageProposalModel> {
    const formData = new FormData();
    formData.append('language', language);
    formData.append('decision', decision);
    if (modificationText) {
      formData.append('modificationText', modificationText);
    }
    return this.http.patch<CrossLanguageProposalModel>(`${this.baseUrl}/${reviewId}/${proposalId}/vote`, formData, { withCredentials: true });
  }

  addComment(reviewId: string, proposalId: string, commentText: string): Observable<CrossLanguageProposalModel> {
    const formData = new FormData();
    formData.append('commentText', commentText);
    return this.http.post<CrossLanguageProposalModel>(`${this.baseUrl}/${reviewId}/${proposalId}/comments`, formData, { withCredentials: true });
  }

  deleteProposal(reviewId: string, proposalId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${reviewId}/${proposalId}`, { withCredentials: true });
  }

  supersedeProposal(reviewId: string, proposalId: string, reason: string): Observable<CrossLanguageProposalModel> {
    const formData = new FormData();
    formData.append('reason', reason);
    return this.http.patch<CrossLanguageProposalModel>(`${this.baseUrl}/${reviewId}/${proposalId}/supersede`, formData, { withCredentials: true });
  }
}
