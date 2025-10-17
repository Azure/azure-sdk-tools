import { ChangeHistory } from "./changeHistory";

export enum CommentType {
    APIRevision = 0,
    SampleRevision
}

export enum CommentSeverity {
    Question = 0,
    Suggestion = 1,
    ShouldFix = 2,
    MustFix = 3
}

export class CommentItemModel {
    id: string = '';
    reviewId: string = '';
    apiRevisionId: string = '';
    elementId: string = '';
    sectionClass: string = '';
    commentText: string = '';
    crossLanguageId: string = '';
    correlationId: string = '';
    changeHistory: ChangeHistory[] = [];
    isResolved: boolean = false;
    upvotes: string[] = [];
    downvotes: string[] = [];
    taggedUsers: Set<string> = new Set<string>();
    commentType: CommentType | null = null;
    severity: CommentSeverity | null = null;
    resolutionLocked: boolean = false;
    createdBy: string = '';
    createdOn: string = '';
    lastEditedOn: string | null = null;
    isDeleted: boolean = false;
    isInEditMode: boolean = false;
    hasRelatedComments: boolean = false; 
    relatedCommentsCount: number = 0; 

    constructor() {
        this.id = '';
        this.reviewId = '';
        this.apiRevisionId = '';
        this.elementId = '';
        this.sectionClass = '';
        this.commentText = '';
        this.crossLanguageId = '';
        this.correlationId = '';
        this.changeHistory = [];
        this.isResolved = false;
        this.upvotes = [];
        this.downvotes = [];
        this.taggedUsers = new Set<string>();
        this.commentType = CommentType.APIRevision;
        this.severity = null;
        this.resolutionLocked = false;
        this.createdBy = '';
        this.createdOn = '';
        this.lastEditedOn = null;
        this.isDeleted = false;
        this.isInEditMode = false;
        this.hasRelatedComments = false;
        this.relatedCommentsCount = 0;
    }
}