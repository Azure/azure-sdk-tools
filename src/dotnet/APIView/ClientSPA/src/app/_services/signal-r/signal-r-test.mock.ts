import { Observable, Subject, of } from 'rxjs';
import { CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { AIReviewJobCompletedDto } from 'src/app/_dtos/aiReviewJobCompletedDto';
import { SiteNotificationDto } from 'src/app/_dtos/siteNotificationDto';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';

export class SignalRServiceMock {
  private commentUpdates: Subject<CommentUpdatesDto> = new Subject<CommentUpdatesDto>();
  private aiReviewUpdates: Subject<AIReviewJobCompletedDto> = new Subject<AIReviewJobCompletedDto>();
  private siteNotifications: Subject<SiteNotificationDto> = new Subject<SiteNotificationDto>();
  private reviewUpdates: Subject<Review> = new Subject<Review>();
  private apiRevisionUpdates: Subject<APIRevision> = new Subject<APIRevision>();

  constructor() {}

  onCommentUpdates(): Observable<CommentUpdatesDto> {
    return this.commentUpdates.asObservable();
  }

  onAIReviewUpdates(): Observable<AIReviewJobCompletedDto> {
    return this.aiReviewUpdates.asObservable();
  }

  onNotificationUpdates(): Observable<SiteNotificationDto> {
    return this.siteNotifications.asObservable();
  }

  onReviewUpdates(): Observable<Review> {
    return this.reviewUpdates.asObservable();
  }

  onAPIRevisionUpdates(): Observable<APIRevision> {
    return this.apiRevisionUpdates.asObservable();
  }

  pushCommentUpdates(commentUpdates: CommentUpdatesDto): void {
    // No-op for mock
  }
}
