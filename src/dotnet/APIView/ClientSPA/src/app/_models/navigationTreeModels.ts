export class NavigationTreeNodeData {
  nodeIdHashed: string = '';
  kind: string = '';
  icon: string = '';
}
  
export class NavigationTreeNode {
  label: string = '';
  data: NavigationTreeNodeData = new NavigationTreeNodeData();
  expanded: boolean = false;
  children: NavigationTreeNode[] = [];
  visible: boolean = true;
}
