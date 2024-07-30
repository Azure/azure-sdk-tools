import { ChangeHistory } from "./changeHistory";
import { StructuredToken } from "./structuredToken";
import { CodePanelRowData } from "./codePanelRowData";
import { APICodeFileModel } from "./apiCodeFileModel";


export enum ReviewPageWorkerMessageDirective {
  CreatePageNavigation,
  UpdateCodePanelData,
  UpdateCodePanelRowData,
  SetHasHiddenAPIFlag
}

export enum ParserStyle {
  Flat = "Flat",
  Tree = "Tree"
}

export class APIRevision {
  id: string
  reviewId: string
  files: APICodeFileModel[];
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
  isReleased: boolean
  releasedOn: string
  isDeleted: boolean
  approvers: string[]
  viewedBy: string[]

  constructor() {
    this.id = ''
    this.reviewId = ''
    this.files = [];
    this.packageName = ''
    this.language = ''
    this.apiRevisionType = ''
    this.pullRequestNo = 0
    this.label = ''
    this.resolvedLabel = ''
    this.packageVersion = ''
    this.changeHistory = []
    this.assignedReviewers = []
    this.isApproved = false
    this.createdBy = ''
    this.createdOn = ''
    this.lastUpdatedOn = ''
    this.isReleased = false,
    this.releasedOn = '',
    this.isDeleted = false,
    this.approvers = [],
    this.viewedBy = []
  }
}

export interface AssignedReviewer {
  assignedBy: string;
  assingedTo: string;
  assingedOn: string;
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
}

export class CodePanelNodeMetaData {
  documentation: CodePanelRowData[] = [];
  diagnostics: CodePanelRowData[] = [];
  codeLines: CodePanelRowData[] = [];
  commentThread: { [key: number]: CodePanelRowData } = {};
  navigationTreeNode: NavigationTreeNode = new NavigationTreeNode();
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