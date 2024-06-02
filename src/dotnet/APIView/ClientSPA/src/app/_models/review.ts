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
  @Expose({ name: 'ca' }) changeAction: string = '';
  @Expose({ name: 'cb' }) changedBy: string = '';
  @Expose({ name: 'co' }) changedOn: string | null = null;
  @Expose({ name: 'n' }) notes: string = '';
}

export interface SelectItemModel {
  label: string
  data: string
}

export class CodeDiagnostic {
  @Expose({ name: 'di' }) diagnosticId: string = '';
  @Expose({ name: 't' }) text: string = '';
  @Expose({ name: 'hlu' }) helpLinkUri: string = '';
  @Expose({ name: 'ti' }) targetId: string = '';
  @Expose({ name: 'l' }) level: string = '';
}

export class CommentItemModel {
  @Expose({ name: 'id' }) id: string = '';
  @Expose({ name: 'ri' }) reviewId: string = '';
  @Expose({ name: 'ari' }) aPIRevisionId: string = '';
  @Expose({ name: 'ei' }) elementId: string = '';
  @Expose({ name: 'sc' }) sectionClass: string = '';
  @Expose({ name: 'ct' }) commentText: string = '';
  @Expose({ name: 'cli' }) crossLanguageId: string = '';
  @Expose({ name: 'ch' }) @Type(() => ChangeHistory) changeHistory: ChangeHistory[] = [];
  @Expose({ name: 'ir' }) isResolved: boolean = false;
  @Expose({ name: 'uv' }) upvotes: string[] = [];
  @Expose({ name: 'tu' }) @Type(() => String) taggedUsers: Set<string> = new Set<string>();
  @Expose({ name: 'cty' }) commentType: CommentType = CommentType.APIRevision;
  @Expose({ name: 'rl' }) resolutionLocked: boolean = false;
  @Expose({ name: 'cb' }) createdBy: string = '';
  @Expose({ name: 'co' }) createdOn: string = '';
  @Expose({ name: 'leo' }) lastEditedOn: string | null = null;
  @Expose({ name: 'idl' }) isDeleted: boolean = false;
}
