import { ChangeHistory } from "./changeHistory";

export enum CommentType {
    APIRevision = 0,
    SampleRevision
}

export class CommentItemModel {
    id: string = '';
    reviewId: string = '';
    apiRevisionId: string = '';
    elementId: string = '';
    sectionClass: string = '';
    commentText: string = '';
    crossLanguageId: string = '';
    changeHistory: ChangeHistory[] = [];
    isResolved: boolean = false;
    upvotes: string[] = [];
    taggedUsers: Set<string> = new Set<string>();
    commentType: CommentType | null = null;
    resolutionLocked: boolean = false;
    createdBy: string = '';
    createdOn: string = '';
    lastEditedOn: string | null = null;
    isDeleted: boolean = false;
    isInEditMode: boolean = false;

    constructor() {
        this.id = '';
        this.reviewId = '';
        this.apiRevisionId = '';
        this.elementId = '';
        this.sectionClass = '';
        this.commentText = '';
        this.crossLanguageId = '';
        this.changeHistory = [];
        this.isResolved = false;
        this.upvotes = [];
        this.taggedUsers = new Set<string>();
        this.commentType = CommentType.APIRevision;
        this.resolutionLocked = false;
        this.createdBy = '';
        this.createdOn = '';
        this.lastEditedOn = null;
        this.isDeleted = false;
        this.isInEditMode = false;
    }
}