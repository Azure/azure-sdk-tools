import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { InputSwitchOnChangeEvent } from 'primeng/inputswitch';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { APIRevision } from 'src/app/_models/revision';

@Component({
  selector: 'app-review-page-options',
  templateUrl: './review-page-options.component.html',
  styleUrls: ['./review-page-options.component.scss']
})
export class ReviewPageOptionsComponent implements OnInit, OnChanges{
  @Input() userProfile: UserProfile | undefined;
  @Input() isDiffView: boolean = false;
  @Input() diffStyleInput: string | undefined;
  @Input() activeAPIRevision : APIRevision | undefined = undefined;

  @Output() diffStyleEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() showCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showSystemCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showDocumentationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() markAsViewedEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  
  showCommentsSwitch : boolean = true;
  showSystemCommentsSwitch : boolean = true;
  showDocumentationSwitch : boolean = true;
  showLineNumberSwitch : boolean = true;
  showLeftNavigationSwitch : boolean = true;
  markedAsViewSwitch : boolean = false;

  activeAPIRevisionIsApprovedByCurrentUser: boolean = false;
  activeAPIRevisionApprovalMessage: string = '';
  activeAPIRevisionApprovalClass: string = '';

  firstReleaseApprovalMessage: string = '';

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

  ngOnInit() {
    this.setSelectedDiffStyle();
    this.showCommentsSwitch = this.userProfile?.preferences.showComments ?? true;
    this.showSystemCommentsSwitch = this.userProfile?.preferences.showSystemComments ?? true;
    this.showDocumentationSwitch = this.userProfile?.preferences.showDocumentation ?? false;
    this.showLineNumberSwitch = this.userProfile?.preferences.hideLineNumbers ?? true;
    this.showLeftNavigationSwitch = this.userProfile?.preferences.hideLeftNavigation ?? true;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['diffStyleInput']) {
      this.setSelectedDiffStyle();
    }

    if (changes['userProfile']) {
      this.showCommentsSwitch = this.userProfile?.preferences.showComments ?? this.showCommentsSwitch;
      this.showSystemCommentsSwitch = this.userProfile?.preferences.showSystemComments ?? this.showSystemCommentsSwitch;
      this.showDocumentationSwitch = this.userProfile?.preferences.showDocumentation ?? this.showDocumentationSwitch;
      this.showLineNumberSwitch = this.userProfile?.preferences.hideLineNumbers ?? this.showLineNumberSwitch;
      this.showLeftNavigationSwitch = this.userProfile?.preferences.hideLeftNavigation ?? this.showLeftNavigationSwitch;
    }
    
    if (changes['activeAPIRevision'] && changes['activeAPIRevision'].currentValue != undefined) {
      this.markedAsViewSwitch = this.activeAPIRevision!.viewedBy.includes(this.userProfile?.userName!);
      this.activeAPIRevisionIsApprovedByCurrentUser = this.activeAPIRevision!.approvers.includes(this.userProfile?.userName!);
      this.activeAPIRevisionApprovalMessage = !this.activeAPIRevision?.isApproved ? 'Approved By:' : 'APIRevision Approval Pending';
    }
  }

  /**
 * Callback to on onlyDiff Change
 * @param event the Filter event
 */
  onDiffStyleChange(event: any) {
    this.diffStyleEmitter.emit(event.value.value);
  }

  /**
 * Callback for commentSwitch Change
 * @param event the Filter event
 */
  onCommentsSwitchChange(event: InputSwitchOnChangeEvent) {
    this.showCommentsEmitter.emit(event.checked);
  }

   /**
  * Callback for systemCommentSwitch Change
  * @param event the Filter event
  */
  onShowSystemCommentsSwitchChange(event: InputSwitchOnChangeEvent) {
    this.showSystemCommentsEmitter.emit(event.checked);
  }

  /**
  * Callback for showDocumentationSwitch Change
  * @param event the Filter event
  */
  onShowDocumentationSwitchChange(event: InputSwitchOnChangeEvent) {
    this.showDocumentationEmitter.emit(event.checked);
  }

    /**
 * Callback for commentSwitch Change
 * @param event the Filter event
 */
    onShowLineNumbersSwitchChange(event: InputSwitchOnChangeEvent) {
      this.showCommentsEmitter.emit(event.checked);
    }

    /**
 * Callback for commentSwitch Change
 * @param event the Filter event
 */
    onShowLeftNavigationSwitchChange(event: InputSwitchOnChangeEvent) {
      this.showCommentsEmitter.emit(event.checked);
    }

  /**
  * Callback for markedAsViewSwitch Change
  * @param event the Filter event
  */
  onMarkedAsViewedSwitchChange(event: InputSwitchOnChangeEvent) {
    this.markAsViewedEmitter.emit(event.checked);
  }

  setSelectedDiffStyle() {
    const inputDiffStyle = this.diffStyleOptions.find(option => option.value === this.diffStyleInput);
    this.selectedDiffStyle = (inputDiffStyle) ? inputDiffStyle : this.diffStyleOptions[0];
  }
}
