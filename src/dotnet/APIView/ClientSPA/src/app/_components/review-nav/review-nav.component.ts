import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { TreeNode } from 'primeng/api';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-review-nav',
  templateUrl: './review-nav.component.html',
  styleUrls: ['./review-nav.component.scss']
})
export class ReviewNavComponent implements OnChanges {
  @Input() reviewPageNavigation: TreeNode[] = [];

  isLoading: boolean = true;
  assetsPath : string = environment.assetsPath;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes["reviewPageNavigation"]) {
      if (changes["reviewPageNavigation"].currentValue.length > 0) {
        this.isLoading = false;
      } else {
        this.isLoading = true;
      }
    }
  }
}
