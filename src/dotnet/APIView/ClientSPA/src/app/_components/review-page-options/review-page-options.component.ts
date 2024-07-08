import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { InputSwitchOnChangeEvent } from 'primeng/inputswitch';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { mapLanguageAliases } from 'src/app/_helpers/service-helpers';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { ConfigService } from 'src/app/_services/config/config.service';

@Component({
  selector: 'app-review-page-options',
  templateUrl: './review-page-options.component.html',
  styleUrls: ['./review-page-options.component.scss']
})
export class ReviewPageOptionsComponent implements OnInit, OnChanges{
  @Input() userProfile: UserProfile | undefined;
  @Input() isDiffView: boolean = false;
  @Input() diffStyleInput: string | undefined;
  @Input() review : Review | undefined = undefined;
  @Input() activeAPIRevision : APIRevision | undefined = undefined;
  @Input() diffAPIRevision : APIRevision | undefined = undefined;
  @Input() preferedApprovers: string[] = [];
  @Input() conversiationInfo : any | undefined = undefined;
  @Input() hasFatalDiagnostics : boolean = false;
  @Input() hasHiddenAPIs : boolean = false;

  @Output() diffStyleEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() showCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showSystemCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showDocumentationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showHiddenAPIEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLeftNavigationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() markAsViewedEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLineNumbersEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() apiRevisionApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() reviewApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();

  webAppUrl : string = this.configService.webAppUrl
  
  showCommentsSwitch : boolean = true;
  showSystemCommentsSwitch : boolean = true;
  showDocumentationSwitch : boolean = true;
  showHiddenAPISwitch : boolean = false;
  showLeftNavigationSwitch : boolean = true;
  markedAsViewSwitch : boolean = false;
  showLineNumbersSwitch : boolean = true;

  canToggleApproveAPIRevision: boolean = false;
  activeAPIRevisionIsApprovedByCurrentUser: boolean = false;
  apiRevisionApprovalMessage: string = '';
  apiRevisionApprovalBtnClass: string = '';
  apiRevisionApprovalBtnLabel: string = '';
  hasActiveConversation : string = '';
  showAPIRevisionApprovalModal: boolean = false;
  overrideActiveConversationforApproval : boolean = false;
  overrideFatalDiagnosticsforApproval : boolean = false;
  
  canApproveReview: boolean | undefined = undefined;
  reviewIsApproved: boolean | undefined = undefined;
  reviewApprover: string = 'azure-sdk';

  selectedApprovers: string[] = [];

  diffStyleOptions : any[] = [
    { label: 'Full Diff', value: "full" },
    { label: 'Only Trees', value: "trees"},
    { label: 'Only Nodes', value: "nodes" }
  ];
  selectedDiffStyle : string = this.diffStyleOptions[0];

  changeHistoryIcons : any = {
    'created': 'bi bi-plus-circle-fill created',
    'approved': 'bi bi-check-circle-fill approved',
    'approvalReverted': 'bi bi-arrow-left-circle-fill approval-reverted',
    'deleted': 'bi bi-trash3-fill deleted',
    'unDeleted': 'bi bi-plus-circle-fill undeleted'
  };

  constructor(private configService: ConfigService, private route: ActivatedRoute, private router: Router) { }

  ngOnInit() {
    this.setSelectedDiffStyle();
    this.showCommentsSwitch = this.userProfile?.preferences.showComments ?? true;
    this.showSystemCommentsSwitch = this.userProfile?.preferences.showSystemComments ?? true;
    this.showDocumentationSwitch = this.userProfile?.preferences.showDocumentation ?? false;
    this.showHiddenAPISwitch = this.userProfile?.preferences.showHiddenApis ?? false;

    if (this.userProfile?.preferences.hideLeftNavigation != undefined) {
      this.showLeftNavigationSwitch = !(this.userProfile?.preferences.hideLeftNavigation);
    } else {
      this.showLeftNavigationSwitch = false;
    }
    if (this.userProfile?.preferences.hideLineNumbers){
      this.showLineNumbersSwitch = false;
    } else {
      this.showLineNumbersSwitch = true;
    }

    this.setHasActiveConversatons();
    this.setAPIRevisionApprovalStates();
    this.setReviewApprovalStatus();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['diffStyleInput']) {
      this.setSelectedDiffStyle();
    }

    if (changes['userProfile']) {
      this.showCommentsSwitch = this.userProfile?.preferences.showComments ?? this.showCommentsSwitch;
      this.showSystemCommentsSwitch = this.userProfile?.preferences.showSystemComments ?? this.showSystemCommentsSwitch;
      this.showDocumentationSwitch = this.userProfile?.preferences.showDocumentation ?? this.showDocumentationSwitch;
      this.showHiddenAPISwitch = this.userProfile?.preferences.showHiddenApis ?? false;

      if (this.userProfile?.preferences.hideLeftNavigation != undefined) {
        this.showLeftNavigationSwitch = !(this.userProfile?.preferences.hideLeftNavigation);
      } else {
        this.showLeftNavigationSwitch = false;
      }
      if (this.userProfile?.preferences.hideLineNumbers){
        this.showLineNumbersSwitch = false;
     } else {
      this.showLineNumbersSwitch = true;
     }
    }
    
    if (changes['activeAPIRevision'] && changes['activeAPIRevision'].currentValue != undefined) {
      this.markedAsViewSwitch = this.activeAPIRevision!.viewedBy.includes(this.userProfile?.userName!);
      this.selectedApprovers = this.activeAPIRevision!.assignedReviewers.map(reviewer => reviewer.assingedTo);
      this.setAPIRevisionApprovalStates();
    }

    if (changes['diffAPIRevision']) {
      this.setAPIRevisionApprovalStates();
    }

    if (changes['review']) {
      this.setReviewApprovalStatus();
    }
  }

  /**
 * Callback to on onlyDiff Change
 * @param event the Filter event
 */
  onDiffStyleChange(event: any) {
    this.updateRoute();
    this.diffStyleEmitter.emit(event.value.value);
  }

  /**
 * Callback for commentSwitch Change
 * @param event the Filter event
 */
  onCommentsSwitchChange(event: InputSwitchOnChangeEvent) {
    this.updateRoute();
    this.showCommentsEmitter.emit(event.checked);
  }

   /**
  * Callback for systemCommentSwitch Change
  * @param event the Filter event
  */
  onShowSystemCommentsSwitchChange(event: InputSwitchOnChangeEvent) {
    this.updateRoute();
    this.showSystemCommentsEmitter.emit(event.checked);
  }

  /**
  * Callback for showDocumentationSwitch Change
  * @param event the Filter event
  */
  onShowDocumentationSwitchChange(event: InputSwitchOnChangeEvent) {
    this.updateRoute();
    this.showDocumentationEmitter.emit(event.checked);
  }

  /**
  * Callback for showLeftnavigationSwitch Change
  * @param event the Filter event
  */
  onShowLeftNavigationSwitchChange(event: InputSwitchOnChangeEvent) {
    this.showLeftNavigationEmitter.emit(event.checked);
  }

  /**
  * Callback for markedAsViewSwitch Change
  * @param event the Filter event
  */
  onMarkedAsViewedSwitchChange(event: InputSwitchOnChangeEvent) {
    this.markAsViewedEmitter.emit(event.checked);
  }

  /**
   * Callback for showLineNumbersSwitch Change
   * @param event the Filter event
   */
  onShowLineNumbersSwitchChange(event: InputSwitchOnChangeEvent) {
    this.showLineNumbersEmitter.emit(event.checked);
  }

   /**
   * Callback for showHiddenAPISwitch Change
   * @param event the Filter event
   */
  onShowHiddenAPISwitchChange(event: InputSwitchOnChangeEvent) {
    this.showHiddenAPIEmitter.emit(event.checked);
  }

  setSelectedDiffStyle() {
    const inputDiffStyle = this.diffStyleOptions.find(option => option.value === this.diffStyleInput);
    this.selectedDiffStyle = (inputDiffStyle) ? inputDiffStyle : this.diffStyleOptions[0];
  }

  setAPIRevisionApprovalStates() {
    this.activeAPIRevisionIsApprovedByCurrentUser = this.activeAPIRevision?.approvers.includes(this.userProfile?.userName!)!;
    const isActiveAPIRevisionAhead = (!this.diffAPIRevision) ? true : ((new Date(this.activeAPIRevision?.createdOn!)) > (new Date(this.diffAPIRevision?.createdOn!)));
    this.canToggleApproveAPIRevision = (!this.diffAPIRevision || this.diffAPIRevision.approvers.length > 0) && isActiveAPIRevisionAhead;

    if (this.canToggleApproveAPIRevision) {
      this.apiRevisionApprovalBtnClass = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "btn btn-outline-secondary" : "btn btn-success";
      this.apiRevisionApprovalBtnLabel = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "Revert API Approval" : "Approve";
      this.apiRevisionApprovalMessage = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "" : "Approves the Current API Revision";
    } else {
      this.apiRevisionApprovalBtnClass = "btn btn-outline-secondary";
      this.apiRevisionApprovalBtnLabel = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "Revert API Approval" : "Approve";
    }
  }

  setReviewApprovalStatus() {
    this.canToggleApproveAPIRevision = (this.review && this.review!.packageName && !(mapLanguageAliases(["Swagger", "TypeSpec"]).includes(this.review?.language!))) ? true : false;
    this.reviewIsApproved = this.review && this.review?.isApproved ? true : false;
    if (this.reviewIsApproved) {
      this.reviewApprover = this.review?.changeHistory.find(ch => ch.changeAction === 'approved')?.changedBy ?? 'azure-sdk';
    }
  }

  setHasActiveConversatons() {
    this.hasActiveConversation = this.conversiationInfo && this.conversiationInfo.totalActiveConversationInApiRevision > 0 && this.conversiationInfo.totalActiveConversationInSampleRevision > 0;
  }

  handleAPIRevisionApprovalAction() {
    if (!this.activeAPIRevisionIsApprovedByCurrentUser && (this.hasActiveConversation || this.hasFatalDiagnostics)) {
      this.showAPIRevisionApprovalModal = true;
    } else {
      this.toggleAPIRevisionApproval();
    }
  }

  handleReviewApprovalAction() {
    this.reviewApprovalEmitter.emit(true);
  }

  toggleAPIRevisionApproval() {
    this.apiRevisionApprovalEmitter.emit(true);
  }

   /**
   * This updates the page route without triggering a state update (i.e the code lines are not rebuilt, only the URI is updated)
   * This is specifically to remove the nId query parameter from the URI
   */
  updateRoute() {
    let newQueryParams = getQueryParams(this.route); // this automatically excludes the nId query parameter
    this.router.navigate([], { queryParams: newQueryParams, state: { skipStateUpdate: true } });
  }
}
