import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class WorkerService {
  private apiTreeBuilder : Worker | null = null;
  private apiTreeMessages : Subject<any> = new Subject<any>();

  constructor() {
    this.startWorker();
  }

  startWorker() : Promise<void> {
    return new Promise((resolve) => {
      this.terminateWorker();
      this.apiTreeMessages = new Subject<any>();
      this.apiTreeBuilder = new Worker(new URL('../../_workers/apitree-builder.worker', import.meta.url));

      this.apiTreeBuilder.onmessage = ({ data }) => {
        this.apiTreeMessages?.next(data);
      };

      this.apiTreeBuilder.onerror = (error) => {
        console.error('An error occurred in the worker:', error);
      };

      resolve();
    });
  }

  postToApiTreeBuilder(message: any) {
    this.apiTreeBuilder?.postMessage(message);
  }

  onMessageFromApiTreeBuilder(): Observable<any> {
    return this.apiTreeMessages?.asObservable();
  }

  terminateWorker() {
    if (this.apiTreeBuilder) {
      this.apiTreeMessages.complete();
      this.apiTreeBuilder.onmessage = null;
      this.apiTreeBuilder.onerror = null;
      this.apiTreeBuilder.terminate();
      this.apiTreeBuilder = null;
    }
  }
}
