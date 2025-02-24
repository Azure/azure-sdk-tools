import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { TreeNode } from 'primeng/api';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-review-nav',
  templateUrl: './review-nav.component.html',
  styleUrls: ['./review-nav.component.scss']
})
export class ReviewNavComponent implements OnChanges {
  @Input() reviewPageNavigation: TreeNode[] = [];
  @Input() loadFailed: boolean = false;

  @Output() navTreeNodeIdEmitter : EventEmitter<string> = new EventEmitter<string>();

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

    if (changes['loadFailed'] && changes['loadFailed'].currentValue) {
      this.isLoading = false;
    }
  }

  handleNavNodeClick(event: TreeNode) {
    this.navTreeNodeIdEmitter.emit(event.data.nodeIdHashed);
  }
}
