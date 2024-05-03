import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class WorkerService {
  private apiTreeBuilder : Worker | null = null;
  private apiTreeMessages = new Subject<any>();
  private workerTimeout : any;

  constructor() {
    this.startWorker();
  }

  startWorker() {
    this.apiTreeBuilder = new Worker(new URL('../../_workers/apitree-builder.worker', import.meta.url));

    this.apiTreeBuilder.onmessage = ({ data }) => {
      this.apiTreeMessages.next(data);
      this.resetWorkerTimeout();
    };
  }

  postToApiTreeBuilder(message: any) {
    if (!this.apiTreeBuilder) {
      this.startWorker();
    }

    this.apiTreeBuilder!.postMessage(message);
    this.resetWorkerTimeout();
  }

  onMessageFromApiTreeBuilder(): Observable<any> {
    return this.apiTreeMessages.asObservable();
  }

  private resetWorkerTimeout() {
    clearTimeout(this.workerTimeout);
    this.workerTimeout = setTimeout(() => {
      this.apiTreeBuilder!.terminate();
      this.apiTreeBuilder = null;
    }, 5000); // Terminate worker after 5 seconds of inactivity
  }
}
