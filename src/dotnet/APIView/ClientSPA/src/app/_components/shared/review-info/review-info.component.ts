import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-review-info',
  templateUrl: './review-info.component.html',
  styleUrls: ['./review-info.component.scss']
})
export class ReviewInfoComponent {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  
  @Input() review : Review | undefined = undefined;
  @Output() revisionsSidePanel : EventEmitter<boolean> = new EventEmitter<boolean>();

  assetsPath : string = environment.assetsPath;

  showRevisionSidePanel() {
    this.revisionsSidePanel.emit(true);
  }
}
