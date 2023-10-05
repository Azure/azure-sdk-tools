import { Component, Input, SimpleChanges } from '@angular/core';
import { NavigationItem } from 'src/app/_models/review';
import { TreeNode } from 'primeng/api'

@Component({
  selector: 'app-review-nav',
  templateUrl: './review-nav.component.html',
  styleUrls: ['./review-nav.component.scss']
})
export class ReviewNavComponent {
  @Input() navigation : NavigationItem[] | null = null;
  navigationTree : TreeNode[] = [];

  ngOnChanges(changes: SimpleChanges) {
    if (changes['navigation'].previousValue){
      this.navigation?.forEach(navigationItem => {
        this.navigationTree.push(this.parseNavigationItemsToTreeNodes(navigationItem));
      });
    }
  }

  parseNavigationItemsToTreeNodes(navigationItem : NavigationItem) {
    let treeNode : TreeNode = {
      label: navigationItem.text,
      data: navigationItem.navigationId,
      expanded: true
    }
    let children : TreeNode[] = [];
    navigationItem.childItems.forEach(child => {
      children.push(this.parseNavigationItemsToTreeNodes(child));
    });
    treeNode.children = children;
    return treeNode;
  }
}
