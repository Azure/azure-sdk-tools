import { Component, Input, SimpleChanges } from '@angular/core';
import { TreeNode } from 'primeng/api';

@Component({
  selector: 'app-review-nav',
  templateUrl: './review-nav.component.html',
  styleUrls: ['./review-nav.component.scss']
})
export class ReviewNavComponent {
  @Input() reviewPageNavigation: TreeNode[] = [];
}
