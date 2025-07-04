import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from '../config/config.service';
import { Observable, Subject } from 'rxjs';
import { CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { AIReviewJobCompletedDto } from 'src/app/_dtos/aiReviewJobCompletedDto';
import { SiteNotificationDto } from 'src/app/_dtos/siteNotificationDto';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private connection : signalR.HubConnection;
  private commentUpdates: Subject<CommentUpdatesDto> = new Subject<CommentUpdatesDto>();
  private aiReviewUpdates: Subject<AIReviewJobCompletedDto> = new Subject<AIReviewJobCompletedDto>();
  private reviewUpdates: Subject<Review> = new Subject<Review>();
  private apiRevisionUpdates: Subject<APIRevision> = new Subject<APIRevision>();
  private siteNotifications: Subject<SiteNotificationDto> = new Subject<SiteNotificationDto>();

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
    this.handleAIReviewUpdates();
    this.handleReviewUpdates();
    this.handleAPIRevisionUpdates();
    this.handleSiteNotification();
  }

  handleConnectionId() {
    this.connection.on("ReceiveConnectionId", (connectionId: string) => {
    });
  }

  handleCommentUpdates() {
    this.connection.on("ReceiveCommentUpdates", (commentUpdates: CommentUpdatesDto) => {
      this.commentUpdates.next(commentUpdates);
    });
  }

  handleSiteNotification() {
    this.connection.on("ReceiveNotification", (siteNotification: SiteNotificationDto) => {
      this.siteNotifications.next(siteNotification);
    });
  }

  handleAIReviewUpdates() {
    this.connection.on("ReceiveAIReviewUpdates", (aiReviewUpdates: AIReviewJobCompletedDto) => {
      this.aiReviewUpdates.next(aiReviewUpdates);
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

  onAIReviewUpdates() : Observable<AIReviewJobCompletedDto> {
    return this.aiReviewUpdates.asObservable();
  }

  onNotificationUpdates() : Observable<SiteNotificationDto> {
    return this.siteNotifications.asObservable();
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
