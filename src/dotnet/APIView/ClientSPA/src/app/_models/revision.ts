import { ChangeHistory, CodeDiagnostic } from "./review"

export enum ReviewPageWorkerMessageDirective {
  CreatePageNavigation,
  InsertCodeLineData,
  UpdateCodeLines,
  InsertDiagnosticsRowData
}

export enum CodePanelRowDatatype {
  CodeLine,
  Diagnostics,
  Comment
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
  rowType: CodePanelRowDatatype
  lineNumber?: number
  lineTokens?: StructuredToken[]
  nodeId: string
  rowClasses: Set<string>
  indent?: number
  diffKind?: string
  rowSize: number
  toggleDocumentationClasses?: string
  diagnostics?: CodeDiagnostic
}

export interface CodePanelToggleableData {
  documentation: CodePanelRowData[]
  diagnostics: CodeDiagnostic[]
}

export interface InsertCodePanelRowDataMessage {
  directive: ReviewPageWorkerMessageDirective
  codePanelRowData: CodePanelRowData
}