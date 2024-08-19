import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from '../config/config.service';
import { CommentItemModel } from 'src/app/_models/commentItemModel';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  connection : signalR.HubConnection;

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
      console.log("Comment Created: ", comment);
    });

    this.connection.on("CommentUpdated", (reviewId: string, commentId: string, commentText: string) => {
      console.log("Comment Updated: ", reviewId, commentId, commentText);
    });

    this.connection.on("CommentResolved", (reviewId: string, elementId: string) => {
      console.log("Comment Resolved: ", reviewId, elementId);
    });

    this.connection.on("CommentUnResolved", (reviewId: string, elementId: string) => {
      console.log("Comment UnResolved: ", reviewId, elementId);
    });

    this.connection.on("CommentUpvoteToggled", (reviewId: string, commentId: string) => {
      console.log("Comment Up Voted: ", reviewId, commentId);
    });

    this.connection.on("CommentDeleted", (reviewId: string, commentId: string) => {
      console.log("Comment Deleted: ", reviewId, commentId);
    });
  }
}
