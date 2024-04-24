import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class WorkerService {
  private apiTreeBuilder : Worker;
  private apiTreeMessages = new Subject<any>();

  constructor() {
    this.apiTreeBuilder = new Worker(new URL('../../_workers/apitree-builder.worker', import.meta.url));

    this.apiTreeBuilder.onmessage = ({ data }) => {
      this.apiTreeMessages.next(data);
    };
  }

  postToApiTreeBuilder(message: any) {
    this.apiTreeBuilder.postMessage(message);
  }

  onMessageFromApiTreeBuilder(): Observable<any> {
    return this.apiTreeMessages.asObservable();
  }
}
