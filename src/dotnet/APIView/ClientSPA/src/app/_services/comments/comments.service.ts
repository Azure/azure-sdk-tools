import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ConfigService } from '../config/config.service';
import { Observable } from 'rxjs';
import { CommentItemModel } from 'src/app/_models/review';

@Injectable({
  providedIn: 'root'
})
export class CommentsService {
  baseUrl : string = this.configService.apiUrl + "comments";

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getComments(reviewId: string) : Observable<CommentItemModel[]> {
    return this.http.get<CommentItemModel[]>(this.baseUrl + `/${reviewId}`, { withCredentials: true });
  }
}
