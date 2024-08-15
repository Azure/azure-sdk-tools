import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from '../config/config.service';

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
  }

  handleConnectionId() {
    this.connection.on("ReceiveConnectionId", (connectionId: string) => {
      console.log("Connected with ConnectiuonId: ", connectionId);
    });
  }
}
