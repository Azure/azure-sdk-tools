export interface AIReviewJobCompletedDto {
    reviewId : string;
    apirevisionId: string;
    status: string;
    details: string;
    noOfGeneratedComments : number;
    jobId: string;
}