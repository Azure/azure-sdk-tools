import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class WorkerService {
  private apiTreeBuilder : Worker;
  private tokenBuilder : Worker;

  constructor() {
    this.apiTreeBuilder = new Worker(new URL('../../_workers/apitree-builder.worker', import.meta.url));
    this.tokenBuilder = new Worker(new URL('../../_workers/token-builder.worker', import.meta.url));
  }

  postToApiTreeBuilder(message: any) {
    this.apiTreeBuilder.postMessage(message);
  }

  postToTokenBuilder(message: any) {
    this.tokenBuilder.postMessage(message);
  }

  onMessageFromApiTreeBuilder(): Observable<any> {
    return new Observable(observer => {
      this.apiTreeBuilder.onmessage = ({ data }) => {
        observer.next(data);
      };
    });
  }

  onMessageFromTokenBuilder(): Observable<any> {
    return new Observable(observer => {
      this.tokenBuilder.onmessage = ({ data }) => {
        observer.next(data);
      };
    });
  }
}
