import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { InputSwitchOnChangeEvent } from 'primeng/inputswitch';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { AuthService } from 'src/app/_services/auth/auth.service';

@Component({
  selector: 'app-review-page-options',
  templateUrl: './review-page-options.component.html',
  styleUrls: ['./review-page-options.component.scss']
})
export class ReviewPageOptionsComponent implements OnInit, OnChanges{
  @Input() userProfile: UserProfile | undefined;
  @Input() isDiffView: boolean = false;
  @Input() onlyDiffInput: boolean | undefined;

  @Output() showOnlyDiffEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  
  
  showCommentsSwitch : boolean = true;
  showSystemCommentsSwitch : boolean = true;
  showDocumentationSwitch : boolean = true;
  showLineNumberSwitch : boolean = true;
  showLeftNavigationSwitch : boolean = true;
  showOnlyDiffSwitch : boolean | undefined;

  constructor(private authService: AuthService) {}

  ngOnInit() {
    this.showOnlyDiffSwitch = this.onlyDiffInput ?? false;
    this.showCommentsSwitch = this.userProfile?.preferences.showComments ?? true;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['onlyDiffInput']) {
      this.showOnlyDiffSwitch = this.onlyDiffInput ?? this.showOnlyDiffSwitch;
    }
  }

  /**
 * Callback to on onlyDiff Change
 * @param event the Filter event
 */
  onOnlyDiffSwitchChange(event: InputSwitchOnChangeEvent) {
    this.showOnlyDiffEmitter.emit(event.checked);
  }

  /**
 * Callback to on onlyDiff Change
 * @param event the Filter event
 */
  onCommentsSwitchChange(event: InputSwitchOnChangeEvent) {
    this.showCommentsEmitter.emit(event.checked);
  }
}
