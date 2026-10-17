import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ConfigService } from '../config/config.service';
import { Observable, Subject } from 'rxjs';
import { CommentItemModel, CommentType, CommentSeverity } from 'src/app/_models/commentItemModel';

@Injectable({
  providedIn: 'root'
})
export class CommentsService {
  baseUrl : string = this.configService.apiUrl + "comments";

  private _qualityScoreRefreshNeeded = new Subject<void>();
  qualityScoreRefreshNeeded$ = this._qualityScoreRefreshNeeded.asObservable();

  private _severityChanged = new Subject<{ commentId: string, newSeverity: CommentSeverity }>();
  severityChanged$ = this._severityChanged.asObservable();

  notifyQualityScoreRefresh() {
    this._qualityScoreRefreshNeeded.next();
  }

  notifySeverityChanged(commentId: string, newSeverity: CommentSeverity) {
    this._severityChanged.next({ commentId, newSeverity });
  }

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getComments(reviewId: string, commentType: CommentType | undefined = undefined) : Observable<CommentItemModel[]> {
    const url = commentType ? `${this.baseUrl}/${reviewId}/?commentType=${commentType}` : `${this.baseUrl}/${reviewId}`;
    return this.http.get<CommentItemModel[]>(url, { withCredentials: true });
  }

  getConversationInfo(reviewId: string, apiRevisionId: string) {
    return this.http.get<any>(this.baseUrl + `/${reviewId}/${apiRevisionId}`, { withCredentials: true });
  }

  // Map numeric enum values to the string names expected by C# JsonStringEnumConverter
  private readonly severityNames: Record<number, string> = {
    [CommentSeverity.Question]: 'Question',
    [CommentSeverity.Suggestion]: 'Suggestion',
    [CommentSeverity.ShouldFix]: 'ShouldFix',
    [CommentSeverity.MustFix]: 'MustFix',
  };

  createComment(reviewId: string, revisionId: string, elementId: string, commentText: string, commentType: CommentType, resolutionLocked : boolean = false, severity: CommentSeverity | null = null, threadId?: string) : Observable<CommentItemModel> {
    const formData = new FormData();
    formData.append('reviewId', reviewId);
    if (commentType == CommentType.APIRevision) {
      formData.append('apiRevisionId', revisionId);
    }
    else if (commentType == CommentType.SampleRevision) {
      formData.append('sampleRevisionId', revisionId);
    }
    formData.append('elementId', elementId);
    formData.append('commentText', commentText);
    formData.append('commentType', commentType);
    formData.append('resolutionLocked', resolutionLocked.toString());
    if (severity !== null) {
      formData.append('severity', this.severityNames[severity] ?? severity.toString());
    }
    if (threadId) {
      formData.append('threadId', threadId);
    }

    return this.http.post<CommentItemModel>(this.baseUrl, formData, { withCredentials: true });

    return this.http.post<CommentItemModel>(this.baseUrl, formData, { withCredentials: true });
  }

  updateComment(reviewId: string, commentId: string, commentText: string) {
    const formData = new FormData();
    formData.append('commentText', commentText);

    return this.http.patch(this.baseUrl + `/${reviewId}/${commentId}/updateCommentText`, formData, {
      observe: 'response',
      withCredentials: true });
  }

  updateCommentSeverity(reviewId: string, commentId: string, severity: CommentSeverity) {
    const formData = new FormData();
    formData.append('severity', this.severityNames[severity] ?? severity.toString());

    return this.http.patch(this.baseUrl + `/${reviewId}/${commentId}/updateCommentSeverity`, formData, {
      observe: 'response',
      withCredentials: true });
  }

  resolveComments(reviewId: string, elementId: string, threadId?: string) {
    let params = new HttpParams();
    params = params.append('elementId', elementId);
    if (threadId) {
      params = params.append('threadId', threadId);
    }

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/resolveComments`, {}, {
      headers: headers,
      observe: 'response',
      params: params,
      withCredentials: true });
  }

  unresolveComments(reviewId: string, elementId: string, threadId?: string) {
    let params = new HttpParams();
    params = params.append('elementId', elementId);
    if (threadId) {
      params = params.append('threadId', threadId);
    }

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/unResolveComments`, {}, {
      headers: headers,
      observe: 'response',
      params: params,
      withCredentials: true });
  }

  commentsBatchOperation(reviewId: string, data: {
    commentIds: string[],
    vote?: 'none' | 'up' | 'down',
    commentReply?: string,
    disposition?: 'keepOpen' | 'resolve' | 'delete',
    severity?: CommentSeverity,
    feedback?: {
      reasons: string[],
      comment: string,
      isDelete: boolean
    }
  }) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    return this.http.patch<CommentItemModel[]>(this.baseUrl + `/${reviewId}/commentsBatchOperation`, data, {
      headers: headers,
      observe: 'response',
      withCredentials: true
    });
  }

  toggleCommentUpVote(reviewId: string, commentId: string) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/${commentId}/toggleCommentUpVote`, {}, {
      headers: headers,
      observe: 'response',
      withCredentials: true
    });
  }

  toggleCommentDownVote(reviewId: string, commentId: string) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/${commentId}/toggleCommentDownVote`, {}, {
      headers: headers,
      observe: 'response',
      withCredentials: true
    });
  }

  submitAICommentFeedback(reviewId: string, commentId: string, reasons: string[], comment: string, isDelete: boolean = false) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    const body = {
      reasons: reasons,
      comment: comment,
      isDelete: isDelete
    };

    return this.http.post(this.baseUrl + `/${reviewId}/${commentId}/feedback`, body, {
      headers: headers,
      observe: 'response',
      withCredentials: true
    });
  }

  deleteComment(reviewId: string, commentId: string) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.delete(this.baseUrl + `/${reviewId}/${commentId}`, {
      headers: headers,
      observe: 'response',
      withCredentials: true });
  }

  clearAutoGeneratedComments(apiRevisionId: string) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })
    return this.http.delete(this.baseUrl + `/${apiRevisionId}/clearAutoComments`, {
      headers: headers,
      observe: 'response',
      withCredentials: true });
  }
}
