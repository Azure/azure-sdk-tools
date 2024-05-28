import { CodeDiagnostic, CommentItemModel } from "./review"

export enum ReviewPageWorkerMessageDirective {
  CreatePageNavigation,
  UpdateCodePanelData,
  UpdateCodePanelRowData,
}

export enum CodePanelRowDatatype {
  CodeLine = "CodeLine",
  Documentation = "Documentation",
  Diagnostics = "Diagnostics",
  CommentThread = "CommentThread"
}

export interface APIRevision {
  id: string
  reviewId: string
  packageName: string
  language: string
  apiRevisionType: string
  pullRequestNo: number
  label: string
  resolvedLabel: string
  packageVersion: string
  assignedReviewers: AssignedReviewer[]
  isApproved: boolean
  createdBy: string
  createdOn: string
  lastUpdatedOn: string
  isReleased: boolean,
  releasedOn: string,
  isDeleted: boolean
}


export interface AssignedReviewer {
  assignedBy: string;
  assingedTo: string;
  assingedOn: string;
}

export interface StructuredToken {
  value: string;
  id: string;
  kind: string;
  properties: { [key: string]: string; }
  renderClasses: Set<string>
}

export interface DiffLineInProcess {
  groupId: string | undefined;
  lineTokens: StructuredToken[];
  tokenIdsInLine: Set<string>;
}

export interface APITreeNode {
  name: string;
  id: string;
  kind: string;
  tags: Set<string>
  properties: { [key: string]: string; }
  topTokens: StructuredToken[];
  bottomTokens: StructuredToken[];
  topDiffTokens: StructuredToken[];
  bottomDiffTokens: StructuredToken[];
  children: APITreeNode[];
  diffKind: string;
}

export interface CodePanelRowData {
  type: CodePanelRowDatatype
  lineNumber?: number
  rowOfTokens?: StructuredToken[]
  nodeId: string
  nodeIdHashed: string
  rowOfTokensPosition: string
  rowClasses: Set<string>
  indent?: number
  diffKind?: string
  rowSize: number
  toggleDocumentationClasses?: string
  toggleCommentsClasses?: string
  diagnostics?: CodeDiagnostic
  comments?: CommentItemModel[]
}

export interface CodePanelNodeMetaData {
  documentation: CodePanelRowData[];
  diagnostics: CodePanelRowData[];
  codeLines: CodePanelRowData[];
  commentThread: CodePanelRowData[];
  navigationTreeNode: NavigationTreeNode;
  parentNodeId: string;
  childrenNodeIdsInOrder: { [key: number]: string };
  isDiffNode: boolean;
  isDiffInDescendants: boolean;
  bottomTokenNodeIdHash: string;
}

export interface CodePanelData {
  nodeMetaData: { [key: string]: CodePanelNodeMetaData };
}

export interface NavigationTreeNodeData {
  kind: string;
  icon: string;
}

export interface NavigationTreeNode {
  label: string;
  data: NavigationTreeNodeData;
  expanded: boolean;
  children: NavigationTreeNode [];
}

export interface InsertCodePanelRowDataMessage {
  directive: ReviewPageWorkerMessageDirective
  payload : any
}

export interface ApiTreeBuilderData {
  onlyDiff: boolean,
  showDocumentation: boolean,
}

export interface CodePanelToggleableData {
  documentation: CodePanelRowData[]
  diagnostics: CodePanelRowData[]
  comments: CodePanelRowData[]
}