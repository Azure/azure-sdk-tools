import { ChangeHistory } from "./review"

export enum ReviewPageWorkerMessageDirective {
  BuildAPITree,
  PassToTokenBuilder,
  BuildTokens,
  CreatePageNavigation,
  CreateCodeLineHusk,
  CreateLineOfTokens
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

export interface CodeHuskNode {
  name: string
  id: string
  indent: number
  position: string
}

export interface CreateCodeLineHuskMessage {
  directive: ReviewPageWorkerMessageDirective
  nodeData: CodeHuskNode
}

export interface CreateLinesOfTokensMessage {
  directive: ReviewPageWorkerMessageDirective
  tokenLine: StructuredToken[]
  nodeId: string
  lineId: string
  position: string
  diffKind: string
}

export interface BuildTokensMessage {
  directive: ReviewPageWorkerMessageDirective
  apiTreeNode: APITreeNode
  huskNodeId: string
  position: string
}
  
  