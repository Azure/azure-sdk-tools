import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from '../config/config.service';
import { Observable, Subject } from 'rxjs';
import { CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { CommentItemModel } from 'src/app/_models/commentItemModel';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private connection : signalR.HubConnection;
  private commentUpdates: Subject<CommentUpdatesDto> = new Subject<CommentUpdatesDto>();
  private aiCommentUpdates: Subject<CommentItemModel> = new Subject<CommentItemModel>();
  private reviewUpdates: Subject<Review> = new Subject<Review>();
  private apiRevisionUpdates: Subject<APIRevision> = new Subject<APIRevision>();

  constructor(private configService: ConfigService) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${ this.configService.hubUrl }notification`, 
        { 
          withCredentials: true
        })
      .configureLogging(signalR.LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    this.setupConnectionHandlers();
    this.startConnection();
  }

  startConnection = () => {
    this.connection.start().then(() => {
      console.log('Connection started');
    }).catch(err => console.log('Error while starting connection: ' + err));
  }

  setupConnectionHandlers() {
    this.connection.onclose(async () => {
      await this.startConnection();
    })
    this.handleConnectionId();
    this.handleCommentUpdates();
    this.handleAICommentUpdates();
    this.handleReviewUpdates();
    this.handleAPIRevisionUpdates();
  }

  handleConnectionId() {
    this.connection.on("ReceiveConnectionId", (connectionId: string) => {
      console.log("Connected with ConnectionId: ", connectionId);
    });
  }

  handleCommentUpdates() {
    this.connection.on("ReceiveCommentUpdates", (commentUpdates: CommentUpdatesDto) => {
      this.commentUpdates.next(commentUpdates);
    });
  }

  handleAICommentUpdates() {
    this.connection.on("ReceiveAICommentUpdates", (comments: CommentItemModel) => {
      this.aiCommentUpdates.next(comments);
    });
  }

  handleReviewUpdates() {
    this.connection.on("ReviewUpdated", (updatedReview: Review) => {
      this.reviewUpdates.next(updatedReview);
    });
  }

  handleAPIRevisionUpdates() {
    this.connection.on("APIRevisionUpdated", (updatedAPIRevision: APIRevision) => {
      this.apiRevisionUpdates.next(updatedAPIRevision);
    });
  }

  onCommentUpdates() : Observable<CommentUpdatesDto> {
    return this.commentUpdates.asObservable();
  }

  onAICommentUpdates() : Observable<CommentItemModel> {
    return this.aiCommentUpdates.asObservable();
  }

  onReviewUpdates() : Observable<Review> {
    return this.reviewUpdates.asObservable();
  }

  onAPIRevisionUpdates() : Observable<APIRevision> {
    return this.apiRevisionUpdates.asObservable();
  }

  pushCommentUpdates(commentUpdates: CommentUpdatesDto) : void {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      this.connection.invoke("PushCommentUpdates", commentUpdates);
    }
  }
}
