import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { TreeNode } from 'primeng/api';
import { environment } from 'src/environments/environment';

@Component({
    selector: 'app-review-nav',
    templateUrl: './review-nav.component.html',
    styleUrls: ['./review-nav.component.scss'],
    standalone: false
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

  isClientType(node: TreeNode): boolean {
    return node.label?.toLowerCase().endsWith('client') || false;
  }

  private static readonly ICON_MAP: Record<string, string> = {
    'assembly': 'codicon-library',
    'class': 'codicon-symbol-class',
    'delegate': 'codicon-symbol-event',
    'enum': 'codicon-symbol-enum',
    'interface': 'codicon-symbol-interface',
    'method': 'codicon-symbol-method',
    'namespace': 'codicon-symbol-namespace',
    'package': 'codicon-package',
    'struct': 'codicon-symbol-struct',
    'type': 'codicon-symbol-type-parameter',
    'typeparam': 'codicon-symbol-type-parameter',
    'dependencies': 'codicon-symbol-reference',
  };

  getCodiconClass(icon: string): string {
    return ReviewNavComponent.ICON_MAP[icon] ?? '';
  }

  hasCodiconIcon(icon: string): boolean {
    return icon in ReviewNavComponent.ICON_MAP;
  }
}
