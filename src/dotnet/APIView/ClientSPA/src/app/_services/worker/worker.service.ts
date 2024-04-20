import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class WorkerService {
  private apiTreeBuilder : Worker;
  private tokenBuilder : Worker;
  private interWorkerChannel : MessageChannel;
  private apiTreeMessages = new Subject<any>();
  private tokenMessages = new Subject<any>();

  constructor() {
    this.apiTreeBuilder = new Worker(new URL('../../_workers/apitree-builder.worker', import.meta.url));
    this.tokenBuilder = new Worker(new URL('../../_workers/token-builder.worker', import.meta.url));
    this.interWorkerChannel = new MessageChannel();

    this.apiTreeBuilder.postMessage({ interWorkerPort: this.interWorkerChannel.port1 }, [this.interWorkerChannel.port1]);
    this.tokenBuilder.postMessage({ interWorkerPort: this.interWorkerChannel.port2 }, [this.interWorkerChannel.port2]);

    this.apiTreeBuilder.onmessage = ({ data }) => {
      this.apiTreeMessages.next(data);
    };

    this.tokenBuilder.onmessage = ({ data }) => {
      this.tokenMessages.next(data);
    };
  }

  postToApiTreeBuilder(message: any) {
    this.apiTreeBuilder.postMessage(message);
  }

  postToTokenBuilder(message: any) {
    this.tokenBuilder.postMessage(message);
  }

  onMessageFromApiTreeBuilder(): Observable<any> {
    return this.apiTreeMessages.asObservable();
  }

  onMessageFromTokenBuilder(): Observable<any> {
    return this.tokenMessages.asObservable();
  }
}
