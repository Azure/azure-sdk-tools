import { Type, Expose } from 'class-transformer';
import { ChangeHistory, CodeDiagnostic, CommentItemModel } from "./review"

export enum ReviewPageWorkerMessageDirective {
  CreatePageNavigation,
  UpdateCodePanelData,
  UpdateCodePanelRowData,
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
  @Expose({ name: 'v' })
  value: string = '';
  @Expose({ name: 'i' })
  id: string = '';
  @Expose({ name: 'k' })
  kind: string = '';
  @Expose({ name: 'p' })
  properties: { [key: string]: string; } = {};
  @Expose({ name: 'rc' })
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
  @Expose({ name: 't' }) type: string = '';
  @Expose({ name: 'ln' }) lineNumber: number = 0;
  @Expose({ name: 'rot' }) @Type(() => StructuredToken) rowOfTokens: StructuredToken[] = [];
  @Expose({ name: 'ni' }) nodeId: string = '';
  @Expose({ name: 'nih' }) nodeIdHashed: string = '';
  @Expose({ name: 'rotp' }) rowOfTokensPosition: string = '';
  @Expose({ name: 'rc' }) @Type(() => String) rowClasses: Set<string> = new Set<string>();
  @Expose({ name: 'i' }) indent: number = 0;
  @Expose({ name: 'dk' }) diffKind: string = '';
  @Expose({ name: 'rs' }) rowSize: number = 21;
  @Expose({ name: 'rdc' }) toggleDocumentationClasses: string = '';
  @Expose({ name: 'tcc' }) toggleCommentsClasses: string = '';
  @Expose({ name: 'd' }) @Type(() => CodeDiagnostic) diagnostics: CodeDiagnostic = new CodeDiagnostic();
  @Expose({ name: 'c' }) @Type(() => CommentItemModel) comments: CommentItemModel[] = [];
  showReplyTextBox: boolean = false;
}

export class NavigationTreeNodeData {
  @Expose({ name: 'nih' }) nodeIdHashed: string = '';
  @Expose({ name: 'k' }) kind: string = '';
  @Expose({ name: 'i' }) icon: string = '';
}

export class NavigationTreeNode {
  @Expose({ name: 'l' }) label: string = '';
  @Expose({ name: 'd' }) @Type(() => NavigationTreeNodeData) data: NavigationTreeNodeData = new NavigationTreeNodeData();
  @Expose({ name: 'e' }) expanded: boolean = false;
  @Expose({ name: 'c' }) @Type(() => NavigationTreeNode) children: NavigationTreeNode[] = [];
}

export class CodePanelNodeMetaData {
  @Expose({ name: 'doc' }) @Type(() => CodePanelRowData) documentation: CodePanelRowData[] = [];
  @Expose({ name: 'd' }) @Type(() => CodePanelRowData) diagnostics: CodePanelRowData[] = [];
  @Expose({ name: 'cl' }) @Type(() => CodePanelRowData) codeLines: CodePanelRowData[] = [];
  @Expose({ name: 'ct' }) @Type(() => CodePanelRowData) commentThread: CodePanelRowData[] = [];
  @Expose({ name: 'ntn' }) @Type(() => NavigationTreeNode) navigationTreeNode: NavigationTreeNode = new NavigationTreeNode();
  @Expose({ name: 'pnih' }) parentNodeIdHashed: string = '';
  @Expose({ name: 'cniio' }) childrenNodeIdsInOrder: { [key: number]: string } = {};
  @Expose({ name: 'inwd' }) isNodeWithDiff: boolean = false;
  @Expose({ name: 'inwdid' }) isNodeWithDiffInDescendants: boolean = false;
  @Expose({ name: 'btnih' }) bottomTokenNodeIdHash: string = '';
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
  diffStyle: string,
  showDocumentation: boolean, 
}

export interface CodePanelToggleableData {
  documentation: CodePanelRowData[]
  diagnostics: CodePanelRowData[]
  comments: CodePanelRowData[]
}