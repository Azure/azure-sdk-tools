import { AfterViewInit, Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MenuItem, TreeNode } from 'primeng/api';
import { BehaviorSubject } from 'rxjs';
import { CommentItemModel, Review, ReviewContent,  } from 'src/app/_models/review';
import { APIRevision, CodeHuskNode, CreateCodeLineHuskMessage, CreateLinesOfTokensMessage, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';

@Component({
  selector: 'app-review-page',
  templateUrl: './review-page.component.html',
  styleUrls: ['./review-page.component.scss']
})
export class ReviewPageComponent implements OnInit, AfterViewInit {
  reviewId : string | null = null;
  activeApiRevisionId : string | null = null;
  diffApiRevisionId : string | null = null;

  review : Review | undefined = undefined;
  reviewComments : CommentItemModel[] | undefined = [];
  revisionSidePanel : boolean | undefined = undefined;
  reviewPageNavigation : TreeNode[] = [];
  apiTreeNodeData: BehaviorSubject<CreateCodeLineHuskMessage | null> = new BehaviorSubject<CreateCodeLineHuskMessage | null>(null);
  tokenLineData: BehaviorSubject<CreateLinesOfTokensMessage | null> = new BehaviorSubject<CreateLinesOfTokensMessage | null>(null);

  sideMenu: MenuItem[] | undefined;

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService, private commentsService: CommentsService, private workerService: WorkerService) {}

  ngOnInit() {
    this.reviewId = this.route.snapshot.paramMap.get('reviewId');
    this.activeApiRevisionId = this.route.snapshot.queryParamMap.get('activeApiRevisionId');
    this.diffApiRevisionId = this.route.snapshot.queryParamMap.get('diffApiRevisionId');

    this.registerWorkerEventHandler();
    this.loadReviewContent(this.reviewId!, this.activeApiRevisionId, this.diffApiRevisionId);

    this.sideMenu = [
      {
          icon: 'bi bi-clock-history',
      },
      {
          icon: 'bi bi-file-diff'
      },
      {
          icon: 'bi bi-chat-left-dots'
      }
    ];
  }

  ngAfterViewInit() {
    this.commentsService.getComments(this.reviewId!).subscribe({
      next: (response: CommentItemModel[]) => {
        this.reviewComments = response;
      }
    });
  }

  registerWorkerEventHandler() {
    this.workerService.onMessageFromApiTreeBuilder().subscribe(data => {
      if (data.directive === ReviewPageWorkerMessageDirective.CreatePageNavigation) {
        this.reviewPageNavigation = data.navTree as TreeNode[];
      }

      if (data.directive === ReviewPageWorkerMessageDirective.CreateCodeLineHusk) {
        if (data.nodeData) {
          this.apiTreeNodeData.next(data);
        }
      }

      if (data.directive === ReviewPageWorkerMessageDirective.UpdateReviewModel) {
        this.review = data.reviewModel as Review;
      }
    });

    this.workerService.onMessageFromTokenBuilder().subscribe(data => {
        this.tokenLineData.next(data);
    });
  }

  loadReviewContent(reviewId: string, activeApiRevisionId: string | null = null, diffApiRevisionId: string | null = null) {
    this.reviewsService.getReviewContent(reviewId, activeApiRevisionId, diffApiRevisionId).subscribe({
      next: (response: ArrayBuffer) => {
          // Passing ArrayBufer to worker is way faster than passing object
          this.workerService.postToApiTreeBuilder(response);
        }
    });
  }

  showRevisionsPanel(showRevisionsPanel : any){
    this.revisionSidePanel = showRevisionsPanel as boolean;
  }
}
