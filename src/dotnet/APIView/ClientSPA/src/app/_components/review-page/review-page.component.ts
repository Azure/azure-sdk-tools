import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MenuItem, TreeNode } from 'primeng/api';
import { BehaviorSubject } from 'rxjs';
import { ReviewContent, ReviewPageWorkerMessageDirective } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';

@Component({
  selector: 'app-review-page',
  templateUrl: './review-page.component.html',
  styleUrls: ['./review-page.component.scss']
})
export class ReviewPageComponent implements OnInit, OnDestroy {
  reviewContent : ReviewContent | undefined = undefined;
  revisionSidePanel : boolean | undefined = undefined;
  reviewPageNavigation : TreeNode[] = [];
  apiTreeBuilder: Worker | undefined = undefined;
  tokenBuilder: Worker | undefined = undefined;
  apiTreeNodeData: BehaviorSubject<any> = new BehaviorSubject<any>({});
  tokenLineData: BehaviorSubject<any> = new BehaviorSubject<any>({});

  sideMenu: MenuItem[] | undefined;

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService) {}

  ngOnInit() {
    this.apiTreeBuilder = new Worker(new URL('../../_workers/review-page.worker', import.meta.url));
    this.tokenBuilder = new Worker(new URL('../../_workers/review-page.worker', import.meta.url));

    this.registerWorkerEventHandler();

    const reviewId = this.route.snapshot.paramMap.get('reviewId');
    const apiRevisionId = this.route.snapshot.queryParamMap.get('revisionId');

    if (reviewId && apiRevisionId) {
      this.loadReviewContent(reviewId, apiRevisionId);
    }
    else if (reviewId) {
      this.loadReviewContent(reviewId);
    }

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

      if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodeLines) {
        this.apiTreeNodeData.next(data.nodeData);
      }
    };

    this.tokenBuilder!.onmessage = ({ data }) => {
      if (data.directive === ReviewPageWorkerMessageDirective.CreateLineOfTokens) {
        this.tokenLineData.next(data);
      }
    }
  }

  loadReviewContent(reviewId: string, revisionId: string | undefined = undefined) {
    this.reviewsService.getReviewContent(reviewId, revisionId).subscribe({
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
