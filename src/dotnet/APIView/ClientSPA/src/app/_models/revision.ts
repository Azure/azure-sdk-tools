import { Type, Expose } from 'class-transformer';
import { ChangeHistory, CodeDiagnostic, CommentItemModel } from "./review"

export enum ReviewPageWorkerMessageDirective {
  CreatePageNavigation,
  UpdateCodePanelData,
  UpdateCodePanelRowData,
  SetHasHiddenAPIFlag
}

export enum CodePanelRowDatatype {
  CodeLine = "codeLine",
  Documentation = "documentation",
  Diagnostics = "diagnostics",
  CommentThread = "commentThread"
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
  changeHistory: ChangeHistory[]
  assignedReviewers: AssignedReviewer[]
  isApproved: boolean
  createdBy: string
  createdOn: string
  lastUpdatedOn: string
  isReleased: boolean,
  releasedOn: string,
  isDeleted: boolean,
  approvers: string[],
  viewedBy: string[]
}


export interface AssignedReviewer {
  assignedBy: string;
  assingedTo: string;
  assingedOn: string;
}

export class StructuredToken {
  value: string = '';
  id: string = '';
  kind: string = '';
  tags: Set<string> = new Set();
  properties: { [key: string]: string; } = {};
  renderClasses: Set<string> = new Set();
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

export class CodePanelRowData {
  type: string = '';
  lineNumber: number = 0;
  @Type(() => StructuredToken) rowOfTokens: StructuredToken[] = [];
  nodeId: string = '';
  nodeIdHashed: string = '';
  rowOfTokensPosition: string = '';
  @Type(() => String) rowClasses: Set<string> = new Set<string>();
  indent: number = 0;
  diffKind: string = '';
  toggleDocumentationClasses: string = '';
  toggleCommentsClasses: string = '';
  @Type(() => CodeDiagnostic) diagnostics: CodeDiagnostic = new CodeDiagnostic();
  @Type(() => CommentItemModel) comments: CommentItemModel[] = [];
  showReplyTextBox: boolean = false;
  isResolvedCommentThread: boolean = false;
  commentThreadIsResolvedBy: string = '';
  isHiddenAPI: boolean = false;
}

export class NavigationTreeNodeData {
  nodeIdHashed: string = '';
  kind: string = '';
  icon: string = '';
}

export class NavigationTreeNode {
  label: string = '';
  @Type(() => NavigationTreeNodeData) data: NavigationTreeNodeData = new NavigationTreeNodeData();
  expanded: boolean = false;
  @Type(() => NavigationTreeNode) children: NavigationTreeNode[] = [];
}

export class CodePanelNodeMetaData {
  @Type(() => CodePanelRowData) documentation: CodePanelRowData[] = [];
  @Type(() => CodePanelRowData) diagnostics: CodePanelRowData[] = [];
  @Type(() => CodePanelRowData) codeLines: CodePanelRowData[] = [];
  @Type(() => CodePanelRowData) commentThread: CodePanelRowData[] = [];
  @Type(() => NavigationTreeNode) navigationTreeNode: NavigationTreeNode = new NavigationTreeNode();
  parentNodeIdHashed: string = '';
  childrenNodeIdsInOrder: { [key: number]: string } = {};
  isNodeWithDiff: boolean = false;
  isNodeWithDiffInDescendants: boolean = false;
  bottomTokenNodeIdHash: string = '';
}

export interface CodePanelData {
  nodeMetaData: { [key: string]: CodePanelNodeMetaData };
}

export interface InsertCodePanelRowDataMessage {
  directive: ReviewPageWorkerMessageDirective
  payload : any
}

export interface ApiTreeBuilderData {
  diffStyle: string,
  showDocumentation: boolean,
  showComments: boolean,
  showSystemComments: boolean,
  showHiddenApis: boolean
}

export interface CodePanelToggleableData {
  documentation: CodePanelRowData[]
  diagnostics: CodePanelRowData[]
  comments: CodePanelRowData[]
}

export interface PullRequestModel {
  id: string;
  reviewId: string;
  apiRevisionId: string;
  baselineAPIRevisionId: string;
  pullRequestNumber: number;
  commits: string[];
  repoName: string;
  filePath: string;
  isOpen: boolean;
  createdBy: string;
  packageName: string;
  language: string;
  assignee: string;
  isDeleted: boolean;
}