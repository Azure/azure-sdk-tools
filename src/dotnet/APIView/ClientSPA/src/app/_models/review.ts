import { Type, Expose } from 'class-transformer';

export enum FirstReleaseApproval {
  Approved,
  Pending,
  All
}

export enum CommentType {
  APIRevision = 0,
  SampleRevision
}

export interface Review {
  id: string
  packageName: string
  language: string
  lastUpdatedOn: string
  isDeleted: boolean
  isApproved: boolean
}

export class ChangeHistory {
  changeAction: string = '';
  changedBy: string = '';
  changedOn: string | null = null;
  notes: string = '';
}


export interface SelectItemModel {
  label: string
  data: string
}

export class CodeDiagnostic {
  diagnosticId: string = '';
  text: string = '';
  helpLinkUri: string = '';
  targetId: string = '';
  level: string = '';
}

export class CommentItemModel {
  id: string = '';
  reviewId: string = '';
  aPIRevisionId: string = '';
  elementId: string = '';
  sectionClass: string = '';
  commentText: string = '';
  crossLanguageId: string = '';
  @Type(() => ChangeHistory) changeHistory: ChangeHistory[] = [];
  isResolved: boolean = false;
  upvotes: string[] = [];
  @Type(() => String) taggedUsers: Set<string> = new Set<string>();
  commentType: CommentType = CommentType.APIRevision;
  resolutionLocked: boolean = false;
  createdBy: string = '';
  createdOn: string = '';
  lastEditedOn: string | null = null;
  isDeleted: boolean = false;
  isInEditMode: boolean = false;
}
