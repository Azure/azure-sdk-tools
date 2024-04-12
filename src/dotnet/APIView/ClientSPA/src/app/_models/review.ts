import { APITreeNode, APIRevision } from "./revision"

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
  lastUpdatedOn: string,
  isDeleted: boolean,
  isApproved: boolean
}

export interface ChangeHistory {
  changeAction: string;
  changedBy: string;
  changedOn: string | null;
  notes: string;
}

export interface SelectItemModel {
  label: string,
  data: string
}

export interface ReviewContent {
  review: Review
  apiForest: APITreeNode[]
  apiRevisions: APIRevision[]
  activeAPIRevision: APIRevision
  diffAPIRevision: APIRevision
}


export interface CommentItemModel {
    id: string;
    reviewId: string;
    aPIRevisionId: string;
    elementId: string;
    sectionClass: string;
    commentText: string;
    crossLanguageId: string;
    changeHistory: ChangeHistory[];
    isResolved: boolean;
    upvotes: string[];
    taggedUsers: Set<string>;
    commentType: CommentType;
    resolutionLocked: boolean;
    createdBy: string;
    createdOn: string;
    lastEditedOn: string | null;
    isDeleted: boolean;
}
