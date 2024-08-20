import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from '../config/config.service';
import { CommentItemModel, } from 'src/app/_models/commentItemModel';
import { Observable, Subject } from 'rxjs';
import { CommentThreadUpdateAction, CommentUpdateDto } from 'src/app/_dtos/commentThreadUpdateDto';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private connection : signalR.HubConnection;
  private commentUpdates: Subject<CommentUpdateDto> = new Subject<CommentUpdateDto>();

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
    this.handleCommentUpdated();
  }

  handleConnectionId() {
    this.connection.on("ReceiveConnectionId", (connectionId: string) => {
      console.log("Connected with ConnectionId: ", connectionId);
    });
  }

  handleCommentUpdated() {
    this.connection.on("CommentCreated", (comment: CommentItemModel) => {
      this.commentUpdates.next({ 
        CommentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated, comment: comment
      });
    });

    this.connection.on("CommentUpdated", (reviewId: string, commentId: string, commentText: string) => {
      this.commentUpdates.next({ 
        CommentThreadUpdateAction: CommentThreadUpdateAction.CommentTextUpdate, reviewId: reviewId,
        commentId: commentId,commentText: commentText
      });
    });

    this.connection.on("CommentResolved", (reviewId: string, elementId: string) => {
      this.commentUpdates.next({ 
        CommentThreadUpdateAction: CommentThreadUpdateAction.CommentResolved, reviewId: reviewId, elementId: elementId
      });
    });

    this.connection.on("CommentUnResolved", (reviewId: string, elementId: string) => {
      this.commentUpdates.next({ 
        CommentThreadUpdateAction: CommentThreadUpdateAction.CommentUnResolved, reviewId: reviewId, elementId: elementId
      });    
    });

    this.connection.on("CommentUpvoteToggled", (reviewId: string, commentId: string) => {
      this.commentUpdates.next({ 
        CommentThreadUpdateAction: CommentThreadUpdateAction.CommentUpVoted, reviewId: reviewId, commentId: commentId
      });
    });

    this.connection.on("CommentDeleted", (reviewId: string, commentId: string) => {
      this.commentUpdates.next({ 
        CommentThreadUpdateAction: CommentThreadUpdateAction.CommentDeleted, reviewId: reviewId, commentId: commentId
      });
    });
  }

  onCommentUpdates() : Observable<CommentUpdateDto> {
    return this.commentUpdates.asObservable();
  }
}
