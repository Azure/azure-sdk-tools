import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MenuItem, TreeNode } from 'primeng/api';
import { CommentItemModel, Review } from 'src/app/_models/review';
import { APIRevision, CodePanelRowData, CodePanelToggleableData, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { RevisionsService } from 'src/app/_services/revisions/revisions.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';

@Component({
  selector: 'app-review-page',
  templateUrl: './review-page.component.html',
  styleUrls: ['./review-page.component.scss']
})
export class ReviewPageComponent implements OnInit {
  reviewId : string | null = null;
  activeApiRevisionId : string | null = null;
  diffApiRevisionId : string | null = null;

  review : Review | undefined = undefined;
  apiRevisions: APIRevision[] = [];
  reviewComments : CommentItemModel[] | undefined = [];
  revisionSidePanel : boolean | undefined = undefined;
  reviewPageNavigation : TreeNode[] = [];

  codeLinesDataBuffer: CodePanelRowData[] = [];
  otherCodePanelData: Map<string, CodePanelToggleableData> = new Map<string, CodePanelToggleableData>();
  codeLinesData: CodePanelRowData[] = [];
  apiRevisionPageSize = 50;

  sideMenu: MenuItem[] | undefined;

  constructor(private route: ActivatedRoute, private apiRevisionsService: RevisionsService, private reviewsService: ReviewsService, private workerService: WorkerService) {}

  ngOnInit() {
    this.reviewId = this.route.snapshot.paramMap.get('reviewId');
    this.activeApiRevisionId = this.route.snapshot.queryParamMap.get('activeApiRevisionId');
    this.diffApiRevisionId = this.route.snapshot.queryParamMap.get('diffApiRevisionId');

    this.registerWorkerEventHandler();
    this.loadReview(this.reviewId!);
    this.loadAPIRevisions(0, this.apiRevisionPageSize);
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

  registerWorkerEventHandler() {
    this.workerService.onMessageFromApiTreeBuilder().subscribe(data => {
      if (data.directive === ReviewPageWorkerMessageDirective.CreatePageNavigation) {
        this.reviewPageNavigation = data.navTree as TreeNode[];
      }

      if (data.directive === ReviewPageWorkerMessageDirective.InsertCodeLineData) {
        if (data.codePanelRowData.rowClasses.has("documentation")) {
          if (this.otherCodePanelData.has(data.codePanelRowData.nodeId)) {
            this.otherCodePanelData.get(data.codePanelRowData.nodeId)?.documentation.push(data.codePanelRowData);
          } else {
            this.otherCodePanelData.set(data.codePanelRowData.nodeId, {
              documentation: [data.codePanelRowData],
              diagnostics: []
            });
          }
        } else {
          this.codeLinesDataBuffer.push(data.codePanelRowData);
        }
      }

      if (data.directive === ReviewPageWorkerMessageDirective.InsertDiagnosticsRowData) {
        if (this.otherCodePanelData.has(data.codePanelRowData.nodeId)) {
          this.otherCodePanelData.get(data.codePanelRowData.nodeId)?.diagnostics.push(data.codePanelRowData);
        } else {
          this.otherCodePanelData.set(data.codePanelRowData.nodeId, {
            documentation: [],
            diagnostics: [data.codePanelRowData]
          });
        }
        this.codeLinesDataBuffer.push(data.codePanelRowData);
      }

      if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodeLines) {
        this.codeLinesData = this.codeLinesDataBuffer;
      }
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

  loadReview(reviewId: string) {
    this.reviewsService.getReview(reviewId).subscribe({
      next: (review: Review) => {
        this.review = review;
      }
    });
  }

  loadAPIRevisions(noOfItemsRead : number, pageSize: number) {
    this.apiRevisionsService.getAPIRevisions(noOfItemsRead, pageSize, this.reviewId!).subscribe({
      next: (response: any) => {
        this.apiRevisions = response.result;
      }
    });

  }

  showRevisionsPanel(showRevisionsPanel : any){
    this.revisionSidePanel = showRevisionsPanel as boolean;
  }
}
