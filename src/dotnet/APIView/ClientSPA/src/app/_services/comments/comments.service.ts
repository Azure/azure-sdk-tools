import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ConfigService } from '../config/config.service';
import { Observable } from 'rxjs';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';

@Injectable({
  providedIn: 'root'
})
export class CommentsService {
  baseUrl : string = this.configService.apiUrl + "comments";

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getComments(reviewId: string, commentType: CommentType | undefined = undefined) : Observable<CommentItemModel[]> {
    const url = commentType ? `${this.baseUrl}/${reviewId}/?commentType=${commentType}` : `${this.baseUrl}/${reviewId}`;
    return this.http.get<CommentItemModel[]>(url, { withCredentials: true });
  }

  getConversationInfo(reviewId: string, apiRevisionId: string) {
    return this.http.get<any>(this.baseUrl + `/${reviewId}/${apiRevisionId}`, { withCredentials: true });
  }

  createComment(reviewId: string, revisionId: string, elementId: string, commentText: string, commentType: CommentType, resolutionLocked : boolean = false) : Observable<CommentItemModel> {
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
    formData.append('commentType', commentType.toString());
    formData.append('resolutionLocked', resolutionLocked.toString());

    return this.http.post<CommentItemModel>(this.baseUrl, formData, { withCredentials: true });
  }

  updateComment(reviewId: string, commentId: string, commentText: string) {
    const formData = new FormData();
    formData.append('commentText', commentText);

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/${commentId}/updateCommentText`, formData, { 
      headers: headers,
      observe: 'response',
      withCredentials: true });
  }

  resolveComments(reviewId: string, elementId: string) {
    let params = new HttpParams();
    params = params.append('elementId', elementId);

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/resolveComments`, {}, { 
      headers: headers,
      observe: 'response',
      params: params,
      withCredentials: true });
  }

  unresolveComments(reviewId: string, elementId: string) {
    let params = new HttpParams();
    params = params.append('elementId', elementId);

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/unResolveComments`, {}, { 
      headers: headers,
      observe: 'response',
      params: params,
      withCredentials: true });
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

  deleteComment(reviewId: string, commentId: string) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.delete(this.baseUrl + `/${reviewId}/${commentId}`, { 
      headers: headers,
      observe: 'response',
      withCredentials: true });
  }
}
