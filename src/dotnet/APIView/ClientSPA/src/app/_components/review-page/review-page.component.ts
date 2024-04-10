import { AfterViewInit, Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MenuItem, TreeNode } from 'primeng/api';
import { BehaviorSubject } from 'rxjs';
import { CommentItemModel, ReviewContent,  } from 'src/app/_models/review';
import { APIRevision, CodeHuskNode, CreateLinesOfTokensMessage, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';

@Component({
  selector: 'app-review-page',
  templateUrl: './review-page.component.html',
  styleUrls: ['./review-page.component.scss']
})
export class ReviewPageComponent implements OnInit, OnDestroy, AfterViewInit {
  reviewId = this.route.snapshot.paramMap.get('reviewId');
  activeApiRevisionId = this.route.snapshot.queryParamMap.get('activeApiRevisionId');
  diffApiRevisionId = this.route.snapshot.queryParamMap.get('diffApiRevisionId');

  reviewContent : ReviewContent | undefined = undefined;
  reviewComments : CommentItemModel[] | undefined = [];
  revisionSidePanel : boolean | undefined = undefined;
  reviewPageNavigation : TreeNode[] = [];
  apiTreeBuilder: Worker | undefined = undefined;
  tokenBuilder: Worker | undefined = undefined;
  apiTreeNodeData: BehaviorSubject<CodeHuskNode | null> = new BehaviorSubject<CodeHuskNode | null>(null);
  tokenLineData: BehaviorSubject<CreateLinesOfTokensMessage | null> = new BehaviorSubject<CreateLinesOfTokensMessage | null>(null);

  sideMenu: MenuItem[] | undefined;

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService, private commentsService: CommentsService) {}

  ngOnInit() {
    this.apiTreeBuilder = new Worker(new URL('../../_workers/review-page.worker', import.meta.url));
    this.tokenBuilder = new Worker(new URL('../../_workers/review-page.worker', import.meta.url));

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

  ngOnDestroy() {
    this.apiTreeBuilder!.terminate();
    this.tokenBuilder!.terminate();
  }

  registerWorkerEventHandler() {
    this.apiTreeBuilder!.onmessage = ({ data }) => {
      if (data.directive === ReviewPageWorkerMessageDirective.CreatePageNavigation) {
        this.reviewPageNavigation = data.navTree as TreeNode[];
      }

      if (data.directive === ReviewPageWorkerMessageDirective.PassToTokenBuilder) {
        data.directive = ReviewPageWorkerMessageDirective.BuildTokens;
        this.tokenBuilder!.postMessage(data);
      }

      if (data.directive === ReviewPageWorkerMessageDirective.CreateCodeLineHusk) {
        if (data.nodeData) {
          this.apiTreeNodeData.next(data.nodeData);
        }
      }
    };

    this.tokenBuilder!.onmessage = ({ data }) => {
      if (data.directive === ReviewPageWorkerMessageDirective.CreateLineOfTokens) {
        this.tokenLineData.next(data);
      }
    }
  }

  loadReviewContent(reviewId: string, activeApiRevisionId: string | null = null, diffApiRevisionId: string | null = null) {
    this.reviewsService.getReviewContent(reviewId, activeApiRevisionId, diffApiRevisionId).subscribe({
      next: (response: ReviewContent) => {
          this.reviewContent = response;
          const message: any = {
            directive: ReviewPageWorkerMessageDirective.BuildAPITree,
            apiTree : this.reviewContent!.apiTree
          };
          this.apiTreeBuilder!.postMessage(message);
        }
    });
  }

  showRevisionsPanel(showRevisionsPanel : any){
    this.revisionSidePanel = showRevisionsPanel as boolean;
  }

  onRevisionSelect(revision: APIRevision) {
    this.reviewContent!.activeAPIRevision = revision;
  } 
}
