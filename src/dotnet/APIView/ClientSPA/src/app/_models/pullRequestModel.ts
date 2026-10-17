export class PullRequestModel {
    id: string;
    reviewId: string;
    apiRevisionId: string;
    baselineApiRevisionId: string;
    pullRequestNumber: number;
    commits: string[] = [];
    repoName: string;
    filePath: string;
    isOpen: boolean = true;
    createdBy: string;
    packageName: string;
    language: string;
    assignee: string;
    isDeleted: boolean;

    constructor(
        id: string,
        reviewId: string,
        apiRevisionId: string,
        baselineApiRevisionId: string,
        pullRequestNumber: number,
        repoName: string,
        filePath: string,
        createdBy: string,
        packageName: string,
        language: string,
        assignee: string,
        isDeleted: boolean
    ) {
        this.id = id;
        this.reviewId = reviewId;
        this.apiRevisionId = apiRevisionId;
        this.baselineApiRevisionId = baselineApiRevisionId;
        this.pullRequestNumber = pullRequestNumber;
        this.repoName = repoName;
        this.filePath = filePath;
        this.createdBy = createdBy;
        this.packageName = packageName;
        this.language = language;
        this.assignee = assignee;
        this.isDeleted = isDeleted;
    }
}