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
    let params = new HttpParams();
    params = params.append('reviewId', reviewId);
    if (commentType == CommentType.APIRevision) {
      params = params.append('apiRevisionId', revisionId);
    }
    else if (commentType == CommentType.SampleRevision) {
      params = params.append('sampleRevisionId', revisionId);
    }
    params = params.append('elementId', elementId);
    params = params.append('commentText', commentText);
    params = params.append('commentType', commentType);
    params = params.append('resolutionLocked', resolutionLocked);

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.post<CommentItemModel>(this.baseUrl, {}, { 
      headers: headers,
      params: params,
      withCredentials: true });
  }

  updateComment(reviewId: string, commentId: string, commentText: string) {
    let params = new HttpParams();
    params = params.append('commentText', commentText);

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.patch(this.baseUrl + `/${reviewId}/${commentId}/updateCommentText`, {}, { 
      headers: headers,
      observe: 'response',
      params: params,
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
