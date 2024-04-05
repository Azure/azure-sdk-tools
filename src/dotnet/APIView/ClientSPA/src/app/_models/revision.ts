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
  properties: { [key: string]: string; }
  renderClasses: Set<string>
}

export interface APITreeNode {
  properties: { [key: string]: string; }
  topTokens: StructuredToken[];
  bottomTokens: StructuredToken[];
  children: APITreeNode[];
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
}

export interface BuildTokensMessage {
  directive: ReviewPageWorkerMessageDirective
  apiTreeNode: APITreeNode
  huskNodeId: string
  position: string
}
  
  