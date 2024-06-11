import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ConfigService } from '../config/config.service';
import { Observable } from 'rxjs';
import { CommentItemModel, CommentType } from 'src/app/_models/review';

@Injectable({
  providedIn: 'root'
})
export class CommentsService {
  baseUrl : string = this.configService.apiUrl + "comments";

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getComments(reviewId: string) : Observable<CommentItemModel[]> {
    return this.http.get<CommentItemModel[]>(this.baseUrl + `/${reviewId}`, { withCredentials: true });
  }

  createComment(reviewId: string, apiRevisionId: string, elementId: string, commentText: string, commentType: CommentType, resolutionLocked : boolean = false) {
    const data = {
      reviewId: reviewId,
      apiRevisionId: apiRevisionId,
      elementId: elementId,
      commentText: commentText,
      commentType: commentType,
      resolutionLocked: resolutionLocked
    };

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    })

    return this.http.post(this.baseUrl, data, { headers: headers, withCredentials: true });
  }
}
