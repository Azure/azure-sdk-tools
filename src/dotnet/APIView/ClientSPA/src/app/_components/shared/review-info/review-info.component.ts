import { Component, EventEmitter, Input, OnInit, Output, SimpleChanges } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { REVIEW_PAGE_NAME, SAMPLES_PAGE_NAME } from 'src/app/_helpers/router-helpers';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { SamplesRevision } from 'src/app/_models/samples';
import { UserProfile } from 'src/app/_models/userProfile';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-review-info',
  templateUrl: './review-info.component.html',
  styleUrls: ['./review-info.component.scss']
})
export class ReviewInfoComponent {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() samplesRevisions: SamplesRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() activeSamplesRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';
  @Input() userProfile: UserProfile | undefined;
  @Input() showPageoptionsButton: boolean = false;

  @Input() review : Review | undefined = undefined;
  @Output() pageOptionsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();

  showPageOptions: boolean = true;

  assetsPath : string = environment.assetsPath;

  constructor(private route: ActivatedRoute) {}

  ngOnInit() {
    if (this.userProfile?.preferences.hideReviewPageOptions != undefined) {
      this.showPageOptions = !(this.userProfile?.preferences.hideReviewPageOptions);
    } else {
      this.showPageOptions = false;
    }
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['userProfile']) {
      this.route.data?.pipe(
        map(data => {
          const pageName = data['pageName'];
          if (pageName === REVIEW_PAGE_NAME) {
            this.showPageOptions = (this.userProfile?.preferences.hideReviewPageOptions != undefined) ? !(this.userProfile?.preferences.hideReviewPageOptions) : false;
          }
          else if (pageName === SAMPLES_PAGE_NAME) {
            this.showPageOptions = (this.userProfile?.preferences.hideSamplesPageOptions != undefined) ? !(this.userProfile?.preferences.hideSamplesPageOptions) : false;
          }
      })).subscribe();
    }
  }

  onRightPanelCheckChange(event: any) {
    this.pageOptionsEmitter.emit(event.target.checked);
  }
}
