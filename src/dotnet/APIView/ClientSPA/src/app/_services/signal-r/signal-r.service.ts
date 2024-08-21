import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from '../config/config.service';
import { CommentItemModel, } from 'src/app/_models/commentItemModel';
import { Observable, Subject } from 'rxjs';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private connection : signalR.HubConnection;
  private commentUpdates: Subject<CommentUpdatesDto> = new Subject<CommentUpdatesDto>();

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

  onCommentUpdates() : Observable<CommentUpdatesDto> {
    return this.commentUpdates.asObservable();
  }

  pushCommentUpdates(commentUpdates: CommentUpdatesDto) : void {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      this.connection.invoke("PushCommentUpdates", commentUpdates);
    }
  }
}
