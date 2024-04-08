import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ConfigService } from '../config/config.service';

@Injectable({
  providedIn: 'root'
})
export class CommentsService {
  baseUrl : string = this.configService.apiUrl + "comments";

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getComments(reviewId: string) {
    return this.http.get(this.baseUrl + `/${reviewId}`);
  }
}
