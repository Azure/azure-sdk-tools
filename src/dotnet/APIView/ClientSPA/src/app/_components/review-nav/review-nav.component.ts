import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { TreeNode } from 'primeng/api';

@Component({
  selector: 'app-review-nav',
  templateUrl: './review-nav.component.html',
  styleUrls: ['./review-nav.component.scss']
})
export class ReviewNavComponent implements OnChanges {
  @Input() reviewPageNavigation: TreeNode[] = [];

  isLoading: boolean = true;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes["reviewPageNavigation"].currentValue.length > 0) {
      this.isLoading = false;
    }
  }
}
