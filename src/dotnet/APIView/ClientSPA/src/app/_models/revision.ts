import { ChangeHistory } from "./review"

export enum ReviewPageWorkerMessageDirective {
  CreatePageNavigation,
  InsertCodeLineData,
  UpdateCodeLines,
  UpdateReviewModel
}

export interface APIRevision {
  id: string
  reviewId: string
  packageName: string
  language: string
  apiRevisionType: string
  label: string
  resolvedLabel: string
  packageVersion: string
  assignedReviewers: AssignedReviewer[]
  isApproved: boolean
  createdBy: string
  createdOn: string
  lastUpdatedOn: string
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
  diffKind: string;
  properties: { [key: string]: string; }
  renderClasses: Set<string>
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
  lineNumber?: number
  lineTokens?: StructuredToken[]
  nodeId: string
  lineClasses: Set<string>
  indent: number
  diffKind?: string
  lineSize: number
  toggleDocumentationClasses?: string
}

export interface CodePanelToggleableData {
  documentation: CodePanelRowData[]
}

export interface InsertCodePanelRowDataMessage {
  directive: ReviewPageWorkerMessageDirective
  codePanelRowData: CodePanelRowData
}